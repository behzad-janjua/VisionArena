from __future__ import annotations

from backend import llm
from backend.models import CombatTelemetry
from backend.prompts import narrator_messages, parse_json_reply

_GUARD_ACTIONS = {"guard", "block"}


class NarratorAgent:
    """Names the player's move and writes one casual line of stream commentary.

    Tracks the last few player actions so it can chirp repeated patterns
    (guard spam, same-move spam) and pass that context to the LLM.
    Falls back to the table below when no LLM key is configured.
    """

    def __init__(self) -> None:
        self._history: list[str] = []  # last 6 player_action strings

    def narrate(self, event: CombatTelemetry) -> tuple[str, str]:
        self._history.append(event.player_action)
        if len(self._history) > 6:
            self._history.pop(0)

        hint = self._pattern_hint()

        if llm.is_enabled():
            try:
                reply = parse_json_reply(llm.chat(narrator_messages(event, hint)))
                if reply:
                    move_name = str(reply.get("move_name", "")).strip()
                    narration = str(reply.get("narration", "")).strip()
                    if move_name and narration:
                        return move_name, narration
            except Exception:
                pass

        return self._fallback(event, hint)

    # ---------------------------------------------------------------------- #
    # Pattern detection
    # ---------------------------------------------------------------------- #

    def _pattern_hint(self) -> str:
        if len(self._history) < 3:
            return ""

        recent = self._history[-4:]
        guard_count = sum(1 for a in recent if a in _GUARD_ACTIONS)

        if guard_count >= 3:
            return "player has been guarding/blocking almost every round"
        if guard_count >= 2:
            return "player is guarding a lot"

        last3 = self._history[-3:]
        if len(set(last3)) == 1 and last3[0] not in _GUARD_ACTIONS:
            return f"player keeps spamming {last3[0]} over and over"

        return ""

    # ---------------------------------------------------------------------- #
    # Deterministic fallback (no LLM key / LLM error)
    # ---------------------------------------------------------------------- #

    @staticmethod
    def _fallback(event: CombatTelemetry, pattern_hint: str = "") -> tuple[str, str]:
        """Plain safety net — only runs when the LLM is unavailable."""
        action = event.player_action
        dmg    = max(0, int(event.damage_dealt_by_player))

        move_names = {
            "very_heavy_punch": "Full Send",
            "heavy_punch":      "Haymaker",
            "left_punch":       "Left Jab",
            "right_punch":      "Right Jab",
            "guard":            "Guard",
            "block":            "Block",
            "punch_combo":      "Combo",
        }
        move = move_names.get(action, "Attack")

        if event.outcome in {"boss_ko", "match_end"} or event.boss_health_after <= 0:
            line = "KO — match over."
        elif dmg > 0:
            line = f"{move} connects for {dmg} damage."
        else:
            line = f"{move} — no damage."

        return move, line
