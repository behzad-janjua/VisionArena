from __future__ import annotations

import asyncio
import logging
from contextlib import asynccontextmanager
from typing import Any

from fastapi import FastAPI, WebSocket, WebSocketDisconnect
from pydantic import BaseModel, Field

from backend.agents import GameMasterAgent
from backend.agents.game_master_agent import _TRACKED_ABILITIES
from backend.agentverse_adapter import respond_to_text as respond_to_agent_text
from backend.commentary_adapter import respond_to_commentary_text
from backend.models import CombatTelemetry, EventType, NormalizedEvent
from backend.player_memory import style_vector
from backend.vision_bridge import vision_bridge_stream

log = logging.getLogger(__name__)


class _ConnectionManager:
    """Tracks connected Unity clients and broadcasts pose events to all of them."""

    def __init__(self) -> None:
        self._queues: list[asyncio.Queue[dict[str, Any]]] = []

    def connect(self) -> asyncio.Queue[dict[str, Any]]:
        q: asyncio.Queue[dict[str, Any]] = asyncio.Queue(maxsize=60)
        self._queues.append(q)
        return q

    def disconnect(self, q: asyncio.Queue[dict[str, Any]]) -> None:
        if q in self._queues:
            self._queues.remove(q)

    def broadcast(self, payload: dict[str, Any]) -> None:
        """Non-blocking push to all client queues; drops frames when a queue is full."""
        for q in self._queues:
            try:
                q.put_nowait(payload)
            except asyncio.QueueFull:
                pass


_manager = _ConnectionManager()
_game_master = GameMasterAgent()


class CombatTelemetryRequest(BaseModel):
    round: int = Field(..., description="Current combat round.")
    player_action: str = Field(..., description="Player action, e.g. left_punch, right_punch, heavy_punch, very_heavy_punch, guard.")
    charge_time: float = Field(..., description="Seconds the player charged before releasing.")
    accuracy: float = Field(..., ge=0.0, le=1.0, description="Player aim/pose accuracy from 0 to 1.")
    damage_dealt_by_player: int
    damage_dealt_by_boss: int
    boss_action: str = Field("unknown", description="Previous or proposed boss action, overwritten by the agent.")
    boss_health_after: int
    player_health_after: int
    outcome: str = Field(..., description="Outcome label, e.g. boss_staggered, boss_ko, player_blocked.")


class AgentChatRequest(BaseModel):
    message: str = Field(..., description="Natural language request or CombatTelemetry JSON.")
    player_id: str = Field("agentverse_player", description="Stable player/session id for memory.")


class AgentChatResponse(BaseModel):
    reply: str
    player_id: str


def _handle_agent_payload(payload: dict[str, Any], player_id: str) -> dict[str, Any]:
    telemetry = CombatTelemetry(**payload)
    return _game_master.handle_combat_event(telemetry, player_id=player_id).to_event().payload


def _model_dict(model: BaseModel) -> dict[str, Any]:
    if hasattr(model, "model_dump"):
        return model.model_dump()
    return model.dict()


async def _vision_task() -> None:
    """Background task: reads CV frames and broadcasts POSE_UPDATE to all Unity clients."""
    async for event in vision_bridge_stream():
        _manager.broadcast(event.to_dict())


@asynccontextmanager
async def lifespan(app: FastAPI):
    task = asyncio.create_task(_vision_task())
    try:
        yield
    finally:
        task.cancel()
        try:
            await task
        except asyncio.CancelledError:
            pass


app = FastAPI(title="KiForge Arena Backend", lifespan=lifespan)


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok", "cv": "live"}


@app.get("/demo/state")
def demo_state(player_id: str = "demo_player") -> dict[str, Any]:
    return _game_master.demo_state(player_id)


@app.get("/demo/player-profile")
def demo_player_profile(player_id: str = "demo_player") -> dict[str, Any]:
    return _game_master.store.get_json(f"player:{player_id}:profile")


@app.get("/demo/fight-lab")
def demo_fight_lab(player_id: str = "demo_player") -> dict[str, Any]:
    state = _game_master.demo_state(player_id)
    profile = state["player_profile"]
    latest_response = state["latest_response"]
    return {
        "player_style": profile.get("style", "unknown"),
        "boss_counter_success_before": profile.get("boss_counter_success_before", 0.0),
        "boss_counter_success_after": profile.get("boss_counter_success_after", 0.0),
        "most_common_player_move": profile.get("favorite_move", "none"),
        "boss_adaptation": profile.get("boss_adaptation", latest_response.get("next_strategy", "")),
        "tactical_plan": profile.get("tactical_plan", latest_response.get("tactical_plan", {})),
        "strategy_weights": profile.get("strategy_weights", latest_response.get("strategy_weights", {})),
        "last_trace": state["recent_traces"][-1] if state["recent_traces"] else {},
        "recent_traces": state["recent_traces"],
    }


