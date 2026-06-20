using System.Collections;
using UnityEngine;

namespace KiForge.Scene
{
    [RequireComponent(typeof(Camera))]
    public sealed class ArenaCameraController : MonoBehaviour
    {
        [SerializeField] private Vector3 lockedPosition = new Vector3(0f, 0.4f, -10f);
        [SerializeField] private float orthographicSize = 5f;
        [SerializeField] private Color backgroundColor = new Color(0.02f, 0.025f, 0.04f);

        public static ArenaCameraController Instance { get; private set; }

        private Camera cam;
        private Vector3 basePosition;
        private bool isShaking;

        private void Awake()
        {
            Instance = this;
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = orthographicSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = backgroundColor;
            transform.position = lockedPosition;
            transform.rotation = Quaternion.identity;
            basePosition = lockedPosition;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void LateUpdate()
        {
            if (!isShaking)
                transform.position = basePosition;
        }

        public void Shake(float duration = 0.25f, float magnitude = 0.18f)
        {
            if (!isShaking)
                StartCoroutine(ShakeRoutine(duration, magnitude));
        }

        private IEnumerator ShakeRoutine(float duration, float magnitude)
        {
            isShaking = true;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = 1f - (elapsed / duration);
                Vector2 offset = Random.insideUnitCircle * (magnitude * t);
                transform.position = basePosition + new Vector3(offset.x, offset.y, 0f);
                elapsed += Time.deltaTime;
                yield return null;
            }
            transform.position = basePosition;
            isShaking = false;
        }
    }
}
