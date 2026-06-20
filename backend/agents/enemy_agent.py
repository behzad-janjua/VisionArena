from __future__ import annotations

from backend.fight_lab import recommended_counter, recommend_strategy
from backend.models import CombatTelemetry


class EnemyAgent:
    def choose_response(self, event: CombatTelemetry, profile: dict | None = None) -> tuple[str, str]:
        event_strategy = recommend_strategy(event)
        strategy = event_strategy
        if event_strategy == "balanced" and profile:
            strategy = str(profile.get("style", "balanced"))
        return recommended_counter(strategy), strategy
