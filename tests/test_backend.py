from backend.fight_lab import evaluate_boss_turn
from backend.agents.game_master_agent import GameMasterAgent
from backend.agents.narrator_agent import NarratorAgent
from backend.agentverse_adapter import respond_to_text
from backend.commentary_adapter import respond_to_commentary_text
from backend.models import CombatTelemetry
from backend.pika_recap import build_recap_prompt
from backend.recap_queue import RecapQueue
from backend.redis_store import RedisStore
from backend.uagents_app import handle_agent_message


def test_fight_lab_recommends_pressure_for_heavy_punch() -> None:
    event = CombatTelemetry(
        round=1,
        player_action="heavy_punch",
        charge_time=3.2,
        accuracy=0.8,
        damage_dealt_by_player=42,
        damage_dealt_by_boss=0,
        boss_action="pressure",
        boss_health_after=80,
        player_health_after=100,
        outcome="boss_hit",
    )

    result = evaluate_boss_turn(event)

    assert result.recommended_strategy == "heavy_puncher"
    assert result.counter_success == 1.0


def test_redis_store_falls_back_to_memory() -> None:
    store = RedisStore(url=None)
    store.set_json("profile", {"style": "balanced"})

    assert store.using_memory
    assert store.get_json("profile") == {"style": "balanced"}


def test_cooldowns_track_and_expire() -> None:
    store = RedisStore(url=None)
    store.set_cooldown("p1", "very_heavy_punch", 8.0)

    assert store.cooldown_remaining("p1", "very_heavy_punch") > 0
    assert "very_heavy_punch" in store.active_cooldowns("p1", ["very_heavy_punch", "guard"])
    assert store.cooldown_remaining("p1", "guard") == 0.0


def test_match_stream_appends_and_reads_back() -> None:
    store = RedisStore(url=None)
    store.stream_add({"player_id": "p1", "round": 1, "move_name": "Bolt"})
    store.stream_add({"player_id": "p1", "round": 2, "move_name": "Heavy Punch"})

    recent = store.stream_recent(count=10)
    assert [entry["move_name"] for entry in recent] == ["Bolt", "Heavy Punch"]


def test_vector_recall_finds_most_similar_player() -> None:
    store = RedisStore(url=None)
    store.index_player_vector("heavy", [0.9, 0.0, 0.1, 0.0, 0.8], {"style": "heavy_puncher"})
    store.index_player_vector("guard", [0.0, 0.9, 0.1, 0.0, 0.0], {"style": "guard_turtle"})

    hits = store.recall_similar([0.85, 0.05, 0.1, 0.0, 0.75], k=1, exclude="new_player")

    assert hits and hits[0]["player_id"] == "heavy"
    assert hits[0]["similarity"] > 0.9


def test_move_names_and_boss_phase_persist() -> None:
    store = RedisStore(url=None)
    store.add_move_name("p1", "Heavy Punch")
    store.set_boss_phase("p1", 2)

    assert store.get_move_names("p1") == ["Heavy Punch"]
    assert store.get_boss_phase("p1") == 2


def test_recap_prompt_uses_match_telemetry() -> None:
    event = CombatTelemetry(
        round=1,
        player_action="very_heavy_punch",
        charge_time=4.0,
        accuracy=1.0,
        damage_dealt_by_player=88,
        damage_dealt_by_boss=0,
        boss_action="dodge",
        boss_health_after=0,
        player_health_after=100,
        outcome="boss_ko",
    )

    prompt = build_recap_prompt([event], "Very Heavy Punch")

    assert "Very Heavy Punch" in prompt
    assert "neon arena" in prompt


def test_narrator_fallback_returns_real_narration() -> None:
    event = CombatTelemetry(
        round=1,
        player_action="heavy_punch",
        charge_time=2.4,
        accuracy=0.8,
        damage_dealt_by_player=28,
        damage_dealt_by_boss=0,
        boss_action="pressure",
        boss_health_after=72,
        player_health_after=100,
        outcome="boss_hit",
    )

    move_name, narration = NarratorAgent().narrate(event)

    assert move_name == "Heavy Punch"
    assert "28 damage" in narration
    assert narration.strip()


