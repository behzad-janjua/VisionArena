"""Prompt templates for KiForge Arena's LLM-backed agents.

Every agent in the demo has a deterministic fallback (see the agent classes),
so the game is fully playable with no API key. When ASI_ONE_API_KEY (or any
OpenAI-compatible endpoint) is configured, these templates upgrade the output
to dynamic, telemetry-grounded text.

Design rules for these templates:
  * Always pin the model with a strict system prompt so output stays in-world.
  * Ask for compact JSON so parsing never depends on prose formatting.
  * Inject only the telemetry fields the agent needs — keep prompts cheap.
  * Forbid markdown / code fences so the raw string parses cleanly.
"""

from __future__ import annotations

import json
from typing import Any

# --------------------------------------------------------------------------- #
# Narrator — names the move and writes one line of dramatic commentary.
# --------------------------------------------------------------------------- #

NARRATOR_SYSTEM = (
    "You are the NarratorAgent for KiForge Arena, a real-time anime boss fight. "
    "You invent a flashy two-to-four-word attack name and one punchy line of "
    "commentary (max 18 words) describing what just happened. Tone: shonen anime "
    "announcer — dramatic, energetic, never breaks character. "
    "Respond with ONLY minified JSON: "
    '{"move_name": "...", "narration": "..."}. No markdown, no extra keys.'
)

NARRATOR_USER_TEMPLATE = (
    "Round {round}. The player used '{player_action}' "
    "(charge {charge_time:.1f}s, accuracy {accuracy:.0%}) dealing {damage_dealt_by_player} "
    "damage. The boss answered with '{boss_action}'. "
    "Boss health is now {boss_health_after}, player health {player_health_after}. "
    "Outcome: {outcome}. Name the move and narrate the moment."
)


def narrator_messages(event: Any) -> list[dict[str, str]]:
    return [
        {"role": "system", "content": NARRATOR_SYSTEM},
        {"role": "user", "content": NARRATOR_USER_TEMPLATE.format(**_event_fields(event))},
    ]


# --------------------------------------------------------------------------- #
# Enemy — explains *why* the boss chose its action (the Arize "boss_reasoning").
# The actual move choice stays deterministic so evals/strategy remain stable;
# this only generates the human-readable rationale shown in the Fight Lab panel.
# --------------------------------------------------------------------------- #

ENEMY_REASONING_SYSTEM = (
    "You are the EnemyAgent's tactical voice in KiForge Arena. Given the player's "
    "fighting style and the move the boss is about to make, explain the boss's "
    "reasoning in ONE sentence (max 20 words), as a cold, calculating opponent. "
    'Respond with ONLY minified JSON: {"boss_reasoning": "..."}. No markdown.'
)

ENEMY_REASONING_USER_TEMPLATE = (
    "Player style: {style}. Stats — blast_rate {blast_rate:.0%}, block_rate {block_rate:.0%}, "
    "slash_rate {slash_rate:.0%}, avg_charge_time {avg_charge_time:.1f}s. "
    "The boss has chosen to '{boss_action}' (strategy: {strategy}). "
    "Explain why this counters the player."
)


def enemy_reasoning_messages(profile: dict[str, Any], boss_action: str, strategy: str) -> list[dict[str, str]]:
    fields = {
        "style": profile.get("style", "balanced"),
        "blast_rate": float(profile.get("blast_rate", 0.0)),
        "block_rate": float(profile.get("block_rate", 0.0)),
        "slash_rate": float(profile.get("slash_rate", 0.0)),
        "avg_charge_time": float(profile.get("avg_charge_time", 0.0)),
        "boss_action": boss_action,
        "strategy": strategy,
    }
    return [
        {"role": "system", "content": ENEMY_REASONING_SYSTEM},
        {"role": "user", "content": ENEMY_REASONING_USER_TEMPLATE.format(**fields)},
    ]


# --------------------------------------------------------------------------- #
# Coach — post-round advice on how the player should adapt.
# --------------------------------------------------------------------------- #

