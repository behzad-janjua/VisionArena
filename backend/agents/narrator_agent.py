from __future__ import annotations

from backend.models import CombatTelemetry


class NarratorAgent:
    def narrate(self, event: CombatTelemetry) -> tuple[str, str]:
        if event.player_action == "ultimate":
            return "Solar Core Cannon", "The arena freezes as the ultimate beam tears across the fight."
        if event.player_action == "charged_blast":
            return "Neon Pulse Breaker", f"A charged blast lands for {event.damage_dealt_by_player} damage."
        if event.player_action.startswith("slash"):
            return "Crosslight Slash", "A bright slash cuts through the boss guard."
        if event.player_action == "shield":
            return "Prism Guard", "A translucent shield blooms around the fighter."
        return "Ki Pulse", "The boss watches for the next opening."
