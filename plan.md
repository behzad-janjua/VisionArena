# KiForge Arena — Hackathon Build Plan

## Project Summary

**Title:** KiForge Arena  
**Main Track:** Ddoski’s Playground  
**Core Pitch:** A real-time anime boss-fight game where your computer-vision hand gestures control movement and punch timing, and AI agents run the enemy, narrator, coach, and post-fight recap.

**Core Interaction:**

- Computer vision detects open palm and fist gestures.
- Open palm drives player movement.
- Closed fist throws a punch.
- Longer punch windups create heavier punch tiers.
- Fetch.ai agents run the game master, enemy behavior, narration, coaching, and recap workflow.
- Arize traces and evaluates enemy decisions so the boss gets better at fighting the player.
- Redis stores player style, boss memory, match history, and generated punch names.
- Pika generates post-fight boxing-style recap videos from real match telemetry.

---

## Strategic Positioning

### Main Track

**Ddoski’s Playground**

This project is a game, an interactive experience, and an experimental AI interface. It is not just a chatbot or dashboard; it is an embodied AI battle system.

### Sponsor Tracks to Target

| Sponsor | Why It Fits |
|---|---|
| **Fetch AI** | Core agent system: GameMasterAgent, EnemyAgent, NarratorAgent, CoachAgent, RecapAgent. Agents should be registered on Agentverse and demoable through ASI:One. |
| **Arize** | Traces and evaluates boss decisions, then uses those evaluations to improve the EnemyAgent’s strategy between rounds. |
| **Redis** | Stores persistent player style, match history, boss strategy memory, cooldowns, generated move names, and recap data. |
| **Pika** | Generates post-fight cinematic recap videos from match telemetry. |
| **Band** | Optional: host EnemyAgent, RefereeAgent, NarratorAgent, and CoachAgent in a shared multi-agent room. |
| **Deepgram** | Optional: voice commands such as “start duel,” “guard,” or AI announcer narration. |
| **Sentry** | Optional: reliability/error monitoring for Unity, backend, and the agent pipeline. |

---

## Core Demo Moment

The ideal judge demo:

1. You stand in front of the webcam.
2. The game tracks your wrist and body position.
3. You open your palm to move forward.
4. You close your fist to throw a punch.
5. Longer windup creates a heavier punch.
6. The boss takes damage if the punch lands in range.
7. The NarratorAgent names the punch.
8. Arize/Fight Lab shows the boss decision trace and evaluation.
9. The boss starts with a bad baseline policy.
10. Arize/Fight Lab catches the bad counter.
11. Battle Agent adapts and chooses the correct punch counter on the next exchange.
12. At the end, the RecapAgent creates a Pika prompt for a boxing-style recap.

---

## Input System

### Computer Vision Gesture Map

| CV Input | Game Action |
|---|---|
| **Open palm** | Walk / move forward |
| **Closed fist** | Throw punch |
| **Short fist / quick tap** | Normal punch |
| **Longer fist windup** | Heavy punch |
| **Longest fist windup** | Very-heavy punch |
| **Keyboard fallback** | Punch and guard controls when CV is unavailable |

### Computer Vision Map

| CV Signal | Game Use |
|---|---|
| Wrist position | Aim reticle and attack origin |
| Hand gesture state | Open palm movement or fist punch |
| Body center x-position | Player movement |
| Arm angle | High/mid/low targeting |
| Pose stillness | Charge stability bonus |
| Body lean | Dodge direction, optional |

### MVP CV Scope

For the current version, only use:

- Open palm for movement.
- Closed fist for punch.
- Keyboard fallback for explicit left/right/heavy punch testing.

Avoid overbuilding CV during the hackathon.

---

## Combat System

### Punch Tier Mechanic

| Punch Tier | Windup / Input | Attack |
|---|---:|---|
| Level 1 | Quick fist / quick key press | Normal punch |
| Level 2 | Medium windup | Heavy punch |
| Level 3 | Longest windup | Very-heavy punch |

### Punch Modifiers

| Condition | Result |
|---|---|
| Longer fist windup | More punch damage |
| Good spacing | Punch connects |
| Too far from boss | Punch whiffs |
| Guard timing | Incoming punch damage is reduced |
| Boss adapts after bad eval | Better counter on next exchange |

