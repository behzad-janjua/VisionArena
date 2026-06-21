using KiForge.Animation;
using KiForge.Combat;
using KiForge.Input;
using KiForge.Scene;
using KiForge.Shared;
using KiForge.Telemetry;
using KiForge.UI;
using UnityEngine;

namespace KiForge.Bootstrap
{
    public sealed class KiForgeArenaBootstrap : MonoBehaviour
    {
        private const int MaxHealth = AttackTuning.MaxHealth; // 120 -> 12 plain punches to KO

        private int round;  // incremented each time an attack connects

        // Max horizontal gap (world units) at which a swing actually connects. Beyond
        // this the attack whiffs, so fighters must close the distance to deal damage.
        private const float HitRange = 2.4f;

        private void Awake()
        {
            EnsureCamera();
            var callScreen = new GameObject("BossCallScreen").AddComponent<KiForge.UI.BossCallUI>();
            callScreen.Setup(null, "demo_player", InitArena);
        }

        private void InitArena()
        {
            // Y = 0.054 plants the fighters' feet on the GrassField (top at Y = 0).
            var player = SpawnFighterModel("Player", new Vector3(-2.2f, 0.054f, 0f), new Color(0.12f, 0.9f, 1f));
            var boss   = SpawnFighterModel("Boss",   new Vector3( 2.2f, 0.054f, 0f), new Color(1f, 0.18f, 0.45f));

            if (player != null && boss != null)
            {
                var playerHealth = player.AddComponent<PlayerCombatController>();
                var bossHealth   = boss.AddComponent<BossCombatController>();
                playerHealth.Initialize(MaxHealth);
                bossHealth.Initialize(MaxHealth);

                // Neither fighter auto-punches: the player attacks on input, the boss via its AI.
                var playerFighter = player.AddComponent<FighterAnimationController>();
                var bossFighter   = boss.AddComponent<FighterAnimationController>();
                playerFighter.Initialize(boss.transform,   false);
                bossFighter.Initialize(player.transform, false);

                // Wire death animation directly to health-depletion events.
                // The telemetry lambdas (further down) already send the KO event that triggers
                // the backend's Pika recap. These callbacks handle the visual side only.
                bossHealth.Defeated   += () => { bossFighter.PlayDying();   playerFighter.StopLoop(); };
                playerHealth.Defeated += () => { playerFighter.PlayDying(); bossFighter.StopLoop();   };

                // --- Player input (left, human): keyboard + MYO charge tiers ---
                var myo = player.AddComponent<MyoChargeInput>();
                var playerInput = player.AddComponent<PlayerAttackInput>();
                playerInput.Initialize(playerFighter, myo);

                var playerWalk = player.AddComponent<PlayerWalkController>();
                playerWalk.Initialize(1.3f, -4.5f, 4.5f, arrowKeys: false); // WASD (forward speed; backward = 0.55x)

                // --- Computer vision: webcam hand gestures via the backend WebSocket ---
                // BackendEventReceiver connects to the FastAPI/MediaPipe gesture stream and
                // republishes POSE_UPDATE frames on the bus; CvAimController maps a closed
                // fist to a punch and an open palm to walking forward. Falls back to the
                // keyboard PlayerWalkController whenever CV frames stop arriving.
                var eventBus = new KiForgeEventBus();
                var backend = new GameObject("BackendEventReceiver").AddComponent<BackendEventReceiver>();
                backend.Initialize(eventBus);

                // Use real MYO bridge events when available; falls back to keyboard (hold B).
                var wsMyo = new WebSocketMyoSource(eventBus);
                myo.Initialize(wsMyo);

                var cvAim = player.AddComponent<CvAimController>();
                cvAim.Initialize(eventBus, player.transform, playerFighter, playerWalk, playerInput, myo, -4.5f, 4.5f);

                // On-screen CV link/gesture status pill (top-center).
                var cvStatus = new GameObject("CV Status").AddComponent<CvStatusUI>();
                cvStatus.Setup(backend, cvAim);

                // --- Boss AI (right): backend/Agentverse-driven decisions + local fallback ---
                var bossAgent = boss.AddComponent<BackendBossAgent>();
                bossAgent.Initialize("http://127.0.0.1:8000/agent/combat");
                var bossBrain = boss.AddComponent<BossAgentController>();
                bossBrain.Initialize(bossFighter, player.transform, bossHealth, playerHealth, bossAgent,
                    () => "balanced", playerInput);

                // --- Telemetry recorder: every hit goes to /agent/combat over the WebSocket ---
                var telemetryRecorder = new GameObject("TelemetryRecorder").AddComponent<MatchTelemetryRecorder>();
                telemetryRecorder.Initialize(eventBus);

                // --- Damage resolution: typed damage, blocking cuts it to 30% ---
                playerFighter.Impact += (src, tgt) =>
                {
                    if (bossHealth.IsDefeated) return;
                    Debug.Log($"[HitRange] player swing gap = {Mathf.Abs(player.transform.position.x - boss.transform.position.x):0.00}");
                    if (!InHitRange(player.transform, boss.transform)) return; // whiff if too far
                    var blocked = bossFighter.IsBlocking;
                    var dmg = ResolveDamage(playerFighter.LastAttack, blocked);
                    bossHealth.ApplyDamage(dmg);
                    bossFighter.PlayPain();

                    round++;
                    var outcome = bossHealth.IsDefeated ? "boss_ko"
                                : blocked              ? "player_blocked"
                                : "boss_staggered";
                    var evt = new CombatTelemetryEvent
                    {
                        round           = round,
                        playerAction    = AttackToPlayerAction(playerFighter.LastAttack),
                        chargeTime      = myo.LastHoldSeconds,
                        accuracy        = blocked ? 0.3f : 1.0f,
                        damageDealtByPlayer = dmg,
                        damageDealtByBoss   = 0,
                        bossAction      = BossDecisionString(bossBrain.CurrentDecision),
                        bossHealthAfter = bossHealth.Health,
                        playerHealthAfter = playerHealth.Health,
                        outcome         = outcome
                    };
                    telemetryRecorder.Record(evt);
                    backend.SendCombatTelemetry(
                        evt.round, PlayerActionString(evt.playerAction), evt.chargeTime, evt.accuracy,
                        evt.damageDealtByPlayer, evt.damageDealtByBoss, evt.bossAction,
                        evt.bossHealthAfter, evt.playerHealthAfter, evt.outcome);
                };

                bossFighter.Impact += (src, tgt) =>
                {
                    if (playerHealth.IsDefeated) return;
                    if (!InHitRange(player.transform, boss.transform)) return; // whiff if too far
                    var blocked = playerFighter.IsBlocking;
                    var dmg = ResolveDamage(bossFighter.LastAttack, blocked);
                    playerHealth.ApplyDamage(dmg);
                    playerFighter.PlayPain();

                    round++;
                    var outcome2 = playerHealth.IsDefeated ? "player_ko"
                                 : blocked                 ? "boss_blocked"
                                 : "player_staggered";
                    var evt2 = new CombatTelemetryEvent
                    {
                        round           = round,
                        playerAction    = PlayerActionType.None,
                        chargeTime      = 0f,
                        accuracy        = 0f,
                        damageDealtByPlayer = 0,
                        damageDealtByBoss   = dmg,
                        bossAction      = BossDecisionString(bossBrain.CurrentDecision),
                        bossHealthAfter = bossHealth.Health,
                        playerHealthAfter = playerHealth.Health,
                        outcome         = outcome2
                    };
                    telemetryRecorder.Record(evt2);
                    backend.SendCombatTelemetry(
                        evt2.round, "guard", evt2.chargeTime, evt2.accuracy,
                        evt2.damageDealtByPlayer, evt2.damageDealtByBoss, evt2.bossAction,
                        evt2.bossHealthAfter, evt2.playerHealthAfter, evt2.outcome);
                };

                var hud = new GameObject("HUD").AddComponent<HealthBarUI>();
                hud.Setup(playerHealth, bossHealth);

                // --- Charge bar: fills while the player holds fist (MYO or keyboard-B) ---
                var chargeBar = new GameObject("ChargeBarUI").AddComponent<ChargeBarUI>();
                chargeBar.Setup(myo);

                // --- Narration display: big anime move-name pop (fades after 3.5s) ---
                var narration = new GameObject("NarrationDisplayUI").AddComponent<NarrationDisplayUI>();
                narration.Setup(eventBus);

                // --- Caption bar: scrolling commentator log at the bottom (persists) ---
                var captions = new GameObject("CaptionBarUI").AddComponent<CaptionBarUI>();
                captions.Setup(eventBus);

                // --- Damage numbers: static red labels below each health bar ---
                var dmgNumbers = new GameObject("DamageNumberUI").AddComponent<DamageNumberUI>();
                dmgNumbers.Setup(playerHealth, bossHealth);

                // --- AI Fight Lab panel: Tab to toggle, polls /demo/fight-lab ---
                var fightLab = new GameObject("FightLabPanelUI").AddComponent<FightLabPanelUI>();
                fightLab.Setup();
            }
        }

