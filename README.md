# KiForge Arena

Unity-first hackathon scaffold for a 2.5D anime boss fight controlled by gesture, pose, and fallback keyboard/mouse input.

## Current Architecture

- `Assets/Scripts/Input`: keyboard fallback plus backend injection points for MYO/PyoMyo and MediaPipe pose events.
- `Assets/Scripts/Combat`: charge levels, boss/player health, shields, slashes, ultimates, and strategy weights.
- `Assets/Scripts/Effects`: runtime aura, shield, beam, slash trail, and screen shake effects.
- `Assets/Scripts/Telemetry`: match event recorder and mock agent client.
- `Assets/Scripts/UI`: runtime HUD, charge bar, health bars, narration, reticle, and fight-lab panel.
- `backend/`: FastAPI WebSocket service, mock-safe agent workflow, Redis wrapper, Arize-style evaluator, Pika prompt generator.

## Unity Demo

1. Open this folder in Unity 2022.3 or newer.
2. Create an empty scene.
3. Add an empty GameObject named `KiForge Arena`.
4. Attach `KiForgeArenaBootstrap`.
5. Press Play.

Fallback controls:

| Input | Action |
| --- | --- |
| Mouse | Aim |
| Arrow keys / horizontal axis | Move |
| Hold `F` | Charge |
| Release `F` | Fire blast |
| `S` | Shield |
| `A` / `D` | Slash left / right |
| Space | Ultimate |

## Backend Demo

The Unity scene runs without the backend by default. To run the mock-safe backend:

```bash
python3 -m venv .venv
source .venv/bin/activate
pip install -r backend/requirements.txt
uvicorn backend.main:app --reload
```

Health check: `http://127.0.0.1:8000/health`

Unity/backend event socket: `ws://127.0.0.1:8000/ws/unity`

## Tests

Backend:

```bash
pytest
```

Unity:

- Open Unity Test Runner.
- Run EditMode tests under `Assets/Tests/EditMode`.

## Hackathon Reliability

MYO, MediaPipe, Fetch.ai, Arize, Redis, and Pika all have mock/fallback seams in this scaffold. The intended flow is to make the local Unity fight fun first, then progressively connect live hardware and sponsor APIs without risking the core demo.
