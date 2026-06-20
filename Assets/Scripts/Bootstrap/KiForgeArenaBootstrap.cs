using KiForge.Animation;
using KiForge.Combat;
using KiForge.Input;
using KiForge.Scene;
using KiForge.UI;
using UnityEngine;

namespace KiForge.Bootstrap
{
    public sealed class KiForgeArenaBootstrap : MonoBehaviour
    {
        private const int   MaxHealth    = 400;
        private const int   PunchDamage  = 10;
        private const float ContactRange = 2.5f;

        private void Awake()
        {
            EnsureCamera();

            var player = SpawnFighterModel("Player", new Vector3(-2.2f, 0f, 0f), new Color(0.12f, 0.9f, 1f));
            var boss   = SpawnFighterModel("Boss",   new Vector3( 2.2f, 0f, 0f), new Color(1f, 0.18f, 0.45f));

            if (player != null && boss != null)
            {
                var playerHealth = player.AddComponent<PlayerCombatController>();
                var bossHealth   = boss.AddComponent<BossCombatController>();
                playerHealth.Initialize(MaxHealth);
                bossHealth.Initialize(MaxHealth);

                var playerFighter = player.AddComponent<FighterAnimationController>();
                var bossFighter   = boss.AddComponent<FighterAnimationController>();
                playerFighter.Initialize(boss.transform,   true, 0f);
                bossFighter.Initialize(player.transform, true, 1.1f);

                playerFighter.Impact += (src, tgt) =>
                {
                    if (bossHealth.IsDefeated) return;
                    // Only register damage if fighters are in physical contact range
                    if (tgt != null && Vector3.Distance(src.transform.position, tgt.position) > ContactRange) return;
                    bossHealth.ApplyDamage(PunchDamage);
                    bossFighter.PlayPain();
                    if (bossHealth.IsDefeated)
                    {
                        bossFighter.PlayDying();
                        playerFighter.StopLoop();
                    }
                };

                bossFighter.Impact += (src, tgt) =>
                {
                    if (playerHealth.Health <= 0) return;
                    if (tgt != null && Vector3.Distance(src.transform.position, tgt.position) > ContactRange) return;
                    playerHealth.ApplyDamage(PunchDamage);
                    playerFighter.PlayPain();
                    if (playerHealth.IsDefeated)
                    {
                        playerFighter.PlayDying();
                        bossFighter.StopLoop();
                    }
                };

                var hud = new GameObject("HUD").AddComponent<HealthBarUI>();
                hud.Setup(playerHealth, bossHealth);

                // Player 1: WASD    Player 2: Arrow keys
                var playerWalk = player.AddComponent<PlayerWalkController>();
                playerWalk.Initialize(3.0f, -4.5f, 4.5f, arrowKeys: false);

                var bossWalk = boss.AddComponent<PlayerWalkController>();
                bossWalk.Initialize(3.0f, -4.5f, 4.5f, arrowKeys: true);
            }
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
            GameObject model = Resources.Load<GameObject>("Punching");
#if UNITY_EDITOR
            if (model == null)
                model = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Animations/Punching.fbx");
#endif
            if (model == null) return null;

            var instance = Instantiate(model);
            instance.name = name;
            instance.transform.position   = position;
            instance.transform.localScale = Vector3.one;

            var animator = instance.GetComponentInChildren<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
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
