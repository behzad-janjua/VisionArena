"""Fight-lab trace builder + optional Arize Phoenix / OpenTelemetry export.

When PHOENIX_COLLECTOR_ENDPOINT is set (or ARIZE_API_KEY + ARIZE_SPACE_ID), each
combat turn is exported as an OpenTelemetry trace so you can inspect the
ReadPlayerMemory → EnemyAgentDecision → Narrator → FightLabEvaluate →
UpdateStrategyMemory pipeline in the Phoenix UI.

Without those env vars the function works exactly as before (local-only traces
returned as FightLabTrace dicts and stored in Redis).
"""

from __future__ import annotations

import logging
import os
from dataclasses import asdict
from uuid import uuid4

from backend.models import CombatTelemetry, FightLabTrace, TraceSpan

log = logging.getLogger(__name__)


def _try_export_to_phoenix(trace: FightLabTrace) -> None:
    """Optional: export the trace to Arize Phoenix via OpenTelemetry.

    Requires one of:
      PHOENIX_COLLECTOR_ENDPOINT=http://localhost:6006/v1/traces   (self-hosted)
      ARIZE_API_KEY + ARIZE_SPACE_ID                               (Arize cloud)

    Install deps:  pip install arize-phoenix-otel openinference-instrumentation
    """
    endpoint = os.getenv("PHOENIX_COLLECTOR_ENDPOINT")
    arize_key = os.getenv("ARIZE_API_KEY")
    arize_space = os.getenv("ARIZE_SPACE_ID")

    if not endpoint and not (arize_key and arize_space):
        return  # Arize not configured — local-only mode

    try:
        from opentelemetry import trace as otel_trace
        from opentelemetry.sdk.trace import TracerProvider
        from opentelemetry.sdk.trace.export import SimpleSpanProcessor
        from opentelemetry.exporter.otlp.proto.http.trace_exporter import OTLPSpanExporter

        if arize_key and arize_space and not endpoint:
            # Arize cloud endpoint (space-based routing).
            endpoint = f"https://otlp.arize.com/v1/traces"
            headers = {"authorization": f"Bearer {arize_key}", "space-id": arize_space}
        else:
            headers = {}

        exporter = OTLPSpanExporter(endpoint=endpoint, headers=headers)
        provider = TracerProvider()
        provider.add_span_processor(SimpleSpanProcessor(exporter))
        tracer = provider.get_tracer("kiforge.fight_lab")

        with tracer.start_as_current_span(
            "MatchTurn",
            attributes={
                "trace_id": trace.trace_id,
                "match_id": trace.match_id,
                "player_id": trace.player_id,
                "round": trace.round,
            },
        ) as root:
            for span_data in trace.spans:
                with tracer.start_as_current_span(
                    span_data.name,
                    attributes={
                        f"input.{k}": str(v) for k, v in (span_data.inputs or {}).items()
                    },
                ) as span:
                    for k, v in (span_data.outputs or {}).items():
                        span.set_attribute(f"output.{k}", str(v))

        log.debug("[Arize] Exported trace %s (%d spans) to %s", trace.trace_id, len(trace.spans), endpoint)
    except Exception as exc:
        log.warning("[Arize] Phoenix export failed (non-fatal): %s", exc)


def build_fight_lab_trace(
    *,
    player_id: str,
    match_id: str,
    event: CombatTelemetry,
    profile_before: dict,
    boss_action: str,
    strategy: str,
    move_name: str,
    narration: str,
    evaluation: object,
    profile_after: dict,
    strategy_weights: dict[str, float],
) -> FightLabTrace:
    eval_payload = evaluation.to_dict() if hasattr(evaluation, "to_dict") else asdict(evaluation)
    trace = FightLabTrace(
        trace_id=f"trace_{uuid4().hex[:12]}",
        match_id=match_id,
        player_id=player_id,
        round=event.round,
        spans=[
            TraceSpan(
                name="ReadPlayerMemory",
                inputs={"player_id": player_id},
                outputs={"profile": profile_before},
            ),
            TraceSpan(
                name="EnemyAgentDecision",
                inputs={"telemetry": asdict(event), "profile": profile_before},
                outputs={"boss_action": boss_action, "strategy": strategy, "strategy_weights": strategy_weights},
            ),
            TraceSpan(
                name="NarratorAgentGenerateCommentary",
                inputs={"player_action": event.player_action, "damage": event.damage_dealt_by_player},
                outputs={"move_name": move_name, "narration": narration},
            ),
            TraceSpan(
                name="FightLabEvaluateDecision",
                inputs={"boss_action": boss_action, "outcome": event.outcome},
                outputs=eval_payload,
            ),
            TraceSpan(
                name="UpdateStrategyMemory",
                inputs={"profile_before": profile_before, "evaluation": eval_payload},
                outputs={"profile_after": profile_after, "strategy_weights": strategy_weights},
            ),
        ],
    )
    _try_export_to_phoenix(trace)
    return trace