### Core Actions

| Action | Description |
|---|---|
| Left punch | Quick left-hand strike |
| Right punch | Quick right-hand strike |
| Heavy punch | Slower, higher-damage punch |
| Very-heavy punch | Slowest, highest-damage punch |
| Guard | Blocks/reduces incoming punch damage |
| Dodge | Boss slips out of range |

### Example Punch Patterns

| Pattern | Tactical Meaning |
|---|---|
| Repeated heavy punch | Player is a `heavy_puncher`; boss should pressure the windup |
| Repeated guard | Player is a `guard_turtle`; boss should bait guard and heavy-counter |
| Repeated quick punches | Player is a `combo_puncher`; boss should dodge and jab back |

---

## Animation Plan

For a 24-hour hackathon, do not build complex character animation. Use procedural animation and layered visual effects.

### Visual Style

- Dark arena background.
- Neon player silhouette.
- Boss silhouette.
- Punch impact flashes.
- Guard arcs.
- Footwork and hit reactions.
- Large anime-style punch names.
- Screen shake on impact.

### Game States

| State | Visual Effect |
|---|---|
| `idle` | Slight bobbing |
| `winding_up` | Fighter commits to a heavier punch |
| `guarding` | Translucent guard arc |
| `left_punch` | Left punch animation |
| `right_punch` | Right punch animation |
| `dash` | Motion blur / quick displacement |
| `heavy_punch` | Slower punch with stronger impact |
| `hit` | Flash + knockback + damage number |
| `very_heavy_punch` | Big windup + flash + screen shake |
| `ko` | Fade/drop animation |

### Effects to Build First

| Effect | Implementation |
|---|---|
| Punch windup | Animator windup / heavier punch tier |
| Health bars | UI bars showing player and boss health |
| Punch impact | Flash/knockback on contact |
| Guard | Semi-transparent arc/circle in front of player |
| Hit impact | Flash, knockback, screen shake, damage number |

### Animation Event Pipeline

The input layer should emit events like:

```json
{
  "type": "CHARGE_START",
  "origin": { "x": 420, "y": 330 },
  "timestamp": 123456
}
```

```json
{
  "type": "CHARGE_UPDATE",
  "charge": 0.72,
  "emgIntensity": 0.81,
  "origin": { "x": 430, "y": 325 },
  "aim": { "x": 0.9, "y": -0.2 }
}
```

```json
{
  "type": "HEAVY_PUNCH_RELEASE",
  "charge": 0.94,
  "origin": { "x": 430, "y": 325 },
  "target": { "x": 760, "y": 260 }
}
```

The animation layer should respond to these events rather than directly depending on MYO or CV code.

---

## Recommended Tech Stack

### Unity-First Build Stack

| Layer | Tool |
|---|---|
| Game client | Unity 2.5D scene |
| Game loop | Unity MonoBehaviours + event bus |
| Visuals | Unity primitives, UI, LineRenderer, particles |
| MYO input | PyoMyo Python listener |
| CV | MediaPipe Pose / Hands |
| Backend | FastAPI + WebSockets |
| Agents | Fetch.ai uAgents + Agentverse + ASI:One |
| Memory | Redis |
| Observability/evals | Arize Phoenix |
| Cinematics | Pika |
| Optional voice | Deepgram |

### Recommended Choice

Use **Unity 2.5D** for the hackathon implementation. Unity owns the real-time combat, input fallback, HUD, punch impacts, guard timing, and arena presentation. The Python backend owns hardware bridges, agents, Redis memory, Arize evals, and Pika recap generation.

---

## System Architecture

```text
MYO Armband
  ↓
PyoMyo Python Listener
  ↓
Gesture Events
  ↓
FastAPI WebSocket Server
  ↓
Unity Game Client

Webcam
  ↓
MediaPipe Pose / Hands
  ↓
Aim + Body Position
  ↓
Unity Game Client

Game Telemetry
  ↓
Fetch.ai Agents
  ↓
Enemy behavior, narration, coaching, recap

Arize
  ↓
Trace boss decisions and evaluate boss performance

Redis
  ↓
Player style memory, boss strategy memory, match history

Pika
  ↓
Post-fight boxing-style recap
```