COACH_SYSTEM = (
    "You are the CoachAgent for KiForge Arena. You analyze the player's combat "
    "history and give ONE encouraging, concrete tip (max 25 words) on how to vary "
    "their attacks and beat the adapting boss. Speak directly to the player. "
    'Respond with ONLY minified JSON: {"coaching": "..."}. No markdown.'
)

COACH_USER_TEMPLATE = (
    "Player style: {style}. Over {events_seen} actions they used "
    "{blasts} charged blasts, {shields} shields, {slashes} slashes, {ultimates} ultimates. "
    "Favorite move: {favorite_move}. The boss is now adapting to: {boss_adaptation}. "
    "Give one tip."
)


def coach_messages(profile: dict[str, Any], boss_adaptation: str) -> list[dict[str, str]]:
    fields = {
        "style": profile.get("style", "balanced"),
        "events_seen": profile.get("events_seen", 0),
        "blasts": profile.get("blast_rate", 0.0),
        "shields": profile.get("blocks_used", 0),
        "slashes": profile.get("slashes_used", 0),
        "ultimates": profile.get("ultimates_used", 0),
        "favorite_move": profile.get("favorite_move", "none"),
        "boss_adaptation": boss_adaptation,
    }
    return [
        {"role": "system", "content": COACH_SYSTEM},
        {"role": "user", "content": COACH_USER_TEMPLATE.format(**fields)},
    ]


# --------------------------------------------------------------------------- #
# Recap — turns end-of-match telemetry into a Pika video prompt.
# (pika_recap.build_recap_prompt is the deterministic fallback for this.)
# --------------------------------------------------------------------------- #

RECAP_SYSTEM = (
    "You are the RecapAgent / AI director for KiForge Arena. From real match "
    "telemetry you write a single vivid prompt for Pika to generate a ~7 second "
    "anime-style battle recap video. Always: neon arena setting, a fighter charging "
    "energy in their fist, the named finisher, fast camera, impact flash, cinematic "
    "lighting, dramatic ending. Keep it under 60 words. "
    'Respond with ONLY minified JSON: {"recap_prompt": "..."}. No markdown.'
)

RECAP_USER_TEMPLATE = (
    "Finisher move: '{move_name}'. The fighter charged {charge_time:.1f}s and "
    "{verb} a shadow boss for {damage} damage. Player style: {style}. "
    "Write the Pika prompt."
)


def recap_messages(move_name: str, charge_time: float, damage: int, defeated: bool, style: str) -> list[dict[str, str]]:
    fields = {
        "move_name": move_name,
        "charge_time": charge_time,
        "damage": damage,
        "verb": "defeats" if defeated else "staggers",
        "style": style,
    }
    return [
        {"role": "system", "content": RECAP_SYSTEM},
        {"role": "user", "content": RECAP_USER_TEMPLATE.format(**fields)},
    ]


# --------------------------------------------------------------------------- #
# Helpers
# --------------------------------------------------------------------------- #

def _event_fields(event: Any) -> dict[str, Any]:
    """Pull the telemetry fields the templates reference, with safe defaults."""
    get = (lambda k, d: getattr(event, k, d))
    return {
        "round": get("round", 1),
        "player_action": get("player_action", "attack"),
        "charge_time": float(get("charge_time", 0.0)),
        "accuracy": float(get("accuracy", 0.0)),
        "damage_dealt_by_player": get("damage_dealt_by_player", 0),
        "boss_action": get("boss_action", "watch"),
        "boss_health_after": get("boss_health_after", 100),
        "player_health_after": get("player_health_after", 100),
        "outcome": get("outcome", "ongoing"),
    }


def parse_json_reply(raw: str | None) -> dict[str, Any] | None:
    """Best-effort parse of an LLM JSON reply, tolerating stray code fences."""
    if not raw:
        return None
    text = raw.strip()
    if text.startswith("```"):
        text = text.strip("`")
        text = text[text.find("{"):]
    try:
        start, end = text.find("{"), text.rfind("}")
        if start == -1 or end == -1:
            return None
        return json.loads(text[start : end + 1])
    except (json.JSONDecodeError, ValueError):
        return None
