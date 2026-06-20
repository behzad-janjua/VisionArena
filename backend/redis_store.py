from __future__ import annotations

import json
import os
from typing import Any


class RedisStore:
    def __init__(self, url: str | None = None) -> None:
        self.url = url or os.getenv("REDIS_URL")
        self._memory: dict[str, Any] = {}
        self._client = None

        if self.url:
            try:
                import redis

                self._client = redis.Redis.from_url(self.url, decode_responses=True)
                self._client.ping()
            except Exception:
                self._client = None

    @property
    def using_memory(self) -> bool:
        return self._client is None

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
