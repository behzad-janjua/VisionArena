from __future__ import annotations

from backend.agents.enemy_agent import EnemyAgent
from backend.agents.narrator_agent import NarratorAgent
from backend.agents.recap_agent import RecapAgent
from backend.fight_tracing import build_fight_lab_trace
from backend.fight_lab import evaluate_boss_turn
from backend.models import AgentResponse, CombatTelemetry
from backend.player_memory import build_player_profile, describe_adaptation, strategy_weights_for_profile
from backend.recap_queue import RecapQueue
from backend.redis_store import RedisStore


class GameMasterAgent:
    def __init__(self, store: RedisStore | None = None, recap_queue: RecapQueue | None = None) -> None:
        self.enemy = EnemyAgent()
        self.narrator = NarratorAgent()
        self.recap = RecapAgent()
        self.store = store or RedisStore()
        self.recap_queue = recap_queue or RecapQueue()
        self.events: list[CombatTelemetry] = []
        self.latest_response: AgentResponse | None = None
        self.latest_trace: dict = {}
        self.latest_recap_job: dict = {}
        self.match_id = "demo_match"

    def handle_combat_event(self, event: CombatTelemetry, player_id: str = "demo_player") -> AgentResponse:
        existing_events = self.store.get_match_events(player_id)
        existing_evals = self.store.get_json_list(f"player:{player_id}:evals")
        profile_before = build_player_profile(existing_events, existing_evals)

        boss_action, strategy = self.enemy.choose_response(event, profile_before)
        move_name, narration = self.narrator.narrate(event)
        evaluated_event = CombatTelemetry(**{**event.__dict__, "boss_action": boss_action})
        evaluation = evaluate_boss_turn(evaluated_event)

        self.events.append(evaluated_event)
        self.store.append_match_event(player_id, evaluated_event.__dict__)
        self.store.append_json(f"player:{player_id}:evals", evaluation.to_dict())

        updated_events = self.store.get_match_events(player_id)
        updated_evals = self.store.get_json_list(f"player:{player_id}:evals")
        profile_after = build_player_profile(updated_events, updated_evals)
        strategy_weights = strategy_weights_for_profile(profile_after)
        adaptation = describe_adaptation(profile_after, strategy_weights)
        recap_prompt = self.recap.create_prompt(self.events, move_name)

        trace = build_fight_lab_trace(
            player_id=player_id,
            match_id=self.match_id,
            event=evaluated_event,
            profile_before=profile_before,
            boss_action=boss_action,
            strategy=strategy,
            move_name=move_name,
            narration=narration,
            evaluation=evaluation,
            profile_after=profile_after,
            strategy_weights=strategy_weights,
        ).to_dict()
        self.store.append_trace(player_id, trace)

        recap_job: dict = {}
        if event.outcome in {"boss_ko", "match_end", "player_ko"} or event.boss_health_after <= 0 or event.player_health_after <= 0:
            recap_job = self.recap_queue.enqueue(
                recap_prompt,
                {
                    "player_id": player_id,
                    "match_id": self.match_id,
                    "round": event.round,
                    "move_name": move_name,
                    "outcome": event.outcome,
                },
            )

        self.store.set_json(
            f"player:{player_id}:profile",
            {
                **profile_after,
                "latest_move_name": move_name,
                "boss_action": boss_action,
                "boss_adaptation": adaptation,
                "strategy_weights": strategy_weights,
                "arize_eval_summary": evaluation.to_dict(),
            },
        )

        response = AgentResponse(
            move_name=move_name,
            narration=narration,
            boss_action=boss_action,
            next_strategy=adaptation,
            recap_prompt=recap_prompt,
            counter_success=evaluation.counter_success,
            survival_score=evaluation.survival_score,
            strategy_weights=strategy_weights,
            player_profile=profile_after,
            trace=trace,
            recap_job=recap_job,
        )
        self.latest_response = response
        self.latest_trace = trace
        self.latest_recap_job = recap_job
        return response

    def demo_state(self, player_id: str = "demo_player") -> dict:
        profile = self.store.get_json(f"player:{player_id}:profile")
        events = self.store.get_match_events(player_id, limit=20)
        traces = self.store.get_traces(player_id, limit=10)
        return {
            "player_id": player_id,
            "match_id": self.match_id,
            "using_memory_store": self.store.using_memory,
            "player_profile": profile,
            "latest_response": self.latest_response.to_event().payload if self.latest_response else {},
            "recent_events": events,
            "recent_traces": traces,
            "recap_jobs": self.recap_queue.list_jobs(),
        }
