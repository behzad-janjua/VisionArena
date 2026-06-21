using System;
using UnityEngine;

namespace KiForge.Combat
{
    public sealed class PlayerCombatController : MonoBehaviour
    {
        [Tooltip("Seconds after a block ends during which the player stays immune to damage.")]
        [SerializeField] private float shieldGraceSeconds = 0.2f;

        public int Health { get; private set; }
        public int MaxHealth { get; private set; }
        public bool IsDefeated => Health <= 0;
        public bool Shielding { get; private set; }

        // Set far in the past so the grace window is not active before the first block.
        private float shieldReleaseTime = -999f;

        // Immune while actively shielding, and for shieldGraceSeconds after the block ends.
        public bool IsBlocking => Shielding || Time.time - shieldReleaseTime <= shieldGraceSeconds;

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

        public void SetShielding(bool value)
        {
            // Start the grace window the moment a block ends.
            if (Shielding && !value)
            {
                shieldReleaseTime = Time.time;
            }

            Shielding = value;
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
