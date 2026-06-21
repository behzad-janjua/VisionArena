from __future__ import annotations

import asyncio
import logging
import os
from collections.abc import AsyncIterator
from concurrent.futures import ThreadPoolExecutor
from time import time

from .models import EventType, NormalizedEvent

log = logging.getLogger(__name__)

_POSE_MODEL_PATH = os.path.join(os.path.dirname(__file__), "pose_landmarker_lite.task")

_TARGET_FPS   = int(os.getenv("CV_TARGET_FPS",    "30") or 30)
_CAMERA_INDEX = int(os.getenv("CV_CAMERA_INDEX",   "0") or 0)

# Tuning — override via env vars if your room / distance differs
_PUNCH_DEPTH_M = float(os.getenv("CV_PUNCH_DEPTH", "0.22"))  # metres wrist extends past shoulder
_GUARD_Y_PAD   = float(os.getenv("CV_GUARD_PAD",   "0.04"))  # wrists must clear nose by this much
_WALK_LEAN     = float(os.getenv("CV_WALK_LEAN",   "0.06"))  # ankle-centre x offset from 0.5 (step left/right)

# MediaPipe Pose landmark indices
_NOSE = 0
_L_SHOULDER, _R_SHOULDER = 11, 12
_L_WRIST,    _R_WRIST    = 15, 16
_L_HIP,      _R_HIP      = 23, 24
_L_ANKLE,    _R_ANKLE    = 27, 28

_MOCK_SEQUENCE = [
    {"gesture": "none",        "confidence": 0.8,  "hand": {"x": 0.5, "y": 0.5}, "bodyCenter": {"x": 0.5,  "y": 0.5}},
    {"gesture": "walk_right",  "confidence": 0.8,  "hand": {"x": 0.4, "y": 0.5}, "bodyCenter": {"x": 0.42, "y": 0.5}},
    {"gesture": "right_punch", "confidence": 0.85, "hand": {"x": 0.3, "y": 0.5}, "bodyCenter": {"x": 0.5,  "y": 0.5}},
    {"gesture": "guard",       "confidence": 0.9,  "hand": {"x": 0.5, "y": 0.3}, "bodyCenter": {"x": 0.5,  "y": 0.5}},
    {"gesture": "none",        "confidence": 0.8,  "hand": {"x": 0.5, "y": 0.5}, "bodyCenter": {"x": 0.5,  "y": 0.5}},
]


def _make_event(payload: dict) -> NormalizedEvent:
    return NormalizedEvent(type=EventType.POSE_UPDATE.value, timestamp=time(), payload=payload)


def _try_import_deps():
    try:
        import cv2
        from mediapipe.tasks.python.core.base_options import BaseOptions
        from mediapipe.tasks.python.vision import (
            PoseLandmarker,
            PoseLandmarkerOptions,
            RunningMode,
        )
        return cv2, PoseLandmarker, PoseLandmarkerOptions, RunningMode, BaseOptions
    except ImportError as e:
        log.warning("[CV] import failed: %s", e)
        return None, None, None, None, None


def _detect_gesture(norm_lms, world_lms) -> tuple[str, float]:
    """Map body pose landmarks to a game gesture.

    Coordinate system notes (MediaPipe):
      norm_lms:  x ∈ [0,1] left→right of camera frame, y ∈ [0,1] top→bottom
      world_lms: x/y/z in metres; z: SMALLER value = closer to camera

    Camera is NOT mirrored: person's RIGHT arm appears at LOW x in frame.

    Gesture priority: guard > heavy_punch > right_punch > left_punch > walk > none
    """
    nose    = norm_lms[_NOSE]
    r_wrist = norm_lms[_R_WRIST]
    l_wrist = norm_lms[_L_WRIST]
    l_hip   = norm_lms[_L_HIP]
    r_hip   = norm_lms[_R_HIP]

    rw_w = world_lms[_R_WRIST];   rs_w = world_lms[_R_SHOULDER]
    lw_w = world_lms[_L_WRIST];   ls_w = world_lms[_L_SHOULDER]

    mid_hip_x = (l_hip.x + r_hip.x) / 2.0

    # Guard: both wrists raised above nose level
    if r_wrist.y < nose.y - _GUARD_Y_PAD and l_wrist.y < nose.y - _GUARD_Y_PAD:
        return "guard", 0.9

    # Punch: wrist extends toward camera → z becomes more negative relative to shoulder
    r_depth_delta = rw_w.z - rs_w.z  # negative when right arm jabs toward camera
    l_depth_delta = lw_w.z - ls_w.z  # negative when left arm jabs toward camera

    r_punching = r_depth_delta < -_PUNCH_DEPTH_M
    l_punching = l_depth_delta < -_PUNCH_DEPTH_M

    if r_punching and l_punching:
        return "heavy_punch", 0.9
    if r_punching:
        return "right_punch", 0.85
    if l_punching:
        return "left_punch", 0.85

    # Walk: lateral step detection via ankle midpoint.
    # Camera is not mirrored: step RIGHT → ankles shift to camera-left (lower x) → walk_right.
    l_ankle = norm_lms[_L_ANKLE]
    r_ankle = norm_lms[_R_ANKLE]
    ankle_visible = (getattr(l_ankle, "visibility", 1.0) > 0.5
                     and getattr(r_ankle, "visibility", 1.0) > 0.5)
    step_x = (l_ankle.x + r_ankle.x) / 2.0 if ankle_visible else mid_hip_x
    lean = step_x - 0.5
    if lean < -_WALK_LEAN:
        return "walk_right", min(0.9, 0.7 + abs(lean) * 2)
    if lean > _WALK_LEAN:
        return "walk_left", min(0.9, 0.7 + abs(lean) * 2)

    return "none", 0.6


