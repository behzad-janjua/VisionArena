from __future__ import annotations

from dataclasses import asdict, dataclass, field
from enum import Enum
from time import time
from typing import Any


class EventType(str, Enum):
    CHARGE_START = "CHARGE_START"
    CHARGE_UPDATE = "CHARGE_UPDATE"
    BLAST_RELEASE = "BLAST_RELEASE"
    SHIELD_START = "SHIELD_START"
    SHIELD_END = "SHIELD_END"
    SLASH_LEFT = "SLASH_LEFT"
    SLASH_RIGHT = "SLASH_RIGHT"
    ULTIMATE = "ULTIMATE"
    POSE_UPDATE = "POSE_UPDATE"
    COMBAT_TELEMETRY = "COMBAT_TELEMETRY"
    AGENT_RESPONSE = "AGENT_RESPONSE"


@dataclass
class Vec2:
    x: float = 0.0
    y: float = 0.0


@dataclass
class NormalizedEvent:
    type: str
    timestamp: float = field(default_factory=time)
    payload: dict[str, Any] = field(default_factory=dict)

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)

    @classmethod
    def from_dict(cls, raw: dict[str, Any]) -> "NormalizedEvent":
        return cls(
            type=str(raw.get("type", "")),
            timestamp=float(raw.get("timestamp", time())),
            payload=dict(raw.get("payload", {})),
        )


@dataclass
class CombatTelemetry:
    round: int
    player_action: str
    charge_time: float
    accuracy: float
    damage_dealt_by_player: int
    damage_dealt_by_boss: int
    boss_action: str
    boss_health_after: int
    player_health_after: int
    outcome: str

    def to_event(self) -> NormalizedEvent:
        return NormalizedEvent(type=EventType.COMBAT_TELEMETRY.value, payload=asdict(self))


@dataclass
class AgentResponse:
    move_name: str
    narration: str
    boss_action: str
    next_strategy: str
    recap_prompt: str = ""
    counter_success: float = 0.0
    survival_score: float = 0.0

    def to_event(self) -> NormalizedEvent:
        return NormalizedEvent(type=EventType.AGENT_RESPONSE.value, payload=asdict(self))