        private static bool InHitRange(Transform a, Transform b)
        {
            return Mathf.Abs(a.position.x - b.position.x) <= HitRange;
        }

        // Maps AttackType -> PlayerActionType for the telemetry struct.
        private static PlayerActionType AttackToPlayerAction(AttackType type)
        {
            switch (type)
            {
                case AttackType.PunchLeft:               return PlayerActionType.LeftPunch;
                case AttackType.PunchRight:              return PlayerActionType.RightPunch;
                case AttackType.KickLeft:                return PlayerActionType.LeftPunch;
                case AttackType.KickRight:               return PlayerActionType.RightPunch;
                case AttackType.HeavyPunch:              return PlayerActionType.HeavyPunch;
                case AttackType.VeryHeavyPunch:          return PlayerActionType.VeryHeavyPunch;
                case AttackType.Ultimate:                return PlayerActionType.VeryHeavyPunch;
                default:                                 return PlayerActionType.None;
            }
        }

        // Serialises PlayerActionType to the snake_case string the backend expects.
        private static string PlayerActionString(PlayerActionType action)
        {
            switch (action)
            {
                case PlayerActionType.LeftPunch:    return "left_punch";
                case PlayerActionType.RightPunch:   return "right_punch";
                case PlayerActionType.HeavyPunch:   return "heavy_punch";
                case PlayerActionType.VeryHeavyPunch: return "very_heavy_punch";
                case PlayerActionType.Guard:        return "guard";
                default:                            return "left_punch";
            }
        }

