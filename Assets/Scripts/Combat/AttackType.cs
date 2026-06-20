namespace KiForge.Combat
{
    /// <summary>
    /// Every melee action a fighter can perform. The animator state name and the
    /// damage value for each are resolved through <see cref="AttackTuning"/>.
    /// </summary>
    public enum AttackType
    {
        PunchLeft,
        PunchRight,
        KickLeft,
        KickRight,
        HeavyPunch,
        VeryHeavyPunch,
        Ultimate
    }

    /// <summary>
    /// Single source of truth for combat balance. Damage values come straight from
    /// the agreed table: 120 max HP -> 12 plain punches to KO, heavier moves hit harder.
    /// Blocking multiplies incoming damage by <see cref="BlockMultiplier"/>.
    /// </summary>
    public static class AttackTuning
    {
        public const int MaxHealth = 120;
        public const float BlockMultiplier = 0.3f;

        public static int Damage(AttackType type)
        {
            switch (type)
            {
                case AttackType.PunchLeft:
                case AttackType.PunchRight:
                    return 10;
                case AttackType.KickLeft:
                case AttackType.KickRight:
                    return 15;
                case AttackType.HeavyPunch:
                    return 20;
                case AttackType.VeryHeavyPunch:
                    return 30;
                case AttackType.Ultimate:
                    return 40;
                default:
                    return 10;
            }
        }

        /// <summary>Animator state that visualises the attack (some share a clip).</summary>
        public static string StateName(AttackType type)
        {
            switch (type)
            {
                case AttackType.PunchLeft:      return "PunchLeft";
                case AttackType.PunchRight:     return "PunchRight";
                case AttackType.KickLeft:       return "KickLeft";
                case AttackType.KickRight:      return "KickRight";
                case AttackType.HeavyPunch:     return "HeavyPunch";
                case AttackType.VeryHeavyPunch: return "HeavyPunch"; // reuses heavy clip, more damage
                case AttackType.Ultimate:       return "Ultimate";
                default:                        return "PunchRight";
            }
        }

        /// <summary>Seconds from animation start until the strike lands.</summary>
        public static float ImpactDelay(AttackType type)
        {
            switch (type)
            {
                case AttackType.KickLeft:
                case AttackType.KickRight:
                    return 0.55f;
                case AttackType.HeavyPunch:
                case AttackType.VeryHeavyPunch:
                    return 0.6f;
                case AttackType.Ultimate:
                    return 0.9f;
                default:
                    return 0.32f;
            }
        }
    }
}
