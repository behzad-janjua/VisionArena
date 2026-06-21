using KiForge.Animation;
using KiForge.Combat;
using KiForge.Shared;
using UnityEngine;

namespace KiForge.Input
{
    /// <summary>
    /// Test mapping for CV hand gestures (MediaPipe GestureRecognizer via the backend):
    ///
    ///   Closed fist  -> throw a punch (edge-triggered: one punch per fist-close)
    ///   Open palm    -> walk forward toward the opponent
    ///
    /// While confident gesture frames arrive, CV owns movement and the keyboard
    /// <see cref="PlayerWalkController"/> is disabled. If frames go stale (hand leaves
    /// frame, camera drops) control reverts to the keyboard so the demo never sticks.
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
        [SerializeField] private float leftBound = -4.5f;
        [SerializeField] private float rightBound = 4.5f;

        [Header("Gesture Punch")]
        [SerializeField] private AttackType punchType = AttackType.PunchRight;

        [Header("Confidence / Staleness")]
        [SerializeField] private float confidenceThreshold = 0.5f;
        [Tooltip("If no confident frame arrives within this many seconds, hand control back to keyboard")]
        [SerializeField] private float poseTimeout = 1.5f;

        [Header("Debug")]
        [Tooltip("Log gesture frames, CV ownership flips, and punch/walk actions to the Console")]
        [SerializeField] private bool debugLog = true;

        private KiForgeEventBus eventBus;
        private string currentGesture = "none";
        private float lastPoseTime = -999f;
        private bool cvOwnsMovement;
        private bool fistActive;
        private bool wasWalking;

        /// <summary>True while CV (not keyboard) is currently driving the player.</summary>
        public bool CvActive => cvOwnsMovement;

        /// <summary>Most recent recognized gesture ("fist", "open_palm", "none").</summary>
        public string CurrentGesture => currentGesture;

        public void Initialize(
            KiForgeEventBus bus,
            Transform player,
            FighterAnimationController playerFighter,
            PlayerWalkController walk,
            PlayerAttackInput attack,
            float left,
            float right)
        {
            eventBus = bus;
            playerTransform = player;
            fighter = playerFighter;
            walkController = walk;
            attackInput = attack;
            leftBound = left;
            rightBound = right;

            eventBus.PoseReceived += OnPose;
        }

        private void OnDestroy()
        {
            if (eventBus != null)
                eventBus.PoseReceived -= OnPose;
        }

        private void OnPose(PoseEvent pose)
        {
            // Synthetic frames (mock stream, no real camera) must not take control away
            // from the keyboard. Only real-camera gestures own movement.
            if (pose.mock || pose.confidence < confidenceThreshold)
                return;

            var g = string.IsNullOrEmpty(pose.gesture) ? "none" : pose.gesture;
            if (debugLog && g != currentGesture)
                Debug.Log($"[CV] gesture -> {g} (conf {pose.confidence:0.00})");
            currentGesture = g;
            lastPoseTime = Time.time;
        }

        private void Update()
        {
            bool fresh = Time.time - lastPoseTime <= poseTimeout;
            SetCvOwnership(fresh);

            if (!cvOwnsMovement)
                return;

            // Fist -> punch, edge-triggered so a held fist fires exactly once.
            if (currentGesture == "fist")
            {
                if (!fistActive)
                {
                    fistActive = true;
                    if (debugLog)
                        Debug.Log($"[CV] FIST -> punch ({punchType}); attackInput={(attackInput != null)}");
                    attackInput?.ThrowExternal(punchType);
                }
            }
            else
            {
                fistActive = false;
            }

            // Open palm -> walk forward toward the opponent.
            if (currentGesture == "open_palm")
                WalkForward();
            else if (fighter != null && !fighter.IsAttacking)
                fighter.StopLocomotion();
        }

        private void SetCvOwnership(bool fresh)
        {
            if (fresh == cvOwnsMovement)
                return;

            cvOwnsMovement = fresh;
            if (debugLog)
                Debug.Log($"[CV] ownership -> {(fresh ? "CV" : "keyboard")}");

            // CV and keyboard must not both write the player's position in the same frame.
            if (walkController != null)
                walkController.enabled = !fresh;

            if (!fresh)
            {
                fistActive = false;
                if (fighter != null)
                    fighter.StopLocomotion();
            }
        }

        private void WalkForward()
        {
            if (playerTransform == null || fighter == null || fighter.IsAttacking)
                return;

            float dir = fighter.ForwardSign; // +1 if opponent is to the right, else -1
            float curX = playerTransform.position.x;
            float newX = Mathf.Clamp(curX + dir * moveSpeed * Time.deltaTime, leftBound, rightBound);

            var newPos = new Vector3(newX, playerTransform.position.y, playerTransform.position.z);
            playerTransform.position = newPos;
            fighter.SetHome(newPos);

            if (Mathf.Abs(newX - curX) > 0.0005f)
            {
                if (debugLog && !wasWalking)
                {
                    wasWalking = true;
                    Debug.Log($"[CV] OPEN PALM -> walking, x {curX:0.00} -> {newX:0.00}");
                }
                fighter.SetLocomotion(dir);
            }
            else
            {
                wasWalking = false;
                fighter.StopLocomotion();
            }
        }
    }
}
