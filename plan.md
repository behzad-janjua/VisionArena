# KiForge Arena — Hackathon Build Plan

## Project Summary

**Title:** KiForge Arena  
**Main Track:** Ddoski’s Playground  
**Core Pitch:** A real-time anime boss-fight game where your muscle gestures control attacks, your webcam controls aim and movement, and AI agents run the enemy, narrator, coach, and post-fight cinematic recap.

**Core Interaction:**

- MYO armband + PyoMyo detects muscle gestures.
- Computer vision tracks wrist, arm direction, and body position.
- Holding a fist charges an energy attack.
- Releasing the fist fires a blast aimed by the player’s arm.
- Fetch.ai agents run the game master, enemy behavior, narration, coaching, and recap workflow.
- Arize traces and evaluates enemy decisions so the boss gets better at fighting the player.
- Redis stores player style, boss memory, match history, and generated move names.
- Pika generates post-fight anime-style recap videos from real match telemetry.

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
| **Pika** | Generates post-fight cinematic recap videos or ultimate-move trailers from match telemetry. |
| **Band** | Optional: host EnemyAgent, RefereeAgent, NarratorAgent, and CoachAgent in a shared multi-agent room. |
| **Deepgram** | Optional: voice commands such as “ultimate,” “start duel,” or AI announcer narration. |
| **Sentry** | Optional: reliability/error monitoring for Unity, backend, and the agent pipeline. |

---

## Core Demo Moment

The ideal judge demo:

1. You stand in front of the webcam.
2. The game tracks your wrist and body position.
3. You make a fist.
4. The aura starts building around your hand.
5. The charge bar fills as you hold the fist.
6. Muscle intensity or hold duration increases the attack power.
7. You aim with your arm.
8. You release your fist.
9. A huge beam fires from your hand toward the boss.
10. The boss takes damage.
11. The NarratorAgent names the move.
12. Arize shows the boss decision trace and evaluation.
13. The EnemyAgent adapts for the next round.
14. At the end, the RecapAgent creates a Pika prompt for an anime-style battle recap.

---

## Input System

### MYO / PyoMyo Gesture Map

| MYO Input | Game Action |
|---|---|
| **Fist hold** | Charge energy attack |
| **Fist release** | Fire charged blast |
| **Fingers spread** | Block / energy shield |
| **Wave right** | Right slash |
| **Wave left** | Left slash |
| **Thumb-to-pinky** | Switch stance, element, or special mode |
| **Double tap** | Dash or ultimate |
| **Hold spread** | Sustained shield |
| **Wave → fist hold** | Elemental charged slash |

### Computer Vision Map

| CV Signal | Game Use |
|---|---|
| Wrist position | Aim reticle and attack origin |
| Elbow-to-wrist vector | Blast direction |
| Body center x-position | Player movement |
| Arm angle | High/mid/low targeting |
| Pose stillness | Charge stability bonus |
| Body lean | Dodge direction, optional |

### MVP CV Scope

For the first version, only use:

- Wrist position for aim.
- Body center x-position for movement.
- Elbow-to-wrist vector for blast direction.

Avoid overbuilding CV during the hackathon.

---

## Combat System

### Charge Attack Mechanic

| Charge Level | Hold Time | Attack |
|---|---:|---|
| Level 1 | 0.3–1.0 sec | Small bolt |
| Level 2 | 1.0–2.0 sec | Medium blast |
| Level 3 | 2.0–3.5 sec | Beam attack |
| Level 4 | 3.5+ sec | Ultimate cannon |

### Charge Modifiers

| Condition | Result |
|---|---|
| Longer fist hold | More damage |
| Higher EMG intensity | Faster charge or stronger aura |
| Stable pose while charging | Accuracy multiplier |
| Moving too much while charging | Accuracy penalty |
| Holding too long | Overcharge instability |
| Release during timing window | Critical hit |
| Take damage while charging | Charge cancel or partial loss |

### Core Actions

| Action | Description |
|---|---|
| Punch / quick bolt | Short fist hold and release |
| Charged blast | Long fist hold and release |
| Shield | Fingers spread |
| Slash left/right | Wave left/right |
| Stance switch | Thumb-to-pinky |
| Ultimate | Double tap when energy meter is full |
| Counter blast | Shield, then fist release during enemy attack window |

### Example Combos

| Combo | Move |
|---|---|
| Fist → wave right | Fire slash |
| Fist → wave left | Ice slash |
| Spread → fist | Shield bash |
| Wave left → wave right | Cross slash |
| Thumb-to-pinky → fist | Elemental punch |
| Double tap → spread | Ultimate barrier |
| Hold fist 2+ sec → release | Charged beam |
| Spread hold → fist | Counter cannon |

---

## Animation Plan

For a 24-hour hackathon, do not build complex character animation. Use procedural animation and layered visual effects.

### Visual Style

- Dark arena background.
- Neon player silhouette.
- Boss silhouette.
- Glowing aura particles.
- Thick energy beams.
- Slash trails.
- Shield arcs.
- Large anime-style move names.
- Screen shake on impact.

### Game States

