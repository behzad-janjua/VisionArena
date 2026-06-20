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
- `POST /demo/combat` accepts a `CombatTelemetry` JSON object and returns an agent response.
- `POST /agent/combat` is the stable Agentverse/API endpoint for external integration.
- `GET /agent/state` exposes the external-agent memory/state view.

Mock MYO bridge:

```bash
python -m backend.myo_listener --repeat 3
python -m backend.myo_listener --ws ws://127.0.0.1:8000/ws/unity --repeat 3
```

Optional Agentverse/uAgents entrypoint:

```bash
python -m backend.uagents_app
```
