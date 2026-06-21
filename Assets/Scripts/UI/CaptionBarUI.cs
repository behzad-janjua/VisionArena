using System.Collections.Generic;
using KiForge.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace KiForge.UI
{
    /// <summary>
    /// Live commentator caption bar — appears at the bottom of the screen like
    /// closed captions on a broadcast. Accumulates the last few NarratorAgent
    /// lines so the commentary is readable even when NarrationDisplayUI has faded.
    ///
    /// Each entry shows: [MOVE NAME] · narration text
    /// Newest line is at the bottom. Older lines lighten progressively.
    /// </summary>
    public sealed class CaptionBarUI : MonoBehaviour
    {
        private const int MaxLines = 4;
        private const float LineHeight = 24f;
        private const float PadH = 14f;
        private const float PadV = 6f;
        private const float BarWidth = 820f;

        private readonly Queue<string> lines = new Queue<string>();
        private KiForgeEventBus eventBus;
        private Text[] lineTexts;
        private CanvasGroup group;

        // Colours for lines, oldest → newest
        private static readonly Color[] LineColors =
        {
            new Color(0.8f, 0.8f, 0.8f, 0.30f),
            new Color(0.9f, 0.9f, 0.9f, 0.50f),
            new Color(0.95f, 0.95f, 0.95f, 0.72f),
            new Color(1f, 1f, 1f, 1f),
        };

        public void Setup(KiForgeEventBus bus)
        {
            eventBus = bus;
            BuildUI();
            eventBus.AgentResponseReceived += OnAgentResponse;
        }

        private void OnDestroy()
        {
            if (eventBus != null)
                eventBus.AgentResponseReceived -= OnAgentResponse;
        }

        private void OnAgentResponse(AgentResponseEvent response)
        {
            var moveName  = (response.moveName  ?? "").Trim();
            var narration = (response.narration ?? "").Trim();
            if (string.IsNullOrEmpty(moveName) && string.IsNullOrEmpty(narration)) return;

            var line = string.IsNullOrEmpty(moveName)
                ? narration
                : string.IsNullOrEmpty(narration)
                    ? moveName
                    : $"[{moveName.ToUpperInvariant()}]  {narration}";

            lines.Enqueue(line);
            if (lines.Count > MaxLines) lines.Dequeue();

            RefreshText();
        }

        private void RefreshText()
        {
            if (lineTexts == null) return;
            var arr = lines.ToArray();
            var offset = MaxLines - arr.Length;

            for (var i = 0; i < MaxLines; i++)
            {
                var srcIdx = i - offset;
                if (srcIdx < 0 || srcIdx >= arr.Length)
                {
                    lineTexts[i].text = "";
                    lineTexts[i].color = Color.clear;
                }
                else
                {
                    lineTexts[i].text = arr[srcIdx];
                    lineTexts[i].color = LineColors[i];
                }
            }
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("Caption Canvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 11;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // Outer container — bottom-centre, above the charge bar
            var barH = MaxLines * LineHeight + PadV * 2f;
            var container = new GameObject("CaptionBar");
            container.transform.SetParent(canvasGO.transform, false);
            var cRT = container.AddComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0.5f, 0f);
            cRT.anchorMax = new Vector2(0.5f, 0f);
            cRT.pivot = new Vector2(0.5f, 0f);
            cRT.anchoredPosition = new Vector2(0f, 58f);   // just above charge bar
            cRT.sizeDelta = new Vector2(BarWidth, barH);

            // Dark semi-transparent backing strip
            var bg = new GameObject("BG");
            bg.transform.SetParent(container.transform, false);
            var bgRT = bg.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.62f);

            // Lines
            lineTexts = new Text[MaxLines];
            var font = ResolveFont();

            for (var i = 0; i < MaxLines; i++)
            {
                var lineGO = new GameObject($"Line{i}");
                lineGO.transform.SetParent(container.transform, false);
                var lRT = lineGO.AddComponent<RectTransform>();
                lRT.anchorMin = new Vector2(0f, 0f);
                lRT.anchorMax = new Vector2(1f, 0f);
                lRT.pivot = new Vector2(0f, 0f);
                var yFromBottom = PadV + i * LineHeight;
                lRT.anchoredPosition = new Vector2(PadH, yFromBottom);
                lRT.sizeDelta = new Vector2(-(PadH * 2f), LineHeight);

                var txt = lineGO.AddComponent<Text>();
                txt.font = font;
                txt.fontSize = 17;
                txt.alignment = TextAnchor.MiddleLeft;
                txt.color = Color.clear;
                txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                txt.verticalOverflow = VerticalWrapMode.Overflow;
                lineTexts[i] = txt;
            }
        }

        private static Font ResolveFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 17);
            return f;
        }
    }
}
