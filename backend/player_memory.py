from __future__ import annotations

from collections import Counter
from typing import Any


_DEFAULT_WEIGHTS = {
    "pressure": 0.25,
    "dodge": 0.25,
    "jab": 0.25,
    "guard": 0.25,
    "heavy_counter": 0.0,
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
        float(profile.get("heavy_punch_rate", 0.0)),
        float(profile.get("guard_rate", profile.get("block_rate", 0.0))),
        float(profile.get("combo_punch_rate", 0.0)),
        min(float(profile.get("very_heavy_punches_used", 0)) / 3.0, 1.0),
        min(avg_charge / 4.0, 1.0),
    ]


def build_player_profile(events: list[dict[str, Any]], evals: list[dict[str, Any]] | None = None) -> dict[str, Any]:
    actions = [str(event.get("player_action", "")) for event in events]
    counts = Counter(actions)
    total = max(len(events), 1)
    heavy_actions = {"heavy_punch", "very_heavy_punch"}
    guard_actions = {"guard", "block"}
    combo_actions = {"left_punch", "right_punch", "punch_combo"}
    charge_times = [float(event.get("charge_time", 0.0)) for event in events if event.get("player_action") in heavy_actions]
    favorite_move = counts.most_common(1)[0][0] if counts else "none"

    style = "balanced"
    avg_charge_time = sum(charge_times) / len(charge_times) if charge_times else 0.0

    heavy_count = sum(counts[action] for action in heavy_actions)
    guard_count = sum(counts[action] for action in guard_actions)
    combo_count = sum(counts[action] for action in combo_actions)

    if heavy_count >= max(2, guard_count + combo_count) or (
        heavy_count >= 1 and avg_charge_time >= 1.5
    ):
        style = "heavy_puncher"
    elif guard_count >= max(2, heavy_count + combo_count) or (
        guard_count >= 1 and len(events) <= 2
    ):
        style = "guard_turtle"
    elif combo_count >= max(2, heavy_count) or (
        combo_count >= 1 and len(events) <= 2
    ):
        style = "combo_puncher"

    # counter_success is stored in each event by GameMasterAgent.
    # Round 0 is always baseline (_bad_first_guess); rounds 1+ use the adapted policy.
    # before = first event's score (baseline);  after = avg of last 3 (adapted).
    counter_scores = [float(e.get("counter_success", -1.0)) for e in events if "counter_success" in e]
    before = counter_scores[0] if counter_scores else 0.0
    after = (sum(counter_scores[-3:]) / len(counter_scores[-3:])) if len(counter_scores) >= 2 else before

    return {
        "player_id": "demo_player",
        "style": style,
        "avg_charge_time": round(avg_charge_time, 2),
        "favorite_move": favorite_move,
        "heavy_punch_rate": round(heavy_count / total, 2),
        "guard_rate": round(guard_count / total, 2),
        "combo_punch_rate": round(combo_count / total, 2),
        "guards_used": guard_count,
        "combo_punches_used": combo_count,
        "very_heavy_punches_used": counts["very_heavy_punch"],
        "boss_counter_success_before": round(before, 2),
        "boss_counter_success_after": round(after, 2),
        "events_seen": len(events),
    }


def strategy_weights_for_profile(profile: dict[str, Any]) -> dict[str, float]:
    style = str(profile.get("style", "balanced"))
    if style == "heavy_puncher":
        return {"pressure": 0.65, "dodge": 0.20, "jab": 0.05, "guard": 0.10, "heavy_counter": 0.0}
    if style == "guard_turtle":
        return {"pressure": 0.20, "dodge": 0.15, "jab": 0.10, "guard": 0.0, "heavy_counter": 0.55}
    if style == "combo_puncher":
        return {"pressure": 0.15, "dodge": 0.45, "jab": 0.30, "guard": 0.10, "heavy_counter": 0.0}
    return dict(_DEFAULT_WEIGHTS)


def describe_adaptation(profile: dict[str, Any], weights: dict[str, float]) -> str:
    style = str(profile.get("style", "balanced"))
    if style == "heavy_puncher":
        return "Pressure long windups before the heavy punch lands."
    if style == "guard_turtle":
        return "Bait the guard, then answer with a heavy counter punch."
    if style == "combo_puncher":
        return "Dodge punch strings, then answer with a jab."
    best_action = max(weights, key=weights.get)
    return f"Stay balanced and lean toward {best_action}."
