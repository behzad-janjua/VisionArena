using System;
using System.Collections;
using KiForge.Combat;
using UnityEngine;

namespace KiForge.Animation
{
    public sealed class FighterAnimationController : MonoBehaviour
    {
        [SerializeField] private string idleStateName = "Idle";
        [SerializeField] private string walkStateName = "Walking";
        [SerializeField] private string walkBackStateName = "WalkingBackwards";
        [SerializeField] private string punchStateName = "Punching";
        [SerializeField] private string dyingStateName = "Dying";
        [SerializeField] private string gettingHitStateName = "GettingHit";
        [SerializeField] private float punchInterval = 2.2f;
        [SerializeField] private float punchTravelDistance = 0.55f;
        [SerializeField] private float impactDelay = 0.38f;
        [SerializeField] private bool autoPunch;
        [SerializeField] private float painFlashSeconds = 0.16f;
        [SerializeField] private float hitReactionSeconds = 0.45f;
        [SerializeField] private float idleCrossFade = 0.15f;
        [SerializeField] private float attackRange = 5.0f;

        private Animator animator;
        private Transform opponent;
        private Vector3 homePosition;
        private float groundY;
        private bool groundYInitialized;
        private Coroutine loop;
        private Coroutine activePunchMotion;
        private Coroutine painRoutine;
        private Coroutine deathRoutine;
        private bool defeated;
        private int locomotion; // 0 = idle, 1 = walking toward opponent, -1 = backing away

        public event Action<FighterAnimationController, Transform> Impact;

        public bool IsAttacking => activePunchMotion != null;
        public bool IsBlocking { get; private set; }
        public bool IsDefeated => defeated;

        /// <summary>The attack carried by the most recent <see cref="Impact"/> event.</summary>
        public AttackType LastAttack { get; private set; }

        public void SetHome(Vector3 pos)
        {
            homePosition = pos;
        }

        private void LateUpdate()
        {
            // Keep the fighter pinned to its ground plane against any residual drift
            // (root motion, coroutine races). The death fall is the only time Y is
            // allowed to change, so skip the lock once defeated.
            if (!groundYInitialized || defeated)
            {
                return;
            }

            var p = transform.position;
            if (!Mathf.Approximately(p.y, groundY))
            {
                transform.position = new Vector3(p.x, groundY, p.z);
            }
        }

        public void Initialize(Transform target, bool startsAutomatically, float intervalOffset = 0f)
        {
            opponent = target;
            autoPunch = startsAutomatically;
            animator = GetComponentInChildren<Animator>();
            homePosition = transform.position;
            groundY = transform.position.y;
            groundYInitialized = true;

            // Fighters live on a fixed-height plane; all motion is driven by explicit
            // transform writes. Root motion (e.g. a vertical component in GettingHit /
            // attack clips) would fight those writes and leave the fighter's Y drifted.
            if (animator != null)
            {
                animator.applyRootMotion = false;

                // Start resting in the idle stance rather than a frozen attack pose.
                if (HasState(idleStateName))
                {
                    animator.Play(idleStateName, 0, 0f);
                }
            }

            FaceOpponent();

            if (autoPunch)
            {
                loop = StartCoroutine(PunchLoop(intervalOffset));
            }
        }

        /// <summary>Backwards-compatible quick punch (used by the auto-punch loop).</summary>
        public void PlayPunch()
        {
            Attack(AttackType.PunchRight);
        }

        /// <summary>Perform a typed attack. Damage is resolved by listeners via <see cref="LastAttack"/>.</summary>
        public void Attack(AttackType type)
        {
            if (defeated)
            {
                return;
            }

            IsBlocking = false;
            locomotion = 0;
            FaceOpponent();

            if (animator != null)
            {
                animator.CrossFadeInFixedTime(AttackTuning.StateName(type), 0.08f);
            }

            if (activePunchMotion != null)
            {
                StopCoroutine(activePunchMotion);
                transform.position = homePosition;
            }

            activePunchMotion = StartCoroutine(AttackMotion(type));
        }

