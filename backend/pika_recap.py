"""Pika recap integration.

build_recap_prompt()      — always available; generates the cinematic prompt from
                            real match telemetry (no API key required).
submit_recap_to_pika()    — optional; calls the Pika REST API to generate the video
                            when PIKA_API_KEY is set in the environment. Falls back
                            gracefully so the demo never blocks on a missing key.

Pika endpoint:  POST https://api.pika.art/v1/generate/text-to-video
Auth:           Authorization: Bearer <PIKA_API_KEY>

Install:  pip install requests   (already in most venvs)
"""

from __future__ import annotations

import logging
import os
from typing import Any

from .models import CombatTelemetry

log = logging.getLogger(__name__)

_PIKA_API_URL = "https://api.pika.art/v1/generate/text-to-video"


def submit_recap_to_pika(prompt: str, metadata: dict[str, Any] | None = None) -> dict[str, Any]:
    """Submit a recap prompt to the Pika API and return the job dict.

    Returns a result dict with at minimum:
      {"status": "submitted"|"skipped"|"error", "job_id": str, "prompt": str}

    When PIKA_API_KEY is not set, returns {"status": "skipped"} immediately so
    the game loop is never blocked.
    """
    api_key = os.getenv("PIKA_API_KEY", "").strip()
    if not api_key:
        log.debug("[Pika] PIKA_API_KEY not set — skipping video generation")
        return {"status": "skipped", "prompt": prompt, "metadata": metadata or {}}

    try:
        import requests  # noqa: PLC0415

        payload: dict[str, Any] = {
            "prompt": prompt,
            "options": {
                "aspectRatio": "16:9",
                "frameRate": 24,
                "camera": {"zoom": "in"},
            },
        }

        response = requests.post(
            _PIKA_API_URL,
            headers={
                "Authorization": f"Bearer {api_key}",
                "Content-Type": "application/json",
            },
            json=payload,
            timeout=10,
        )
        response.raise_for_status()
        data = response.json()
        job_id = data.get("id") or data.get("job_id") or data.get("task_id") or "unknown"
        log.info("[Pika] Video job submitted: %s", job_id)
        return {
            "status": "submitted",
            "job_id": str(job_id),
            "prompt": prompt,
            "metadata": metadata or {},
            "raw": data,
        }
    except Exception as exc:
        log.warning("[Pika] Submission failed (non-fatal): %s", exc)
        return {"status": "error", "error": str(exc), "prompt": prompt, "metadata": metadata or {}}


def build_recap_prompt(events: list[CombatTelemetry], move_name: str = "Heavy Punch") -> str:
    if not events:
        return (
            "Create a 7-second anime-style battle recap in a neon arena. "
            "A fighter steps in with tight footwork and trades punches with a shadow boss."
        )

    finisher = max(events, key=lambda event: event.damage_dealt_by_player)
    outcome = "defeats" if finisher.boss_health_after <= 0 else "staggers"
    return (
        "Create a 7-second anime-style battle recap in a neon arena. "
        f"A fighter uses {move_name} after {finisher.charge_time:.1f}s of windup, "
        f"and {outcome} a towering shadow boss with a clean punch. Show footwork, guard timing, "
        "impact flash, cinematic lighting, and a dramatic ring-side finish."
    )
