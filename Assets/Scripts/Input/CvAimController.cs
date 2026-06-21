using KiForge.Animation;
using KiForge.Shared;
using UnityEngine;

namespace KiForge.Input
{
    /// <summary>
    /// Applies live CV pose data to the player.
    ///
    /// Body-center X → player horizontal position (drives the fighter's home so
    /// locomotion animation and post-attack return still work).
    /// Wrist position → world-space aim reticle.
    ///
    /// While confident CV frames are arriving, body tracking owns movement and the
    /// keyboard <see cref="PlayerWalkController"/> is disabled. If frames go stale
    /// (camera unplugged, person leaves frame) movement reverts to the keyboard so
    /// the demo never gets stuck.
    /// </summary>
    public sealed class CvAimController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private FighterAnimationController fighter;
        [SerializeField] private PlayerWalkController walkController;

        [Header("Camera Mirror")]
        [Tooltip("Flip X axis — enable for typical selfie/mirror webcams")]
        [SerializeField] private bool flipCameraX = true;

        [Header("World-Space Mapping")]
        [Tooltip("Body-center [0,1] maps across this half-width of horizontal travel")]
        [SerializeField] private float playerHalfRange = 4.5f;
        [Tooltip("Horizontal bias added to the mapped body-center X")]
        [SerializeField] private float playerCenterBias = 0f;
        [SerializeField] private float leftBound = -4.5f;
        [SerializeField] private float rightBound = 4.5f;
        [SerializeField] private float wristWorldScaleX = 5f;
        [SerializeField] private float wristWorldScaleY = 4f;
        [SerializeField] private float wristWorldOffsetY = 0.5f;

        [Header("Smoothing")]
        [Tooltip("Max horizontal units per second the body-tracked player travels")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float reticleSmoothSpeed = 20f;

        [Header("Confidence / Staleness")]
        [SerializeField] private float confidenceThreshold = 0.4f;
        [Tooltip("If no confident frame arrives within this many seconds, hand movement back to keyboard")]
        [SerializeField] private float poseTimeout = 1.0f;

        private KiForgeEventBus eventBus;
        private float targetPlayerX;
        private Vector3 targetReticlePos;
        private GameObject reticleObj;
        private float lastPoseTime = -999f;
        private bool cvOwnsMovement;

        /// <summary>World-space wrist position from the latest CV frame.</summary>
        public Vector2 WorldWristPosition { get; private set; }

        /// <summary>True once at least one confident CV frame has been received.</summary>
        public bool HasLivePose { get; private set; }

        /// <summary>True while CV (not keyboard) is currently driving movement.</summary>
        public bool CvActive => cvOwnsMovement;

        public void Initialize(
            KiForgeEventBus bus,
            Transform player,
            FighterAnimationController playerFighter,
            PlayerWalkController walk,
            float left,
            float right)
        {
            eventBus = bus;
            playerTransform = player;
            fighter = playerFighter;
            walkController = walk;
            leftBound = left;
            rightBound = right;

            if (playerTransform != null)
                targetPlayerX = playerTransform.position.x;

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
            lastPoseTime = Time.time;

            float cx = pose.bodyCenter.x;
            float wx = pose.wrist.x;
            float wy = pose.wrist.y;

            if (flipCameraX)
            {
                cx = 1f - cx;
                wx = 1f - wx;
            }

            // Body center [0,1] → world X
            float worldX = (cx - 0.5f) * 2f * playerHalfRange + playerCenterBias;
            targetPlayerX = Mathf.Clamp(worldX, leftBound, rightBound);

            // Wrist [0,1] → world-space reticle
            float wrx = (wx - 0.5f) * wristWorldScaleX;
            float wry = (wy - 0.5f) * wristWorldScaleY + wristWorldOffsetY;
            WorldWristPosition = new Vector2(wrx, wry);
            targetReticlePos = new Vector3(wrx, wry, -0.3f);
        }

        private void Update()
        {
            bool fresh = HasLivePose && Time.time - lastPoseTime <= poseTimeout;
            SetCvOwnership(fresh);

            if (!cvOwnsMovement)
            {
                if (reticleObj != null)
                    reticleObj.SetActive(false);
                return;
            }

            DriveMovement();
            DriveReticle();
        }

        private void SetCvOwnership(bool fresh)
        {
            if (fresh == cvOwnsMovement)
                return;

            cvOwnsMovement = fresh;

            // CV and keyboard must not both write the player's position in the same frame.
            if (walkController != null)
                walkController.enabled = !fresh;

            if (!fresh && fighter != null)
                fighter.StopLocomotion();
        }

        private void DriveMovement()
        {
            if (playerTransform == null)
                return;

            // Let an in-progress attack (lunge + return-to-home) finish uninterrupted.
            if (fighter != null && fighter.IsAttacking)
                return;

            float curX = playerTransform.position.x;
            float newX = Mathf.Clamp(
                Mathf.MoveTowards(curX, targetPlayerX, moveSpeed * Time.deltaTime),
                leftBound,
                rightBound);

            var newPos = new Vector3(newX, playerTransform.position.y, playerTransform.position.z);
            playerTransform.position = newPos;

            if (fighter != null)
            {
                fighter.SetHome(newPos);
                float delta = newX - curX;
                if (Mathf.Abs(delta) > 0.0005f)
                    fighter.SetLocomotion(Mathf.Sign(delta));
                else
                    fighter.StopLocomotion();
            }
        }

        private void DriveReticle()
        {
            if (reticleObj == null)
                return;

            reticleObj.SetActive(true);
            reticleObj.transform.position = Vector3.Lerp(
                reticleObj.transform.position, targetReticlePos, Time.deltaTime * reticleSmoothSpeed);
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
