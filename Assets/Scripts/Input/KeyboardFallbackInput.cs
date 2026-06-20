using KiForge.Shared;
using UnityEngine;

namespace KiForge.Input
{
    public sealed class KeyboardFallbackInput : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float keyboardMoveSpeed = 4f;

        /// <summary>Set to false by CvAimController when live body tracking takes over.</summary>
        public bool EnableMovement = true;

        private KiForgeEventBus eventBus;
        private Transform playerRoot;
        private bool charging;
        private float chargeStartedAt;
        private Vector2 latestAim = Vector2.right;

        public void Initialize(KiForgeEventBus bus, Transform player, Camera cameraForAim)
        {
            eventBus = bus;
            playerRoot = player;
            targetCamera = cameraForAim;
        }

        private void Update()
        {
            if (eventBus == null || playerRoot == null)
            {
                return;
            }

            UpdateMovement();
            UpdateAim();
            UpdateChargeInput();
            UpdateActionButtons();
            PublishPose();
        }

        private void UpdateMovement()
        {
            if (!EnableMovement) return;
            var x = UnityEngine.Input.GetAxisRaw("Horizontal");
            playerRoot.position += Vector3.right * x * keyboardMoveSpeed * Time.deltaTime;
            playerRoot.position = new Vector3(Mathf.Clamp(playerRoot.position.x, -6f, 1f), playerRoot.position.y, playerRoot.position.z);
        }

        private void UpdateAim()
        {
            if (targetCamera == null)
            {
                return;
            }

            var mouse = UnityEngine.Input.mousePosition;
            var world = targetCamera.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, -targetCamera.transform.position.z));
            latestAim = ((Vector2)world - (Vector2)playerRoot.position).normalized;
            if (latestAim.sqrMagnitude < 0.001f)
            {
                latestAim = Vector2.right;
            }
        }

        private void UpdateChargeInput()
        {
            var origin = (Vector2)playerRoot.position + Vector2.up * 0.6f;

            if (UnityEngine.Input.GetKeyDown(KeyCode.F))
            {
                charging = true;
                chargeStartedAt = Time.time;
                eventBus.PublishGesture(GestureEvent.Create(GestureEventType.ChargeStart, origin, latestAim));
            }

            if (charging && UnityEngine.Input.GetKey(KeyCode.F))
            {
                var holdSeconds = Time.time - chargeStartedAt;
                eventBus.PublishGesture(GestureEvent.Create(GestureEventType.ChargeUpdate, origin, latestAim, Mathf.Clamp01(holdSeconds / 3.5f), holdSeconds));
            }

            if (charging && UnityEngine.Input.GetKeyUp(KeyCode.F))
            {
                charging = false;
                var holdSeconds = Time.time - chargeStartedAt;
                eventBus.PublishGesture(GestureEvent.Create(GestureEventType.BlastRelease, origin, latestAim, Mathf.Clamp01(holdSeconds / 3.5f), holdSeconds));
            }
        }

        private void UpdateActionButtons()
        {
            var origin = (Vector2)playerRoot.position + Vector2.up * 0.6f;

            if (UnityEngine.Input.GetKeyDown(KeyCode.S))
            {
                eventBus.PublishGesture(GestureEvent.Create(GestureEventType.ShieldStart, origin, latestAim));
            }

            if (UnityEngine.Input.GetKeyUp(KeyCode.S))
            {
                eventBus.PublishGesture(GestureEvent.Create(GestureEventType.ShieldEnd, origin, latestAim));
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.A))
            {
                eventBus.PublishGesture(GestureEvent.Create(GestureEventType.SlashLeft, origin, Vector2.left));
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.D))
            {
                eventBus.PublishGesture(GestureEvent.Create(GestureEventType.SlashRight, origin, Vector2.right));
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Space))
            {
                eventBus.PublishGesture(GestureEvent.Create(GestureEventType.Ultimate, origin, latestAim, 1f, 4f));
            }
        }

        private void PublishPose()
        {
            eventBus.PublishPose(new PoseEvent
            {
                wrist = (Vector2)playerRoot.position + Vector2.up * 0.6f,
                bodyCenter = playerRoot.position,
                aim = latestAim,
                confidence = 1f,
                timestamp = Time.realtimeSinceStartupAsDouble
            });
        }
    }
}
