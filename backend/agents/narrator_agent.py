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
        if event.player_action == "ultimate":
            return "Solar Core Cannon", "The arena freezes as the ultimate beam tears across the fight."
        if event.player_action == "charged_blast":
            return "Neon Pulse Breaker", f"A charged blast lands for {event.damage_dealt_by_player} damage."
        if event.player_action.startswith("slash"):
            return "Crosslight Slash", "A bright slash cuts through the boss guard."
        if event.player_action == "shield":
            return "Prism Guard", "A translucent shield blooms around the fighter."
        return "Ki Pulse", "The boss watches for the next opening."
