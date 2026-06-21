using System;
using UnityEngine;

namespace KiForge.Combat
{
    public sealed class PlayerCombatController : MonoBehaviour
    {
        [Tooltip("Seconds after a block ends during which the player stays immune to damage.")]
        [SerializeField] private float guardGraceSeconds = 0.2f;

        public int Health { get; private set; }
        public int MaxHealth { get; private set; }
        public bool IsDefeated => Health <= 0;
        public bool Guarding { get; private set; }

        // Set far in the past so the grace window is not active before the first block.
        private float guardReleaseTime = -999f;

        // Immune while actively guarding, and for guardGraceSeconds after the block ends.
        public bool IsBlocking => Guarding || Time.time - guardReleaseTime <= guardGraceSeconds;

        public event Action<int> Damaged;
        public event Action Defeated;

public void Initialize(int maxHealth)
        {
            Initialize(maxHealth, maxHealth);
        }

        public void Initialize(int maxHealth, int startingHealth)
        {
            MaxHealth = Mathf.Max(1, maxHealth);
            Health = Mathf.Clamp(startingHealth, 0, MaxHealth);
        }

        public void SetGuarding(bool value)
        {
            // Start the grace window the moment a block ends.
            if (Guarding && !value)
            {
                guardReleaseTime = Time.time;
            }

            Guarding = value;
        }

public int ApplyDamage(int damage)
        {
            if (Health <= 0)
            {
                return 0;
            }

            // Blocking grants full immunity during the block and for a short grace
            // window afterward, so a well-timed block fully negates an incoming attack.
            if (IsBlocking)
            {
                return 0;
            }

            var applied = Mathf.Max(0, damage);
            Health = Mathf.Max(0, Health - applied);

            if (applied > 0)
            {
                Damaged?.Invoke(applied);
            }

            if (Health <= 0)
            {
                Defeated?.Invoke();
            }

            return applied;
        }
    }
}
