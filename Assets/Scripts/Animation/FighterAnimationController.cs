using System;
using System.Collections;
using UnityEngine;

namespace KiForge.Animation
{
    public sealed class FighterAnimationController : MonoBehaviour
    {
        [SerializeField] private string punchStateName = "Punching";
        [SerializeField] private string dyingStateName = "Dying";
        [SerializeField] private float punchInterval = 2.2f;
        [SerializeField] private float punchTravelDistance = 0.55f;
        [SerializeField] private float impactDelay = 0.38f;
        [SerializeField] private bool autoPunch;
        [SerializeField] private float painFlashSeconds = 0.16f;
        [SerializeField] private float attackRange = 3.2f;

        private Animator animator;
        private Transform opponent;
        private Vector3 homePosition;
        private Coroutine loop;
        private Coroutine activePunchMotion;
        private Coroutine painRoutine;
        private Coroutine deathRoutine;
        private bool defeated;

        public event Action<FighterAnimationController, Transform> Impact;

        public bool IsAttacking => activePunchMotion != null;

        public void SetHome(Vector3 pos)
        {
            homePosition = pos;
        }

        public void Initialize(Transform target, bool startsAutomatically, float intervalOffset = 0f)
        {
            opponent = target;
            autoPunch = startsAutomatically;
            animator = GetComponentInChildren<Animator>();
            homePosition = transform.position;

            FaceOpponent();

            if (autoPunch)
            {
                loop = StartCoroutine(PunchLoop(intervalOffset));
            }
        }

        public void PlayPunch()
        {
            if (defeated)
            {
                return;
            }

            FaceOpponent();

            if (animator != null)
            {
                animator.CrossFadeInFixedTime(punchStateName, 0.08f);
            }

            if (activePunchMotion != null)
            {
                StopCoroutine(activePunchMotion);
                transform.position = homePosition;
            }

            activePunchMotion = StartCoroutine(PunchMotion());
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
            var away = opponent == null ? -transform.right : (transform.position - opponent.position).normalized;
            transform.position = recoilStart + away * 0.16f;
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

        private IEnumerator PunchMotion()
        {
            var start = homePosition;
            var direction = opponent == null ? transform.right : (opponent.position - transform.position).normalized;
            var lunge = start + direction * punchTravelDistance;

            var timer = 0f;
            while (timer < impactDelay)
            {
                timer += Time.deltaTime;
                transform.position = Vector3.Lerp(start, lunge, timer / impactDelay);
                yield return null;
            }

            Impact?.Invoke(this, opponent);

            timer = 0f;
            while (timer < impactDelay)
            {
                timer += Time.deltaTime;
                // Return to current homePosition so walking during a punch lands at the right spot
                transform.position = Vector3.Lerp(lunge, homePosition, timer / impactDelay);
                yield return null;
            }

            transform.position = homePosition;
            activePunchMotion = null;
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