---

## Agent Design

### Minimum Agents

| Agent | Role |
|---|---|
| **GameMasterAgent** | Starts match, tracks round state, controls high-level events |
| **EnemyAgent** | Chooses boss behavior between phases or turns |
| **NarratorAgent** | Generates dramatic commentary and move names |
| **RecapAgent** | Turns match telemetry into a Pika recap prompt |

### Optional Agents

| Agent | Role |
|---|---|
| **CoachAgent** | Analyzes player style after match and suggests combos |
| **RefereeAgent** | Validates combos, cooldowns, and hit logic |
| **LoreAgent** | Generates boss backstory and arena themes |

### Example Agent Input

```json
{
  "event": "heavy_punch_release",
  "charge_level": 3,
  "hold_seconds": 2.4,
  "accuracy": 0.81,
  "damage": 42,
  "boss_health_after": 18,
  "player_style": "heavy_puncher"
}
```

### Example Agent Output

```json
{
  "move_name": "Heavy Punch",
  "narration": "A heavy punch lands clean and knocks the boss off rhythm.",
  "boss_reaction": "staggered",
  "next_strategy": "pressure_long_windups"
}
```

---

## Arize Integration: Making the Boss Get Better

### Core Framing

**Arize is the fight lab.** Every boss decision, player action, agent response, and round outcome is traced and evaluated. The boss uses those evaluations to adapt its next strategy.

This is stronger than simply logging traces. The pitch should be:

> KiForge Arena is not just an AI game. It is an observable AI opponent. Arize traces every decision, evaluates whether the boss countered the player effectively, and feeds those insights back into the EnemyAgent so the boss adapts between rounds.

### What to Trace

Each turn or major combat event should produce a trace with spans like:

```text
MatchTurn
  ├── ReadPlayerMemory
  ├── EnemyAgentDecision
  ├── RefereeAgentResolveMove
  ├── NarratorAgentGenerateCommentary
  └── UpdateStrategyMemory
```

### Example Fight Event

```json
{
  "round": 2,
  "player_action": "heavy_punch",
  "charge_time": 2.4,
  "player_accuracy": 0.77,
  "boss_action": "pressure",
  "boss_reasoning": "Player overuses long heavy-punch windups",
  "damage_dealt_by_player": 42,
  "damage_dealt_by_boss": 15,
  "boss_health_after": 38,
  "player_health_after": 61,
  "outcome": "boss_staggered"
}
```

### Boss Evaluation Metrics

| Eval | Meaning |
|---|---|
| **damage_efficiency** | Did the boss reduce player health? |
| **counter_success** | Did the boss choose a move that counters the player’s dominant style? |
| **survival_score** | Did the boss avoid taking huge damage? |
| **variety_score** | Is the boss avoiding repetitive moves? |
| **fun_score** | Did the fight stay close instead of becoming one-sided? |
| **commentary_accuracy** | Did the narrator describe the actual game state correctly? |

### Simple Deterministic Evaluator

```python
def evaluate_boss_turn(event):
    score = 0

    if event["damage_dealt_by_boss"] > 0:
        score += 1

    if event["damage_dealt_by_player"] < 25:
        score += 1

    if event["boss_action"] == event["recommended_counter"]:
        score += 1

    if event["health_gap_after"] < 40:
        score += 1

    return score / 4
```

### Store Bad Decisions

Examples to save:

```text
Good decision:
Player wound up a heavy punch → boss used pressure → interrupted the windup.

Bad decision:
Player kept guarding → boss used weak jabs → got blocked repeatedly.
```

### Adapt the Boss Between Rounds

The boss does not need true reinforcement learning. Use evaluation-informed strategy updates.

Example player profile:

```json
{
  "player_profile": {
    "style": "heavy_puncher",
    "avg_charge_time": 2.4,
    "guard_rate": 0.18,
    "combo_punch_rate": 0.22,
    "heavy_punch_rate": 0.60
  },
  "arize_eval_summary": {
    "boss_counter_success": 0.31,
    "worst_failure": "boss allowed heavy punches to land too often",
    "recommended_strategy": "pressure long windups and bait guard timing"
  }
}
```

EnemyAgent prompt for the next round:

