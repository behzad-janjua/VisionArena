from __future__ import annotations

from backend.agents.enemy_agent import EnemyAgent
from backend.agents.narrator_agent import NarratorAgent
from backend.agents.recap_agent import RecapAgent
from backend.fight_lab import evaluate_boss_turn
from backend.models import AgentResponse, CombatTelemetry
from backend.redis_store import RedisStore


class GameMasterAgent:
    def __init__(self, store: RedisStore | None = None) -> None:
        self.enemy = EnemyAgent()
        self.narrator = NarratorAgent()
        self.recap = RecapAgent()
        self.store = store or RedisStore()
        self.events: list[CombatTelemetry] = []

    def handle_combat_event(self, event: CombatTelemetry, player_id: str = "demo_player") -> AgentResponse:
        boss_action, strategy = self.enemy.choose_response(event)
        move_name, narration = self.narrator.narrate(event)
        evaluated_event = CombatTelemetry(**{**event.__dict__, "boss_action": boss_action})
        evaluation = evaluate_boss_turn(evaluated_event)

        self.events.append(evaluated_event)
        self.store.append_match_event(player_id, evaluated_event.__dict__)
        self.store.set_json(
            f"player:{player_id}:profile",
            {
                "style": strategy,
                "latest_move_name": move_name,
                "boss_action": boss_action,
                "arize_eval_summary": evaluation.to_dict(),
            },
        )

        return AgentResponse(
            move_name=move_name,
            narration=narration,
            boss_action=boss_action,
            next_strategy=strategy,
            recap_prompt=self.recap.create_prompt(self.events, move_name),
            counter_success=evaluation.counter_success,
            survival_score=evaluation.survival_score,
        )
