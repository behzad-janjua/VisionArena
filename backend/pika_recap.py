from __future__ import annotations

from .models import CombatTelemetry


def build_recap_prompt(events: list[CombatTelemetry], move_name: str = "Solar Core Cannon") -> str:
    if not events:
        return (
            "Create a 7-second anime-style battle recap in a neon arena. "
            "A fighter charges glowing energy and faces a shadow boss."
        )

    finisher = max(events, key=lambda event: event.damage_dealt_by_player)
    outcome = "defeats" if finisher.boss_health_after <= 0 else "staggers"
    return (
        "Create a 7-second anime-style battle recap in a neon arena. "
        f"A fighter uses {move_name}, a glowing attack after {finisher.charge_time:.1f}s of charge, "
        f"and {outcome} a towering shadow boss. Show fast camera movement, impact flash, "
        "cinematic lighting, dramatic ending, and readable energy trails."
    )
