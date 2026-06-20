using System;
using UnityEngine;

namespace KiForge.Combat
{
    public sealed class PlayerCombatController : MonoBehaviour
    {
        public int Health { get; private set; }
        public int MaxHealth { get; private set; }
        public bool Shielding { get; private set; }

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
            Shielding = value;
        }

public int ApplyDamage(int damage)
        {
            if (Health <= 0)
            {
                return 0;
            }

            var mitigated = Shielding ? Mathf.RoundToInt(damage * 0.25f) : damage;
            var applied = Mathf.Max(0, mitigated);
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
