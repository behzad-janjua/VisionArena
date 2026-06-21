using KiForge.Animation;
using KiForge.Combat;
using KiForge.Shared;
using UnityEngine;

namespace KiForge.Input
{
    /// <summary>
    /// Full-body CV mapping (MediaPipe PoseLandmarker via the backend WebSocket).
    ///
    /// Gesture → game action:
    ///   right_punch  — extend right arm toward camera → PunchRight
    ///   left_punch   — extend left arm toward camera  → PunchLeft
    ///   heavy_punch  — extend both arms               → HeavyPunch
    ///   guard        — raise both wrists above nose   → StartBlock / StopBlock
    ///   walk_right   — lean body right (toward boss)  → SetLocomotion(+1)
    ///   walk_left    — lean body left  (away from boss)→ SetLocomotion(-1)
    ///
    /// MYO priority: while the MYO reports an active fist charge
    /// (CurrentHoldSeconds > 0.1), body punch gestures are suppressed.
    /// The punch fires on MYO fist release with the accumulated charge tier.
    ///
    /// CV owns movement only while fresh frames arrive. Reverts to the keyboard
    /// PlayerWalkController after poseTimeout seconds of silence.
    /// </summary>
    public sealed class CvAimController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private FighterAnimationController fighter;
        [SerializeField] private PlayerWalkController walkController;
        [SerializeField] private PlayerAttackInput attackInput;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 1.3f;
        [SerializeField] private float leftBound  = -4.5f;
        [SerializeField] private float rightBound =  4.5f;

        [Header("Confidence / Staleness")]
        [SerializeField] private float confidenceThreshold = 0.6f;
        [Tooltip("Seconds without a confident frame before reverting to keyboard control")]
        [SerializeField] private float poseTimeout = 1.5f;

        [Header("Debug")]
        [Tooltip("Log gesture transitions and action triggers to the Console")]
        [SerializeField] private bool debugLog = true;

        private KiForgeEventBus eventBus;
        private MyoChargeInput myo;

        private string currentGesture = "none";
        private float lastPoseTime = -999f;
        private bool cvOwnsMovement;

        // Edge-trigger state — each fires once on enter, resets on neutral/exit
        private bool rightPunchFired;
        private bool leftPunchFired;
        private bool heavyPunchFired;
        private bool guardActive;

        /// <summary>True while CV frames (not keyboard) are driving the player.</summary>
        public bool CvActive => cvOwnsMovement;

        /// <summary>Most recent body gesture from the backend.</summary>
        public string CurrentGesture => currentGesture;

        public void Initialize(
            KiForgeEventBus bus,
            Transform player,
            FighterAnimationController playerFighter,
            PlayerWalkController walk,
            PlayerAttackInput attack,
            MyoChargeInput myoChargeInput,
            float left,
            float right)
        {
            eventBus        = bus;
            playerTransform = player;
            fighter         = playerFighter;
            walkController  = walk;
            attackInput     = attack;
            myo             = myoChargeInput;
            leftBound       = left;
            rightBound      = right;

            eventBus.PoseReceived += OnPose;
        }

        private void OnDestroy()
        {
            if (eventBus != null)
                eventBus.PoseReceived -= OnPose;
        }

        // ------------------------------------------------------------------ //
        // Backend event handler (called from Unity's main thread via the queue)
        // ------------------------------------------------------------------ //

        private void OnPose(PoseEvent pose)
        {
            if (pose.confidence < confidenceThreshold)
                return;

            var g = string.IsNullOrEmpty(pose.gesture) ? "none" : pose.gesture;
            if (debugLog && g != currentGesture)
                Debug.Log($"[CV] gesture {currentGesture} → {g}  (conf {pose.confidence:0.00})");
            currentGesture = g;
            lastPoseTime   = Time.time;
        }

        // ------------------------------------------------------------------ //
        // Per-frame logic
        // ------------------------------------------------------------------ //

        private void Update()
        {
            bool fresh = Time.time - lastPoseTime <= poseTimeout;
            SetCvOwnership(fresh);
            if (!cvOwnsMovement) return;

            HandlePunches();
            HandleGuard();
            HandleWalk();
        }

        private void HandlePunches()
        {
            // While MYO is charging, let it own the attack. It fires on fist release.
            bool myoCharging = myo != null && myo.CurrentHoldSeconds > 0.1f;

            bool isRight = currentGesture == "right_punch";
            bool isLeft  = currentGesture == "left_punch";
            bool isHeavy = currentGesture == "heavy_punch";

            if (!myoCharging)
            {
                if (isHeavy && !heavyPunchFired)
                {
                    heavyPunchFired = true;
                    Log("BOTH ARMS → HeavyPunch");
                    attackInput?.ThrowExternal(AttackType.HeavyPunch);
                }
                else if (isRight && !rightPunchFired && !isHeavy)
                {
                    rightPunchFired = true;
                    Log("RIGHT ARM → PunchRight");
                    attackInput?.ThrowExternal(AttackType.PunchRight);
                }
                else if (isLeft && !leftPunchFired && !isHeavy)
                {
                    leftPunchFired = true;
                    Log("LEFT ARM  → PunchLeft");
                    attackInput?.ThrowExternal(AttackType.PunchLeft);
                }
            }

            // Reset edge triggers once the arm comes back to neutral
            if (!isRight && !isHeavy) rightPunchFired  = false;
            if (!isLeft  && !isHeavy) leftPunchFired   = false;
            if (!isHeavy)             heavyPunchFired   = false;
        }

        private void HandleGuard()
        {
            bool wantsGuard = currentGesture == "guard";

            if (wantsGuard && !guardActive)
            {
                guardActive = true;
                Log("HANDS UP → guard on");
                fighter?.StartBlock(false);
            }
            else if (!wantsGuard && guardActive)
            {
                guardActive = false;
                Log("HANDS DOWN → guard off");
                fighter?.StopBlock();
            }
        }

        private void HandleWalk()
        {
            if (guardActive) return;

            if (currentGesture == "walk_right")
                Walk(+1f);
            else if (currentGesture == "walk_left")
                Walk(-1f);
            else if (fighter != null && !fighter.IsAttacking)
                fighter.StopLocomotion();
        }

        private void Walk(float worldDirX)
        {
            if (playerTransform == null || fighter == null || fighter.IsAttacking)
                return;

            float curX = playerTransform.position.x;
            float newX = Mathf.Clamp(
                curX + worldDirX * moveSpeed * Time.deltaTime,
                leftBound, rightBound);

            var newPos = new Vector3(newX, playerTransform.position.y, playerTransform.position.z);
            playerTransform.position = newPos;
            fighter.SetHome(newPos);
            fighter.SetLocomotion(worldDirX);
        }

        private void SetCvOwnership(bool fresh)
        {
            if (fresh == cvOwnsMovement) return;
            cvOwnsMovement = fresh;
            Log($"CV ownership → {(fresh ? "CV (body tracker)" : "keyboard")}");

            if (walkController != null)
                walkController.enabled = !fresh;

            if (!fresh)
            {
                rightPunchFired = leftPunchFired = heavyPunchFired = false;
                if (guardActive) { guardActive = false; fighter?.StopBlock(); }
                fighter?.StopLocomotion();
            }
        }

        private void Log(string msg)
        {
            if (debugLog) Debug.Log($"[CV] {msg}");
        }
    }
}
