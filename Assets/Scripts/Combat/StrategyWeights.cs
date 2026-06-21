using System;

namespace KiForge.Combat
{
    [Serializable]
    public sealed class StrategyWeights
    {
        public float pressure = 0.25f;
        public float dodge = 0.25f;
        public float jab = 0.25f;
        public float guard = 0.25f;
        public float heavyCounter = 0f;

        public void AdaptForStyle(string playerStyle)
        {
            switch (playerStyle)
            {
                case "heavy_puncher":
                    pressure = 0.65f;
                    dodge = 0.20f;
                    jab = 0.05f;
                    guard = 0.10f;
                    heavyCounter = 0f;
                    break;
                case "guard_turtle":
                    pressure = 0.20f;
                    dodge = 0.15f;
                    jab = 0.10f;
                    guard = 0f;
                    heavyCounter = 0.55f;
                    break;
                case "combo_puncher":
                    pressure = 0.15f;
                    dodge = 0.45f;
                    jab = 0.30f;
                    guard = 0.10f;
                    heavyCounter = 0f;
                    break;
                default:
                    pressure = 0.25f;
                    dodge = 0.25f;
                    jab = 0.25f;
                    guard = 0.25f;
                    heavyCounter = 0f;
                    break;
            }
        }
    }
}
