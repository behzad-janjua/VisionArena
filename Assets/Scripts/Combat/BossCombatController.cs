using System;
using UnityEngine;

namespace KiForge.Combat
{
    public sealed class BossCombatController : MonoBehaviour
    {
        public int Health { get; private set; }
        public int MaxHealth { get; private set; }
        public bool IsDefeated => Health <= 0;

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

public int ApplyDamage(int damage)
        {
            if (Health <= 0)
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
