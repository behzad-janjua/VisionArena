from __future__ import annotations

import asyncio
import logging
import os
from collections.abc import AsyncIterator
from concurrent.futures import ThreadPoolExecutor
from time import time

from .models import EventType, NormalizedEvent

log = logging.getLogger(__name__)

# GestureRecognizer model (bundles hand landmarker + canonical gesture classifier).
_MODEL_PATH = os.path.join(os.path.dirname(__file__), "gesture_recognizer.task")

# MediaPipe canonical gesture labels -> the game gestures we care about for this test.
_GESTURE_MAP = {
    "Closed_Fist": "fist",       # -> punch
    "Open_Palm": "open_palm",    # -> walk forward
}

_TARGET_FPS = int(os.getenv("CV_TARGET_FPS", "30") or 30)
_CAMERA_INDEX = int(os.getenv("CV_CAMERA_INDEX", "0") or 0)

# Mock loop (no camera): a few open-palm frames (walk) then a fist (punch) then rest.
_MOCK_SEQUENCE = [
    {"gesture": "open_palm", "hand": {"x": 0.55, "y": 0.45}, "confidence": 0.95},
    {"gesture": "open_palm", "hand": {"x": 0.55, "y": 0.45}, "confidence": 0.95},
    {"gesture": "open_palm", "hand": {"x": 0.55, "y": 0.45}, "confidence": 0.95},
    {"gesture": "fist", "hand": {"x": 0.52, "y": 0.48}, "confidence": 0.95},
    {"gesture": "none", "hand": {"x": 0.50, "y": 0.50}, "confidence": 0.50},
]


def _make_event(payload: dict) -> NormalizedEvent:
    return NormalizedEvent(type=EventType.POSE_UPDATE.value, timestamp=time(), payload=payload)


def _try_import_deps():
    try:
        import cv2  # noqa: F401
        from mediapipe.tasks.python.core.base_options import BaseOptions
        from mediapipe.tasks.python.vision import (
            GestureRecognizer,
            GestureRecognizerOptions,
            RunningMode,
        )

        return cv2, GestureRecognizer, GestureRecognizerOptions, RunningMode, BaseOptions
    except ImportError as e:
        log.warning("[CV] import failed: %s", e)
        return None, None, None, None, None


def _extract_payload(result) -> dict | None:
    if not result.gestures:
        return None

    top = result.gestures[0][0]  # first hand, highest-score category
    gesture = _GESTURE_MAP.get(top.category_name, "none")

    # Approximate hand center from the wrist landmark (index 0) when available.
    hand_x, hand_y = 0.5, 0.5
    if result.hand_landmarks:
        wrist = result.hand_landmarks[0][0]
        hand_x, hand_y = wrist.x, 1.0 - wrist.y

    return {
        "gesture": gesture,
        "hand": {"x": float(hand_x), "y": float(hand_y)},
        "confidence": float(top.score),
    }


def _blocking_init(GestureRecognizer, GestureRecognizerOptions, RunningMode, BaseOptions):
    import cv2

    cap = cv2.VideoCapture(_CAMERA_INDEX)
    if not cap.isOpened():
        raise RuntimeError(f"Camera index {_CAMERA_INDEX} not available")

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

    options = GestureRecognizerOptions(
        base_options=BaseOptions(model_asset_path=_MODEL_PATH),
        running_mode=RunningMode.VIDEO,
        num_hands=1,
    )
    recognizer = GestureRecognizer.create_from_options(options)
    return cap, recognizer


def _blocking_capture(cap, recognizer) -> dict | None:
    import cv2
    from mediapipe import Image, ImageFormat

    ret, frame = cap.read()
    if not ret:
        return None

    rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
    mp_img = Image(image_format=ImageFormat.SRGB, data=rgb)
    timestamp_ms = int(time() * 1000)
    result = recognizer.recognize_for_video(mp_img, timestamp_ms)
    return _extract_payload(result)


async def vision_bridge_stream() -> AsyncIterator[NormalizedEvent]:
    """
    Yields POSE_UPDATE events carrying a recognized hand gesture.

    Closed fist -> "fist" (punch), open palm -> "open_palm" (walk forward).
    Falls back to a looping mock gesture stream when the camera or model is missing.
    """
    cv2, GestureRecognizer, GestureRecognizerOptions, RunningMode, BaseOptions = _try_import_deps()

    if cv2 is None:
        log.warning("[CV] mediapipe/opencv not available — using mock gesture stream")
        async for evt in _mock_stream():
            yield evt
        return

    if not os.path.exists(_MODEL_PATH):
        log.warning("[CV] Model file not found at %s — using mock gesture stream", _MODEL_PATH)
        async for evt in _mock_stream():
            yield evt
        return

    executor = ThreadPoolExecutor(max_workers=1)
    loop = asyncio.get_event_loop()
    try:
        cap, recognizer = await loop.run_in_executor(
            executor, _blocking_init, GestureRecognizer, GestureRecognizerOptions, RunningMode, BaseOptions
        )
    except Exception as exc:
        log.warning("[CV] Camera unavailable (%s) — using mock gesture stream", exc)
        executor.shutdown(wait=False)
        async for evt in _mock_stream():
            yield evt
        return

    log.info("[CV] Webcam opened — MediaPipe GestureRecognizer running (fist=punch, open palm=walk)")
    try:
        while True:
            payload = await loop.run_in_executor(executor, _blocking_capture, cap, recognizer)
            if payload:
                yield _make_event(payload)
    finally:
        loop.run_in_executor(executor, cap.release)
        executor.shutdown(wait=False)


async def _mock_stream() -> AsyncIterator[NormalizedEvent]:
    frame_delay = 1.0 / _TARGET_FPS
    i = 0
    while True:
        # Hold each mock gesture for ~0.5s so the punch edge-trigger is visible.
        # Flag every frame as synthetic so the Unity client won't let canned gestures
        # seize control from the keyboard when no real camera is attached.
        payload = {**_MOCK_SEQUENCE[(i // 15) % len(_MOCK_SEQUENCE)], "mock": True}
        yield _make_event(payload)
        i += 1
        await asyncio.sleep(frame_delay)
