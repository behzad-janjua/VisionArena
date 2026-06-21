using KiForge.Animation;
using UnityEngine;

namespace KiForge.Input
{
    [RequireComponent(typeof(FighterAnimationController))]
    public sealed class PlayerWalkController : MonoBehaviour
    {
        private FighterAnimationController fighter;
        private float walkSpeed;
        private float backwardSpeedMultiplier = 0.55f;
        private float leftBound;
        private float rightBound;
        private float groundY;

        private bool useArrows;

        public void Initialize(float speed, float left, float right, bool arrowKeys = false, float backwardMultiplier = 0.55f)
        {
            fighter    = GetComponent<FighterAnimationController>();
            walkSpeed  = speed;
            backwardSpeedMultiplier = backwardMultiplier;
            leftBound  = left;
            rightBound = right;
            groundY    = transform.position.y;
            useArrows  = arrowKeys;
        }

        private void Update()
        {
            if (fighter == null || fighter.IsAttacking) return;

            float h = 0f;
            if (useArrows)
            {
                if (UnityEngine.Input.GetKey(KeyCode.LeftArrow))  h = -1f;
                if (UnityEngine.Input.GetKey(KeyCode.RightArrow)) h =  1f;
            }
            else
            {
                if (UnityEngine.Input.GetKey(KeyCode.A)) h = -1f;
                if (UnityEngine.Input.GetKey(KeyCode.D)) h =  1f;
            }

            if (Mathf.Abs(h) < 0.01f)
            {
                fighter.StopLocomotion();
                return;
            }

            // Moving away from the opponent (backpedalling) is slower than advancing,
            // matching the fighting-game feel and the backward-walk animation.
            bool movingBackward = Mathf.Sign(h) != fighter.ForwardSign;
            float speed = movingBackward ? walkSpeed * backwardSpeedMultiplier : walkSpeed;

            float newX  = Mathf.Clamp(transform.position.x + h * speed * Time.deltaTime, leftBound, rightBound);
            var   newPos = new Vector3(newX, groundY, transform.position.z);
            fighter.SetHome(newPos);
            transform.position = newPos;
            fighter.SetLocomotion(h);
        }
    }
}
