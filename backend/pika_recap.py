"""Pika / Kling cinematic recap integration.

Two-layer system:
  1. FightHighlights — accumulates the biggest moments during a live match
     (first blood, biggest hit, longest charge, KO blow).
  2. build_cinematic_prompt() — turns those highlights into a rich, multi-beat
     director's brief that video models can follow shot-by-shot.
  3. submit_recap() — POSTs to the Pika REST API (PIKA_API_KEY required).
     Falls back gracefully so the demo never blocks on a missing key.

Pika endpoint:  POST https://api.pika.art/v1/generate/text-to-video
Auth:           Authorization: Bearer <PIKA_API_KEY>
"""

from __future__ import annotations

import logging
import os
from dataclasses import dataclass, field
from typing import Any

from .models import CombatTelemetry

log = logging.getLogger(__name__)

_PIKA_API_URL = "https://api.pika.art/v1/generate/text-to-video"

_KO_OUTCOMES = {"boss_ko", "player_ko", "match_end"}


# ---------------------------------------------------------------------------
# Highlight tracker
# ---------------------------------------------------------------------------

@dataclass
class FightHighlights:
    """Accumulates the most cinematic moments across a match."""

    first_blood: CombatTelemetry | None = None
    first_blood_name: str = ""

    biggest_hit: CombatTelemetry | None = None
    biggest_hit_name: str = ""

    heaviest_charged: CombatTelemetry | None = None
    heaviest_charged_name: str = ""

    ko_blow: CombatTelemetry | None = None
    ko_blow_name: str = ""

    total_player_dmg: int = 0
    total_boss_dmg: int = 0
    rounds_played: int = 0
    player_won: bool = False

    _move_name_log: list[tuple[int, str]] = field(default_factory=list)

    def record(self, event: CombatTelemetry, move_name: str = "") -> None:
        self.rounds_played += 1
        self.total_player_dmg += max(0, event.damage_dealt_by_player)
        self.total_boss_dmg += max(0, event.damage_dealt_by_boss)

        if move_name:
            self._move_name_log.append((event.round, move_name))

        if self.first_blood is None and event.damage_dealt_by_player > 0:
            self.first_blood = event
            self.first_blood_name = move_name or "a quick strike"

        if event.damage_dealt_by_player > 0:
            if (
                self.biggest_hit is None
                or event.damage_dealt_by_player > self.biggest_hit.damage_dealt_by_player
            ):
                self.biggest_hit = event
                self.biggest_hit_name = move_name or "a heavy blow"

        if event.charge_time > 0 and event.damage_dealt_by_player > 0:
            if (
                self.heaviest_charged is None
                or event.charge_time > self.heaviest_charged.charge_time
            ):
                self.heaviest_charged = event
                self.heaviest_charged_name = move_name or "a charged punch"

        if event.outcome in _KO_OUTCOMES:
            if event.boss_health_after <= 0:
                self.player_won = True
                self.ko_blow = event
                self.ko_blow_name = move_name or "the finisher"
            elif event.player_health_after <= 0:
                self.ko_blow = event
                self.ko_blow_name = move_name or "the boss counter"


# ---------------------------------------------------------------------------
# Cinematic prompt builder
# ---------------------------------------------------------------------------

