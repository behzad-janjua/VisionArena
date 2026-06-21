"""Standalone body CV smoke test — prints live body gestures from the webcam.

Run from the repo root (venv active):
    python -m backend.cv_check

Stand in front of your camera and try:
  • Extend right arm toward camera (jab)    → RIGHT_PUNCH
  • Extend left arm toward camera            → LEFT_PUNCH
  • Extend both arms simultaneously          → HEAVY_PUNCH
  • Raise both hands above nose level        → GUARD
  • Lean body to your right                  → WALK_RIGHT
  • Lean body to your left                   → WALK_LEFT

Ctrl-C stops. [MOCK] means the camera or model couldn't be opened.
Check System Settings > Privacy & Security > Camera for Terminal permissions.

Tune sensitivity via env vars if needed:
  CV_PUNCH_DEPTH=0.22   metres wrist must extend past shoulder (default 0.22)
  CV_GUARD_PAD=0.04     wrist clearance above nose (default 0.04)
  CV_WALK_LEAN=0.10     hip-centre offset from 0.5 to trigger walk (default 0.10)
"""
from __future__ import annotations

import asyncio
from backend.vision_bridge import _CAMERA_INDEX, vision_bridge_stream


async def main() -> None:
    print(
        f"[cv_check] camera {_CAMERA_INDEX} — full-body tracker\n"
        "  jab toward camera = punch | raise hands = guard | lean = walk\n"
    )
    n = 0
    async for evt in vision_bridge_stream():
        p = evt.payload
        n += 1
        gesture = p["gesture"].upper().ljust(13)
        hip_x   = p.get("bodyCenter", {}).get("x", 0.5)
        mock    = "  [MOCK]" if p.get("mock") else ""
        print(f"#{n:04d}  {gesture}  conf={p['confidence']:.2f}  hip_x={hip_x:.2f}{mock}")


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        print("\n[cv_check] stopped")
