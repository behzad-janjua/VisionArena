"""MYO Armband bridge via PyoMyo.

Architecture rationale
----------------------
CV (MediaPipe) owns body movement and aim: body-center-x -> walk direction,
wrist/elbow vector -> blast aim direction. MYO owns hand-state and charge timing
because separating the two sensors prevents the accuracy collapse that happens when
a single sensor must classify both gesture type and continuous body position at once.

MYO pose -> game event mapping
  fist -> rest      -> HEAVY_PUNCH_RELEASE (fires immediately on fist release)
  fingers_spread    -> GUARD_START (held) / GUARD_END (released)

Charge tier (mirrors CombatConfig.cs and plan.md)
  0.20-1.0 s  -> level 1  (small bolt)
  1.0-2.0 s   -> level 2  (medium blast)
  2.0-3.5 s   -> level 3  (beam)
  3.5+   s    -> level 4  (ultimate cannon)

EMG intensity (RMS of 8 channels, normalised 0-1) is forwarded as emgIntensity
on every CHARGE_UPDATE so Unity can pulse the charge aura proportionally to how
hard the player is squeezing.

Run with real hardware:
  python -m backend.myo_listener --ws ws://127.0.0.1:8000/ws/unity

Run mock sequence:
  python -m backend.myo_listener --mock
  python -m backend.myo_listener --mock --ws ws://127.0.0.1:8000/ws/unity
"""

from __future__ import annotations

import argparse
import asyncio
import json
import logging
import os
import time
from dataclasses import asdict
from collections.abc import Iterator
from typing import Any

from .models import NormalizedEvent

log = logging.getLogger(__name__)

# String constants that match EventType values in models.py / KiForgeEventNames.cs.
# Using plain strings avoids linter-rewrite of enum attribute names.
_EVT_CHARGE_START  = "CHARGE_START"
_EVT_CHARGE_UPDATE = "CHARGE_UPDATE"
_EVT_BLAST_RELEASE = "HEAVY_PUNCH_RELEASE"   # fist-hold release -> heavy punch tier
_EVT_SHIELD_START  = "GUARD_START"            # spread fingers -> guard on
_EVT_SHIELD_END    = "GUARD_END"              # spread released -> guard off
_EVT_SLASH_LEFT    = "LEFT_PUNCH"             # wave_in
_EVT_SLASH_RIGHT   = "RIGHT_PUNCH"            # wave_out
_EVT_ULTIMATE      = "VERY_HEAVY_PUNCH"       # double_tap

# Minimum fist-hold to register as a punch (avoids misfires from brief twitches).
_CHARGE_THRESHOLD = 0.20
# Holds beyond this cap at level-4 damage but don't scale further.
_MAX_CHARGE = 3.5
# How often CHARGE_UPDATE events are emitted while the fist is held.
_UPDATE_HZ = 20


# ---------------------------------------------------------------------------
# Event builders
# ---------------------------------------------------------------------------

def _evt(type_str: str, payload: dict[str, Any]) -> NormalizedEvent:
    return NormalizedEvent(type=type_str, timestamp=time.time(), payload=payload)


def _charge_start_evt(origin: dict | None = None, aim: dict | None = None) -> NormalizedEvent:
    return _evt(_EVT_CHARGE_START, {
        "origin": origin or {"x": 0.0, "y": 0.0},
        "aim":    aim    or {"x": 1.0, "y": 0.0},
    })


def _charge_update_evt(
    charge: float,
    hold_seconds: float,
    emg_intensity: float,
    origin: dict | None = None,
    aim: dict | None = None,
) -> NormalizedEvent:
    return _evt(_EVT_CHARGE_UPDATE, {
        "charge":       round(min(charge, 1.0), 3),
        "holdSeconds":  round(hold_seconds, 3),
        "emgIntensity": round(emg_intensity, 3),
        "origin":       origin or {"x": 0.0, "y": 0.0},
        "aim":          aim    or {"x": 1.0, "y": 0.0},
    })


