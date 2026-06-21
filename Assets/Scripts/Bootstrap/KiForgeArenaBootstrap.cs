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

        // Max horizontal gap (world units) at which a swing actually connects. Beyond
        // this the attack whiffs, so fighters must close the distance to deal damage.
        private const float HitRange = 2.4f;

        private void Awake()
        {
            EnsureCamera();

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
                bossHealth.Defeated   += () => { bossFighter.PlayDying();   playerFighter.StopLoop(); };
                playerHealth.Defeated += () => { playerFighter.PlayDying(); bossFighter.StopLoop();   };

                // --- Player input (left, human): keyboard + MYO charge tiers ---
                var myo = player.AddComponent<MyoChargeInput>();
                myo.Initialize(null); // null -> keyboard MYO sim (hold B)
                var playerInput = player.AddComponent<PlayerAttackInput>();
                playerInput.Initialize(playerFighter, myo);

                var playerWalk = player.AddComponent<PlayerWalkController>();
                playerWalk.Initialize(1.3f, -4.5f, 4.5f, arrowKeys: false); // WASD (forward speed; backward = 0.55x)

                // --- Computer vision: webcam body tracking via the backend WebSocket ---
                // BackendEventReceiver connects to the FastAPI/MediaPipe pose stream and
                // republishes POSE_UPDATE frames on the bus; CvAimController turns body
                // center into movement and wrist into an aim reticle. Falls back to the
                // keyboard PlayerWalkController whenever CV frames stop arriving.
                var eventBus = new KiForgeEventBus();
                var backend = new GameObject("BackendEventReceiver").AddComponent<BackendEventReceiver>();
                backend.Initialize(eventBus);

                var cvAim = player.AddComponent<CvAimController>();
                cvAim.Initialize(eventBus, player.transform, playerFighter, playerWalk, -4.5f, 4.5f);

                // --- Arize coach feedback loop (player improvement) ---
                var coach = new GameObject("ArizeCoach").AddComponent<ArizeCoachFeedback>();

                // --- Boss AI (right): agent-driven decisions + movement ---
                var bossAgent = new PlaceholderBossAgent();
                var bossBrain = boss.AddComponent<BossAgentController>();
                bossBrain.Initialize(bossFighter, player.transform, bossHealth, playerHealth, bossAgent,
                    () => coach.PlayerStyle, playerInput);

                coach.Initialize(playerInput, () => bossBrain.CurrentDecision);

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
                    coach.RecordPlayerLanded(playerFighter.LastAttack, blocked);
                };

                bossFighter.Impact += (src, tgt) =>
                {
                    if (playerHealth.IsDefeated) return;
                    if (!InHitRange(player.transform, boss.transform)) return; // whiff if too far
                    var blocked = playerFighter.IsBlocking;
                    var dmg = ResolveDamage(bossFighter.LastAttack, blocked);
                    playerHealth.ApplyDamage(dmg);
                    playerFighter.PlayPain();
                    coach.RecordPlayerTookHit();
                };

                var hud = new GameObject("HUD").AddComponent<HealthBarUI>();
                hud.Setup(playerHealth, bossHealth);
            }
        }

        private static bool InHitRange(Transform a, Transform b)
        {
            return Mathf.Abs(a.position.x - b.position.x) <= HitRange;
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
