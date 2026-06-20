using System;

namespace KiForge.Combat
{
    [Serializable]
    public sealed class StrategyWeights
    {
        public float rush = 0.25f;
        public float dodge = 0.25f;
        public float projectile = 0.25f;
        public float block = 0.25f;
        public float unblockable = 0f;

        public void AdaptForStyle(string playerStyle)
        {
            switch (playerStyle)
            {
                case "patient_charger":
                    rush = 0.65f;
                    dodge = 0.20f;
                    projectile = 0.05f;
                    block = 0.10f;
                    unblockable = 0f;
                    break;
                case "shield_turtle":
                    rush = 0.20f;
                    dodge = 0.15f;
                    projectile = 0.10f;
                    block = 0f;
                    unblockable = 0.55f;
                    break;
                case "slash_spammer":
                    rush = 0.15f;
                    dodge = 0.45f;
                    projectile = 0.30f;
                    block = 0.10f;
                    unblockable = 0f;
                    break;
                default:
                    rush = 0.25f;
                    dodge = 0.25f;
                    projectile = 0.25f;
                    block = 0.25f;
                    unblockable = 0f;
                    break;
            }
        }
    }
}
