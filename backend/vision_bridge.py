from __future__ import annotations

from collections.abc import Iterator

from .models import EventType, NormalizedEvent


def mock_pose_events() -> Iterator[NormalizedEvent]:
    yield NormalizedEvent(
        type=EventType.POSE_UPDATE.value,
        payload={
            "wrist": {"x": -3.5, "y": -0.6},
            "bodyCenter": {"x": -4.0, "y": -1.6},
            "aim": {"x": 1.0, "y": 0.1},
            "confidence": 1.0,
        },
    )
