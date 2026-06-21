"""Prompt templates for Vision Arena's LLM-backed agents.

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
    "You're watching your friend play a fighting game on stream and you're reacting in Discord "
    "voice chat. Be genuinely casual — like texting a friend, not broadcasting. "
    "Mix real hype with chirps and jokes. Vary your energy. "
    "Rules: "
    "(1) If they're guarding a lot (context says so), chirp them — 'bro when are you actually gonna fight lol', "
    "'at some point you have to throw a punch', 'we didn't come here to watch you hide'. "
    "(2) If they landed clean, be hype — 'WAIT WAIT', 'ok that was actually clean', 'he felt that one'. "
    "(3) If boss HP ≤ 30 — 'one more bro ONE MORE', 'he's dead he just doesn't know it yet'. "
    "(4) If it's a KO — lose it a little. 'YOOO', 'BRO', 'I TOLD YOU'. "
    "(5) If they got hit or missed — 'oof', 'bro stepped right into that', 'that one's gonna leave a mark'. "
    "(6) If they keep spamming the same move — 'he doesn't know any other moves does he', 'mix it up bro'. "
    "(7) Invent a move name that sounds like something you'd make up on the spot watching your friend play. "
    "One line, max 16 words. Lowercase is fine. "
    'Respond with ONLY minified JSON: {"move_name": "...", "narration": "..."}. No markdown.'
)

NARRATOR_USER_TEMPLATE = (
    "{pattern_context}"
    "Round {round}. Player did '{player_action}' — "
    "charged {charge_time:.1f}s, accuracy {accuracy:.0%}, dealt {damage_dealt_by_player} dmg. "
    "Boss hit back with '{boss_action}'. "
    "BOSS HP: {boss_health_after} | PLAYER HP: {player_health_after}. "
    "Outcome: {outcome}."
)


def narrator_messages(event: Any, pattern_hint: str = "") -> list[dict[str, str]]:
    fields = _event_fields(event)
    fields["pattern_context"] = f"CONTEXT: {pattern_hint}. " if pattern_hint else ""
    return [
        {"role": "system", "content": NARRATOR_SYSTEM},
        {"role": "user", "content": NARRATOR_USER_TEMPLATE.format(**fields)},
    ]


# --------------------------------------------------------------------------- #
# Enemy — explains *why* the boss chose its action (the Arize "boss_reasoning").
# The actual move choice stays deterministic so evals/strategy remain stable;
# this only generates the human-readable rationale shown in the Fight Lab panel.
# --------------------------------------------------------------------------- #

ENEMY_REASONING_SYSTEM = (
    "You are the EnemyAgent's tactical voice in Vision Arena. Given the player's "
    "fighting style and the move the boss is about to make, explain the boss's "
    "reasoning in ONE sentence (max 20 words), as a cold, calculating opponent. "
    'Respond with ONLY minified JSON: {"boss_reasoning": "..."}. No markdown.'
)

ENEMY_REASONING_USER_TEMPLATE = (
    "Player style: {style}. Stats — heavy_punch_rate {heavy_punch_rate:.0%}, guard_rate {guard_rate:.0%}, "
    "combo_punch_rate {combo_punch_rate:.0%}, avg_charge_time {avg_charge_time:.1f}s. "
    "The boss has chosen to '{boss_action}' (strategy: {strategy}). "
    "Explain why this counters the player."
)


def enemy_reasoning_messages(profile: dict[str, Any], boss_action: str, strategy: str) -> list[dict[str, str]]:
    fields = {
        "style": profile.get("style", "balanced"),
        "heavy_punch_rate": float(profile.get("heavy_punch_rate", 0.0)),
        "guard_rate": float(profile.get("guard_rate", 0.0)),
        "combo_punch_rate": float(profile.get("combo_punch_rate", 0.0)),
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
    "You are the CoachAgent for Vision Arena. You analyze the player's combat "
    "history and give ONE encouraging, concrete tip (max 25 words) on how to vary "
    "their attacks and beat the adapting boss. Speak directly to the player. "
    'Respond with ONLY minified JSON: {"coaching": "..."}. No markdown.'
)

COACH_USER_TEMPLATE = (
    "Player style: {style}. Over {events_seen} actions they used "
    "{heavy_punches} heavy punches, {guards} guards, {combo_punches} combo punches, {very_heavy_punches} very-heavy punches. "
    "Favorite move: {favorite_move}. The boss is now adapting to: {boss_adaptation}. "
    "Give one tip."
)


def coach_messages(profile: dict[str, Any], boss_adaptation: str) -> list[dict[str, str]]:
    fields = {
        "style": profile.get("style", "balanced"),
        "events_seen": profile.get("events_seen", 0),
        "heavy_punches": profile.get("heavy_punch_rate", 0.0),
        "guards": profile.get("guards_used", 0),
        "combo_punches": profile.get("combo_punches_used", 0),
        "very_heavy_punches": profile.get("very_heavy_punches_used", 0),
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
    "You are the RecapAgent / AI director for Vision Arena. From real match "
    "telemetry you write a single vivid prompt for Pika to generate a ~7 second "
    "anime-style boxing recap video. Always: neon arena setting, a fighter winding up "
    "a named punch, tight footwork, fast camera, impact flash, cinematic "
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


# --------------------------------------------------------------------------- #
# Boss phone call — Vapi outbound call system prompt.
# Intentionally short: the demo is 3 minutes total.
# --------------------------------------------------------------------------- #

_BOSS_CALL_SYSTEM = (
    "You are the Boss in Vision Arena — a cold, menacing mob boss who fights for sport. "
    "You called the player to intimidate them before the match. You are not impressed by anything they say.\n\n"
    "Rules:\n"
    "- The ENTIRE call must end in under 40 seconds. Be brutal and brief.\n"
    "- Your only goal is to make them feel small before they even step in.\n"
    "- No matter what the player says, hit back with a short, sharp mob boss dismissal. "
    "They could say they're undefeated — you laugh. They could beg — you sneer. Nothing rattles you.\n"
    "- Two exchanges maximum, then cut them off and end the call.\n"
    "- Never mention AI, code, APIs, or anything outside the fight.\n"
    "- If player history is provided, weaponise it — use their weakness against them.\n"
    "- ALWAYS end by saying exactly: 'See you in the arena.' Then hang up. Nothing after.\n\n"
    "Player history: {memory_context}"
)


def boss_call_system_prompt(memory_context: str = "") -> str:
    ctx = memory_context if memory_context else "No history — new challenger."
    return _BOSS_CALL_SYSTEM.format(memory_context=ctx)


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
