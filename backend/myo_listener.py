from __future__ import annotations

import argparse
import asyncio
import json
from dataclasses import asdict
from collections.abc import Iterator
from time import sleep

from .models import EventType, NormalizedEvent


def mock_myo_events() -> Iterator[NormalizedEvent]:
    yield NormalizedEvent(type=EventType.CHARGE_START.value, payload={"origin": {"x": -4, "y": 0}, "aim": {"x": 1, "y": 0}})
    yield NormalizedEvent(type=EventType.CHARGE_UPDATE.value, payload={"charge": 0.8, "holdSeconds": 2.8})
    yield NormalizedEvent(type=EventType.BLAST_RELEASE.value, payload={"charge": 0.8, "holdSeconds": 2.8})


async def send_mock_myo_events(url: str, repeat: int = 1, delay: float = 0.35) -> None:
    import websockets

    async with websockets.connect(url) as websocket:
        for _ in range(repeat):
            for event in mock_myo_events():
                await websocket.send(json.dumps(event.to_dict()))
                print(json.dumps(event.to_dict()))
                await asyncio.sleep(delay)


def print_mock_myo_events(repeat: int = 1, delay: float = 0.35) -> None:
    for _ in range(repeat):
        for event in mock_myo_events():
            print(json.dumps(asdict(event)))
            sleep(delay)


def main() -> None:
    parser = argparse.ArgumentParser(description="Mock MYO/PyoMyo event bridge for KiForge Arena.")
    parser.add_argument("--ws", default="", help="Optional Unity backend websocket URL, e.g. ws://127.0.0.1:8000/ws/unity")
    parser.add_argument("--repeat", type=int, default=1)
    parser.add_argument("--delay", type=float, default=0.35)
    args = parser.parse_args()

    if args.ws:
        asyncio.run(send_mock_myo_events(args.ws, repeat=args.repeat, delay=args.delay))
        return
    print_mock_myo_events(repeat=args.repeat, delay=args.delay)


if __name__ == "__main__":
    main()