def build_cinematic_prompt(highlights: FightHighlights) -> str:
    """Turn fight highlights into a shot-by-shot director's brief for Kling/Pika."""

    winner = "the fighter" if highlights.player_won else "the shadow boss"
    shots: list[str] = []

    if highlights.first_blood:
        fb = highlights.first_blood
        shots.append(
            f"Shot 1 — round {fb.round}: {highlights.first_blood_name} lands clean, "
            f"drawing first blood for {fb.damage_dealt_by_player} damage; "
            "close-up on impact flash and the opponent's recoil."
        )
    else:
        shots.append(
            "Shot 1 — fighters circle each other under neon ring lights; "
            "quick footwork, guard hands raised."
        )

    if highlights.biggest_hit and highlights.biggest_hit is not highlights.first_blood:
        bh = highlights.biggest_hit
        charge_note = (
            f" after {bh.charge_time:.1f}s of windup" if bh.charge_time > 0.3 else ""
        )
        shots.append(
            f"Shot 2 — round {bh.round}: {highlights.biggest_hit_name}{charge_note}, "
            f"the match's biggest blow at {bh.damage_dealt_by_player} damage — "
            "slow-motion impact frame, shockwave ripples across the ring."
        )

    if (
        highlights.heaviest_charged
        and highlights.heaviest_charged is not highlights.biggest_hit
        and highlights.heaviest_charged.charge_time > 0.5
    ):
        hc = highlights.heaviest_charged
        shots.append(
            f"Shot 3 — round {hc.round}: {highlights.heaviest_charged_name} — "
            f"{hc.charge_time:.1f}s windup, fist crackling with electricity, "
            "camera zoom during charge then explosive release."
        )

    if highlights.ko_blow:
        ko = highlights.ko_blow
        shots.append(
            f"Final shot — round {ko.round}: {highlights.ko_blow_name} — "
            f"{winner} stands victorious; ring lights explode, crowd motion blur, "
            "dramatic slow fade to black."
        )
    else:
        shots.append(
            f"Final shot — {winner} stands in the ring, dramatic spotlight, "
            "crowd erupts, slow fade."
        )

    stats_note = (
        f"Match stats: {highlights.rounds_played} rounds, "
        f"player dealt {highlights.total_player_dmg} total damage."
    )

    style_note = (
        "Visual style: Hajime no Ippo x JoJo's Bizarre Adventure — "
        "neon arena, impact frames, speed lines, cinematic lighting, no text overlays, "
        "anime shading, 7 seconds total, smooth camera transitions between shots."
    )

    return " ".join(shots) + " " + stats_note + " " + style_note


# ---------------------------------------------------------------------------
# Legacy alias kept so recap_queue.py and unit tests don't break
# ---------------------------------------------------------------------------

def build_recap_prompt(events: list[CombatTelemetry], move_name: str = "Heavy Punch") -> str:
    if not events:
        return (
            "Create a 7-second anime-style battle recap in a neon arena. "
            "A fighter steps in with tight footwork and trades punches with a shadow boss."
        )
    tracker = FightHighlights()
    for ev in events:
        tracker.record(ev, move_name)
    return build_cinematic_prompt(tracker)


# ---------------------------------------------------------------------------
# Submission
# ---------------------------------------------------------------------------

def submit_recap(prompt: str, metadata: dict[str, Any] | None = None) -> dict[str, Any]:
    """POST the prompt to the Pika REST API.

    Returns {"status": "submitted"|"skipped"|"error", ...}.
    When PIKA_API_KEY is not set, returns {"status": "skipped"} immediately.
    """
    api_key = os.getenv("PIKA_API_KEY", "").strip()
    if not api_key:
        log.debug("[Pika] PIKA_API_KEY not set — skipping video generation")
        return {"status": "skipped", "prompt": prompt, "metadata": metadata or {}}

    try:
        import requests  # noqa: PLC0415

        payload: dict[str, Any] = {
            "prompt": prompt,
            "options": {
                "aspectRatio": "16:9",
                "frameRate": 24,
                "camera": {"zoom": "in"},
            },
        }
        response = requests.post(
            _PIKA_API_URL,
            headers={
                "Authorization": f"Bearer {api_key}",
                "Content-Type": "application/json",
            },
            json=payload,
            timeout=10,
        )
        response.raise_for_status()
        data = response.json()
        job_id = data.get("id") or data.get("job_id") or data.get("task_id") or "unknown"
        log.info("[Pika] Video job submitted: %s", job_id)
        return {
            "status": "submitted",
            "job_id": str(job_id),
            "prompt": prompt,
            "metadata": metadata or {},
            "raw": data,
        }
    except Exception as exc:
        log.warning("[Pika] Submission failed (non-fatal): %s", exc)
        return {"status": "error", "error": str(exc), "prompt": prompt, "metadata": metadata or {}}


# Alias kept so recap_queue.py import doesn't break
submit_recap_to_pika = submit_recap
