# KiForge Backend

Run the mock-safe service:

```bash
python3 -m venv .venv
source .venv/bin/activate
pip install -r backend/requirements.txt
uvicorn backend.main:app --reload
```

The Unity client can run without this backend. When connected, send normalized JSON events to `ws://127.0.0.1:8000/ws/unity`.
