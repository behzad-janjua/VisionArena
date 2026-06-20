#if UNITY_EDITOR
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace KiForge.Editor
{
    public static class PunchingAnimatorSetup
    {
        private const string AnimationFolder = "Assets/Art/Animations";
        private const string PunchingFbxPath = "Assets/Art/Animations/Punching.fbx";
        private const string ControllerPath = "Assets/Art/Animations/PunchingRuntime.controller";

        [InitializeOnLoadMethod]
        public static void EnsurePunchingController()
        {
            if (!File.Exists(PunchingFbxPath))
            {
                return;
            }

            var punchingClip = LoadFirstClip(PunchingFbxPath);
            if (punchingClip == null)
            {
                return;
            }

            var changed = false;
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                changed = true;
            }

            var stateMachine = controller.layers[0].stateMachine;
            var punchingState = EnsureState(stateMachine, "Punching", punchingClip, new Vector3(200f, 0f, 0f), ref changed);
            stateMachine.defaultState = punchingState;

            var dyingFbxPath = FindAnimationFbx("Dying");
            var dyingClip = string.IsNullOrEmpty(dyingFbxPath) ? null : LoadFirstClip(dyingFbxPath);
            if (dyingClip != null)
            {
                EnsureState(stateMachine, "Dying", dyingClip, new Vector3(200f, 90f, 0f), ref changed);
            }

            if (!changed)
            {
                return;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Updated {ControllerPath} from Punching.fbx{(dyingClip == null ? string.Empty : " and Dying.fbx")}");
        }

        private static AnimatorState EnsureState(AnimatorStateMachine stateMachine, string stateName, Motion motion, Vector3 position, ref bool changed)
        {
            var existing = stateMachine.states.FirstOrDefault(childState => childState.state.name == stateName);
            var state = existing.state;
            if (state == null)
            {
                state = stateMachine.AddState(stateName, position);
                changed = true;
            }

            if (state.motion != motion)
            {
                state.motion = motion;
                changed = true;
            }

            return state;
        }

        private static AnimationClip LoadFirstClip(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => !clip.name.StartsWith("__preview__", System.StringComparison.OrdinalIgnoreCase));
        }

        private static string FindAnimationFbx(string assetName)
        {
            var matches = AssetDatabase.FindAssets($"{assetName} t:Model", new[] { AnimationFolder });
            return matches
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(path => string.Equals(Path.GetFileNameWithoutExtension(path).Trim(), assetName, System.StringComparison.OrdinalIgnoreCase));
        }
    }
}
#endif
