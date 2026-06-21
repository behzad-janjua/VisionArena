from __future__ import annotations

from backend import llm
from backend.models import CombatTelemetry
from backend.prompts import narrator_messages, parse_json_reply


class NarratorAgent:
    """Names the player's move and writes one line of commentary.

    Uses the ASI:One LLM when a key is configured (see backend.llm); otherwise
    falls back to the deterministic table below so the demo always has a line.
    """

    def narrate(self, event: CombatTelemetry) -> tuple[str, str]:
        if llm.is_enabled():
            reply = parse_json_reply(llm.chat(narrator_messages(event)))
            if reply and reply.get("move_name") and reply.get("narration"):
                return str(reply["move_name"]), str(reply["narration"])
        return self._fallback(event)

    @staticmethod
    def _fallback(event: CombatTelemetry) -> tuple[str, str]:
        if event.player_action == "very_heavy_punch":
            return "Very Heavy Punch", f"A full-power punch lands for {event.damage_dealt_by_player} damage."
        if event.player_action == "heavy_punch":
            return "Heavy Punch", f"A heavy punch lands for {event.damage_dealt_by_player} damage."
        if event.player_action == "left_punch":
            return "Left Punch", "A left punch tests the boss guard."
        if event.player_action == "right_punch":
            return "Right Punch", "A right punch snaps through the opening."
        if event.player_action in {"guard", "block"}:
            return "Guard", "The fighter raises guard and waits for the counter."
        return "Basic Punch", "The boss watches the fighter's punch timing."