@app.get("/demo/redis")
def demo_redis(player_id: str = "demo_player") -> dict[str, Any]:
    """Sponsor-track panel: shows every Redis structure powering the game."""
    store = _game_master.store
    return {
        "backend": store.backend_label,
        "using_memory_store": store.using_memory,
        "vector_search": store.using_vector_search,
        "boss_phase": store.get_boss_phase(player_id),
        "active_cooldowns": store.active_cooldowns(player_id, list(_TRACKED_ABILITIES)),
        "move_names": store.get_move_names(player_id, limit=20),
        "recap_prompts": store.get_recap_prompts(player_id, limit=5),
        "match_stream": store.stream_recent(count=20),
        "memory_recall": _game_master.store.get_json(f"player:{player_id}:profile").get("memory_recall", []),
    }


@app.get("/demo/memory-recall")
def demo_memory_recall(player_id: str = "demo_player", k: int = 3) -> dict[str, Any]:
    """KNN over player-style vectors: who has the boss fought that fights like this?"""
    store = _game_master.store
    profile = store.get_json(f"player:{player_id}:profile")
    vector = style_vector(profile)
    return {
        "player_id": player_id,
        "vector_search": store.using_vector_search,
        "query_vector": vector,
        "similar_players": store.recall_similar(vector, k=k, exclude=player_id),
    }


@app.get("/demo/cooldowns")
def demo_cooldowns(player_id: str = "demo_player") -> dict[str, Any]:
    return {
        "player_id": player_id,
        "active_cooldowns": _game_master.store.active_cooldowns(player_id, list(_TRACKED_ABILITIES)),
    }


@app.get("/demo/stream")
def demo_stream(count: int = 20) -> dict[str, Any]:
    return {"match_stream": _game_master.store.stream_recent(count=count)}


@app.get("/demo/recap")
def demo_recap() -> dict[str, Any]:
    state = _game_master.demo_state()
    latest_response = state["latest_response"]
    return {
        "latest_prompt": latest_response.get("recap_prompt", ""),
        "latest_job": latest_response.get("recap_job", {}),
        "queued_jobs": state["recap_jobs"],
    }


@app.post("/demo/combat")
def demo_combat(payload: CombatTelemetryRequest, player_id: str = "demo_player") -> dict[str, Any]:
    return _handle_agent_payload(_model_dict(payload), player_id)


@app.post("/agent/combat")
def agent_combat(payload: CombatTelemetryRequest, player_id: str = "agentverse_player") -> dict[str, Any]:
    """Stable Agentverse/API integration route for combat decisions."""
    return _handle_agent_payload(_model_dict(payload), player_id)


@app.post("/agent/chat", response_model=AgentChatResponse)
def agent_chat(payload: AgentChatRequest) -> AgentChatResponse:
    """Agentverse FastAPI chat route: natural language in, boss-agent reply out."""
    reply = respond_to_agent_text(payload.message, _handle_agent_payload, payload.player_id)
    return AgentChatResponse(reply=reply, player_id=payload.player_id)


@app.post("/agent/commentary", response_model=AgentChatResponse)
def agent_commentary(payload: AgentChatRequest) -> AgentChatResponse:
    """Agentverse chat route for the Commentator Agent: move name + narration only."""
    reply = respond_to_commentary_text(payload.message)
    return AgentChatResponse(reply=reply, player_id=payload.player_id)


@app.get("/agent/state")
def agent_state(player_id: str = "agentverse_player") -> dict[str, Any]:
    """Small read endpoint for hosted agent health checks and judge demos."""
    return _game_master.demo_state(player_id)


@app.get("/agent/manifest")
def agent_manifest() -> dict[str, Any]:
    """Small capability card for Agentverse external integration setup."""
    return {
        "name": "battle_agent",
        "title": "Battle Agent",
        "description": "Adaptive anime boss-fight agent that counters player gestures, narrates moves, and records fight-lab memory.",
        "framework": "FastAPI",
        "protocols": ["Agent Chat Protocol", "structured combat telemetry"],
        "chat_endpoint": "/agent/chat",
        "commentary_endpoint": "/agent/commentary",
        "combat_endpoint": "/agent/combat",
        "state_endpoint": "/agent/state",
        "example_message": "start duel",
    }


@app.websocket("/ws/unity")
async def unity_socket(websocket: WebSocket) -> None:
    """
    Full-duplex Unity connection:
      - Server pushes POSE_UPDATE frames from the CV vision task.
      - Client sends COMBAT_TELEMETRY; server replies with AGENT_RESPONSE.
    """
    await websocket.accept()
    q = _manager.connect()

    async def _send_loop() -> None:
        while True:
            data = await q.get()
            await websocket.send_json(data)

    async def _recv_loop() -> None:
        while True:
            raw = await websocket.receive_json()
            event = NormalizedEvent.from_dict(raw)
            if event.type == EventType.COMBAT_TELEMETRY.value:
                telemetry = CombatTelemetry(**event.payload)
                response = _game_master.handle_combat_event(telemetry)
                await q.put(response.to_event().to_dict())
            else:
                log.debug("[WS] Unhandled event type from Unity: %s", event.type)

    send_task = asyncio.create_task(_send_loop())
    try:
        await _recv_loop()
    except WebSocketDisconnect:
        pass
    except Exception as exc:
        log.warning("[WS] Unity socket error: %s", exc)
    finally:
        send_task.cancel()
        _manager.disconnect(q)