        // Serialises BossDecision to the snake_case string the backend expects.
        private static string BossDecisionString(BossDecision decision)
        {
            switch (decision.kind)
            {
                case BossActionKind.Block: return "guard";
                case BossActionKind.Dodge: return "dodge";
                default:
                    switch (decision.attack)
                    {
                        case AttackType.HeavyPunch:
                        case AttackType.VeryHeavyPunch: return "heavy_counter";
                        case AttackType.PunchLeft:      return "left_punch";
                        default:                        return "right_punch";
                    }
            }
        }

        private static int ResolveDamage(AttackType type, bool blocked)
        {
            var dmg = AttackTuning.Damage(type);
            if (blocked)
            {
                dmg = Mathf.Max(1, Mathf.RoundToInt(dmg * AttackTuning.BlockMultiplier));
            }

            return dmg;
        }

        private static Camera EnsureCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            if (camera.GetComponent<ArenaCameraController>() == null)
                camera.gameObject.AddComponent<ArenaCameraController>();

            return camera;
        }

        private static GameObject SpawnFighterModel(string name, Vector3 position, Color tint)
        {
            GameObject model = Resources.Load<GameObject>("Heavy_Punch");
#if UNITY_EDITOR
            if (model == null)
                model = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Animations/Heavy_Punch.fbx");
#endif
            if (model == null) return null;

            var instance = Instantiate(model);
            instance.name = name;
            instance.transform.position   = position;
            instance.transform.localScale = Vector3.one;

            var animator = instance.GetComponentInChildren<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.applyRootMotion = false;
#if UNITY_EDITOR
            var controller = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Art/Animations/PunchingRuntime.controller");
            if (controller != null) animator.runtimeAnimatorController = controller;
#endif
            TintRenderers(instance, tint);
            return instance;
        }

        private static void TintRenderers(GameObject root, Color tint)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>())
                foreach (var mat in r.materials)
                    if (mat.HasProperty("_Color"))
                        mat.color = tint;
        }

    }
}
