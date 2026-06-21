# Vision Arena Devpost Notes

## Track Story

Vision Arena is a 2.5D Unity boss fight controlled by body input. Unity handles the real-time combat loop while backend agents handle strategy, narration, memory, evaluation, and recap generation.

## Sponsor Mapping

- Fetch.ai: GameMaster, Enemy, Narrator, and Recap agent boundaries.
- Arize: deterministic fight-lab evaluator now, trace/export integration later.
- Redis: player profile, boss strategy, match history, generated move names.
- Pika: recap prompt generation from real match telemetry.

## Demo Path

1. Start Unity scene with `KiForgeArenaBootstrap`.
2. Use keyboard/mouse fallback to charge, aim, slash, shield, and ultimate.
3. Show mock agent narration and fight-lab adaptation panel.
4. Optionally start FastAPI backend and route combat telemetry through `/ws/unity`.
5. Show generated Pika recap prompt after a fight.
