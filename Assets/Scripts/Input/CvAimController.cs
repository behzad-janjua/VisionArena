using KiForge.Shared;
using UnityEngine;

namespace KiForge.Input
{
    /// <summary>
    /// Applies live CV pose data to the player.
    ///
    /// Wrist position → aim reticle in world space.
    /// Body-center X → player horizontal position.
    /// Aim vector is forwarded via the event bus and consumed by ArenaGameController.
    ///
    /// When live CV pose arrives (confidence above threshold), movement priority shifts
    /// from keyboard arrows to body tracking.  The KeyboardFallbackInput continues to
    /// handle all attack inputs (charge, slash, etc.) regardless.
    /// </summary>
    public sealed class CvAimController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform playerTransform;

        [Header("Camera Mirror")]
        [Tooltip("Flip X axis — enable for typical selfie/mirror webcams")]
        [SerializeField] private bool flipCameraX = true;

        [Header("World-Space Mapping")]
        [Tooltip("Half-width the player can travel horizontally")]
        [SerializeField] private float playerHalfRange = 3.5f;
        [Tooltip("Horizontal player range bias: 0 = fully left-biased, 0.5 = centred")]
        [SerializeField] private float playerCenterBias = -0.5f;
        [SerializeField] private float wristWorldScaleX = 5f;
        [SerializeField] private float wristWorldScaleY = 4f;
        [SerializeField] private float wristWorldOffsetY = 0.5f;

        [Header("Smoothing")]
        [SerializeField] private float moveSmoothSpeed = 6f;
        [SerializeField] private float reticleSmoothSpeed = 20f;

        [Header("Confidence Gate")]
        [SerializeField] private float confidenceThreshold = 0.4f;

        // Optional: assign a KeyboardFallbackInput to disable its horizontal movement
        // when CV takes over.  Leave null if not needed.
        [SerializeField] private KeyboardFallbackInput keyboardInput;

        private KiForgeEventBus eventBus;
        private Vector3 targetPlayerPos;
        private Vector3 targetReticlePos;
        private GameObject reticleObj;

        /// <summary>World-space wrist position from the latest CV frame.</summary>
        public Vector2 WorldWristPosition { get; private set; }

        /// <summary>True once at least one confident CV frame has been received.</summary>
        public bool HasLivePose { get; private set; }

        public void Initialize(KiForgeEventBus bus, Transform player, KeyboardFallbackInput keyboard = null)
        {
            eventBus = bus;
            playerTransform = player;
            keyboardInput = keyboard;

            if (playerTransform != null)
                targetPlayerPos = playerTransform.position;

            CreateReticle();
            eventBus.PoseReceived += OnPose;
        }

        private void OnDestroy()
        {
            if (eventBus != null)
                eventBus.PoseReceived -= OnPose;
        }

        private void OnPose(PoseEvent pose)
        {
            if (pose.confidence < confidenceThreshold)
                return;

            HasLivePose = true;

            float cx = pose.bodyCenter.x;
            float wx = pose.wrist.x;
            float wy = pose.wrist.y;
            float ax = pose.aim.x;

            if (flipCameraX)
            {
                cx = 1f - cx;
                wx = 1f - wx;
                ax = -ax;
            }

            // Body center [0,1] → world X within [-playerHalfRange, +playerHalfRange]
            float worldX = (cx - 0.5f) * 2f * playerHalfRange + playerCenterBias;
            if (playerTransform != null)
                targetPlayerPos = new Vector3(worldX, playerTransform.position.y, playerTransform.position.z);

            // Wrist [0,1] normalized → world space
            float wrx = (wx - 0.5f) * wristWorldScaleX;
            float wry = (wy - 0.5f) * wristWorldScaleY + wristWorldOffsetY;
            WorldWristPosition = new Vector2(wrx, wry);
            targetReticlePos = new Vector3(wrx, wry, -0.3f);

            if (keyboardInput != null)
                keyboardInput.EnableMovement = false;
        }

        private void Update()
        {
            if (!HasLivePose)
                return;

            if (playerTransform != null)
                playerTransform.position = Vector3.Lerp(
                    playerTransform.position, targetPlayerPos, Time.deltaTime * moveSmoothSpeed);

            if (reticleObj != null)
            {
                reticleObj.SetActive(true);
                reticleObj.transform.position = Vector3.Lerp(
                    reticleObj.transform.position, targetReticlePos, Time.deltaTime * reticleSmoothSpeed);
            }
        }

        private void CreateReticle()
        {
            reticleObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            reticleObj.name = "CV Wrist Reticle";
            reticleObj.transform.localScale = Vector3.one * 0.22f;
            reticleObj.GetComponent<Renderer>().material =
                new Material(Shader.Find("Sprites/Default")) { color = new Color(0.1f, 1f, 0.5f, 0.7f) };
            Object.Destroy(reticleObj.GetComponent<Collider>());
            reticleObj.SetActive(false);
        }
    }
}
