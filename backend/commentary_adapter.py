from __future__ import annotations

import json

from backend.agents.narrator_agent import NarratorAgent
from backend.agentverse_adapter import _COMBAT_KEYS, demo_combat_payload
from backend.models import CombatTelemetry


def format_commentary_reply(move_name: str, narration: str) -> str:
    return f"{move_name}\n{narration}"


def respond_to_commentary_text(text: str) -> str:
    stripped = text.strip()
    narrator = NarratorAgent()

    try:
        data = json.loads(stripped)
        if isinstance(data, dict) and _COMBAT_KEYS.issubset(data.keys()):
            return format_commentary_reply(*narrator.narrate(CombatTelemetry(**data)))
    except (json.JSONDecodeError, ValueError, TypeError):
        pass

    lowered = stripped.lower()
    if any(word in lowered for word in ("start", "demo", "commentate", "narrate", "punch", "fight", "duel")):
        return format_commentary_reply(*narrator.narrate(CombatTelemetry(**demo_combat_payload())))

    return (
        "I am Commentator Agent.\n"
        "Say 'start commentary' to narrate a demo punch, or paste CombatTelemetry JSON "
        "and I will return a move name plus a real narration line."
    )
