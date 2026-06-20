from backend.fight_lab import evaluate_boss_turn
from backend.agents.game_master_agent import GameMasterAgent
from backend.models import CombatTelemetry
from backend.pika_recap import build_recap_prompt
from backend.recap_queue import RecapQueue
from backend.redis_store import RedisStore
from backend.uagents_app import handle_agent_message


def test_fight_lab_recommends_rush_for_long_charge() -> None:
    event = CombatTelemetry(
        round=1,
        player_action="charged_blast",
        charge_time=3.2,
        accuracy=0.8,
        damage_dealt_by_player=42,
        damage_dealt_by_boss=0,
        boss_action="rush",
        boss_health_after=80,
        player_health_after=100,
        outcome="boss_hit",
    )

    result = evaluate_boss_turn(event)

    assert result.recommended_strategy == "patient_charger"
    assert result.counter_success == 1.0


def test_redis_store_falls_back_to_memory() -> None:
    store = RedisStore(url=None)
    store.set_json("profile", {"style": "balanced"})

    assert store.using_memory
    assert store.get_json("profile") == {"style": "balanced"}


def test_cooldowns_track_and_expire() -> None:
    store = RedisStore(url=None)
    store.set_cooldown("p1", "ultimate", 8.0)

    assert store.cooldown_remaining("p1", "ultimate") > 0
    assert "ultimate" in store.active_cooldowns("p1", ["ultimate", "shield"])
    assert store.cooldown_remaining("p1", "shield") == 0.0


def test_match_stream_appends_and_reads_back() -> None:
    store = RedisStore(url=None)
    store.stream_add({"player_id": "p1", "round": 1, "move_name": "Bolt"})
    store.stream_add({"player_id": "p1", "round": 2, "move_name": "Beam"})

    recent = store.stream_recent(count=10)
    assert [entry["move_name"] for entry in recent] == ["Bolt", "Beam"]


def test_vector_recall_finds_most_similar_player() -> None:
    store = RedisStore(url=None)
    store.index_player_vector("charger", [0.9, 0.0, 0.1, 0.0, 0.8], {"style": "patient_charger"})
    store.index_player_vector("turtle", [0.0, 0.9, 0.1, 0.0, 0.0], {"style": "shield_turtle"})

    hits = store.recall_similar([0.85, 0.05, 0.1, 0.0, 0.75], k=1, exclude="new_player")

    assert hits and hits[0]["player_id"] == "charger"
    assert hits[0]["similarity"] > 0.9


def test_move_names_and_boss_phase_persist() -> None:
    store = RedisStore(url=None)
    store.add_move_name("p1", "Solar Core Cannon")
    store.set_boss_phase("p1", 2)

    assert store.get_move_names("p1") == ["Solar Core Cannon"]
    assert store.get_boss_phase("p1") == 2


def test_recap_prompt_uses_match_telemetry() -> None:
    event = CombatTelemetry(
        round=1,
        player_action="ultimate",
        charge_time=4.0,
        accuracy=1.0,
        damage_dealt_by_player=88,
        damage_dealt_by_boss=0,
        boss_action="dodge",
        boss_health_after=0,
        player_health_after=100,
        outcome="boss_ko",
    )

    prompt = build_recap_prompt([event], "Solar Core Cannon")

    assert "Solar Core Cannon" in prompt
    assert "neon arena" in prompt


def test_game_master_updates_profile_traces_and_strategy_weights(tmp_path) -> None:
    store = RedisStore(url=None)
    queue = RecapQueue(tmp_path / "recaps.jsonl")
    master = GameMasterAgent(store=store, recap_queue=queue)
    event = CombatTelemetry(
        round=1,
        player_action="charged_blast",
        charge_time=3.6,
        accuracy=0.75,
        damage_dealt_by_player=48,
        damage_dealt_by_boss=5,
        boss_action="projectile",
        boss_health_after=0,
        player_health_after=70,
        outcome="boss_ko",
    )

    response = master.handle_combat_event(event)
    profile = store.get_json("player:demo_player:profile")
    traces = store.get_traces("demo_player")

    assert profile["style"] == "patient_charger"
    assert profile["strategy_weights"]["rush"] == 0.65
    assert response.recap_job["status"] == "queued"
    assert traces[-1]["spans"][1]["name"] == "EnemyAgentDecision"


def test_recap_queue_persists_jobs(tmp_path) -> None:
    queue = RecapQueue(tmp_path / "recaps.jsonl")
    job = queue.enqueue("Create a recap", {"round": 1})

    assert job["job_id"].startswith("recap_")
    assert queue.list_jobs()[0]["prompt"] == "Create a recap"


def test_agent_adapter_returns_combat_response() -> None:
    response = handle_agent_message(
        {
            "round": 1,
            "player_action": "shield",
            "charge_time": 0.0,
            "accuracy": 0.5,
            "damage_dealt_by_player": 0,
            "damage_dealt_by_boss": 12,
            "boss_action": "projectile",
            "boss_health_after": 100,
            "player_health_after": 88,
            "outcome": "player_blocked",
        },
        player_id="test_agent_player",
    )

    assert response["boss_action"] == "unblockable"
    assert response["player_profile"]["style"] == "shield_turtle"
