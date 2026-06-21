from __future__ import annotations

from backend.models import CombatTelemetry


class CoachAgent:
    def summarize_style(self, events: list[CombatTelemetry]) -> str:
        heavy = sum(1 for event in events if event.player_action in {"heavy_punch", "very_heavy_punch"})
        guards = sum(1 for event in events if event.player_action in {"guard", "block"})
        combos = sum(1 for event in events if event.player_action in {"left_punch", "right_punch", "punch_combo"})

        if heavy >= max(2, guards + combos):
            return "You favor heavy punches. Mix in quick jabs before long windups."
        if guards >= max(2, heavy + combos):
            return "You turtle behind guard. Add quick left-right punches after blocking."
        if combos >= max(2, heavy):
            return "You pressure with punch strings. Wind up a heavy punch once the boss starts dodging."
        return "Balanced style. Keep varying jabs, guard, and heavy punch timing."
