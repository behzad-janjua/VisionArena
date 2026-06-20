using KiForge.Effects;
using KiForge.Shared;
using KiForge.Telemetry;
using KiForge.UI;
using UnityEngine;

namespace KiForge.Combat
{
    public sealed class ArenaGameController : MonoBehaviour
    {
        [SerializeField] private CombatConfig config = new CombatConfig();
        [SerializeField] private PlayerCombatController player;
        [SerializeField] private BossCombatController boss;
        [SerializeField] private ArenaEffectsController effects;
        [SerializeField] private KiForgeHudController hud;
        [SerializeField] private MatchTelemetryRecorder telemetry;
        [SerializeField] private int bossStartingHealthForDeathTest = 1;

        private KiForgeEventBus eventBus;
        private int round = 1;
        private float currentCharge;
        private Vector2 latestAim = Vector2.right;

        public CombatConfig Config => config;
        public StrategyWeights StrategyWeights { get; } = new StrategyWeights();

        public void Initialize(KiForgeEventBus bus, PlayerCombatController playerController, BossCombatController bossController, ArenaEffectsController effectsController, KiForgeHudController hudController, MatchTelemetryRecorder telemetryRecorder)
        {
            eventBus = bus;
            player = playerController;
            boss = bossController;
            effects = effectsController;
            hud = hudController;
            telemetry = telemetryRecorder;

            player.Initialize(config.playerMaxHealth);
            boss.Initialize(config.bossMaxHealth, bossStartingHealthForDeathTest);

            eventBus.GestureReceived += OnGesture;
            eventBus.PoseReceived += OnPose;
            eventBus.AgentResponseReceived += OnAgentResponse;

            hud.Bind(player, boss);
            hud.SetFightLab("Player Style: unknown\nBoss Strategy: balanced\nCounter Success: --");
        }

        private void OnDestroy()
        {
            if (eventBus == null)
            {
                return;
            }

            eventBus.GestureReceived -= OnGesture;
            eventBus.PoseReceived -= OnPose;
            eventBus.AgentResponseReceived -= OnAgentResponse;
        }

        private void OnPose(PoseEvent pose)
        {
            latestAim = pose.aim.sqrMagnitude > 0.001f ? pose.aim.normalized : latestAim;
            hud.SetAimReticle(pose.wrist + latestAim * 2f);
        }

        private void OnGesture(GestureEvent gesture)
        {
            switch (gesture.type)
            {
                case GestureEventType.ChargeStart:
                    currentCharge = 0f;
                    effects.ShowAura(true);
                    hud.SetCharge(0f);
                    break;
                case GestureEventType.ChargeUpdate:
                    currentCharge = gesture.charge;
                    effects.SetAuraPower(gesture.charge);
                    hud.SetCharge(gesture.charge);
                    break;
                case GestureEventType.BlastRelease:
                    ResolveBlast(gesture);
                    break;
                case GestureEventType.ShieldStart:
                    player.SetShielding(true);
                    effects.ShowShield(true);
                    PublishTelemetry(PlayerActionType.Shield, 0f, 0, "shielded");
                    break;
                case GestureEventType.ShieldEnd:
                    player.SetShielding(false);
                    effects.ShowShield(false);
                    break;
                case GestureEventType.SlashLeft:
                    ResolveSlash(PlayerActionType.SlashLeft, gesture.origin, Vector2.left);
                    break;
                case GestureEventType.SlashRight:
                    ResolveSlash(PlayerActionType.SlashRight, gesture.origin, Vector2.right);
                    break;
                case GestureEventType.Ultimate:
                    ResolveUltimate(gesture);
                    break;
            }
        }

        private void ResolveBlast(GestureEvent gesture)
        {
            effects.ShowAura(false);
            hud.SetCharge(0f);

            var aim = gesture.aim.sqrMagnitude > 0.001f ? gesture.aim.normalized : latestAim;
            var accuracy = Mathf.Lerp(0.65f, 1.15f, Mathf.Clamp01(Vector2.Dot(aim, Vector2.right) * 0.5f + 0.5f));
            var damage = config.DamageForCharge(gesture.holdSeconds, accuracy);
            boss.ApplyDamage(damage);
            effects.FireBeam(gesture.origin, gesture.origin + aim * config.beamRange, config.ChargeLevel(gesture.holdSeconds));
            PublishTelemetry(PlayerActionType.ChargedBlast, gesture.holdSeconds, damage, boss.IsDefeated ? "boss_ko" : "boss_hit");
        }

        private void ResolveSlash(PlayerActionType action, Vector2 origin, Vector2 direction)
        {
            boss.ApplyDamage(config.slashDamage);
            effects.ShowSlash(origin, direction);
            PublishTelemetry(action, 0f, config.slashDamage, boss.IsDefeated ? "boss_ko" : "slash_hit");
        }

        private void ResolveUltimate(GestureEvent gesture)
        {
            boss.ApplyDamage(config.ultimateDamage);
            effects.FireBeam(gesture.origin, gesture.origin + gesture.aim.normalized * config.beamRange, 4);
            effects.ScreenShake();
            PublishTelemetry(PlayerActionType.Ultimate, gesture.holdSeconds, config.ultimateDamage, boss.IsDefeated ? "boss_ko" : "ultimate_hit");
        }

        private void PublishTelemetry(PlayerActionType action, float chargeTime, int damage, string outcome)
        {
            var evt = new CombatTelemetryEvent
            {
                round = round,
                playerAction = action,
                chargeTime = chargeTime,
                accuracy = currentCharge,
                damageDealtByPlayer = damage,
                damageDealtByBoss = 0,
                bossAction = "observe",
                bossHealthAfter = boss.Health,
                playerHealthAfter = player.Health,
                outcome = outcome
            };

            telemetry.Record(evt);
            eventBus.PublishCombatTelemetry(evt);
            hud.Refresh();
        }

        private void OnAgentResponse(AgentResponseEvent response)
        {
            if (!string.IsNullOrEmpty(response.nextStrategy))
            {
                StrategyWeights.AdaptForStyle(response.nextStrategy);
            }

            hud.SetNarration(response.moveName, response.narration);
            hud.SetFightLab($"Player Style: {response.nextStrategy}\nBoss Counter Success: {response.counterSuccess:P0}\nBoss Action: {response.bossAction}");
        }
    }
}
