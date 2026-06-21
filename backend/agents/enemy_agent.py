from __future__ import annotations

from backend.models import CombatTelemetry

# Maps player style → boss counter action. Must stay in sync with fight_lab.recommended_counter.
# heavy_puncher → pressure (interrupt the windup before it lands)
# guard_turtle  → heavy_counter (bait the guard, then punish it)
# combo_puncher → dodge (slip the string, then jab back)
_COUNTER_MAP: dict[str, str] = {
    "heavy_puncher":   "pressure",
    "guard_turtle":    "heavy_counter",
    "combo_puncher":   "dodge",
    "aggressive":      "pressure",
    "patient_charger": "dodge",
    "balanced":        "jab",
}

# Maps player action → player style label.
_ACTION_STYLE: dict[str, str] = {
    "heavy_punch":      "heavy_puncher",
    "very_heavy_punch": "heavy_puncher",
    "guard":            "guard_turtle",
    "left_punch":       "combo_puncher",
    "right_punch":      "combo_puncher",
    "punch_combo":      "combo_puncher",
}


def _recommend_strategy(event: CombatTelemetry) -> str:
    return _ACTION_STYLE.get(event.player_action, "balanced")


def _recommended_counter(strategy: str) -> str:
    return _COUNTER_MAP.get(strategy, "pressure")


class EnemyAgent:
    def choose_response(self, event: CombatTelemetry, profile: dict | None = None, learning_enabled: bool = True) -> tuple[str, str]:
        event_strategy = _recommend_strategy(event)
        strategy = event_strategy
        if event_strategy == "balanced" and profile:
            strategy = str(profile.get("style", "balanced"))
        if not learning_enabled:
            return self._bad_first_guess(event), strategy
        return _recommended_counter(strategy), strategy

    @staticmethod
    def _bad_first_guess(event: CombatTelemetry) -> str:
        """Intentionally weak baseline: readable, predictable, and easy to punish."""
        if event.player_action in {"guard", "block"}:
            return "jab"
        if event.player_action in {"left_punch", "right_punch", "punch_combo"}:
            return "pressure"
        if event.player_action in {"heavy_punch", "very_heavy_punch"}:
            return "jab"
        return "jab"

    def build_tactical_plan(
        self,
        event: CombatTelemetry,
        profile: dict,
        boss_action: str,
        strategy: str,
        strategy_weights: dict[str, float],
        memory_recall: list[dict] | None = None,
        active_cooldowns: dict[str, float] | None = None,
        learning_enabled: bool = True,
    ) -> dict:
        memory_recall = memory_recall or []
        active_cooldowns = active_cooldowns or {}
        style = profile.get("style", strategy)
        favorite = profile.get("favorite_move", event.player_action)
        rates = {
            "heavy": profile.get("heavy_punch_rate", 0.0),
            "guard": profile.get("guard_rate", 0.0),
            "combo": profile.get("combo_punch_rate", 0.0),
        }

        observe = (
            f"Player currently reads as {style}; most common move is {favorite}. "
            f"Rates: heavy punch {rates['heavy']:.0%}, guard {rates['guard']:.0%}, punch combo {rates['combo']:.0%}."
        )
        if active_cooldowns:
            observe += f" Cooldowns active: {', '.join(active_cooldowns.keys())}."

        if learning_enabled:
            mode = "adapted"
            intent, execute, contingency = self._plan_language(boss_action, style, event)
        else:
            mode = "baseline"
            intent, execute, contingency = self._baseline_plan_language(boss_action, event)
        recall = None
        if memory_recall:
            remembered = memory_recall[0]
            recall = (
                f"Similar previous player: {remembered.get('player_id')} "
                f"({remembered.get('style', 'unknown')}, similarity {remembered.get('similarity', 0.0)})."
            )

        return {
            "read": observe,
            "intent": intent,
            "execute": execute,
            "contingency": contingency,
            "adaptation": self._adaptation_summary(style),
            "weights": strategy_weights,
            "memory_recall": recall,
            "mode": mode,
            "arize_fix": (
                "Local Fight Lab logged the baseline counter score; Battle Agent now uses "
                "the deterministic counter-policy for this detected style."
                if learning_enabled
                else "Baseline policy is collecting its first local eval before counter-policy mode."
            ),
        }

    @staticmethod
    def _plan_language(boss_action: str, style: str, event: CombatTelemetry) -> tuple[str, str, str]:
        action = boss_action.lower()
        if action == "pressure":
            return (
                "Deny windup space before the heavy punch lands.",
                "Step into range and use fast punch pressure.",
                "If the player guards, rotate next decision toward a heavy counter punch.",
            )
        if action == "heavy_counter":
            return (
                "Punish defensive habits instead of feeding the guard.",
                "Commit a heavy counter punch that beats guard timing.",
                "If it whiffs, dodge backward and reset spacing.",
            )
        if action == "dodge":
            return (
                "Make the player overextend, then counter after their punch string.",
                "Slip out of range for one beat.",
                "If the player stops attacking, re-enter with a jab.",
            )
        if action in {"block", "guard"}:
            return (
                "Absorb the immediate threat and preserve boss health.",
                "Raise guard on the likely attack side.",
                "If the player keeps winding up, switch to pressure next.",
            )
        return (
            f"Probe the player's {style} pattern without overcommitting.",
            "Use a safe jab to test timing.",
            "If the player closes distance, guard or dodge on the next read.",
        )

    @staticmethod
    def _baseline_plan_language(boss_action: str, event: CombatTelemetry) -> tuple[str, str, str]:
        return (
            "Use the naive starter policy before fight-lab feedback is available.",
            f"Throw a predictable {boss_action} even if it may not counter {event.player_action}.",
            "If this scores badly, local Fight Lab records it and the next turn uses counter-policy mode.",
        )

    @staticmethod
    def _adaptation_summary(style: str) -> str:
        if style == "heavy_puncher":
            return "Increase pressure weight until heavy punches stop landing."
        if style == "guard_turtle":
            return "Shift weight into heavy counter punches and reduce weak jabs."
        if style == "combo_puncher":
            return "Raise dodge weight and answer punch strings with delayed jabs."
        return "Keep weights balanced while collecting more telemetry."
