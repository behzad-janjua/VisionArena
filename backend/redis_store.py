from __future__ import annotations

import json
import math
import os
import time
from typing import Any

# Key namespacing keeps the demo readable in redis-cli / RedisInsight.
_NS = "arena"
_VEC_INDEX = f"{_NS}:player_idx"
_VEC_PREFIX = f"{_NS}:vss:"
_VEC_REGISTRY = f"{_NS}:vec:index"
_MATCH_STREAM = f"{_NS}:match:stream"
_NARRATION_CHANNEL = f"{_NS}:narration"
_LEADERBOARD_KEY = f"{_NS}:leaderboard"
_GHOST_SEED_FLAG = f"{_NS}:ghosts_seeded"
_STYLE_VECTOR_DIM = 5

# Pre-computed style fingerprints for KNN recall to work from the first match.
# Vector layout: [heavy_punch_rate, guard_rate, combo_punch_rate, very_heavy_norm, avg_charge_norm]
_GHOST_PLAYERS = [
    {
        "player_id": "ghost_heavy_puncher",
        "vector": [0.70, 0.10, 0.10, 0.67, 0.60],
        "style": "heavy_puncher",
        "counter_success": 0.31,
        "strategy_weights": {"pressure": 0.65, "dodge": 0.20, "jab": 0.05, "guard": 0.10, "heavy_counter": 0.0},
        "leaderboard_score": 220.0,
    },
    {
        "player_id": "ghost_guard_turtle",
        "vector": [0.10, 0.70, 0.10, 0.0, 0.10],
        "style": "guard_turtle",
        "counter_success": 0.28,
        "strategy_weights": {"pressure": 0.20, "dodge": 0.15, "jab": 0.10, "guard": 0.0, "heavy_counter": 0.55},
        "leaderboard_score": 145.0,
    },
    {
        "player_id": "ghost_combo_puncher",
        "vector": [0.10, 0.10, 0.70, 0.0, 0.08],
        "style": "combo_puncher",
        "counter_success": 0.35,
        "strategy_weights": {"pressure": 0.15, "dodge": 0.45, "jab": 0.30, "guard": 0.10, "heavy_counter": 0.0},
        "leaderboard_score": 310.0,
    },
]


