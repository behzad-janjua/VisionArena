using System;
using UnityEngine;

namespace KiForge.Combat
{
    [Serializable]
    public sealed class CombatConfig
    {
        public int playerMaxHealth = 100;
        public int bossMaxHealth = 100;
        public float maxChargeSeconds = 3.5f;
        public int levelOneDamage = 12;
        public int levelTwoDamage = 24;
        public int levelThreeDamage = 42;
        public int levelFourDamage = 65;
        public int quickPunchDamage = 18;
        public int veryHeavyPunchDamage = 88;
        public float punchReach = 9f;

        public int ChargeLevel(float holdSeconds)
        {
            if (holdSeconds >= 3.5f)
            {
                return 4;
            }

            if (holdSeconds >= 2f)
            {
                return 3;
            }

            if (holdSeconds >= 1f)
            {
                return 2;
            }

            return holdSeconds >= 0.3f ? 1 : 0;
        }

        public int DamageForCharge(float holdSeconds, float accuracy = 1f)
        {
            var level = ChargeLevel(holdSeconds);
            var baseDamage = 0;
            if (level == 4)
            {
                baseDamage = levelFourDamage;
            }
            else if (level == 3)
            {
                baseDamage = levelThreeDamage;
            }
            else if (level == 2)
            {
                baseDamage = levelTwoDamage;
            }
            else if (level == 1)
            {
                baseDamage = levelOneDamage;
            }

            return Mathf.RoundToInt(baseDamage * Mathf.Clamp(accuracy, 0.25f, 1.35f));
        }
    }
}
