import os
from uagents_core.utils.registration import (
    register_chat_agent,
    RegistrationRequestCredentials,
)

register_chat_agent(
    "Commentator Agent",
    "https://proxy-abide-proofs.ngrok-free.dev/agent/commentary",
    active=True,
    credentials=RegistrationRequestCredentials(
        agentverse_api_key=os.environ["COMMENTATOR_AGENTVERSE_KEY"],
        agent_seed_phrase=os.environ["COMMENTATOR_AGENT_SEED_PHRASE"],
    ),
)