```text
The player is a heavy puncher. Previous boss strategy failed because it allowed long windups.
In the next round, prioritize pressure when charge_time > 1.5s, dodge predictable punch strings, and punish guard habits with heavy counters.
```

### Strategy Weights

Initial boss strategy:

```json
{
  "pressure": 0.25,
  "dodge": 0.25,
  "jab": 0.25,
  "guard": 0.25,
  "heavy_counter": 0.00
}
```

If the player winds up heavy punches too often:

```json
{
  "pressure": 0.65,
  "dodge": 0.20,
  "jab": 0.05,
  "guard": 0.10,
  "heavy_counter": 0.00
}
```

If the player guards too often:

```json
{
  "pressure": 0.20,
  "dodge": 0.15,
  "jab": 0.10,
  "guard": 0.00,
  "heavy_counter": 0.55
}
```

If the player throws left-right punch strings too often:

```json
{
  "pressure": 0.15,
  "dodge": 0.45,
  "jab": 0.30,
  "guard": 0.10,
  "heavy_counter": 0.00
}
```

### AI Fight Lab Panel

Show this beside the game:

```text
Player Style: Heavy Puncher
Boss Counter Success: 31% → 68%
Most Common Player Move: Heavy punch
Boss Adaptation: Pressure during windup
Last Failed Decision: Jab into guard
Next Strategy: Bait guard, then heavy counter
```

### Why This Helps the Arize Sponsor Track

The sponsor story:

1. Before Arize/evals: boss chooses moves randomly or with static rules.
2. With Arize traces: you can inspect why bad boss decisions happened.
3. With Arize eval summaries: the game identifies the player’s dominant style.
4. With Redis memory: the EnemyAgent receives strategy memory.
5. After adaptation: boss counter-success improves.

This demonstrates that Arize was used and actually improved the application.

---

## Redis Usage

Use Redis for fast game memory and sponsor-track visibility.

### Store

- Match history.
- Player style.
- Boss phase.
- Favorite moves.
- Charge accuracy.
- Generated move names.
- Recap prompts.
- Boss strategy weights.
- Arize eval summaries.

### Example Player Profile

```json
{
  "player_id": "demo_player",
  "style": "heavy_puncher",
  "avg_charge_time": 2.4,
  "favorite_move": "heavy_punch",
  "guards_used": 4,
  "combo_punches_used": 7,
  "very_heavy_punches_used": 1,
  "boss_counter_success_before": 0.31,
  "boss_counter_success_after": 0.68
}
```

### Redis Pitch

> Redis gives the boss memory. The enemy adapts to how you fight across rounds.

---

## Pika Usage

Use Pika after the fight, not during real-time gameplay.

### Pika Feature

At match end, `RecapAgent` generates a prompt from telemetry:

```text
Create a 7-second anime-style battle recap in a neon arena. 
A fighter reads the boss guard, winds up, and lands a clean heavy punch that turns the round. 
Fast camera movement, impact flash, cinematic lighting, dramatic ending.
```

### Good Pika Outputs

- Post-fight recap video.
- Heavy-punch highlight.
- Boss intro clip.
- Devpost demo trailer asset.

### Pika Pitch

> Our live game runs locally for low latency. Pika is used by our AI director agent to generate cinematic recaps from real match telemetry.

---

## Band Usage Optional

Use Band if time allows.

### Multi-Agent Room

Agents:

- EnemyAgent
- RefereeAgent
- NarratorAgent
- CoachAgent

Workflow:

1. Player performs action.
2. RefereeAgent validates move and cooldowns.
3. EnemyAgent chooses response.
4. NarratorAgent creates commentary.
5. CoachAgent updates player profile after the round.

This makes multi-agent collaboration visible and sponsor-relevant.

---

## Deepgram Usage Optional

Deepgram should only be added after the core loop works.

Possible uses:

- Voice command: “guard.”
- Voice command: “start duel.”
- Player taunts affect the boss.
- AI announcer voice for commentary.
- Spoken post-fight recap.

Do not make voice required for gameplay unless the rest is stable.

---

## Sentry Usage Optional

Useful for polish and reliability:

- Capture Unity client crashes.
- Capture backend WebSocket errors.
- Capture failed agent calls.
- Capture Pika generation failures.
- Capture MYO disconnect errors.