def _blast_release_evt(
    charge: float,
    hold_seconds: float,
    emg_intensity: float,
    origin: dict | None = None,
    aim: dict | None = None,
) -> NormalizedEvent:
    return _evt(_EVT_BLAST_RELEASE, {
        "charge":       round(min(charge, 1.0), 3),
        "holdSeconds":  round(hold_seconds, 3),
        "emgIntensity": round(emg_intensity, 3),
        "origin":       origin or {"x": 0.0, "y": 0.0},
        "aim":          aim    or {"x": 1.0, "y": 0.0},
    })


def _simple_evt(type_str: str) -> NormalizedEvent:
    return _evt(type_str, {})


# Legacy aliases kept for backwards compat with existing test code.
def mock_myo_events() -> Iterator[NormalizedEvent]:
    """Scripted sequence covering all gesture types. Used for tests and demos."""
    yield _charge_start_evt({"x": -4, "y": 0}, {"x": 1, "y": 0})
    yield _charge_update_evt(0.3, 1.0, 0.45)
    yield _charge_update_evt(0.6, 2.0, 0.65)
    yield _charge_update_evt(0.88, 3.1, 0.82)
    yield _blast_release_evt(0.88, 3.1, 0.82, {"x": -4, "y": 0}, {"x": 1, "y": 0})
    yield _simple_evt(_EVT_SLASH_RIGHT)
    yield _simple_evt(_EVT_SHIELD_START)
    yield _simple_evt(_EVT_SHIELD_END)
    yield _simple_evt(_EVT_ULTIMATE)


# ---------------------------------------------------------------------------
# PyoMyo real hardware integration (optional import)
# ---------------------------------------------------------------------------

def _try_import_pyomyo():
    try:
        from pyomyo import Myo, emg_mode  # type: ignore  # noqa: F401
        return Myo, emg_mode
    except ImportError:
        return None, None


