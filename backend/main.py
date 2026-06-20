from __future__ import annotations

from fastapi import FastAPI, WebSocket, WebSocketDisconnect

from backend.agents import GameMasterAgent
from backend.models import CombatTelemetry, EventType, NormalizedEvent

app = FastAPI(title="KiForge Arena Backend")
game_master = GameMasterAgent()


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok", "mode": "mock-safe"}


@app.websocket("/ws/unity")
async def unity_socket(websocket: WebSocket) -> None:
    await websocket.accept()
    try:
        while True:
            raw = await websocket.receive_json()
            event = NormalizedEvent.from_dict(raw)
            if event.type == EventType.COMBAT_TELEMETRY.value:
                telemetry = CombatTelemetry(**event.payload)
                response = game_master.handle_combat_event(telemetry)
                await websocket.send_json(response.to_event().to_dict())
            else:
                await websocket.send_json(event.to_dict())
    except WebSocketDisconnect:
        return