def _extract_payload(result) -> dict | None:
    if not result.pose_landmarks:
        return None
    norm_lms  = result.pose_landmarks[0]
    world_lms = result.pose_world_landmarks[0]
    gesture, confidence = _detect_gesture(norm_lms, world_lms)
    mid_hip_x = (norm_lms[_L_HIP].x + norm_lms[_R_HIP].x) / 2.0
    mid_hip_y = (norm_lms[_L_HIP].y + norm_lms[_R_HIP].y) / 2.0
    return {
        "gesture":    gesture,
        "confidence": float(confidence),
        "hand":       {"x": float(norm_lms[_R_WRIST].x), "y": float(1.0 - norm_lms[_R_WRIST].y)},
        "bodyCenter": {"x": float(mid_hip_x),             "y": float(1.0 - mid_hip_y)},
    }


def _blocking_init(PoseLandmarker, PoseLandmarkerOptions, RunningMode, BaseOptions):
    import cv2
    cap = cv2.VideoCapture(_CAMERA_INDEX)
    if not cap.isOpened():
        raise RuntimeError(f"Camera index {_CAMERA_INDEX} not available")
    for _ in range(10):
        ret, _ = cap.read()
        if ret:
            break
    else:
        cap.release()
        raise RuntimeError(
            "Camera opened but cannot read frames — grant Camera permission to Terminal "
            "in System Settings > Privacy & Security > Camera"
        )
    options = PoseLandmarkerOptions(
        base_options=BaseOptions(model_asset_path=_POSE_MODEL_PATH),
        running_mode=RunningMode.VIDEO,
        num_poses=1,
    )
    landmarker = PoseLandmarker.create_from_options(options)
    return cap, landmarker


def _blocking_capture(cap, landmarker) -> dict | None:
    import cv2
    from mediapipe import Image, ImageFormat
    ret, frame = cap.read()
    if not ret:
        return None
    rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
    mp_img = Image(image_format=ImageFormat.SRGB, data=rgb)
    result = landmarker.detect_for_video(mp_img, int(time() * 1000))
    return _extract_payload(result)


async def vision_bridge_stream() -> AsyncIterator[NormalizedEvent]:
    """
    Yields POSE_UPDATE events from full-body pose tracking (MediaPipe PoseLandmarker).

    Gestures emitted:
      right_punch  — extend right arm toward camera (jab)
      left_punch   — extend left arm toward camera
      heavy_punch  — extend both arms simultaneously
      guard        — raise both wrists above nose level
      walk_right   — step your body to the right (toward the boss)
      walk_left    — step your body to the left (away from boss)
      none         — neutral / no clear gesture

    Falls back to a looping mock stream when camera or model is unavailable.
    """
    cv2, PoseLandmarker, PoseLandmarkerOptions, RunningMode, BaseOptions = _try_import_deps()

    if cv2 is None:
        log.warning("[CV] mediapipe/opencv not available — using mock gesture stream")
        async for evt in _mock_stream():
            yield evt
        return

    if not os.path.exists(_POSE_MODEL_PATH):
        log.warning("[CV] Pose model not found at %s — using mock stream", _POSE_MODEL_PATH)
        async for evt in _mock_stream():
            yield evt
        return

    executor = ThreadPoolExecutor(max_workers=1)
    loop = asyncio.get_event_loop()
    try:
        cap, landmarker = await loop.run_in_executor(
            executor, _blocking_init, PoseLandmarker, PoseLandmarkerOptions, RunningMode, BaseOptions
        )
    except Exception as exc:
        log.warning("[CV] Camera unavailable (%s) — mock stream", exc)
        executor.shutdown(wait=False)
        async for evt in _mock_stream():
            yield evt
        return

    log.info(
        "[CV] Full-body tracker running — jab toward camera=punch | "
        "raise hands=guard | step left/right=walk"
    )
    try:
        while True:
            payload = await loop.run_in_executor(executor, _blocking_capture, cap, landmarker)
            if payload:
                yield _make_event(payload)
    finally:
        loop.run_in_executor(executor, cap.release)
        executor.shutdown(wait=False)


async def _mock_stream() -> AsyncIterator[NormalizedEvent]:
    frame_delay = 1.0 / _TARGET_FPS
    i = 0
    while True:
        payload = {**_MOCK_SEQUENCE[(i // 15) % len(_MOCK_SEQUENCE)], "mock": True}
        yield _make_event(payload)
        i += 1
        await asyncio.sleep(frame_delay)
