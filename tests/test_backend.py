from backend.fight_lab import evaluate_boss_turn
from backend.agents.game_master_agent import GameMasterAgent
from backend.models import CombatTelemetry
from backend.pika_recap import build_recap_prompt
from backend.recap_queue import RecapQueue
from backend.redis_store import RedisStore


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
