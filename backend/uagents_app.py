"""Battle Agent — Fetch.ai Agentverse entrypoint (External Integration).

This runs the GameMasterAgent locally and registers it on Agentverse via a
**Mailbox**, so it is discoverable and chattable from ASI:One without exposing
a public server. Two protocols are published:

  * Chat Protocol (ACP)  — lets ASI:One / Agentverse Chat talk to the boss in
    natural language or by pasting combat telemetry JSON.
  * CombatEvent protocol — structured request/reply for the Unity client and
    other agents.

Run it:  python -m backend.uagents_app
Set FETCH_AI_AGENT_SEED in .env to keep a stable agent address across restarts.
"""

from __future__ import annotations

import os
from datetime import datetime, timezone
from uuid import uuid4

from backend.agents import GameMasterAgent
from backend.agentverse_adapter import respond_to_text as respond_to_agent_text
from backend.models import CombatTelemetry


game_master = GameMasterAgent()

def handle_agent_message(payload: dict, player_id: str = "agentverse_player") -> dict:
    """Shared adapter for Agentverse/ASI:One wrappers and local tests."""
    telemetry = CombatTelemetry(**payload)
    return game_master.handle_combat_event(telemetry, player_id=player_id).to_event().payload


def respond_to_text(text: str, player_id: str = "asi_one_player") -> str:
    """Core chat handler — usable standalone in tests, no uagents needed."""
    return respond_to_agent_text(text, handle_agent_message, player_id)


# --------------------------------------------------------------------------- #
# uAgents wiring (optional import — keeps tests runnable without the package).
# --------------------------------------------------------------------------- #
try:
    from uagents import Agent, Context, Model, Protocol
    from uagents_core.contrib.protocols.chat import (
        ChatAcknowledgement,
        ChatMessage,
        TextContent,
        chat_protocol_spec,
    )

    class CombatEvent(Model):
        round: int
        player_action: str
        charge_time: float
        accuracy: float
        damage_dealt_by_player: int
        damage_dealt_by_boss: int
        boss_action: str
        boss_health_after: int
        player_health_after: int
        outcome: str

    class AgentReply(Model):
        payload: dict

    # Own port so it doesn't collide with the FastAPI backend (default 8000).
    agent = Agent(
        name="battle_agent",
        seed=os.getenv("FETCH_AI_AGENT_SEED", "kiforge-arena-demo-seed"),
        port=int(os.getenv("AGENT_PORT", "8001")),
        mailbox=True,
        publish_agent_details=True,
    )

    def _text_message(text: str) -> ChatMessage:
        return ChatMessage(
            timestamp=datetime.now(timezone.utc),
            msg_id=uuid4(),
            content=[TextContent(type="text", text=text)],
        )

    # --- Chat protocol (ACP) for ASI:One / Agentverse Chat ------------------ #
    chat_proto = Protocol(spec=chat_protocol_spec)

    @chat_proto.on_message(ChatMessage)
    async def on_chat(ctx: Context, sender: str, msg: ChatMessage) -> None:
        # Acknowledge receipt first (required by the chat protocol).
        await ctx.send(
            sender,
            ChatAcknowledgement(timestamp=datetime.now(timezone.utc), acknowledged_msg_id=msg.msg_id),
        )
        for item in msg.content:
            if isinstance(item, TextContent):
                reply = respond_to_text(item.text, player_id=f"asi:{sender[:16]}")
                await ctx.send(sender, _text_message(reply))

    @chat_proto.on_message(ChatAcknowledgement)
    async def on_chat_ack(ctx: Context, sender: str, msg: ChatAcknowledgement) -> None:
        ctx.logger.debug("Chat ack from %s for %s", sender, msg.acknowledged_msg_id)

    # --- Structured combat protocol for the Unity client -------------------- #
    combat_proto = Protocol(name="battle_combat", version="1.0")

    @combat_proto.on_message(model=CombatEvent)
    async def on_combat_event(ctx: Context, sender: str, msg: CombatEvent) -> None:
        payload = handle_agent_message(msg.dict())
        await ctx.send(sender, AgentReply(payload=payload))

    @agent.on_event("startup")
    async def startup(ctx: Context) -> None:
        ctx.logger.info("Battle Agent ready: %s", agent.address)
        ctx.logger.info("Add this address to ASI:One / Agentverse to chat with the boss.")

    agent.include(chat_proto, publish_manifest=True)
    agent.include(combat_proto, publish_manifest=True)

except Exception:  # noqa: BLE001 — uagents not installed or failed to import
    agent = None


if __name__ == "__main__":
    if agent is None:
        raise SystemExit("uagents is not installed. Run: pip install uagents")
    agent.run()
