using System.Collections;
using KiForge.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace KiForge.UI
{
    /// <summary>
    /// Shows the NarratorAgent's move name and commentary line on screen.
    /// The move name appears large in anime style, the narration line below it.
    /// Both fade in quickly then fade out after displaySeconds.
    /// </summary>
    public sealed class NarrationDisplayUI : MonoBehaviour
    {
        [SerializeField] private float displaySeconds = 3.5f;
        [SerializeField] private float fadeOutSeconds = 0.8f;

        private KiForgeEventBus eventBus;
        private Text moveNameText;
        private Text narrationText;
        private CanvasGroup group;
        private Coroutine fadeCoroutine;

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
            if (string.IsNullOrWhiteSpace(response.moveName) && string.IsNullOrWhiteSpace(response.narration))
                return;

            if (moveNameText != null) moveNameText.text = response.moveName;
            if (narrationText != null) narrationText.text = response.narration;

            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(ShowThenFade());
        }

        private IEnumerator ShowThenFade()
        {
            if (group != null) group.alpha = 1f;
            yield return new WaitForSeconds(displaySeconds);

            float elapsed = 0f;
            while (elapsed < fadeOutSeconds)
            {
                elapsed += Time.deltaTime;
                if (group != null) group.alpha = 1f - elapsed / fadeOutSeconds;
                yield return null;
            }

            if (group != null) group.alpha = 0f;
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("Narration Canvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 12;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // CanvasGroup drives the fade
            group = canvasGO.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            // Root container — bottom-centre, above the charge bar
            var container = new GameObject("NarrationContainer");
            container.transform.SetParent(canvasGO.transform, false);
            var cRT = container.AddComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0.5f, 0f);
            cRT.anchorMax = new Vector2(0.5f, 0f);
            cRT.pivot = new Vector2(0.5f, 0f);
            cRT.anchoredPosition = new Vector2(0f, 62f);
            cRT.sizeDelta = new Vector2(700f, 90f);

            // Move name (large, bold-style)
            var nameGO = new GameObject("MoveName");
            nameGO.transform.SetParent(container.transform, false);
            var nameRT = nameGO.AddComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0f, 0.5f);
            nameRT.anchorMax = new Vector2(1f, 1f);
            nameRT.offsetMin = nameRT.offsetMax = Vector2.zero;
            moveNameText = nameGO.AddComponent<Text>();
            moveNameText.font = ResolveFont();
            moveNameText.fontSize = 36;
            moveNameText.fontStyle = FontStyle.Bold;
            moveNameText.alignment = TextAnchor.MiddleCenter;
            moveNameText.color = new Color(1f, 0.9f, 0.2f);
            moveNameText.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Narration line (smaller, below the move name)
            var narGO = new GameObject("Narration");
            narGO.transform.SetParent(container.transform, false);
            var narRT = narGO.AddComponent<RectTransform>();
            narRT.anchorMin = Vector2.zero;
            narRT.anchorMax = new Vector2(1f, 0.5f);
            narRT.offsetMin = narRT.offsetMax = Vector2.zero;
            narrationText = narGO.AddComponent<Text>();
            narrationText.font = ResolveFont();
            narrationText.fontSize = 20;
            narrationText.alignment = TextAnchor.MiddleCenter;
            narrationText.color = new Color(0.9f, 0.9f, 0.9f);
            narrationText.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        private static Font ResolveFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 24);
            return f;
        }
    }
}
