from __future__ import annotations

from backend.models import CombatTelemetry
from backend.pika_recap import build_recap_prompt


class RecapAgent:
    def create_prompt(self, events: list[CombatTelemetry], move_name: str) -> str:
        return build_recap_prompt(events, move_name)
