from __future__ import annotations

from collections.abc import Iterator

from .models import EventType, NormalizedEvent


def mock_myo_events() -> Iterator[NormalizedEvent]:
    yield NormalizedEvent(type=EventType.CHARGE_START.value, payload={"origin": {"x": -4, "y": 0}, "aim": {"x": 1, "y": 0}})
    yield NormalizedEvent(type=EventType.CHARGE_UPDATE.value, payload={"charge": 0.8, "holdSeconds": 2.8})
    yield NormalizedEvent(type=EventType.BLAST_RELEASE.value, payload={"charge": 0.8, "holdSeconds": 2.8})
