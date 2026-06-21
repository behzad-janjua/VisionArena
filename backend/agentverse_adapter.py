from __future__ import annotations

import json
from collections.abc import Callable


CombatHandler = Callable[[dict, str], dict]

_COMBAT_KEYS = {
    "round",
    "player_action",
    "charge_time",
    "accuracy",
    "damage_dealt_by_player",
    "damage_dealt_by_boss",
    "boss_action",
    "boss_health_after",
    "player_health_after",
    "outcome",
}


def demo_combat_payload() -> dict:
    return {
        "round": 1,
        "player_action": "heavy_punch",
        "charge_time": 3.6,
        "accuracy": 0.82,
        "damage_dealt_by_player": 44,
        "damage_dealt_by_boss": 6,
        "boss_action": "pressure",
        "boss_health_after": 38,
        "player_health_after": 78,
        "outcome": "heavy_punch_landed",
    }


def format_combat_reply(result: dict) -> str:
    plan = result.get("tactical_plan") or {}
    mode = result.get("learning_mode") or plan.get("mode") or "adapted"
    plan_text = ""
    if plan:
        plan_text = (
            "\n\nPlan:\n"
            f"Mode: {mode}\n"
            f"1. Read: {plan.get('read', 'Collecting telemetry.')}\n"
            f"2. Intent: {plan.get('intent', 'Choose a safe counter.')}\n"
            f"3. Execute: {plan.get('execute', result.get('boss_action', 'adapt'))}\n"
            f"4. If wrong: {plan.get('contingency', 'Reset spacing and reread.')}\n"
            f"Fight Lab: {plan.get('arize_fix', 'Evaluation will update the next decision.')}"
        )

    return (
        f"{result.get('move_name', 'Basic Punch')}\n"
        f"{result.get('narration', '')}\n\n"
        f"Boss countered with: {result.get('boss_action', 'watch')}\n"
        f"Next strategy: {result.get('next_strategy', 'adapt')}\n"
        f"Counter-success: {result.get('counter_success', 0.0):.0%} | "
        f"Survival: {result.get('survival_score', 0.0):.0%}"
        f"{plan_text}"
    )


def respond_to_text(text: str, handler: CombatHandler, player_id: str = "asi_one_player") -> str:
    stripped = text.strip()

    try:
        data = json.loads(stripped)
        if isinstance(data, dict) and _COMBAT_KEYS.issubset(data.keys()):
            return format_combat_reply(handler(data, player_id))
    except (json.JSONDecodeError, ValueError, TypeError):
        pass

    lowered = stripped.lower()
    if any(word in lowered for word in ("start", "duel", "fight", "attack", "demo", "punch")):
        return format_combat_reply(handler(demo_combat_payload(), player_id))

    return (
        "I am Battle Agent, an adaptive anime boss brain.\n"
        "Say 'start duel' to run a demo combat turn, or paste CombatTelemetry JSON "
        "and I will choose the boss punch counter, name the punch, and narrate the fight."
    )
