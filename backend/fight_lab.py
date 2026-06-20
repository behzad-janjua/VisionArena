from __future__ import annotations

from dataclasses import asdict, dataclass

from .models import CombatTelemetry


@dataclass
class FightLabEval:
    damage_efficiency: float
    counter_success: float
    survival_score: float
    variety_score: float
    fun_score: float
    commentary_accuracy: float
    recommended_strategy: str

    def to_dict(self) -> dict[str, float | str]:
        return asdict(self)


def recommend_strategy(event: CombatTelemetry) -> str:
    if event.player_action == "charged_blast" and event.charge_time >= 1.5:
        return "patient_charger"
    if event.player_action == "shield":
        return "shield_turtle"
    if event.player_action in {"slash_left", "slash_right"}:
        return "slash_spammer"
    return "balanced"


def recommended_counter(strategy: str) -> str:
    return {
        "patient_charger": "rush",
        "shield_turtle": "unblockable",
        "slash_spammer": "dodge",
        "balanced": "projectile",
    }.get(strategy, "projectile")


def evaluate_boss_turn(event: CombatTelemetry) -> FightLabEval:
    strategy = recommend_strategy(event)
    counter = recommended_counter(strategy)
    damage_efficiency = 1.0 if event.damage_dealt_by_boss > 0 else 0.0
    counter_success = 1.0 if event.boss_action == counter else 0.0
    survival_score = 1.0 if event.damage_dealt_by_player < 25 else 0.35 if event.boss_health_after > 0 else 0.0
    variety_score = 0.7 if event.boss_action in {"rush", "dodge", "projectile", "unblockable", "block"} else 0.3
    health_gap = abs(event.boss_health_after - event.player_health_after)
    fun_score = 1.0 if health_gap < 35 else 0.4
    commentary_accuracy = 1.0 if event.player_action and event.outcome else 0.0
    return FightLabEval(
        damage_efficiency=damage_efficiency,
        counter_success=counter_success,
        survival_score=survival_score,
        variety_score=variety_score,
        fun_score=fun_score,
        commentary_accuracy=commentary_accuracy,
        recommended_strategy=strategy,
    )