        public void StartBlock(bool left)
        {
            if (defeated)
            {
                return;
            }

            IsBlocking = true;
            locomotion = 0;
            FaceOpponent();

            if (activePunchMotion != null)
            {
                StopCoroutine(activePunchMotion);
                activePunchMotion = null;
                transform.position = homePosition;
            }

            if (animator != null)
            {
                animator.CrossFadeInFixedTime(left ? "LeftBlock" : "RightBlock", 0.1f);
            }
        }

        public void StopBlock()
        {
            if (!IsBlocking)
            {
                return;
            }

            IsBlocking = false;
            ReturnToIdle();
        }

        /// <summary>Cross-fade back to the resting idle stance, unless busy or defeated.</summary>
        private void ReturnToIdle()
        {
            if (defeated || IsBlocking || IsAttacking || animator == null)
            {
                return;
            }

            var target = HasState(idleStateName) ? idleStateName : punchStateName;
            animator.CrossFadeInFixedTime(target, idleCrossFade);
        }

        /// <summary>
        /// Drive the walk animation from a world-space horizontal move direction
        /// (negative = moving -X, positive = moving +X). Walking toward the opponent
        /// plays the forward walk; moving away plays the backward walk. Attacks,
        /// blocks, pain and death take priority and suppress locomotion.
        /// </summary>
        public void SetLocomotion(float worldDirX)
        {
            if (defeated || IsAttacking || IsBlocking || animator == null)
            {
                return;
            }

            if (Mathf.Abs(worldDirX) < 0.01f)
            {
                StopLocomotion();
                return;
            }

            var facingRight = opponent == null || opponent.position.x > transform.position.x;
            var forwardSign = facingRight ? 1f : -1f;
            var dir = Mathf.Sign(worldDirX) == forwardSign ? 1 : -1;

            if (dir == locomotion)
            {
                return;
            }

            locomotion = dir;
            var state = dir == 1 ? walkStateName : walkBackStateName;
            if (HasState(state))
            {
                animator.CrossFadeInFixedTime(state, idleCrossFade);
            }
        }

        /// <summary>Stop walking and settle back into the idle stance.</summary>
        public void StopLocomotion()
        {
            if (locomotion == 0)
            {
                return;
            }

            locomotion = 0;
            ReturnToIdle();
        }

        public void StopLoop()
        {
            if (loop != null)
            {
                StopCoroutine(loop);
                loop = null;
            }
        }

        public void PlayPain()
        {
            if (defeated)
            {
                return;
            }

            // Blocking absorbs the hit: keep the guard pose, just a small flash.
            if (!IsBlocking && animator != null && HasState(gettingHitStateName))
            {
                locomotion = 0;
                animator.CrossFadeInFixedTime(gettingHitStateName, 0.06f);
            }

            if (painRoutine != null)
            {
                StopCoroutine(painRoutine);
            }

            painRoutine = StartCoroutine(PainFlash());
        }

        public void PlayDying()
        {
            if (defeated)
            {
                return;
            }

            defeated = true;
            IsBlocking = false;
            StopLoop();

            if (activePunchMotion != null)
            {
                StopCoroutine(activePunchMotion);
                activePunchMotion = null;
                transform.position = homePosition;
            }

            if (painRoutine != null)
            {
                StopCoroutine(painRoutine);
                painRoutine = null;
            }

            var hasDyingAnimation = animator != null && HasState(dyingStateName);
            if (hasDyingAnimation)
            {
                animator.CrossFadeInFixedTime(dyingStateName, 0.08f);
            }

            if (deathRoutine != null)
            {
                StopCoroutine(deathRoutine);
            }

            if (!hasDyingAnimation)
            {
                deathRoutine = StartCoroutine(DeathFallback());
            }
        }

        private bool HasState(string stateName)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return false;
            }