class _MyoBridge:
    """Wraps a PyoMyo Myo instance and converts poses + EMG into KiForge events.

    State machine:
      rest/wave/spread  -- idle; edges trigger slash/shield/ultimate
      fist (enter)      -- emit CHARGE_START
      fist (held)       -- emit CHARGE_UPDATE every tick with charge level + EMG
      fist (exit)       -- emit BLAST_RELEASE with total hold time
                           Unity maps hold time to punch tier (see CombatConfig.cs)
    """

    # Per-channel threshold and minimum active channels for a fist
    _EMG_CHAN_THRESHOLD = float(os.getenv("MYO_EMG_THRESHOLD", "20"))
    _EMG_MIN_CHANNELS   = int(os.getenv("MYO_MIN_CHANNELS", "3"))
    # Consecutive ticks required to enter/exit FIST state
    _DEBOUNCE_TICKS = int(os.getenv("MYO_DEBOUNCE", "2"))

    def __init__(self) -> None:
        Myo, emg_mode = _try_import_pyomyo()
        if Myo is None:
            raise ImportError("pyomyo not installed. Install with: pip install pyomyo")

        self._myo = Myo(mode=emg_mode.RAW)
        self._emg: list[int] = [0] * 8
        self._fist_start: float | None = None
        self._shield_active: bool = False
        self._fist_ticks: int = 0   # consecutive ticks above threshold (entry debounce)
        self._rest_ticks: int = 0   # consecutive ticks below threshold (exit debounce)
        self._tick_count: int = 0
        self._current_pose: str = "rest"
        self._prev_pose: str = "rest"
        self._last_logged_state: str | None = None

        self._myo.add_emg_handler(self._on_emg)
        self._myo.add_pose_handler(self._on_pose)

    def _on_emg(self, emg: tuple, movement: Any) -> None:
        self._emg = [abs(v) for v in emg]

    def _on_pose(self, pose: Any) -> None:
        self._current_pose = str(pose.name) if hasattr(pose, "name") else str(pose)

    def _emg_intensity(self) -> float:
        """RMS of all 8 raw EMG channels, normalised to [0, 1]."""
        if not self._emg:
            return 0.0
        rms = (sum(v * v for v in self._emg) / len(self._emg)) ** 0.5
        return round(min(rms / 80.0, 1.0), 3)

    def _raw_above_threshold(self) -> bool:
        if not self._emg:
            return False
        active = sum(1 for v in self._emg if v > self._EMG_CHAN_THRESHOLD)
        return active >= self._EMG_MIN_CHANNELS

    def tick(self) -> list[NormalizedEvent]:
        """Run one PyoMyo poll cycle and return any new game events."""
        self._myo.run()
        events: list[NormalizedEvent] = []
        emg = self._emg_intensity()
        raw = self._emg[:8] if self._emg else [0]*8

        self._tick_count += 1
        if self._tick_count % (2 * _UPDATE_HZ) == 0:
            peak = max(raw) if raw else 0
            active = sum(1 for v in raw if v > self._EMG_CHAN_THRESHOLD)
            print(f"[MYO] alive  emg={emg:.2f}  peak={peak}  active={active}/{len(raw)}", flush=True)

        # Debounce entry AND exit — prevents flickering during sustained hold
        currently_fist = self._fist_start is not None
        if self._raw_above_threshold():
            self._fist_ticks = min(self._fist_ticks + 1, self._DEBOUNCE_TICKS)
            self._rest_ticks = 0
        else:
            self._rest_ticks = min(self._rest_ticks + 1, self._DEBOUNCE_TICKS)
            self._fist_ticks = 0

        if not currently_fist:
            fist = self._fist_ticks >= self._DEBOUNCE_TICKS  # need N ticks to enter
        else:
            fist = self._rest_ticks < self._DEBOUNCE_TICKS   # need N ticks to exit

        # Print only on state change
        new_state = "FIST" if fist else "rest"
        if new_state != self._last_logged_state:
            peak = max(raw) if raw else 0
            active = sum(1 for v in raw if v > self._EMG_CHAN_THRESHOLD)
            print(f"[MYO] {new_state}  emg={emg:.2f}  peak={peak}  active_channels={active}/{len(raw)}", flush=True)
            self._last_logged_state = new_state

        # Fist contraction -> CHARGE_START once, CHARGE_UPDATE every tick, punch on release
        if fist:
            if self._fist_start is None:
                self._fist_start = time.time()
                events.append(_charge_start_evt())
            else:
                hold = time.time() - self._fist_start
                charge = min(hold / _MAX_CHARGE, 1.0)
                events.append(_charge_update_evt(charge, hold, emg))
        else:
            if self._fist_start is not None:
                hold = time.time() - self._fist_start
                self._fist_start = None
                if hold >= _CHARGE_THRESHOLD:
                    charge = min(hold / _MAX_CHARGE, 1.0)
                    events.append(_blast_release_evt(charge, hold, emg))
                    print(f"\n[MYO] >>> PUNCH fired  (held {hold:.2f}s  charge={charge:.2f})", flush=True)
                else:
                    print(f"\n[MYO]     twitch ignored ({hold:.3f}s < {_CHARGE_THRESHOLD}s threshold)", flush=True)

        # Pose-based gestures: guard, directional slashes, ultimate
        # _on_pose fires during self._myo.run() above, so _current_pose is fresh here.
        pose = self._current_pose
        if pose != self._prev_pose:
            self._prev_pose = pose
            if pose in ("fingers_spread", "spread"):
                if not self._shield_active:
                    self._shield_active = True
                    events.append(_simple_evt(_EVT_SHIELD_START))
                    print("[MYO] -> GUARD_START", flush=True)
            else:
                if self._shield_active:
                    self._shield_active = False
                    events.append(_simple_evt(_EVT_SHIELD_END))
                    print("[MYO] -> GUARD_END", flush=True)
                if not fist:
                    if pose == "wave_in":
                        events.append(_simple_evt(_EVT_SLASH_LEFT))
                        print("[MYO] -> LEFT_PUNCH", flush=True)
                    elif pose == "wave_out":
                        events.append(_simple_evt(_EVT_SLASH_RIGHT))
                        print("[MYO] -> RIGHT_PUNCH", flush=True)
                    elif pose == "double_tap":
                        events.append(_simple_evt(_EVT_ULTIMATE))
                        print("[MYO] -> VERY_HEAVY_PUNCH", flush=True)

        return events

    def connect(self) -> None:
        self._myo.connect()
        log.info("[MYO] Connected. Short vibrate confirms link.")
        self._myo.vibrate(1)

    def disconnect(self) -> None:
        try:
            self._myo.disconnect()
        except Exception:
            pass


