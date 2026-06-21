"""Standalone CV smoke test — prints the live hand gesture from the webcam.

Run from the repo root (venv active):

    python -m backend.cv_check

Hold a closed fist (-> punch) or an open palm (-> walk forward) in front of the
camera. You should see the gesture label flip between FIST / OPEN_PALM / none with
its confidence every frame. Ctrl-C stops.

If you see "using mock gesture stream", the camera or model could not be opened —
grant Camera permission to your terminal in System Settings > Privacy & Security >
Camera, or set CV_CAMERA_INDEX in .env to a different webcam.
"""
from __future__ import annotations

import asyncio

from backend.vision_bridge import _CAMERA_INDEX, vision_bridge_stream


async def main() -> None:
    print(f"[cv_check] opening camera index {_CAMERA_INDEX} — show fist / open palm (Ctrl-C to stop)\n")
    n = 0
    async for evt in vision_bridge_stream():
        p = evt.payload
        n += 1
        label = p["gesture"].upper().ljust(10)
        print(
            f"#{n:04d} gesture={label} conf={p['confidence']:.2f}  "
            f"hand=({p['hand']['x']:.2f},{p['hand']['y']:.2f})"
        )


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        print("\n[cv_check] stopped")
