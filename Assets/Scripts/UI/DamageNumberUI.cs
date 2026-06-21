using KiForge.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace KiForge.UI
{
    public sealed class DamageNumberUI : MonoBehaviour
    {
        private Text playerDmgText;
        private Text bossDmgText;

        public void Setup(PlayerCombatController player, BossCombatController boss)
        {
            var canvas = BuildCanvas();
            playerDmgText = BuildLabel(canvas, isLeft: true);
            bossDmgText   = BuildLabel(canvas, isLeft: false);

            player.Damaged += dmg => { if (dmg > 0) playerDmgText.text = $"-{dmg}"; };
            boss.Damaged   += dmg => { if (dmg > 0) bossDmgText.text   = $"-{dmg}"; };
        }

        private static Canvas BuildCanvas()
        {
            var go     = new GameObject("Damage Number Canvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 12;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution  = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight   = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static Text BuildLabel(Canvas canvas, bool isLeft)
        {
            var go = new GameObject(isLeft ? "PlayerDmg" : "BossDmg");
            go.transform.SetParent(canvas.transform, false);

            var rt         = go.AddComponent<RectTransform>();
            rt.sizeDelta   = new Vector2(120f, 36f);

            // Mirror the health bar anchors; shift down 30px so the number sits
            // just below the HP bar row (which occupies y = -24 to -46).
            if (isLeft)
            {
                rt.anchorMin        = new Vector2(0f, 1f);
                rt.anchorMax        = new Vector2(0f, 1f);
                rt.pivot            = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(24f, -54f);
            }
            else
            {
                rt.anchorMin        = new Vector2(1f, 1f);
                rt.anchorMax        = new Vector2(1f, 1f);
                rt.pivot            = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-24f, -54f);
            }

            var txt              = go.AddComponent<Text>();
            txt.font             = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                                ?? Font.CreateDynamicFontFromOSFont("Arial", 28);
            txt.fontSize         = 30;
            txt.fontStyle        = FontStyle.Bold;
            txt.color            = new Color(1f, 0.15f, 0.15f, 1f);
            txt.alignment        = isLeft ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.text             = "";
            return txt;
        }
    }
}