            var stateHash = Animator.StringToHash(stateName);
            return animator.HasState(0, stateHash);
        }

        private IEnumerator PainFlash()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            var originalColors = new Color[renderers.Length];

            for (var i = 0; i < renderers.Length; i++)
            {
                var material = renderers[i].material;
                originalColors[i] = material.HasProperty("_Color") ? material.color : Color.white;
                if (material.HasProperty("_Color"))
                {
                    material.color = Color.Lerp(originalColors[i], Color.red, 0.75f);
                }
            }

            var recoilStart = transform.position;
            var fromOpponent = opponent == null ? -transform.right : transform.position - opponent.position;
            fromOpponent.y = 0f;
            var away = fromOpponent == Vector3.zero ? -transform.right : fromOpponent.normalized;
            // Blocked hits barely move you; clean hits recoil more.
            transform.position = recoilStart + away * (IsBlocking ? 0.05f : 0.16f);
            yield return new WaitForSeconds(painFlashSeconds);

            transform.position = recoilStart;
            for (var i = 0; i < renderers.Length; i++)
            {
                var material = renderers[i].material;
                if (material.HasProperty("_Color"))
                {
                    material.color = originalColors[i];
                }
            }

            // Let the getting-hit reaction read, then settle back into idle.
            var remainder = hitReactionSeconds - painFlashSeconds;
            if (remainder > 0f)
            {
                yield return new WaitForSeconds(remainder);
            }

            ReturnToIdle();
            painRoutine = null;
        }

        private IEnumerator DeathFallback()
        {
            var startRotation = transform.rotation;
            var fallDirection = opponent == null || transform.position.x < opponent.position.x ? 1f : -1f;
            var endRotation = startRotation * Quaternion.Euler(0f, 0f, fallDirection * 88f);
            var startPosition = transform.position;
            var endPosition = startPosition + Vector3.down * 0.42f;
            var timer = 0f;
            const float duration = 0.75f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, timer / duration);
                transform.rotation = Quaternion.Slerp(startRotation, endRotation, t);
                transform.position = Vector3.Lerp(startPosition, endPosition, t);
                yield return null;
            }

            transform.rotation = endRotation;
            transform.position = endPosition;
        }

        private IEnumerator PunchLoop(float intervalOffset)
        {
            if (intervalOffset > 0f)
                yield return new WaitForSeconds(intervalOffset);

            while (enabled)
            {
                var inRange = opponent == null ||
                              Vector3.Distance(homePosition, opponent.position) <= attackRange;
                if (inRange) PlayPunch();
                yield return new WaitForSeconds(punchInterval);
            }
        }

        private IEnumerator AttackMotion(AttackType type)
        {
            var delay = AttackTuning.ImpactDelay(type);
            var reach = type == AttackType.KickLeft || type == AttackType.KickRight
                ? punchTravelDistance * 1.25f
                : punchTravelDistance;

            var start = homePosition;
            var toOpponent = opponent == null ? transform.right : opponent.position - transform.position;
            toOpponent.y = 0f;
            var direction = toOpponent == Vector3.zero ? transform.right : toOpponent.normalized;
            var lunge = start + direction * reach;

            var timer = 0f;
            while (timer < delay)
            {
                timer += Time.deltaTime;
                transform.position = Vector3.Lerp(start, lunge, timer / delay);
                yield return null;
            }

            LastAttack = type;
            Impact?.Invoke(this, opponent);

            timer = 0f;
            while (timer < delay)
            {
                timer += Time.deltaTime;
                // Return to current homePosition so walking during a punch lands at the right spot
                transform.position = Vector3.Lerp(lunge, homePosition, timer / delay);
                yield return null;
            }

            transform.position = homePosition;
            activePunchMotion = null;

            // Settle back into the idle stance instead of holding the attack pose.
            ReturnToIdle();
        }

        private void FaceOpponent()
        {
            if (opponent == null)
            {
                return;
            }

            var facingRight = opponent.position.x > transform.position.x;
            transform.rotation = Quaternion.Euler(0f, facingRight ? 90f : -90f, 0f);
        }
    }
}
