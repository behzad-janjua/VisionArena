from __future__ import annotations

from backend.models import CombatTelemetry


class CoachAgent:
    def summarize_style(self, events: list[CombatTelemetry]) -> str:
        blasts = sum(1 for event in events if event.player_action == "charged_blast")
        shields = sum(1 for event in events if event.player_action == "shield")
        slashes = sum(1 for event in events if event.player_action in {"slash_left", "slash_right"})

        if blasts >= max(2, shields + slashes):
            return "You favor charged blasts. Mix in shield baits before long holds."
        if shields >= max(2, blasts + slashes):
            return "You turtle behind shields. Add quick slashes after blocking."
        if slashes >= max(2, blasts):
            return "You pressure with slashes. Charge once the boss starts dodging."
        return "Balanced style. Keep varying charge, shield, and slash timing."