# ---------------------------------------------------------------------------
# Async stream (real hardware)
# ---------------------------------------------------------------------------

async def myo_event_stream(ws_url: str | None = None) -> None:
    """Connect to the MYO armband and stream KiForge events.

    If ws_url is provided, events are forwarded to the Unity backend WebSocket.
    Otherwise events are printed to stdout for local debugging.
    """
    bridge = _MyoBridge()
    bridge.connect()

    import websockets  # type: ignore

    ws = None
    if ws_url:
        try:
            ws = await websockets.connect(ws_url)
            log.info("[MYO] Streaming to %s", ws_url)
        except Exception as exc:
            print(f"[MYO] Cannot connect to {ws_url}: {exc}", flush=True)
            print("[MYO] Backend may not be running — events will print to stdout instead", flush=True)

    tick_s = 1.0 / _UPDATE_HZ
    cu_log_count = 0
    try:
        while True:
            evts = bridge.tick()
            for evt in evts:
                payload = json.dumps(asdict(evt))
                if ws is not None:
                    try:
                        await ws.send(payload)
                    except Exception as exc:
                        print(f"[MYO] WebSocket send failed ({exc}) — backend down? Falling back to stdout", flush=True)
                        ws = None
                if ws is None:
                    print(payload)
                    continue
                if evt.type == _EVT_CHARGE_UPDATE:
                    cu_log_count += 1
                    if cu_log_count % 10 == 1:  # ~2 Hz
                        p = evt.payload
                        print(f"[MYO] -> CHARGE_UPDATE  charge={p.get('charge', 0):.2f}  hold={p.get('holdSeconds', 0):.1f}s  emg={p.get('emgIntensity', 0):.2f}", flush=True)
                else:
                    cu_log_count = 0
                    print(f"[MYO] -> {evt.type}", flush=True)
            await asyncio.sleep(tick_s)
    except KeyboardInterrupt:
        log.info("[MYO] Stopped by user")
    finally:
        bridge.disconnect()
        if ws is not None:
            await ws.close()


# ---------------------------------------------------------------------------
# Mock stream helpers
# ---------------------------------------------------------------------------

async def send_mock_myo_events(url: str, repeat: int = 1, delay: float = 0.35) -> None:
    import websockets  # type: ignore

    async with websockets.connect(url) as websocket:
        for _ in range(repeat):
            for evt in mock_myo_events():
                payload = json.dumps(asdict(evt))
                await websocket.send(payload)
                print(payload)
                await asyncio.sleep(delay)


def print_mock_myo_events(repeat: int = 1, delay: float = 0.35) -> None:
    import time as _time

    for _ in range(repeat):
        for evt in mock_myo_events():
            print(json.dumps(asdict(evt)))
            _time.sleep(delay)


# ---------------------------------------------------------------------------
# CLI entry point
# ---------------------------------------------------------------------------

def main() -> None:
    logging.basicConfig(level=logging.INFO, format="[MYO] %(message)s")
    parser = argparse.ArgumentParser(description="MYO / PyoMyo event bridge for KiForge Arena.")
    parser.add_argument(
        "--ws",
        default="",
        metavar="URL",
        help="Unity backend WebSocket URL (e.g. ws://127.0.0.1:8000/ws/unity)",
    )
    parser.add_argument(
        "--mock",
        action="store_true",
        default=False,
        help="Use the scripted mock sequence instead of real MYO hardware",
    )
    parser.add_argument("--repeat", type=int, default=1)
    parser.add_argument("--delay", type=float, default=0.35)
    args = parser.parse_args()

    Myo, _ = _try_import_pyomyo()
    use_mock = args.mock or Myo is None
    if Myo is None and not args.mock:
        log.warning(
            "pyomyo not installed — falling back to mock sequence. "
            "Install the MYO driver with:  pip install pyomyo"
        )

    if use_mock:
        if args.ws:
            asyncio.run(send_mock_myo_events(args.ws, repeat=args.repeat, delay=args.delay))
        else:
            print_mock_myo_events(repeat=args.repeat, delay=args.delay)
        return

    asyncio.run(myo_event_stream(args.ws or None))


if __name__ == "__main__":
    main()
