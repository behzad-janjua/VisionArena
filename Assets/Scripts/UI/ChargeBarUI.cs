using KiForge.Input;
using UnityEngine;
using UnityEngine.UI;

namespace KiForge.UI
{
    /// <summary>
    /// On-screen charge bar that fills while the player holds a fist (MYO or keyboard-B).
    /// Three tiers are indicated by color:
    ///   0–50%   cyan      (level 1–2 bolt)
    ///   50–85%  yellow    (level 3 beam)
    ///   85–100% orange    (level 4 ultimate cannon)
    /// </summary>
    public sealed class ChargeBarUI : MonoBehaviour
    {
        private static readonly Color ColorNormal   = new Color(0.15f, 0.9f, 1f);
        private static readonly Color ColorHeavy    = new Color(1f, 0.9f, 0.1f);
        private static readonly Color ColorUltimate = new Color(1f, 0.4f, 0.05f);

        private const float MaxCharge = 3.5f;

        private MyoChargeInput myo;
        private Image fill;
        private float displayCharge;

        public void Setup(MyoChargeInput myoInput)
        {
            myo = myoInput;
            BuildUI();
        }

        private void Update()
        {
            if (myo == null || fill == null) return;

            float target = Mathf.Clamp01(myo.CurrentHoldSeconds / MaxCharge);
            displayCharge = Mathf.MoveTowards(displayCharge, target, Time.deltaTime * 3f);

            fill.fillAmount = displayCharge;
            fill.color = displayCharge >= 0.85f ? ColorUltimate
                       : displayCharge >= 0.50f ? ColorHeavy
                       : ColorNormal;
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("Charge Bar Canvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // Container — centred at the bottom of the screen
            var bar = new GameObject("ChargeBar");
            bar.transform.SetParent(canvasGO.transform, false);
            var barRT = bar.AddComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0.5f, 0f);
            barRT.anchorMax = new Vector2(0.5f, 0f);
            barRT.pivot = new Vector2(0.5f, 0f);
            barRT.anchoredPosition = new Vector2(0f, 28f);
            barRT.sizeDelta = new Vector2(340f, 20f);

            // Dark background
            var bg = new GameObject("BG");
            bg.transform.SetParent(bar.transform, false);
            var bgRT = bg.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.05f, 0.05f, 0.05f, 0.88f);

            // Fill (Image.Type.Filled, left-to-right)
            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(bar.transform, false);
            var fillRT = fillGO.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = new Vector2(3f, 3f);
            fillRT.offsetMax = new Vector2(-3f, -3f);
            fill = fillGO.AddComponent<Image>();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;
            fill.color = ColorNormal;

            // Label
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(bar.transform, false);
            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = labelRT.offsetMax = Vector2.zero;
            var lbl = labelGO.AddComponent<Text>();
            lbl.font = ResolveFont();
            lbl.fontSize = 13;
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.text = "CHARGE  [hold B]";
            lbl.color = new Color(1f, 1f, 1f, 0.6f);
        }

        private static Font ResolveFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 14);
            return f;
        }
    }
}