def test_game_master_updates_profile_traces_and_strategy_weights(tmp_path) -> None:
    store = RedisStore(url=None)
    queue = RecapQueue(tmp_path / "recaps.jsonl")
    master = GameMasterAgent(store=store, recap_queue=queue)
    event = CombatTelemetry(
        round=1,
        player_action="heavy_punch",
        charge_time=3.6,
        accuracy=0.75,
        damage_dealt_by_player=48,
        damage_dealt_by_boss=5,
        boss_action="jab",
        boss_health_after=0,
        player_health_after=70,
        outcome="boss_ko",
    )

    response = master.handle_combat_event(event)
    profile = store.get_json("player:demo_player:profile")
    traces = store.get_traces("demo_player")

    assert profile["style"] == "heavy_puncher"
    assert profile["strategy_weights"]["pressure"] == 0.65
    assert response.learning_mode == "baseline"
    assert "naive starter policy" in response.tactical_plan["intent"]
    assert response.recap_job["status"] == "queued"
    assert traces[-1]["spans"][1]["name"] == "EnemyAgentDecision"


def test_battle_agent_starts_bad_then_arize_adapts(tmp_path) -> None:
    store = RedisStore(url=None)
    master = GameMasterAgent(store=store, recap_queue=RecapQueue(tmp_path / "recaps.jsonl"))
    event = CombatTelemetry(
        round=1,
        player_action="heavy_punch",
        charge_time=3.6,
        accuracy=0.75,
        damage_dealt_by_player=48,
        damage_dealt_by_boss=0,
        boss_action="unknown",
        boss_health_after=72,
        player_health_after=100,
        outcome="boss_hit",
    )

    first = master.handle_combat_event(event, player_id="learn_demo")
    second = master.handle_combat_event(event, player_id="learn_demo")

    assert first.learning_mode == "baseline"
    assert first.boss_action == "jab"
    assert first.counter_success == 0.0
    assert second.learning_mode == "adapted"
    assert second.boss_action == "pressure"
    assert second.counter_success == 1.0


def test_recap_queue_persists_jobs(tmp_path) -> None:
    queue = RecapQueue(tmp_path / "recaps.jsonl")
    job = queue.enqueue("Create a recap", {"round": 1})

    assert job["job_id"].startswith("recap_")
    assert queue.list_jobs()[0]["prompt"] == "Create a recap"


def test_agent_adapter_returns_combat_response() -> None:
    payload = {
        "round": 1,
        "player_action": "guard",
        "charge_time": 0.0,
        "accuracy": 0.5,
        "damage_dealt_by_player": 0,
        "damage_dealt_by_boss": 12,
        "boss_action": "jab",
        "boss_health_after": 100,
        "player_health_after": 88,
        "outcome": "player_blocked",
    }

    first = handle_agent_message(payload, player_id="test_agent_player")
    second = handle_agent_message(payload, player_id="test_agent_player")

    assert first["learning_mode"] == "baseline"
    assert first["boss_action"] == "jab"
    assert second["learning_mode"] == "adapted"
    assert second["boss_action"] == "heavy_counter"
    assert second["player_profile"]["style"] == "guard_turtle"


def test_agentverse_text_adapter_runs_demo_turn() -> None:
    calls = []

    def fake_handler(payload: dict, player_id: str) -> dict:
        calls.append((payload, player_id))
        return {
            "move_name": "Heavy Punch",
            "narration": "A heavy punch lands.",
            "boss_action": "pressure",
            "next_strategy": "Pressure long windups.",
            "counter_success": 1.0,
            "survival_score": 0.5,
            "learning_mode": "adapted",
            "tactical_plan": {
                "read": "Player charges a lot.",
                "intent": "Deny windup space.",
                "execute": "Step in with punches.",
                "contingency": "Punish guard next.",
                "arize_fix": "Fight Lab fixed the baseline.",
            },
        }

    reply = respond_to_text("start duel", fake_handler, "agentverse_player")

    assert calls[0][0]["player_action"] == "heavy_punch"
    assert calls[0][1] == "agentverse_player"
    assert "Heavy Punch" in reply
    assert "Plan:" in reply
    assert "Mode: adapted" in reply


def test_commentary_adapter_returns_narration_only() -> None:
    reply = respond_to_commentary_text("start commentary")

    assert "Heavy Punch" in reply
    assert "Boss countered with" not in reply
