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
        dmg      = max(0, int(event.damage_dealt_by_player))
        finisher = event.outcome in {"boss_ko", "match_end"} or event.boss_health_after <= 0
        low_hp   = event.boss_health_after <= 30

        # Pattern chirps take priority
        if "guarding" in pattern_hint or "blocking" in pattern_hint:
            return "The Big Stall", "bro when are you actually going to fight lol"

        if "spamming" in pattern_hint:
            action = event.player_action.replace("_", " ")
            return "Same Move Again", f"he doesn't know any other move, just {action} every time"

        # Per-action lines
        action = event.player_action

        if action == "very_heavy_punch":
            move = "The Big One"
            line = f"WAIT WAIT — that actually landed for {dmg} damage??" if dmg > 0 else "ok he wound up for like 3 seconds and missed"
        elif action == "heavy_punch":
            move = "The Haymaker"
            line = f"ok that was actually clean, {dmg} damage" if dmg > 0 else "he telegraphed that one way too hard"
        elif action == "left_punch":
            move = "Left Hook Thing"
            line = "left one snuck through" if dmg > 0 else "oof, nothing"
        elif action == "right_punch":
            move = "Right Jab"
            line = "right one lands clean" if dmg > 0 else "boss just slapped that away"
        elif action in _GUARD_ACTIONS:
            move = "The Turtle"
            line = "still blocking... still blocking... ok we get it"
        elif action == "punch_combo":
            move = "The String"
            line = f"combo lands for {dmg} — he's actually cooking"
        else:
            move = "Something"
            line = "I'm not sure what that was but ok"

        if finisher:
            line = "YOOO HE'S DOWN. BRO IT'S OVER."
        elif low_hp and action not in _GUARD_ACTIONS:
            line = f"{line} — one more and he's done"

        return move, line
