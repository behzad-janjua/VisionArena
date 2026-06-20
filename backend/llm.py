"""Minimal OpenAI-compatible chat client for KiForge Arena agents.

Targets Fetch.ai's ASI:One by default (OpenAI-compatible chat completions), but
works with any OpenAI-style endpoint via env vars. The whole module is optional:
if no key is set, no package is installed, or a call fails, `chat()` returns None
and the calling agent falls back to its deterministic output. This keeps the
hackathon demo bulletproof while still showcasing live LLM agents when keys exist.

Env:
  ASI_ONE_API_KEY        API key (also accepts OPENAI_API_KEY)
  ASI_ONE_BASE_URL       default https://api.asi1.ai/v1
  ASI_ONE_MODEL          default asi1-mini
"""

from __future__ import annotations

import logging
import os

log = logging.getLogger(__name__)

_DEFAULT_BASE_URL = "https://api.asi1.ai/v1"
_DEFAULT_MODEL = "asi1-mini"


def is_enabled() -> bool:
    return bool(os.getenv("ASI_ONE_API_KEY") or os.getenv("OPENAI_API_KEY"))


def chat(messages: list[dict[str, str]], *, temperature: float = 0.8, max_tokens: int = 200) -> str | None:
    """Run a chat completion. Returns the assistant text, or None on any failure."""
    api_key = os.getenv("ASI_ONE_API_KEY") or os.getenv("OPENAI_API_KEY")
    if not api_key:
        return None

    base_url = os.getenv("ASI_ONE_BASE_URL", _DEFAULT_BASE_URL)
    model = os.getenv("ASI_ONE_MODEL", _DEFAULT_MODEL)

    try:
        from openai import OpenAI

        client = OpenAI(api_key=api_key, base_url=base_url)
        resp = client.chat.completions.create(
            model=model,
            messages=messages,
            temperature=temperature,
            max_tokens=max_tokens,
        )
        return (resp.choices[0].message.content or "").strip() or None
    except Exception as exc:  # noqa: BLE001 — never let the LLM break gameplay
        log.warning("[LLM] chat failed, using deterministic fallback: %s", exc)
        return None
