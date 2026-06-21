using KiForge.Effects;
using KiForge.Shared;
using KiForge.Telemetry;
using UnityEngine;

namespace KiForge.Combat
{
    public sealed class ArenaGameController : MonoBehaviour
    {
        [SerializeField] private CombatConfig config = new CombatConfig();
        [SerializeField] private PlayerCombatController player;
        [SerializeField] private BossCombatController boss;
        [SerializeField] private ArenaEffectsController effects;
        [SerializeField] private MatchTelemetryRecorder telemetry;
        [SerializeField] private int bossStartingHealthForDeathTest = 1;

        private KiForgeEventBus eventBus;
        private int round = 1;
        private float currentCharge;
        private Vector2 latestAim = Vector2.right;
        private Vector2 latestCvWrist;
        private bool hasCvWrist;

        public CombatConfig Config => config;
        public StrategyWeights StrategyWeights { get; } = new StrategyWeights();

        public void Initialize(KiForgeEventBus bus, PlayerCombatController playerController, BossCombatController bossController, ArenaEffectsController effectsController, MatchTelemetryRecorder telemetryRecorder)
        {
            eventBus = bus;
            player = playerController;
            boss = bossController;
            effects = effectsController;
            telemetry = telemetryRecorder;

            player.Initialize(config.playerMaxHealth);
            boss.Initialize(config.bossMaxHealth, bossStartingHealthForDeathTest);

            eventBus.GestureReceived += OnGesture;
            eventBus.PoseReceived += OnPose;
            eventBus.AgentResponseReceived += OnAgentResponse;
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
            if (pose.aim.sqrMagnitude > 0.001f)
                latestAim = pose.aim.normalized;

            // Wrist from CV backend arrives as normalized [0,1] screen coords.
            // Keyboard fallback publishes world-space coords (typically outside [0,1]).
            // Detect CV frames by checking both components are in unit range.
            bool isCvFrame = pose.wrist.x >= 0f && pose.wrist.x <= 1f
                             && pose.wrist.y >= 0f && pose.wrist.y <= 1f
                             && pose.confidence > 0f && pose.confidence < 1f;
            if (isCvFrame)
            {
                // Mirror and scale to match CvAimController world-space mapping
                float wx = (1f - pose.wrist.x - 0.5f) * 5f;
                float wy = (pose.wrist.y - 0.5f) * 4f + 0.5f;
                latestCvWrist = new Vector2(wx, wy);
                hasCvWrist = true;
            }
        }

        private Vector2 AttackOrigin(GestureEvent gesture) =>
            hasCvWrist ? latestCvWrist : gesture.origin;

        private void OnGesture(GestureEvent gesture)
        {
            switch (gesture.type)
            {
                case GestureEventType.ChargeStart:
                    currentCharge = 0f;
                    effects.ShowAura(true);
                    break;
                case GestureEventType.ChargeUpdate:
                    currentCharge = gesture.charge;
                    effects.SetAuraPower(gesture.charge);
                    break;
                case GestureEventType.HeavyPunchRelease:
                    ResolveHeavyPunch(gesture);
                    break;
                case GestureEventType.GuardStart:
                    player.SetGuarding(true);
                    effects.ShowGuard(true);
                    PublishTelemetry(PlayerActionType.Guard, 0f, 0, "guarded");
                    break;
                case GestureEventType.GuardEnd:
                    player.SetGuarding(false);
                    effects.ShowGuard(false);
                    break;
                case GestureEventType.LeftPunch:
                    ResolveQuickPunch(PlayerActionType.LeftPunch, gesture.origin, Vector2.left);
                    break;
                case GestureEventType.RightPunch:
                    ResolveQuickPunch(PlayerActionType.RightPunch, gesture.origin, Vector2.right);
                    break;
                case GestureEventType.VeryHeavyPunch:
                    ResolveVeryHeavyPunch(gesture);
                    break;
            }
        }

        private void ResolveHeavyPunch(GestureEvent gesture)
        {
            effects.ShowAura(false);
            var origin = AttackOrigin(gesture);
            var aim = gesture.aim.sqrMagnitude > 0.001f ? gesture.aim.normalized : latestAim;
            var accuracy = Mathf.Lerp(0.65f, 1.15f, Mathf.Clamp01(Vector2.Dot(aim, Vector2.right) * 0.5f + 0.5f));
            var damage = config.DamageForCharge(gesture.holdSeconds, accuracy);
            boss.ApplyDamage(damage);
            effects.ShowHeavyPunchTrail(origin, origin + aim * config.punchReach, config.ChargeLevel(gesture.holdSeconds));
            PublishTelemetry(PlayerActionType.HeavyPunch, gesture.holdSeconds, damage, boss.IsDefeated ? "boss_ko" : "heavy_punch_hit");
        }

        private void ResolveQuickPunch(PlayerActionType action, Vector2 origin, Vector2 direction)
        {
            var o = hasCvWrist ? latestCvWrist : origin;
            boss.ApplyDamage(config.quickPunchDamage);
            effects.ShowQuickPunchTrail(o, direction);
            PublishTelemetry(action, 0f, config.quickPunchDamage, boss.IsDefeated ? "boss_ko" : "quick_punch_hit");
        }

        private void ResolveVeryHeavyPunch(GestureEvent gesture)
        {
            var origin = AttackOrigin(gesture);
            boss.ApplyDamage(config.veryHeavyPunchDamage);
            effects.ShowHeavyPunchTrail(origin, origin + gesture.aim.normalized * config.punchReach, 4);
            effects.ScreenShake();
            PublishTelemetry(PlayerActionType.VeryHeavyPunch, gesture.holdSeconds, config.veryHeavyPunchDamage, boss.IsDefeated ? "boss_ko" : "very_heavy_punch_hit");
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
        }

        private void OnAgentResponse(AgentResponseEvent response)
        {
            if (!string.IsNullOrEmpty(response.nextStrategy))
            {
                StrategyWeights.AdaptForStyle(response.nextStrategy);
            }

        }
    }
}