class RedisStore:
    """Thin Redis wrapper for KiForge Arena game memory.

    Every method degrades to an in-memory equivalent when no ``REDIS_URL`` is
    configured (or the server is unreachable), so the backend and tests run with
    or without Redis. When a real Redis (ideally Redis Stack) is connected the
    store additionally uses Streams, TTL cooldowns, Pub/Sub, and RediSearch
    vector similarity for the boss's associative memory.
    """

    def __init__(self, url: str | None = None) -> None:
        self.url = url or os.getenv("REDIS_URL")
        self._memory: dict[str, Any] = {}
        # In-memory fallbacks for the specialized structures.
        self._mem_cooldowns: dict[str, float] = {}
        self._mem_stream: list[dict[str, Any]] = []
        self._mem_vectors: dict[str, dict[str, Any]] = {}
        self._mem_leaderboard: dict[str, float] = {}
        self._client = None
        self._vector_search = False

        if self.url:
            try:
                import redis

                self._client = redis.Redis.from_url(self.url, decode_responses=True)
                self._client.ping()
            except Exception:
                self._client = None

        if self._client is not None:
            self._ensure_vector_index()

    @property
    def using_memory(self) -> bool:
        return self._client is None

    @property
    def using_vector_search(self) -> bool:
        """True when RediSearch vector similarity is available (Redis Stack)."""
        return self._vector_search

    @property
    def backend_label(self) -> str:
        if self._client is None:
            return "in-memory"
        return "redis-stack" if self._vector_search else "redis"

    # ------------------------------------------------------------------ #
    # Simple JSON key/value
    # ------------------------------------------------------------------ #
    def set_json(self, key: str, value: dict[str, Any]) -> None:
        if self._client is None:
            self._memory[key] = value
            return
        self._client.set(key, json.dumps(value))

    def get_json(self, key: str, default: dict[str, Any] | None = None) -> dict[str, Any]:
        if self._client is None:
            return dict(self._memory.get(key, default or {}))

        raw = self._client.get(key)
        if not raw:
            return default or {}
        return json.loads(raw)

    def append_match_event(self, player_id: str, event: dict[str, Any]) -> None:
        key = f"player:{player_id}:match_events"
        if self._client is None:
            self._memory.setdefault(key, []).append(event)
            return
        self._client.rpush(key, json.dumps(event))
        # Keep only the last 100 events so stale rounds from old sessions don't skew the profile.
        self._client.ltrim(key, -100, -1)

    def append_json(self, key: str, value: dict[str, Any]) -> None:
        if self._client is None:
            self._memory.setdefault(key, []).append(value)
            return
        self._client.rpush(key, json.dumps(value))

    def get_json_list(self, key: str, limit: int | None = None) -> list[dict[str, Any]]:
        if self._client is None:
            items = list(self._memory.get(key, []))
        else:
            start = 0 if limit is None else -limit
            items = [json.loads(raw) for raw in self._client.lrange(key, start, -1)]

        if limit is not None:
            return items[-limit:]
        return items

    def get_match_events(self, player_id: str, limit: int | None = None) -> list[dict[str, Any]]:
        return self.get_json_list(f"player:{player_id}:match_events", limit=limit)

    def append_trace(self, player_id: str, trace: dict[str, Any]) -> None:
        self.append_json(f"player:{player_id}:fight_lab_traces", trace)

    def get_traces(self, player_id: str, limit: int | None = None) -> list[dict[str, Any]]:
        return self.get_json_list(f"player:{player_id}:fight_lab_traces", limit=limit)

    # ------------------------------------------------------------------ #
    # plan.md memory: move names, recap prompts, boss phase
    # ------------------------------------------------------------------ #
    def add_move_name(self, player_id: str, move_name: str) -> None:
        if move_name:
            self.append_json(f"player:{player_id}:move_names", {"move_name": move_name, "ts": time.time()})

    def get_move_names(self, player_id: str, limit: int | None = None) -> list[str]:
        items = self.get_json_list(f"player:{player_id}:move_names", limit=limit)
        return [str(item.get("move_name", "")) for item in items]

    def add_recap_prompt(self, player_id: str, prompt: str, metadata: dict[str, Any] | None = None) -> None:
        if prompt:
            self.append_json(
                f"player:{player_id}:recap_prompts",
                {"prompt": prompt, "metadata": metadata or {}, "ts": time.time()},
            )

    def get_recap_prompts(self, player_id: str, limit: int | None = None) -> list[dict[str, Any]]:
        return self.get_json_list(f"player:{player_id}:recap_prompts", limit=limit)

    def set_boss_phase(self, player_id: str, phase: int) -> None:
        self.set_json(f"player:{player_id}:boss_phase", {"phase": int(phase)})

    def get_boss_phase(self, player_id: str) -> int:
        return int(self.get_json(f"player:{player_id}:boss_phase").get("phase", 1))

    # ------------------------------------------------------------------ #
    # Cooldowns via TTL (SET EX)
    # ------------------------------------------------------------------ #
    def _cooldown_key(self, player_id: str, ability: str) -> str:
        return f"{_NS}:cooldown:{player_id}:{ability}"

    def set_cooldown(self, player_id: str, ability: str, seconds: float) -> None:
        if seconds <= 0:
            return
        key = self._cooldown_key(player_id, ability)
        if self._client is None:
            self._mem_cooldowns[key] = time.time() + seconds
            return
        self._client.set(key, "1", ex=int(math.ceil(seconds)))

    def cooldown_remaining(self, player_id: str, ability: str) -> float:
        key = self._cooldown_key(player_id, ability)
        if self._client is None:
            remaining = self._mem_cooldowns.get(key, 0.0) - time.time()
            return round(max(remaining, 0.0), 2)
        ttl = self._client.ttl(key)
        return float(ttl) if ttl and ttl > 0 else 0.0

    def active_cooldowns(self, player_id: str, abilities: list[str]) -> dict[str, float]:
        result = {}
        for ability in abilities:
            remaining = self.cooldown_remaining(player_id, ability)
            if remaining > 0:
                result[ability] = remaining
        return result

    # ------------------------------------------------------------------ #
    # Match telemetry stream (XADD) + Pub/Sub narration
    # ------------------------------------------------------------------ #
    def stream_add(self, fields: dict[str, Any], stream: str = _MATCH_STREAM, maxlen: int = 500) -> None:
        flat = {k: (json.dumps(v) if isinstance(v, (dict, list)) else str(v)) for k, v in fields.items()}
        if self._client is None:
            entry = {"id": f"{int(time.time() * 1000)}-{len(self._mem_stream)}", **flat}
            self._mem_stream.append(entry)
            if len(self._mem_stream) > maxlen:
                self._mem_stream = self._mem_stream[-maxlen:]
            return
        self._client.xadd(stream, flat, maxlen=maxlen, approximate=True)

    def stream_recent(self, count: int = 20, stream: str = _MATCH_STREAM) -> list[dict[str, Any]]:
        if self._client is None:
            return list(self._mem_stream[-count:])
        entries = self._client.xrevrange(stream, count=count)
        out = [{"id": entry_id, **fields} for entry_id, fields in entries]
        out.reverse()
        return out

    def publish(self, message: dict[str, Any], channel: str = _NARRATION_CHANNEL) -> None:
        if self._client is None:
            return
        try:
            self._client.publish(channel, json.dumps(message))
        except Exception:
            pass

    # ------------------------------------------------------------------ #
    # Match lifecycle
    # ------------------------------------------------------------------ #
    def clear_match_events(self, player_id: str) -> None:
        """Delete the raw event list so the next match starts with a clean profile."""
        key = f"player:{player_id}:match_events"
        if self._client is None:
            self._memory.pop(key, None)
            return
        self._client.delete(key)

    # ------------------------------------------------------------------ #
    # Sorted Set leaderboard (ZADD / ZREVRANGE)
    # ------------------------------------------------------------------ #
    def update_leaderboard(self, player_id: str, score_delta: float) -> None:
        if score_delta <= 0:
            return
        if self._client is None:
            self._mem_leaderboard[player_id] = self._mem_leaderboard.get(player_id, 0.0) + score_delta
            return
        self._client.zincrby(_LEADERBOARD_KEY, score_delta, player_id)

    def get_leaderboard(self, limit: int = 10) -> list[dict[str, Any]]:
        if self._client is None:
            ranked = sorted(self._mem_leaderboard.items(), key=lambda x: x[1], reverse=True)
            return [{"player_id": pid, "score": round(score, 1)} for pid, score in ranked[:limit]]
        entries = self._client.zrevrange(_LEADERBOARD_KEY, 0, limit - 1, withscores=True)
        return [{"player_id": pid, "score": round(score, 1)} for pid, score in entries]

    # ------------------------------------------------------------------ #
    # Ghost player seeding — makes KNN recall return results from round 1
    # ------------------------------------------------------------------ #
    def seed_ghost_players(self) -> None:
        """Seed pre-computed ghost players once so vector similarity recall is non-empty."""
        if self._client is not None:
            if self._client.exists(_GHOST_SEED_FLAG):
                return
            self._client.set(_GHOST_SEED_FLAG, "1", ex=86400)
        elif _GHOST_SEED_FLAG in self._memory:
            return
        else:
            self._memory[_GHOST_SEED_FLAG] = "1"

        for ghost in _GHOST_PLAYERS:
            self.index_player_vector(
                ghost["player_id"],
                ghost["vector"],
                {
                    "style": ghost["style"],
                    "counter_success": ghost["counter_success"],
                    "strategy_weights": ghost["strategy_weights"],
                },
            )
            self.update_leaderboard(ghost["player_id"], ghost["leaderboard_score"])

    # ------------------------------------------------------------------ #
    # Vector similarity: the boss's associative memory
    # ------------------------------------------------------------------ #
    def _ensure_vector_index(self) -> None:
        """Create the RediSearch vector index if Redis Stack is available."""
        try:
            from redis.commands.search.field import NumericField, TagField, TextField, VectorField
            from redis.commands.search.indexDefinition import IndexDefinition, IndexType
        except Exception:
            self._vector_search = False
            return

        try:
            self._client.ft(_VEC_INDEX).info()
            self._vector_search = True
            return
        except Exception:
            pass

        try:
            schema = (
                TagField("player_id"),
                TagField("style"),
                NumericField("counter_success"),
                TextField("strategy"),
                VectorField(
                    "vector",
                    "FLAT",
                    {"TYPE": "FLOAT32", "DIM": _STYLE_VECTOR_DIM, "DISTANCE_METRIC": "COSINE"},
                ),
            )
            definition = IndexDefinition(prefix=[_VEC_PREFIX], index_type=IndexType.HASH)
            self._client.ft(_VEC_INDEX).create_index(schema, definition=definition)
            self._vector_search = True
        except Exception:
            self._vector_search = False

    def index_player_vector(
        self,
        player_id: str,
        vector: list[float],
        metadata: dict[str, Any],
    ) -> None:
        """Store the player's style fingerprint plus the strategy used against them."""
        record = {
            "player_id": player_id,
            "vector": [float(v) for v in vector],
            "style": str(metadata.get("style", "balanced")),
            "counter_success": float(metadata.get("counter_success", 0.0)),
            "strategy": metadata.get("strategy_weights", {}),
        }
        # JSON mirror powers the in-memory cosine fallback and stays human-readable.
        self.set_json(f"{_NS}:vecmeta:{player_id}", record)

        if self._client is None:
            self._mem_vectors[player_id] = record
            return

        self._client.sadd(_VEC_REGISTRY, player_id)
        if not self._vector_search:
            return
        try:
            import numpy as np

            blob = np.asarray(record["vector"], dtype=np.float32).tobytes()
            self._client.hset(
                f"{_VEC_PREFIX}{player_id}",
                mapping={
                    "player_id": player_id,
                    "style": record["style"],
                    "counter_success": record["counter_success"],
                    "strategy": json.dumps(record["strategy"]),
                    "vector": blob,
                },
            )
        except Exception:
            pass

    def recall_similar(
        self,
        vector: list[float],
        k: int = 1,
        exclude: str | None = None,
    ) -> list[dict[str, Any]]:
        """Return the most similar previously-seen players (closest style first)."""
        if self._client is not None and self._vector_search:
            hits = self._recall_via_redisearch(vector, k, exclude)
            if hits is not None:
                return hits
        return self._recall_via_cosine(vector, k, exclude)

    def _recall_via_redisearch(
        self,
        vector: list[float],
        k: int,
        exclude: str | None,
    ) -> list[dict[str, Any]] | None:
        try:
            import numpy as np
            from redis.commands.search.query import Query

            blob = np.asarray(vector, dtype=np.float32).tobytes()
            # Pull a few extra so we can drop the excluded player and still return k.
            fetch = k + (1 if exclude else 0)
            query = (
                Query(f"*=>[KNN {fetch} @vector $vec AS score]")
                .sort_by("score")
                .return_fields("player_id", "style", "counter_success", "strategy", "score")
                .dialect(2)
            )
            res = self._client.ft(_VEC_INDEX).search(query, query_params={"vec": blob})
            out: list[dict[str, Any]] = []
            for doc in res.docs:
                player_id = getattr(doc, "player_id", "")
                if exclude and player_id == exclude:
                    continue
                out.append(
                    {
                        "player_id": player_id,
                        "style": getattr(doc, "style", "balanced"),
                        "counter_success": float(getattr(doc, "counter_success", 0.0)),
                        "strategy_weights": _safe_json(getattr(doc, "strategy", "{}")),
                        "similarity": round(1.0 - float(getattr(doc, "score", 1.0)), 3),
                    }
                )
                if len(out) >= k:
                    break
            return out
        except Exception:
            return None

    def _recall_via_cosine(
        self,
        vector: list[float],
        k: int,
        exclude: str | None,
    ) -> list[dict[str, Any]]:
        if self._client is None:
            records = list(self._mem_vectors.values())
        else:
            player_ids = self._client.smembers(_VEC_REGISTRY) or set()
            records = [self.get_json(f"{_NS}:vecmeta:{pid}") for pid in player_ids]

        scored: list[dict[str, Any]] = []
        for record in records:
            pid = record.get("player_id")
            if not pid or pid == exclude:
                continue
            sim = _cosine(vector, record.get("vector", []))
            scored.append(
                {
                    "player_id": pid,
                    "style": record.get("style", "balanced"),
                    "counter_success": float(record.get("counter_success", 0.0)),
                    "strategy_weights": record.get("strategy", {}),
                    "similarity": round(sim, 3),
                }
            )
        scored.sort(key=lambda item: item["similarity"], reverse=True)
        return scored[:k]


def _safe_json(raw: Any) -> dict[str, Any]:
    try:
        value = json.loads(raw) if isinstance(raw, str) else raw
        return value if isinstance(value, dict) else {}
    except Exception:
        return {}


def _cosine(a: list[float], b: list[float]) -> float:
    if not a or not b or len(a) != len(b):
        return 0.0
    dot = sum(x * y for x, y in zip(a, b))
    na = math.sqrt(sum(x * x for x in a))
    nb = math.sqrt(sum(y * y for y in b))
    if na == 0 or nb == 0:
        return 0.0
    return dot / (na * nb)
