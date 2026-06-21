# KiForge Backend

Run the mock-safe service:

```bash
python3 -m venv .venv
source .venv/bin/activate
pip install -r backend/requirements.txt
uvicorn backend.main:app --reload
```

The Unity client can run without this backend. When connected, send normalized JSON events to `ws://127.0.0.1:8000/ws/unity`.

Demo endpoints:

- `GET /demo/state` returns player memory, recent combat telemetry, traces, and recap jobs.
- `GET /demo/player-profile` returns the Redis-backed player profile.
- `GET /demo/fight-lab` returns Arize-style evals, adaptation, strategy weights, and traces.
- `GET /demo/recap` returns the latest Pika prompt plus queued recap jobs.
- `GET /demo/redis` is the Redis sponsor panel: backend type, boss phase, live TTL cooldowns, generated move names, recent match-stream entries, and vector recall.
- `GET /demo/memory-recall` runs KNN over player-style vectors to find the most similar player the boss has fought before.
- `GET /demo/cooldowns` returns live ability cooldowns (Redis TTL countdown).
- `GET /demo/stream` returns recent entries from the Redis match telemetry stream.
- `POST /demo/combat` accepts a `CombatTelemetry` JSON object and returns an agent response.
- `POST /agent/combat` is the stable Agentverse/API endpoint for external integration.
- `GET /agent/state` exposes the external-agent memory/state view.

## Redis usage

Set `REDIS_URL` to enable persistence; leave it blank for an in-memory fallback so
everything still runs without Redis. Point it at a **Redis Stack** instance
(Redis Cloud, or `docker run -p 6379:6379 redis/redis-stack`) to unlock vector recall.

| Feature | Redis structure | Where |
|---|---|---|
| Player profile, style, strategy weights, eval summaries | JSON strings | `player:<id>:profile` |
| Match history + fight-lab traces | Lists | `player:<id>:match_events`, `:fight_lab_traces` |
| Generated move names | List | `player:<id>:move_names` |
| Recap prompts | List | `player:<id>:recap_prompts` |
| Boss phase | JSON string | `player:<id>:boss_phase` |
| Ability cooldowns | Keys with TTL (`SET ... EX`) | `arena:cooldown:<id>:<ability>` |
| Match telemetry feed | Stream (`XADD`) | `arena:match:stream` |
| Live narration | Pub/Sub (`PUBLISH`) | channel `arena:narration` |
| Boss associative memory | RediSearch vector index (KNN, cosine) | `arena:player_idx` / `arena:vss:<id>` |

The boss's associative memory is the headline: each player's fighting style is
encoded as a 5-dim vector. When a player appears, the boss runs a KNN query to find
the most similar player it has fought before and reuses the strategy that beat them.
Without Redis Stack modules the same recall runs via an in-memory cosine fallback.

Mock MYO bridge:

```bash
python -m backend.myo_listener --repeat 3
python -m backend.myo_listener --ws ws://127.0.0.1:8000/ws/unity --repeat 3
```

## Agentverse + ASI:One (Fetch.ai)

The GameMasterAgent registers on Agentverse using **External Integration** — it runs
locally and connects via a **Mailbox**, so there's no public server to host. It
publishes two protocols: the **Agent Chat Protocol (ACP)** for ASI:One, and a
structured `kiforge_combat` protocol for the Unity client.

```bash
# (uses port 8001 so it doesn't collide with the FastAPI backend on 8000)
python -m backend.uagents_app
```

On startup it prints the agent address and an **Agent Inspector** link. To finish
registration:

1. Open the printed `https://agentverse.ai/inspect/?...` link (log in to Agentverse).
2. Click **Create Mailbox** — this links the locally-running agent to Agentverse.
3. Open **ASI:One**, search for the agent by name (`kiforge_game_master`) or address,
   and chat: say `start duel` to run a demo turn, or paste `CombatTelemetry` JSON.

Set `FETCH_AI_AGENT_SEED` in `.env` to keep a stable agent address across restarts
(override the port with `AGENT_PORT`). The agent works without funds — Almanac *API*
registration succeeds; the "not enough funds to register on Almanac contract" warning
is expected and not needed for Mailbox + ASI:One.
