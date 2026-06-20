from __future__ import annotations

import asyncio
import logging
import math
import os
from collections.abc import AsyncIterator
from concurrent.futures import ThreadPoolExecutor
from time import time

from .models import EventType, NormalizedEvent

log = logging.getLogger(__name__)

# MediaPipe Pose landmark indices
_RIGHT_WRIST = 16
_RIGHT_ELBOW = 14
_LEFT_HIP = 23
_RIGHT_HIP = 24

_MODEL_PATH = os.path.join(os.path.dirname(__file__), "pose_landmarker_lite.task")

_MOCK_SEQUENCE = [
    {"wrist": {"x": 0.65, "y": 0.38}, "bodyCenter": {"x": 0.52, "y": 0.60},
     "aim": {"x": 0.92, "y": -0.10}, "confidence": 1.0},
    {"wrist": {"x": 0.67, "y": 0.36}, "bodyCenter": {"x": 0.51, "y": 0.61},
     "aim": {"x": 0.94, "y": -0.12}, "confidence": 1.0},
    {"wrist": {"x": 0.63, "y": 0.40}, "bodyCenter": {"x": 0.50, "y": 0.60},
     "aim": {"x": 0.90, "y": -0.08}, "confidence": 1.0},
]

_TARGET_FPS = 30


def _make_event(payload: dict) -> NormalizedEvent:
    return NormalizedEvent(type=EventType.POSE_UPDATE.value, timestamp=time(), payload=payload)


def _try_import_deps():
    try:
        import cv2
        import mediapipe as mp
        from mediapipe.tasks.python.vision import PoseLandmarker, PoseLandmarkerOptions, RunningMode
        from mediapipe.tasks.python.core.base_options import BaseOptions
        return cv2, mp, PoseLandmarker, PoseLandmarkerOptions, RunningMode, BaseOptions
    except ImportError as e:
        log.warning("[CV] import failed: %s", e)
        return None, None, None, None, None, None


def _extract_payload(result) -> dict | None:
    if not result.pose_landmarks:
        return None

    lm = result.pose_landmarks[0]   # first (and only) detected person
    if len(lm) <= _RIGHT_HIP:
        return None

    rw = lm[_RIGHT_WRIST]
    re = lm[_RIGHT_ELBOW]
    lh = lm[_LEFT_HIP]
    rh = lm[_RIGHT_HIP]

    # Use presence score as confidence proxy (tasks API uses presence/visibility)
    confidence = (rw.presence + re.presence) / 2.0 if hasattr(rw, "presence") else 0.8
    if confidence < 0.40:
        return None

    # elbow→wrist direction; y-flip so up is positive in game coords
    dx = rw.x - re.x
    dy = -(rw.y - re.y)
    length = math.sqrt(dx * dx + dy * dy) or 1.0

    cx = (lh.x + rh.x) / 2.0
    cy = 1.0 - (lh.y + rh.y) / 2.0

    return {
        "wrist":      {"x": rw.x,       "y": 1.0 - rw.y},
        "bodyCenter": {"x": cx,          "y": cy},
        "aim":        {"x": dx / length, "y": dy / length},
        "confidence": float(min(confidence, 0.99)),  # keep < 1.0 to distinguish from keyboard fallback
    }


def _blocking_init(PoseLandmarker, PoseLandmarkerOptions, RunningMode, BaseOptions):
    import cv2
    cap = cv2.VideoCapture(0)
    if not cap.isOpened():
        raise RuntimeError("Camera index 0 not available")

    # Warm up and verify we can actually read (triggers macOS permission check)
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
        base_options=BaseOptions(model_asset_path=_MODEL_PATH),
        running_mode=RunningMode.VIDEO,
        num_poses=1,
        min_pose_detection_confidence=0.5,
        min_pose_presence_confidence=0.5,
        min_tracking_confidence=0.5,
    )
    landmarker = PoseLandmarker.create_from_options(options)
    return cap, landmarker


def _blocking_capture(cap, landmarker) -> dict | None:
    import cv2
    from mediapipe.framework.formats import image as mp_image
    from mediapipe import Image, ImageFormat

    ret, frame = cap.read()
    if not ret:
        return None

    rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
    mp_img = Image(image_format=ImageFormat.SRGB, data=rgb)
    timestamp_ms = int(time() * 1000)
    result = landmarker.detect_for_video(mp_img, timestamp_ms)
    return _extract_payload(result)


async def vision_bridge_stream() -> AsyncIterator[NormalizedEvent]:
    """
    Yields POSE_UPDATE events from the webcam via MediaPipe Pose Landmarker.
    Falls back to looping mock data when the camera or model is unavailable.
    """
    imports = _try_import_deps()
    cv2, mp, PoseLandmarker, PoseLandmarkerOptions, RunningMode, BaseOptions = imports

    if cv2 is None:
        log.warning("[CV] mediapipe/opencv not available — using mock pose stream")
        async for evt in _mock_stream():
            yield evt
        return

    if not os.path.exists(_MODEL_PATH):
        log.warning("[CV] Model file not found at %s — using mock pose stream", _MODEL_PATH)
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
        log.warning("[CV] Camera unavailable (%s) — using mock pose stream", exc)
        executor.shutdown(wait=False)
        async for evt in _mock_stream():
            yield evt
        return

    log.info("[CV] Webcam opened — MediaPipe Pose Landmarker running at ~%d fps", _TARGET_FPS)
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
        yield _make_event(_MOCK_SEQUENCE[i % len(_MOCK_SEQUENCE)])
        i += 1
        await asyncio.sleep(frame_delay)
