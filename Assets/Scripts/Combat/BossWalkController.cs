using KiForge.Animation;
using UnityEngine;

namespace KiForge.Combat
{
    [RequireComponent(typeof(FighterAnimationController))]
    public sealed class BossWalkController : MonoBehaviour
    {
        private FighterAnimationController fighter;
        private Transform playerTransform;
        private float walkSpeed;
        private float attackRange;
        private float leftBound;
        private float rightBound;
        private float groundY;

        public void Initialize(Transform player, float speed, float range, float left, float right)
        {
            fighter         = GetComponent<FighterAnimationController>();
            playerTransform = player;
            walkSpeed       = speed;
            attackRange     = range;
            leftBound       = left;
            rightBound      = right;
            groundY         = transform.position.y;
        }

        private void Update()
        {
            if (fighter == null || fighter.IsAttacking || playerTransform == null) return;

            float dist = Mathf.Abs(transform.position.x - playerTransform.position.x);
            if (dist <= attackRange)
            {
                fighter.StopLocomotion();
                return;
            }

            float dir  = Mathf.Sign(playerTransform.position.x - transform.position.x);
            float newX = Mathf.Clamp(transform.position.x + dir * walkSpeed * Time.deltaTime, leftBound, rightBound);
            var   newPos = new Vector3(newX, groundY, transform.position.z);
            fighter.SetHome(newPos);
            transform.position = newPos;
            fighter.SetLocomotion(dir);
        }
    }
}
