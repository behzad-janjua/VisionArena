from __future__ import annotations

from backend.fight_lab import recommended_counter, recommend_strategy
from backend.models import CombatTelemetry


class EnemyAgent:
    def choose_response(self, event: CombatTelemetry) -> tuple[str, str]:
        strategy = recommend_strategy(event)
        return recommended_counter(strategy), strategy
