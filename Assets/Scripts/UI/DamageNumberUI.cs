using System.Collections;
using KiForge.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace KiForge.UI
{
    public sealed class DamageNumberUI : MonoBehaviour
    {
        private Canvas canvas;

        public void Setup(PlayerCombatController player, BossCombatController boss)
        {
            canvas = BuildCanvas();

            player.Damaged += dmg => { if (dmg > 0) SpawnNumber(dmg, isLeft: true); };
            boss.Damaged   += dmg => { if (dmg > 0) SpawnNumber(dmg, isLeft: false); };
        }

        private static Canvas BuildCanvas()
        {
            var go     = new GameObject("Damage Number Canvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 12;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private void SpawnNumber(float dmg, bool isLeft)
        {
            var go = new GameObject("DmgNum");
            go.transform.SetParent(canvas.transform, false);

            var rt       = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200f, 70f);

            float jitter = Random.Range(-22f, 22f);
            if (isLeft)
            {
                rt.anchorMin        = new Vector2(0f, 1f);
                rt.anchorMax        = new Vector2(0f, 1f);
                rt.pivot            = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(28f + jitter, -86f);
            }
            else
            {
                rt.anchorMin        = new Vector2(1f, 1f);
                rt.anchorMax        = new Vector2(1f, 1f);
                rt.pivot            = new Vector2(1f, 0.5f);
                rt.anchoredPosition = new Vector2(-28f + jitter, -86f);
            }

            var txt                = go.AddComponent<Text>();
            txt.font               = ResolveFont();
            txt.fontSize           = 42;
            txt.fontStyle          = FontStyle.Bold;
            txt.color              = DamageColor(dmg);
            txt.alignment          = isLeft ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow   = VerticalWrapMode.Overflow;
            txt.text               = $"-{(int)dmg}";

            var outline            = go.AddComponent<Outline>();
            outline.effectColor    = new Color(0f, 0f, 0f, 0.88f);
            outline.effectDistance = new Vector2(2.5f, -2.5f);

            StartCoroutine(AnimateNumber(go, rt, txt));
        }

        private IEnumerator AnimateNumber(GameObject go, RectTransform rt, Text txt)
        {
            Vector2 startPos   = rt.anchoredPosition;
            Color   startColor = txt.color;

            // Scale punch: 1.3 → 1.0 in 80ms
            float elapsed = 0f;
            while (elapsed < 0.08f)
            {
                elapsed += Time.deltaTime;
                float scale = Mathf.Lerp(1.3f, 1.0f, elapsed / 0.08f);
                go.transform.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }
            go.transform.localScale = Vector3.one;

            // Float up + fade over 1.1s (fade starts at 0.65s)
            const float floatDuration = 1.1f;
            const float floatDistance = 65f;
            const float fadeStart     = 0.65f;

            elapsed = 0f;
            while (elapsed < floatDuration)
            {
                elapsed += Time.deltaTime;
                float t     = Mathf.Clamp01(elapsed / floatDuration);
                float eased = 1f - (1f - t) * (1f - t);
                rt.anchoredPosition = startPos + new Vector2(0f, floatDistance * eased);

                float fadeT = Mathf.Clamp01((elapsed - fadeStart) / (floatDuration - fadeStart));
                Color c     = startColor;
                c.a         = 1f - fadeT;
                txt.color   = c;
                yield return null;
            }

            Destroy(go);
        }

        private static Color DamageColor(float dmg)
        {
            if (dmg >= 30f) return new Color(1f, 0.35f, 0.1f);  // orange-red: heavy
            if (dmg >= 15f) return new Color(1f, 0.95f, 0.08f); // yellow: medium
            return Color.white;                                    // white: light
        }

        private static Font ResolveFont()
        {
            var f = Font.CreateDynamicFontFromOSFont("Impact", 42);
            if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial Bold", 42);
            if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return f;
        }
    }
}