Sponsor pitch:

> Since this is a live hardware + CV + agent game, reliability matters. Sentry helped us catch broken WebSocket events, missing telemetry fields, and failed agent calls.

---

## MVP Definition

The MVP is complete when this works:

1. Webcam tracks wrist position.
2. MYO fist hold fills charge bar.
3. Releasing fist throws a heavy punch.
4. Open palm movement positions the fighter.
5. Keyboard fallback supports left/right punch, guard, and heavy punch tiers.
6. Boss takes damage and changes phase.
7. EnemyAgent chooses boss responses.
8. NarratorAgent names attacks and comments on the fight.
9. Arize traces boss decisions and computes simple evals.
10. Redis stores player style and boss strategy memory.
11. Boss strategy changes between rounds.
12. RecapAgent creates a Pika recap prompt.
13. Devpost has Agentverse and ASI:One links.

---

## Fallback Controls

Build the game with keyboard/mouse first, then map MYO and CV into the same input API.

| Input | Keyboard Fallback |
|---|---|
| Fist hold | Hold `F` |
| Fist release | Release `F` |
| Guard | `S` |
| Left punch | `A` |
| Right punch | `D` |
| Very-heavy punch | Space |
| CV aim | Mouse position |
| CV movement | Arrow keys |

This guarantees a demo even if MYO or webcam tracking fails.

---

## 24-Hour Build Plan

### Hour 0–2: Input Proof

Goal: MYO and webcam both work.

Build:

- PyoMyo listener.
- MediaPipe webcam tracker.
- Keyboard fallback.

Success condition:

```text
MYO fist prints CHARGE_START
MYO rest prints HEAVY_PUNCH_RELEASE
Webcam shows wrist position
Keyboard fallback controls same actions
```

### Hour 2–5: Local Game Prototype

Goal: playable without agents.

Build:

- Unity 2.5D arena.
- Player.
- Boss.
- Health bars.
- Charge bar.
- Heavy punch.
- Guard.
- Left/right punches.

Use keyboard fallback first.

### Hour 5–8: CV Aiming

Goal: punch origin comes from your real hand.

Build:

- Wrist reticle.
- Punch origin from wrist.
- Body-center movement.
- Aim smoothing.

MVP formula:

```text
punch_origin = wrist_position
target = wrist_position + normalized(elbow_to_wrist_vector) * range
```

### Hour 8–12: Fetch.ai Agents

Goal: agents affect gameplay.

Build:

- GameMasterAgent.
- EnemyAgent.
- NarratorAgent.
- RecapAgent.

Minimum integration:

- Send match event to agent.
- Receive move name and narration.
- Display narration in game.
- Register at least one agent on Agentverse.
- Prepare ASI:One demo chat.

### Hour 12–15: Redis Memory

Goal: boss adapts using memory.

Build:

- Store match events.
- Store player style.
- Store boss strategy weights.
- Boss changes behavior based on player style.

Rules:

```text
If player winds up heavy punches too often → boss pressures.
If player guards often → boss uses heavy counter.
If player throws left-right punch strings often → boss dodges or backs away.
```

### Hour 15–18: Arize Fight Lab

Goal: prove Arize improves the boss.

Build:

- Trace EnemyAgent decisions.
- Log combat outcomes.
- Compute counter-success and survival-score evals.
- Show before/after strategy weights.
- Show AI Fight Lab panel.

Minimum Arize demo:

```text
Round 1: boss counter success = 31%
Arize identifies player as heavy puncher
Boss updates strategy to pressure during windup
Round 2: boss counter success = 68%
```

### Hour 18–20: Pika Recap

Goal: cinematic sponsor feature.

Build:

- End-of-match telemetry summary.
- RecapAgent generates Pika prompt.
- Generate or queue one recap clip.
- If live Pika is slow, show generated prompt plus pre-generated example from test telemetry.

### Hour 20–22: Polish

Goal: make it judge-ready.

Add:

- Screen shake.
- Move names.
- Sound effects.
- Better particles.
- Boss phases.
- Clean UI.
- Sponsor tech panel.

### Hour 22–24: Submission and Pitch

Create:

