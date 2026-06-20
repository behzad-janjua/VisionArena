from __future__ import annotations

import json
import os
from pathlib import Path
from time import time
from typing import Any
from uuid import uuid4


_DEFAULT_QUEUE_PATH = Path(__file__).with_name("recap_jobs.jsonl")


class RecapQueue:
    def __init__(self, path: str | os.PathLike[str] | None = None) -> None:
        self.path = Path(path or os.getenv("PIKA_RECAP_QUEUE", _DEFAULT_QUEUE_PATH))

    def enqueue(self, prompt: str, metadata: dict[str, Any] | None = None) -> dict[str, Any]:
        self.path.parent.mkdir(parents=True, exist_ok=True)
        job = {
            "job_id": f"recap_{uuid4().hex[:12]}",
            "status": "queued",
            "provider": "pika",
            "prompt": prompt,
            "metadata": metadata or {},
            "created_at": time(),
        }
        with self.path.open("a", encoding="utf-8") as fh:
            fh.write(json.dumps(job) + "\n")
        return job

    def list_jobs(self, limit: int = 20) -> list[dict[str, Any]]:
        if not self.path.exists():
            return []
        lines = self.path.read_text(encoding="utf-8").splitlines()
        jobs = [json.loads(line) for line in lines if line.strip()]
        return jobs[-limit:]
