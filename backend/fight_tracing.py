from __future__ import annotations

from dataclasses import asdict
from uuid import uuid4

from backend.models import CombatTelemetry, FightLabTrace, TraceSpan


def build_fight_lab_trace(
    *,
    player_id: str,
    match_id: str,
    event: CombatTelemetry,
    profile_before: dict,
    boss_action: str,
    strategy: str,
    move_name: str,
    narration: str,
    evaluation: object,
    profile_after: dict,
    strategy_weights: dict[str, float],
) -> FightLabTrace:
    eval_payload = evaluation.to_dict() if hasattr(evaluation, "to_dict") else asdict(evaluation)
    return FightLabTrace(
        trace_id=f"trace_{uuid4().hex[:12]}",
        match_id=match_id,
        player_id=player_id,
        round=event.round,
        spans=[
            TraceSpan(
                name="ReadPlayerMemory",
                inputs={"player_id": player_id},
                outputs={"profile": profile_before},
            ),
            TraceSpan(
                name="EnemyAgentDecision",
                inputs={"telemetry": asdict(event), "profile": profile_before},
                outputs={"boss_action": boss_action, "strategy": strategy, "strategy_weights": strategy_weights},
            ),
            TraceSpan(
                name="NarratorAgentGenerateCommentary",
                inputs={"player_action": event.player_action, "damage": event.damage_dealt_by_player},
                outputs={"move_name": move_name, "narration": narration},
            ),
            TraceSpan(
                name="FightLabEvaluateDecision",
                inputs={"boss_action": boss_action, "outcome": event.outcome},
                outputs=eval_payload,
            ),
            TraceSpan(
                name="UpdateStrategyMemory",
                inputs={"profile_before": profile_before, "evaluation": eval_payload},
                outputs={"profile_after": profile_after, "strategy_weights": strategy_weights},
            ),
        ],
    )
