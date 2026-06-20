using KiForge.Combat;
using KiForge.Effects;
using KiForge.Input;
using KiForge.Telemetry;
using KiForge.UI;
using UnityEngine;

namespace KiForge.Bootstrap
{
    public sealed class KiForgeArenaBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            var eventBus = new Shared.KiForgeEventBus();
            var camera = EnsureCamera();

            var playerObject = CreateActor("Player", new Vector3(-4f, -1.6f, 0f), new Color(0.12f, 0.9f, 1f), new Vector3(0.8f, 2.2f, 1f));
            var bossObject = CreateActor("Boss", new Vector3(4f, -1.25f, 0f), new Color(1f, 0.18f, 0.45f), new Vector3(1.8f, 3.2f, 1f));
            TryAttachFighterModel(playerObject, new Color(0.12f, 0.9f, 1f));
            TryAttachFighterModel(bossObject, new Color(1f, 0.18f, 0.45f));

            var player = playerObject.AddComponent<PlayerCombatController>();
            var boss = bossObject.AddComponent<BossCombatController>();

            var effects = gameObject.AddComponent<ArenaEffectsController>();
            effects.Initialize(playerObject.transform);

            var hud = gameObject.AddComponent<KiForgeHudController>();
            hud.BuildRuntimeHud();

            var telemetry = gameObject.AddComponent<MatchTelemetryRecorder>();
            telemetry.Initialize(eventBus);

            var mockAgent = gameObject.AddComponent<MockAgentClient>();
            mockAgent.Initialize(eventBus, telemetry);

            var game = gameObject.AddComponent<ArenaGameController>();
            game.Initialize(eventBus, player, boss, effects, hud, telemetry);

            AddPunchExchange(playerObject, bossObject, player, boss, effects, hud);

            var fallbackInput = gameObject.AddComponent<KeyboardFallbackInput>();
            fallbackInput.Initialize(eventBus, playerObject.transform, camera);

            var backendReceiver = gameObject.AddComponent<BackendEventReceiver>();
            backendReceiver.Initialize(eventBus);

            CreateArenaFloor();
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

            camera.transform.position = new Vector3(0f, 0.4f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.025f, 0.04f);
            return camera;
        }

        private static GameObject CreateActor(string name, Vector3 position, Color color, Vector3 scale)
        {
            var actor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            actor.name = name;
            actor.transform.position = position;
            actor.transform.localScale = scale;
            var renderer = actor.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default")) { color = color };
            return actor;
        }

        private static void TryAttachFighterModel(GameObject actor, Color tint)
        {
            var model = Resources.Load<GameObject>("Punching");
#if UNITY_EDITOR
            if (model == null)
            {
                model = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Animations/Punching.fbx");
            }
#endif
            if (model == null)
            {
                return;
            }

            var renderer = actor.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }

            var instance = Instantiate(model, actor.transform);
            instance.name = "Punching Fighter Model";
            instance.transform.localPosition = Vector3.down * 1.05f;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one * 0.012f;
            AssignPunchingController(instance);
            TintRenderers(instance, tint);
        }

        private static void AssignPunchingController(GameObject modelInstance)
        {
#if UNITY_EDITOR
            var controller = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Art/Animations/PunchingRuntime.controller");
            var animator = modelInstance.GetComponentInChildren<Animator>();
            if (animator != null && controller != null)
            {
                animator.runtimeAnimatorController = controller;
            }
#endif
        }

        private static void AddPunchExchange(GameObject playerObject, GameObject bossObject, PlayerCombatController player, BossCombatController boss, ArenaEffectsController effects, KiForgeHudController hud)
        {
            var playerPuncher = playerObject.AddComponent<KiForge.Animation.FighterAnimationController>();
            var bossPuncher = bossObject.AddComponent<KiForge.Animation.FighterAnimationController>();
            playerPuncher.Initialize(bossObject.transform, true, 0f);
            bossPuncher.Initialize(playerObject.transform, true, 1.1f);

            var punchCombat = playerObject.AddComponent<TwoCharacterPunchCombat>();
            punchCombat.Initialize(player, boss, effects, hud, playerPuncher, bossPuncher);
        }

        private static void TintRenderers(GameObject root, Color color)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            foreach (var targetRenderer in renderers)
            {
                foreach (var material in targetRenderer.materials)
                {
                    if (material.HasProperty("_Color"))
                    {
                        material.color = color;
                    }
                }
            }
        }

        private static void CreateArenaFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Neon Arena Floor";
            floor.transform.position = new Vector3(0f, -2.85f, 0.4f);
            floor.transform.localScale = new Vector3(12f, 0.08f, 1f);
            floor.GetComponent<Renderer>().material = new Material(Shader.Find("Sprites/Default"))
            {
                color = new Color(0.18f, 0.8f, 1f, 0.75f)
            };
        }
    }
}
