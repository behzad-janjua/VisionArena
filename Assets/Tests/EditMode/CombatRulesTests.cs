using KiForge.Combat;
using NUnit.Framework;

namespace KiForge.Tests
{
    public sealed class CombatRulesTests
    {
        [Test]
        public void ChargeLevelMatchesHackathonTimingWindows()
        {
            var config = new CombatConfig();

            Assert.AreEqual(0, config.ChargeLevel(0.1f));
            Assert.AreEqual(1, config.ChargeLevel(0.5f));
            Assert.AreEqual(2, config.ChargeLevel(1.4f));
            Assert.AreEqual(3, config.ChargeLevel(2.4f));
            Assert.AreEqual(4, config.ChargeLevel(3.8f));
        }

        [Test]
        public void ChargeDamageScalesWithAccuracy()
        {
            var config = new CombatConfig();

            var weak = config.DamageForCharge(2.4f, 0.5f);
            var strong = config.DamageForCharge(2.4f, 1.2f);

            Assert.Greater(strong, weak);
        }

        [Test]
        public void StrategyWeightsAdaptToPatientCharger()
        {
            var weights = new StrategyWeights();

            weights.AdaptForStyle("patient_charger");

            Assert.Greater(weights.rush, weights.dodge);
            Assert.Less(weights.projectile, 0.1f);
        }

        [Test]
        public void BossDamageCannotDropHealthBelowZero()
        {
            var boss = new UnityEngine.GameObject("Boss").AddComponent<BossCombatController>();
            boss.Initialize(50);

            boss.ApplyDamage(999);

            Assert.AreEqual(0, boss.Health);
        }
    }
}
