from __future__ import annotations

import logging
import os
from typing import Any

import requests as http

from backend.redis_store import RedisStore

log = logging.getLogger(__name__)

_VAPI_BASE = "https://api.vapi.ai"
_MAX_CALL_SECONDS = 40  # hard cap keeps the demo tight


def trigger_boss_call(phone: str, player_id: str, store: RedisStore) -> dict[str, Any]:
    """Initiate an outbound Vapi call from the boss to the player."""
    api_key = os.getenv("VAPI_API_KEY")
    if not api_key:
        log.warning("[Vapi] VAPI_API_KEY not set — mock mode, proceeding immediately")
        mock_id = f"mock_{player_id}"
        store.set_json(f"vapi:call:{mock_id}", {"player_id": player_id, "status": "ended", "transcript": ""})
        return {"call_id": mock_id, "status": "mock"}

    from backend.prompts import boss_call_system_prompt
    memory_context = _build_memory_context(store, player_id)

    body: dict[str, Any] = {
        "assistant": {
            "firstMessage": "I know exactly how you fight. This won't take long." if memory_context else "You called the wrong number. Or maybe the right one.",
            "model": {
                "provider": "anthropic",
                "model": "claude-haiku-4-5-20251001",
                "systemPrompt": boss_call_system_prompt(memory_context),
                "temperature": 0.9,
                "maxTokens": 300,
            },
            "voice": {
                "provider": "elevenlabs",
                "voiceId": os.getenv("VAPI_VOICE_ID", "EXAVITQu4vr4xnSDxMaL"),
                "stability": 0.35,
                "similarityBoost": 0.8,
            },
            "endCallMessage": "See you in the arena.",
            "endCallPhrases": ["see you in the arena", "see you there"],
            "maxDurationSeconds": _MAX_CALL_SECONDS,
        },
        "customer": {"number": phone},
    }

    phone_number_id = os.getenv("VAPI_PHONE_NUMBER_ID")
    if phone_number_id:
        body["phoneNumberId"] = phone_number_id

    resp = http.post(
        f"{_VAPI_BASE}/call/phone",
        headers={"Authorization": f"Bearer {api_key}", "Content-Type": "application/json"},
        json=body,
        timeout=10,
    )
    resp.raise_for_status()
    call_id = resp.json().get("id", "")
    store.set_json(f"vapi:call:{call_id}", {"player_id": player_id, "status": "ringing", "transcript": ""})
    log.info("[Vapi] initiated call %s → %s", call_id, phone)
    return {"call_id": call_id, "status": "ringing"}


def get_call_status(call_id: str, store: RedisStore) -> dict[str, Any]:
    record = store.get_json(f"vapi:call:{call_id}") or {}
    return {"call_id": call_id, "status": record.get("status", "unknown")}


def process_webhook(payload: dict[str, Any], store: RedisStore) -> None:
    """Handle Vapi end-of-call webhook — marks call ended and stores transcript."""
    msg = payload.get("message") or payload
    if (msg.get("type") or payload.get("type", "")) != "end-of-call-report":
        return
    call_id = (msg.get("call") or {}).get("id", "")
    if not call_id:
        return
    transcript = (msg.get("artifact") or {}).get("transcript", "")
    record = store.get_json(f"vapi:call:{call_id}") or {}
    record.update({"status": "ended", "transcript": transcript})
    store.set_json(f"vapi:call:{call_id}", record)
    log.info("[Vapi] call %s ended (%d chars transcript)", call_id, len(transcript))


def _build_memory_context(store: RedisStore, player_id: str) -> str:
    profile = store.get_json(f"player:{player_id}:profile") or {}
    if not profile or profile.get("events_seen", 0) == 0:
        return ""
    style = profile.get("style", "balanced")
    fav = profile.get("favorite_move", "unknown")
    seen = profile.get("events_seen", 0)
    return f"This player has fought {seen} rounds before. Style: {style}. Favourite move: {fav}."
