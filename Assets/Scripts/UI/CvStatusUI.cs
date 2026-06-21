using KiForge.Input;
using UnityEngine;
using UnityEngine.UI;

namespace KiForge.UI
{
    /// <summary>
    /// Small top-center status pill showing the live CV link state and current
    /// gesture, so the WebSocket connection and fist/palm recognition are visible
    /// on screen during a demo without watching the Console.
    /// </summary>
    public sealed class CvStatusUI : MonoBehaviour
    {
        private static readonly Color Gray   = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color Yellow = new Color(1f, 0.85f, 0.3f);
        private static readonly Color Green  = new Color(0.3f, 1f, 0.55f);

        private BackendEventReceiver receiver;
        private CvAimController cv;
        private Text label;
        private string lastText;

        public void Setup(BackendEventReceiver backendReceiver, CvAimController cvController)
        {
            receiver = backendReceiver;
            cv = cvController;
            BuildUI();
        }

        private void Update()
        {
            if (label == null)
                return;

            bool connected = receiver != null && receiver.IsConnected;
            string text;
            Color color;

            if (!connected)
            {
                text = "CV: connecting…";
                color = Gray;
            }
            else if (cv != null && cv.CvActive)
            {
                text = GestureText(cv.CurrentGesture); // pre-built per gesture, no per-frame alloc
                color = Green;
            }
            else
            {
                text = "CV: connected · keyboard";
                color = Yellow;
            }

            // Only touch the Text component when the string actually changes.
            if (text != lastText)
            {
                label.text = text;
                lastText = text;
            }
            label.color = color;
        }

        private static string GestureText(string gesture)
        {
            switch (gesture)
            {
                case "fist":      return "CV: live · FIST (punch)";
                case "open_palm": return "CV: live · OPEN PALM (walk)";
                default:          return "CV: live · —";
            }
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("CV Status Canvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 11; // above the health-bar HUD (10)

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Background pill
            var panelGO = new GameObject("Pill");
            panelGO.transform.SetParent(canvasGO.transform, false);
            var panelRT = panelGO.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 1f);
            panelRT.anchorMax = new Vector2(0.5f, 1f);
            panelRT.pivot = new Vector2(0.5f, 1f);
            panelRT.anchoredPosition = new Vector2(0f, -18f);
            panelRT.sizeDelta = new Vector2(380f, 44f);
            var panelImg = panelGO.AddComponent<Image>();
            panelImg.color = new Color(0.05f, 0.05f, 0.05f, 0.85f);

            // Text
            var textGO = new GameObject("Label");
            textGO.transform.SetParent(panelGO.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = textRT.offsetMax = Vector2.zero;

            label = textGO.AddComponent<Text>();
            label.font = ResolveFont();
            label.fontSize = 24;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.text = "CV: connecting…";
            label.color = Gray;
        }

        private static Font ResolveFont()
        {
            // Unity 2022+ removed the bundled Arial; LegacyRuntime is the replacement.
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null)
                font = Font.CreateDynamicFontFromOSFont("Arial", 24);
            return font;
        }
    }
}
