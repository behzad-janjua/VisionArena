from __future__ import annotations

from backend.agents import GameMasterAgent
from backend.models import CombatTelemetry


game_master = GameMasterAgent()


def handle_agent_message(payload: dict) -> dict:
    """Shared adapter for Agentverse/ASI:One wrappers and local tests."""
    telemetry = CombatTelemetry(**payload)
    return game_master.handle_combat_event(telemetry).to_event().payload


try:
    from uagents import Agent, Context, Model

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

    agent = Agent(name="kiforge_game_master", seed="kiforge-arena-demo-seed")

    @agent.on_event("startup")
    async def startup(ctx: Context) -> None:
        ctx.logger.info("KiForge GameMasterAgent ready: %s", agent.address)

    @agent.on_message(model=CombatEvent)
    async def on_combat_event(ctx: Context, sender: str, msg: CombatEvent) -> None:
        payload = handle_agent_message(msg.dict())
        await ctx.send(sender, AgentReply(payload=payload))

except Exception:
    agent = None


if __name__ == "__main__":
    if agent is None:
        raise SystemExit("uagents is not installed. Install it before running this Agentverse entrypoint.")
    agent.run()