| State | Visual Effect |
|---|---|
| `idle` | Slight bobbing |
| `charging` | Aura particles + growing charge bar |
| `blocking` | Translucent shield arc |
| `slash_left` | Left fading slash trail |
| `slash_right` | Right fading slash trail |
| `dash` | Motion blur / quick displacement |
| `blast` | Beam or projectile from wrist to target |
| `hit` | Flash + knockback + damage number |
| `ultimate` | Freeze frame + flash + screen shake |
| `ko` | Fade/drop animation |

### Effects to Build First

| Effect | Implementation |
|---|---|
| Aura charge | Particles orbiting or moving toward the wrist |
| Charge bar | UI rectangle fills based on fist hold time |
| Beam blast | Thick line/capsule from wrist to target |
| Slash trail | Curved arc fading over 250–300ms |
| Shield | Semi-transparent arc/circle in front of player |
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
  "type": "BLAST_RELEASE",
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

Use **Unity 2.5D** for the hackathon implementation. Unity owns the real-time combat, input fallback, HUD, particles, beams, shields, and arena presentation. The Python backend owns hardware bridges, agents, Redis memory, Arize evals, and Pika recap generation.

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
Post-fight anime recap / ultimate move trailer
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
  "event": "charged_attack_release",
  "charge_level": 4,
  "hold_seconds": 4.2,
  "accuracy": 0.81,
  "damage": 42,
  "boss_health_after": 18,
  "player_style": "patient_charger"
}
```

### Example Agent Output

```json
{
  "move_name": "Solar Core Cannon",
  "narration": "A perfect charge erupts across the arena.",
  "boss_reaction": "staggered",
  "next_strategy": "boss_becomes_aggressive"
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
  "player_action": "charged_blast",
  "charge_time": 3.8,
  "player_accuracy": 0.77,
  "boss_action": "rush_attack",
  "boss_reasoning": "Player overuses long charge attacks",
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
Player charged for 4.1s → boss used rush attack → interrupted charge.

Bad decision:
Player spammed shield → boss used weak projectile → got blocked repeatedly.
```

### Adapt the Boss Between Rounds

The boss does not need true reinforcement learning. Use evaluation-informed strategy updates.

Example player profile:

```json
{
  "player_profile": {
    "style": "patient_charger",
    "avg_charge_time": 3.4,
    "block_rate": 0.18,
    "slash_rate": 0.22,
    "blast_rate": 0.60
  },
  "arize_eval_summary": {
    "boss_counter_success": 0.31,
    "worst_failure": "boss allowed full-charge blasts too often",
    "recommended_strategy": "rush during charge and bait shield cooldown"
  }
}
```

EnemyAgent prompt for the next round:

```text
The player is a patient charger. Previous boss strategy failed because it allowed full-charge blasts.
In the next round, prioritize rush attacks when charge_time > 1.5s, dodge when arm aim is stable, and punish shield cooldown.
```

### Strategy Weights

Initial boss strategy:

```json
{
  "rush": 0.25,
  "dodge": 0.25,
  "projectile": 0.25,
  "block": 0.25
}
```

If the player charges too often:

```json
{
  "rush": 0.65,
  "dodge": 0.20,
  "projectile": 0.05,
  "block": 0.10
}
```

If the player blocks too often:

```json
{
  "rush": 0.20,
  "dodge": 0.15,
  "projectile": 0.10,
  "grab_or_unblockable": 0.55
}
```

If the player slashes too often:

```json
{
  "rush": 0.15,
  "dodge": 0.45,
  "projectile": 0.30,
  "block": 0.10
}
```

### AI Fight Lab Panel

Show this beside the game:

```text
Player Style: Patient Charger
Boss Counter Success: 31% → 68%
Most Common Player Move: Full-charge blast
Boss Adaptation: Rush during charge
Last Failed Decision: Projectile into shield
Next Strategy: Bait shield, then dash strike
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
  "style": "patient_charger",
  "avg_charge_time": 3.1,
  "favorite_move": "charged_blast",
  "blocks_used": 4,
  "slashes_used": 7,
  "ultimates_used": 1,
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
A fighter charges glowing energy in their fist, aura rising, then releases a massive beam called Solar Core Cannon at a shadow bear boss. 
Fast camera movement, impact flash, cinematic lighting, dramatic ending.
```

### Good Pika Outputs

- Post-fight recap video.
- Ultimate move trailer.
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

- Voice command: “ultimate.”
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
3. Releasing fist fires beam from wrist direction.
4. Fingers spread creates shield.
5. Wave left/right creates slash.
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
| Fingers spread | `S` |
| Wave left | `A` |
| Wave right | `D` |
| Double tap | Space |
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
MYO rest prints BLAST_RELEASE
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
- Blast attack.
- Shield.
- Slash.

Use keyboard fallback first.

### Hour 5–8: CV Aiming

Goal: blast comes from your real hand.

Build:

- Wrist reticle.
- Beam origin from wrist.
- Body-center movement.
- Aim smoothing.

MVP formula:

```text
beam_origin = wrist_position
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
If player charges too often → boss rushes.
If player blocks often → boss uses unblockable/grab.
If player slashes often → boss dodges or backs away.
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
Arize identifies player as patient charger
Boss updates strategy to rush during charge
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