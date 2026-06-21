"""Standalone CV smoke test — prints live pose frames from the webcam.

Run from the repo root (venv active):

    python -m backend.cv_check

Stand in front of the camera and move side to side / raise a hand. You should see
the confidence, wrist, body-center, and aim values change every frame. Ctrl-C stops.

If you see "using mock pose stream", the camera or model could not be opened — grant
Camera permission to your terminal in System Settings > Privacy & Security > Camera,
or set CV_CAMERA_INDEX in .env to a different webcam.
"""
from __future__ import annotations

import asyncio

from backend.vision_bridge import _CAMERA_INDEX, vision_bridge_stream


async def main() -> None:
    print(f"[cv_check] opening camera index {_CAMERA_INDEX} — stand in frame (Ctrl-C to stop)\n")
    n = 0
    async for evt in vision_bridge_stream():
        p = evt.payload
        n += 1
        print(
            f"#{n:04d} conf={p['confidence']:.2f}  "
            f"wrist=({p['wrist']['x']:.2f},{p['wrist']['y']:.2f})  "
            f"body=({p['bodyCenter']['x']:.2f},{p['bodyCenter']['y']:.2f})  "
            f"aim=({p['aim']['x']:.2f},{p['aim']['y']:.2f})"
        )


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        print("\n[cv_check] stopped")
