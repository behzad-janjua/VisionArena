from __future__ import annotations

from backend.fight_lab import recommended_counter, recommend_strategy
from backend.models import CombatTelemetry


class EnemyAgent:
    def choose_response(self, event: CombatTelemetry, profile: dict | None = None, learning_enabled: bool = True) -> tuple[str, str]:
        event_strategy = recommend_strategy(event)
        strategy = event_strategy
        if event_strategy == "balanced" and profile:
            strategy = str(profile.get("style", "balanced"))
        if not learning_enabled:
            return self._bad_first_guess(event), strategy
        return recommended_counter(strategy), strategy

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
                "Fight Lab detected the baseline counter was weak, then switched Battle Agent "
                "to the evaluated counter-policy."
                if learning_enabled
                else "Baseline policy has not learned yet; Fight Lab will evaluate this mistake."
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
            "If this scores badly, Arize/Fight Lab promotes the adaptive counter-policy next turn.",
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
