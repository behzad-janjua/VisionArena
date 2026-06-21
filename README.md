# Vision Arena

A real-time 2.5D anime boss fight controlled by full-body pose tracking, MYO EMG armband, and AI agents — built for the Fetch.ai / ASI:One hackathon.

**Live agent (ASI:One):** https://asi1.ai/chat/a8e8512f-aac3-40ee-949b-4b7dbf310f3e
**Agentverse address:** `agent1q0x73mhcy54lj0efh3eus5zxxqgkjkzascm8syy222c2ypx2zzkmy0gtez6`

## Current Architecture

- `Assets/Scripts/Input`: keyboard fallback plus backend injection points for MYO/PyoMyo and MediaPipe pose events.
- `Assets/Scripts/Combat`: punch tiers, boss/player health, guard timing, and strategy weights.
- `Assets/Scripts/Effects`: runtime charge aura, guard flash, punch impact trails, and screen shake effects.
- `Assets/Scripts/Telemetry`: match event recorder and mock agent client.
- `Assets/Scripts/UI`: runtime HUD, charge bar, health bars, narration, reticle, and fight-lab panel.
- `backend/`: FastAPI WebSocket service, mock-safe agent workflow, Redis wrapper, Arize-style evaluator, Pika prompt generator.

## Unity Demo

1. Open this folder in Unity 2022.3 or newer.
2. Create an empty scene.
3. Add an empty GameObject named `Vision Arena`.
4. Attach `KiForgeArenaBootstrap`.
5. Press Play.

Fallback controls:

| Input | Action |
| --- | --- |
| `A` / `D` | Move left / right |
| `J` / `K` | Left / right punch |
| `U` / `I` | Heavy / very-heavy punch |
| `;` / `'` | Hold to guard left / right |

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
