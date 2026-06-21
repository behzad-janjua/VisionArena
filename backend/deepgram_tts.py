from __future__ import annotations

import base64
import logging
import os

import requests as http

log = logging.getLogger(__name__)

_DG_TTS_URL = "https://api.deepgram.com/v1/speak"


def synthesize_b64(text: str, voice: str) -> str | None:
    """Call Deepgram Aura TTS and return base64-encoded WAV, or None if unavailable.

    WAV (linear16) is requested so Unity can decode PCM samples without a
    third-party MP3 library — AudioClip.Create() + SetData() handles it directly.
    """
    api_key = os.getenv("DEEPGRAM_API_KEY")
    if not api_key:
        log.debug("[Deepgram] DEEPGRAM_API_KEY not set — skipping TTS")
        return None
    try:
        resp = http.post(
            f"{_DG_TTS_URL}?model={voice}&encoding=linear16&container=wav",
            headers={"Authorization": f"Token {api_key}", "Content-Type": "application/json"},
            json={"text": text},
            timeout=10,
        )
        resp.raise_for_status()
        return base64.b64encode(resp.content).decode()
    except Exception as exc:
        log.warning("[Deepgram] TTS failed: %s", exc)
        return None
