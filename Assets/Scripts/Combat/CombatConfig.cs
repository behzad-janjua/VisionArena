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
        public int levelOneDamage = 9;
        public int levelTwoDamage = 18;
        public int levelThreeDamage = 23;
        public int levelFourDamage = 34;
        public int quickPunchDamage = 14;
        public int veryHeavyPunchDamage = 45;
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
