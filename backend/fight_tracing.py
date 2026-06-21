"""Arize fight-lab tracing — instruments ASI:One LLM calls and combat events.

Call setup_tracing() once at startup. When ARIZE_API_KEY + ARIZE_SPACE_ID are
present it registers a persistent OTLP provider pointed at Arize cloud and
auto-instruments every OpenAI (ASI:One) call so LLM spans appear in the UI.

combat_event spans are also written to Redis via record_combat_span() so the
Unity Fight Lab panel (/demo/fight-lab) can show the last trace.
"""

from __future__ import annotations

import logging
import os
import time
from typing import TYPE_CHECKING, Any

if TYPE_CHECKING:
    from backend.models import AgentResponse, CombatTelemetry

log = logging.getLogger(__name__)

_tracer = None
_active = False


def setup_tracing() -> bool:
    """Register Arize OTel provider + auto-instrument OpenAI. Call once at startup."""
    global _tracer, _active

    api_key  = os.getenv("ARIZE_API_KEY", "").strip()
    space_id = os.getenv("ARIZE_SPACE_ID", "").strip()

    if not (api_key and space_id):
        log.info("[Arize] ARIZE_API_KEY / ARIZE_SPACE_ID not set — tracing disabled")
        return False

    try:
        from arize.otel import register  # type: ignore[import-untyped]

        tracer_provider = register(
            space_id=space_id,
            api_key=api_key,
            model_id="kiforge-arena",
        )

        # Auto-instrument every OpenAI client call (ASI:One is OpenAI-compatible).
        try:
            from openinference.instrumentation.openai import OpenAIInstrumentor  # type: ignore[import-untyped]
            OpenAIInstrumentor().instrument(tracer_provider=tracer_provider)
            log.info("[Arize] OpenAI auto-instrumentation active")
        except ImportError:
            log.warning("[Arize] openinference-instrumentation-openai not found — LLM spans skipped")

        from opentelemetry import trace  # type: ignore[import-untyped]
        _tracer = trace.get_tracer("kiforge.arena")
        _active = True
        log.info("[Arize] Tracing active → space %s…", space_id[:12])
        return True

    except Exception as exc:  # noqa: BLE001
        log.warning("[Arize] Setup failed (%s) — continuing without tracing", exc)
        return False


def record_combat_span(
    event: "CombatTelemetry",
    response: "AgentResponse",
    player_id: str,
) -> dict[str, Any]:
    """Emit an OTel span for one combat round and return a dict for Redis storage."""
    trace_dict: dict[str, Any] = {
        "ts": time.time(),
        "player_id": player_id,
        "event": event.player_action,
        "round": event.round,
        "outcome": event.outcome,
        "boss_health": event.boss_health_after,
        "player_health": event.player_health_after,
        "move_name": response.move_name,
        "boss_action": response.boss_action,
        "narration": response.narration,
        "arize_active": _active,
        "trace_id": "",
    }

    if _tracer is None:
        return trace_dict

    with _tracer.start_as_current_span("combat_event") as span:
        span.set_attribute("player.id", player_id)
        span.set_attribute("player.action", event.player_action)
        span.set_attribute("player.health_after", event.player_health_after)
        span.set_attribute("combat.round", event.round)
        span.set_attribute("combat.outcome", event.outcome)
        span.set_attribute("combat.damage_by_player", event.damage_dealt_by_player)
        span.set_attribute("combat.damage_by_boss", event.damage_dealt_by_boss)
        span.set_attribute("boss.action", response.boss_action)
        span.set_attribute("boss.health_after", event.boss_health_after)
        span.set_attribute("narrator.move_name", response.move_name)
        span.set_attribute("narrator.narration", response.narration)
        style = (response.player_profile or {}).get("style", "")
        span.set_attribute("player.style", style)

        from opentelemetry import trace as otel_trace  # type: ignore[import-untyped]
        ctx = otel_trace.get_current_span().get_span_context()
        if ctx and ctx.is_valid:
            trace_dict["trace_id"] = format(ctx.trace_id, "032x")

    return trace_dict