- Devpost.
- GitHub README.
- Demo video.
- ASI:One shared chat.
- Agentverse profile links.
- Architecture diagram.
- Sponsor explanation.

---

## Repo Structure

```text
kiforge-arena/
  Assets/
    Scripts/
      Bootstrap/
        KiForgeArenaBootstrap.cs
      Input/
        KeyboardFallbackInput.cs
        BackendEventReceiver.cs
      Combat/
        ArenaGameController.cs
        CombatConfig.cs
        StrategyWeights.cs
        PlayerCombatController.cs
        BossCombatController.cs
      Effects/
        ArenaEffectsController.cs
      Telemetry/
        MatchTelemetryRecorder.cs
        MockAgentClient.cs
      UI/
        KiForgeHudController.cs
      Shared/
        KiForgeEvents.cs
        KiForgeEventBus.cs
    Tests/
      EditMode/
        CombatRulesTests.cs
  Packages/
    manifest.json
  ProjectSettings/
    ProjectVersion.txt
  backend/
    main.py
    myo_listener.py
    vision_bridge.py
    agents/
      game_master_agent.py
      enemy_agent.py
      narrator_agent.py
      recap_agent.py
      coach_agent.py
    fight_lab.py
    redis_store.py
    pika_recap.py
  docs/
    architecture.png
    devpost_notes.md
  README.md
```

---

## Devpost / README Checklist

Include:

- Project title and short description.
- Main track: Ddoski’s Playground.
- Sponsor tracks targeted.
- Demo video.
- Public GitHub repo.
- ASI:One shared chat link.
- Agentverse profile links for all agents.
- Architecture diagram.
- How MYO/PyoMyo is used.
- How CV is used.
- How Fetch.ai agents are used.
- How Arize improved the boss.
- How Redis stores player/boss memory.
- How Pika generates cinematic recap.
- Setup instructions.
- Fallback controls.

---

## Pitch Script

### Opening

“We built KiForge Arena, a real-time anime boss fight controlled by muscle signals and computer vision.”

### Novelty

“Most AI games are still keyboard prompts. We wanted the player’s body to become the controller.”

### Demo Setup

“My fist charges the attack, my webcam tracks where I aim, and the AI boss adapts to how I fight.”

### Agent System

“Fetch.ai agents run the game master, enemy, narrator, coach, and recap workflow.”

### Arize

“Arize is our fight lab. It traces every boss decision, evaluates whether the boss countered me correctly, and feeds that insight back into the EnemyAgent so it gets better between rounds.”

### Redis

“Redis stores match history, player style, boss strategy weights, and generated move names.”

### Pika

“At the end of the fight, our RecapAgent turns real match telemetry into a Pika prompt for a cinematic anime battle recap.”

### Closing

“This is a prototype of embodied AI gaming: not just chatting with an agent, but physically fighting inside an agent-powered world.”

---

## Judge Questions to Prepare For

### “Where is the AI?”

The AI is in the enemy strategy, narration, coaching, and recap generation. The local game engine handles real-time combat, while Fetch.ai agents handle higher-level decisions.

### “Why use Arize?”

Arize lets us inspect and evaluate boss decisions. We use those evals to update boss strategy, which makes the opponent better over time.

### “Why not do everything with agents in real time?”

Real-time combat needs low latency. Agents are used for strategic and narrative decisions between turns or rounds, where latency is acceptable.

### “What happens if MYO fails?”

The same action API supports keyboard fallback. The demo can still run, and the MYO integration can be shown through logs if Bluetooth is unstable.

### “What did Redis do?”

Redis stores persistent game memory: player style, match history, boss strategy, generated move names, and evaluation summaries.

### “What did Pika do?”

Pika generates post-fight cinematic recaps from real gameplay telemetry.

---

## Final Recommendation

Prioritize in this order:

1. Make the local game fun with keyboard fallback.
2. Add MYO charge/release.
3. Add CV wrist aiming.
4. Add Fetch.ai agents for narration and enemy behavior.
5. Add Redis player memory.
6. Add Arize fight lab so the boss improves.
7. Add Pika recap.

The winning version is not the one with the most features. The winning version is the one where a judge immediately understands:

> I charge attacks with my muscles, aim with my body, fight an AI boss, and the boss learns how to counter me.
![alt text](image.png)
