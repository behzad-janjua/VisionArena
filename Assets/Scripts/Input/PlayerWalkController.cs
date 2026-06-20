using KiForge.Animation;
using UnityEngine;

namespace KiForge.Input
{
    [RequireComponent(typeof(FighterAnimationController))]
    public sealed class PlayerWalkController : MonoBehaviour
    {
        private FighterAnimationController fighter;
        private float walkSpeed;
        private float leftBound;
        private float rightBound;
        private float groundY;

        private bool useArrows;

        public void Initialize(float speed, float left, float right, bool arrowKeys = false)
        {
            fighter    = GetComponent<FighterAnimationController>();
            walkSpeed  = speed;
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

            if (Mathf.Abs(h) < 0.01f) return;

            float newX  = Mathf.Clamp(transform.position.x + h * walkSpeed * Time.deltaTime, leftBound, rightBound);
            var   newPos = new Vector3(newX, groundY, transform.position.z);
            fighter.SetHome(newPos);
            transform.position = newPos;
        }
    }
}
