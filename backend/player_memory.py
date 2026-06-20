from __future__ import annotations

from collections import Counter
from typing import Any


_DEFAULT_WEIGHTS = {
    "rush": 0.25,
    "dodge": 0.25,
    "projectile": 0.25,
    "block": 0.25,
    "unblockable": 0.0,
}

# Dimension of the player-style vector indexed in Redis for similarity recall.
STYLE_VECTOR_DIM = 5


def style_vector(profile: dict[str, Any]) -> list[float]:
    """Turn a player profile into a fixed-length fingerprint for Redis vector recall.

    The boss uses KNN over these vectors to find the most similar player it has
    fought before and reuse the strategy that worked against them.
    """
    avg_charge = float(profile.get("avg_charge_time", 0.0))
    return [
        float(profile.get("blast_rate", 0.0)),
        float(profile.get("block_rate", 0.0)),
        float(profile.get("slash_rate", 0.0)),
        min(float(profile.get("ultimates_used", 0)) / 3.0, 1.0),
        min(avg_charge / 4.0, 1.0),
    ]


def build_player_profile(events: list[dict[str, Any]], evals: list[dict[str, Any]] | None = None) -> dict[str, Any]:
    evals = evals or []
    actions = [str(event.get("player_action", "")) for event in events]
    counts = Counter(actions)
    total = max(len(events), 1)
    charge_times = [float(event.get("charge_time", 0.0)) for event in events if event.get("player_action") == "charged_blast"]
    favorite_move = counts.most_common(1)[0][0] if counts else "none"

    style = "balanced"
    avg_charge_time = sum(charge_times) / len(charge_times) if charge_times else 0.0

    if counts["charged_blast"] >= max(2, counts["shield"] + counts["slash_left"] + counts["slash_right"]) or (
        counts["charged_blast"] >= 1 and avg_charge_time >= 1.5
    ):
        style = "patient_charger"
    elif counts["shield"] >= max(2, counts["charged_blast"] + counts["slash_left"] + counts["slash_right"]) or (
        counts["shield"] >= 1 and len(events) <= 2
    ):
        style = "shield_turtle"
    elif counts["slash_left"] + counts["slash_right"] >= max(2, counts["charged_blast"]) or (
        counts["slash_left"] + counts["slash_right"] >= 1 and len(events) <= 2
    ):
        style = "slash_spammer"

    counter_scores = [float(item.get("counter_success", 0.0)) for item in evals]
    before = counter_scores[0] if counter_scores else 0.0
    after = sum(counter_scores[-3:]) / len(counter_scores[-3:]) if counter_scores else 0.0

    return {
        "player_id": "demo_player",
        "style": style,
        "avg_charge_time": round(avg_charge_time, 2),
        "favorite_move": favorite_move,
        "blast_rate": round(counts["charged_blast"] / total, 2),
        "block_rate": round(counts["shield"] / total, 2),
        "slash_rate": round((counts["slash_left"] + counts["slash_right"]) / total, 2),
        "blocks_used": counts["shield"],
        "slashes_used": counts["slash_left"] + counts["slash_right"],
        "ultimates_used": counts["ultimate"],
        "boss_counter_success_before": round(before, 2),
        "boss_counter_success_after": round(after, 2),
        "events_seen": len(events),
    }


def strategy_weights_for_profile(profile: dict[str, Any]) -> dict[str, float]:
    style = str(profile.get("style", "balanced"))
    if style == "patient_charger":
        return {"rush": 0.65, "dodge": 0.20, "projectile": 0.05, "block": 0.10, "unblockable": 0.0}
    if style == "shield_turtle":
        return {"rush": 0.20, "dodge": 0.15, "projectile": 0.10, "block": 0.0, "unblockable": 0.55}
    if style == "slash_spammer":
        return {"rush": 0.15, "dodge": 0.45, "projectile": 0.30, "block": 0.10, "unblockable": 0.0}
    return dict(_DEFAULT_WEIGHTS)


def describe_adaptation(profile: dict[str, Any], weights: dict[str, float]) -> str:
    style = str(profile.get("style", "balanced"))
    if style == "patient_charger":
        return "Rush during long charges and bait shield cooldown."
    if style == "shield_turtle":
        return "Use unblockable pressure after repeated shields."
    if style == "slash_spammer":
        return "Dodge slash pressure, then answer with projectiles."
    best_action = max(weights, key=weights.get)
    return f"Stay balanced and lean toward {best_action}."
