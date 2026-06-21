from __future__ import annotations

from backend.agents.enemy_agent import EnemyAgent
from backend.agents.narrator_agent import NarratorAgent
from backend.agents.recap_agent import RecapAgent
from backend.fight_tracing import record_combat_span
from backend.models import AgentResponse, CombatTelemetry
from backend.pika_recap import FightHighlights, build_cinematic_prompt
from backend.player_memory import (
    build_player_profile,
    describe_adaptation,
    strategy_weights_for_profile,
    style_vector,
)
from backend.recap_queue import RecapQueue
from backend.redis_store import RedisStore


# Ability cooldowns (seconds) stored in Redis with a TTL so the HUD can show a
# live countdown that the boss also reads when deciding how to punish.
_COOLDOWNS = {"very_heavy_punch": 8.0, "heavy_punch": 3.0, "guard": 2.0}
_TRACKED_ABILITIES = tuple(_COOLDOWNS.keys())


def _boss_phase(boss_health_after: int) -> int:
    if boss_health_after <= 0:
        return 3
    if boss_health_after <= 33:
        return 3
    if boss_health_after <= 66:
        return 2
    return 1


class GameMasterAgent:
    def __init__(self, store: RedisStore | None = None) -> None:
        self.enemy = EnemyAgent()
        self.narrator = NarratorAgent()
        self.recap = RecapAgent()
        self.store = store or RedisStore()
        self.recap_queue = RecapQueue()
        self.highlights = FightHighlights()
        self.events: list[CombatTelemetry] = []
        self.latest_response: AgentResponse | None = None
        self.match_id = "demo_match"

    def handle_combat_event(self, event: CombatTelemetry, player_id: str = "demo_player") -> AgentResponse:
        existing_events = self.store.get_match_events(player_id)
        profile_before = build_player_profile(existing_events)
        # Adapted mode kicks in once we have at least one prior event from this player.
        learning_enabled = len(existing_events) > 0
        learning_mode = "adapted" if learning_enabled else "baseline"

        boss_action, strategy = self.enemy.choose_response(event, profile_before, learning_enabled=learning_enabled)
        move_name, narration = self.narrator.narrate(event)
        evaluated_event = CombatTelemetry(**{**event.__dict__, "boss_action": boss_action})

        self.highlights.record(evaluated_event, move_name)
        self.events.append(evaluated_event)
        self.store.append_match_event(player_id, evaluated_event.__dict__)

        updated_events = self.store.get_match_events(player_id)
        profile_after = build_player_profile(updated_events)
        strategy_weights = strategy_weights_for_profile(profile_after)
        adaptation = describe_adaptation(profile_after, strategy_weights)
        recap_prompt = build_cinematic_prompt(self.highlights)

        # On KO, submit the highlight reel to Pika/Kling for video generation.
        recap_job: dict = {}
        if event.outcome in {"boss_ko", "player_ko", "match_end"} or event.boss_health_after <= 0:
            recap_job = self.recap_queue.enqueue(
                recap_prompt,
                metadata={"player_id": player_id, "round": event.round, "outcome": event.outcome},
            )
            self.highlights = FightHighlights()  # reset for next match

        # --- Redis associative memory: recall the most similar past player so the
        # boss can reuse the strategy that worked against them, then index this one.
        profile_vector = style_vector(profile_after)
        memory_recall = self.store.recall_similar(profile_vector, k=1, exclude=player_id)
        self.store.index_player_vector(
            player_id,
            profile_vector,
            {
                "style": profile_after.get("style", "balanced"),
                "strategy_weights": strategy_weights,
            },
        )

        # --- Redis ability cooldowns (TTL) ---
        cooldown = _COOLDOWNS.get(event.player_action)
        if cooldown:
            self.store.set_cooldown(player_id, event.player_action, cooldown)
        active_cooldowns = self.store.active_cooldowns(player_id, list(_TRACKED_ABILITIES))
        tactical_plan = self.enemy.build_tactical_plan(
            evaluated_event,
            profile_after,
            boss_action,
            strategy,
            strategy_weights,
            memory_recall=memory_recall,
            active_cooldowns=active_cooldowns,
            learning_enabled=learning_enabled,
        )

        # --- Redis match telemetry stream, move-name memory, boss phase, narration ---
        boss_phase = _boss_phase(event.boss_health_after)
        self.store.set_boss_phase(player_id, boss_phase)
        self.store.add_move_name(player_id, move_name)
        self.store.stream_add(
            {
                "player_id": player_id,
                "round": event.round,
                "player_action": event.player_action,
                "boss_action": boss_action,
                "move_name": move_name,
                "damage_dealt_by_player": event.damage_dealt_by_player,
                "damage_dealt_by_boss": event.damage_dealt_by_boss,
                "boss_phase": boss_phase,
            }
        )
        self.store.publish({"player_id": player_id, "move_name": move_name, "narration": narration})

        self.store.set_json(
            f"player:{player_id}:profile",
            {
                **profile_after,
                "latest_move_name": move_name,
                "boss_action": boss_action,
                "boss_adaptation": adaptation,
                "tactical_plan": tactical_plan,
                "learning_mode": learning_mode,
                "strategy_weights": strategy_weights,
                "boss_phase": boss_phase,
                "active_cooldowns": active_cooldowns,
                "memory_recall": memory_recall,
            },
        )

        response = AgentResponse(
            move_name=move_name,
            narration=narration,
            boss_action=boss_action,
            next_strategy=adaptation,
            recap_prompt=recap_prompt,
            counter_success=0.0,
            survival_score=0.0,
            strategy_weights=strategy_weights,
            player_profile=profile_after,
            tactical_plan=tactical_plan,
            learning_mode=learning_mode,
            trace={},
            recap_job=recap_job,
        )

        trace_dict = record_combat_span(event, response, player_id)
        self.store.append_trace(player_id, trace_dict)

        self.latest_response = response
        return response

    def demo_state(self, player_id: str = "demo_player") -> dict:
        profile = self.store.get_json(f"player:{player_id}:profile")
        return {
            "player_id": player_id,
            "match_id": self.match_id,
            "using_memory_store": self.store.using_memory,
            "redis_backend": self.store.backend_label,
            "vector_search": self.store.using_vector_search,
            "player_profile": profile,
            "latest_response": self.latest_response.to_event().payload if self.latest_response else {},
            "recent_events": self.store.get_match_events(player_id, limit=20),
            "recent_traces": self.store.get_traces(player_id, limit=10),
            "boss_phase": self.store.get_boss_phase(player_id),
            "active_cooldowns": self.store.active_cooldowns(player_id, list(_TRACKED_ABILITIES)),
            "move_names": self.store.get_move_names(player_id, limit=20),
            "match_stream": self.store.stream_recent(count=20),
            "memory_recall": profile.get("memory_recall", []),
        }
