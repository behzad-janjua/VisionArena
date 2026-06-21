using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace KiForge.UI
{
    /// <summary>
    /// Shown after a KO. Polls /demo/recap/url until the Pika video is ready,
    /// then displays a "Watch Highlights" button that opens the URL in a browser.
    /// </summary>
    public sealed class RecapOverlayUI : MonoBehaviour
    {
        private const string PollEndpoint = "http://127.0.0.1:8000/demo/recap/url?player_id=demo_player";
        private const float  PollInterval  = 5f;
        private const float  PollTimeout   = 180f; // give up after 3 min

        private Canvas  canvas;
        private Text    statusText;
        private Button  watchButton;
        private Text    watchLabel;
        private bool    shown;

        private void Awake()
        {
            BuildUI();
        }

        public void ShowAfterKO(bool playerWon)
        {
            if (shown) return;
            shown = true;
            canvas.gameObject.SetActive(true);
            statusText.text = playerWon ? "VICTORY" : "DEFEATED";
            StartCoroutine(PollForUrl());
        }

        private IEnumerator PollForUrl()
        {
            watchButton.gameObject.SetActive(false);
            var generatingGO = statusText.gameObject.transform.parent.Find("Generating");
            if (generatingGO != null) generatingGO.gameObject.SetActive(true);

            float elapsed = 0f;
            while (elapsed < PollTimeout)
            {
                yield return new WaitForSeconds(PollInterval);
                elapsed += PollInterval;

                using var req = UnityWebRequest.Get(PollEndpoint);
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var data = JsonUtility.FromJson<RecapUrlResponse>(req.downloadHandler.text);
                    if (data != null && data.ready && !string.IsNullOrEmpty(data.video_url))
                    {
                        ShowWatchButton(data.video_url);
                        yield break;
                    }
                }
            }

            // Timed out — hide the generating label silently
            if (generatingGO != null) generatingGO.gameObject.SetActive(false);
        }

        private void ShowWatchButton(string url)
        {
            watchButton.gameObject.SetActive(true);
            watchButton.onClick.RemoveAllListeners();
            watchButton.onClick.AddListener(() => Application.OpenURL(url));

            // Hide the "Generating…" label
            var generatingGO = canvas.transform.Find("Panel/Generating");
            if (generatingGO != null) generatingGO.gameObject.SetActive(false);
        }

        private void BuildUI()
        {
            float sf = Screen.height / 1080f;

            var canvasGO = new GameObject("Recap Overlay Canvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            canvasGO.AddComponent<GraphicRaycaster>();

            // Dark vignette panel
            var panelGO = new GameObject("Panel");
            panelGO.transform.SetParent(canvasGO.transform, false);
            var panelRT = panelGO.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot     = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(480f * sf, 200f * sf);
            panelRT.anchoredPosition = new Vector2(0f, -60f * sf);
            var panelImg  = panelGO.AddComponent<Image>();
            panelImg.color = new Color(0f, 0f, 0f, 0.72f);

            // Status text (VICTORY / DEFEATED)
            var statusGO = new GameObject("Status");
            statusGO.transform.SetParent(panelGO.transform, false);
            var statusRT = statusGO.AddComponent<RectTransform>();
            statusRT.anchorMin        = new Vector2(0f, 1f);
            statusRT.anchorMax        = new Vector2(1f, 1f);
            statusRT.pivot            = new Vector2(0.5f, 1f);
            statusRT.anchoredPosition = new Vector2(0f, -20f * sf);
            statusRT.sizeDelta        = new Vector2(0f, 48f * sf);
            statusText                = statusGO.AddComponent<Text>();
            statusText.font           = ResolveFont();
            statusText.fontSize       = Mathf.RoundToInt(42 * sf);
            statusText.fontStyle      = FontStyle.Bold;
            statusText.alignment      = TextAnchor.MiddleCenter;
            statusText.color          = Color.white;
            var statusOutline         = statusGO.AddComponent<Outline>();
            statusOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            statusOutline.effectDistance = new Vector2(2f, -2f);

            // "Generating highlight…" label
            var genGO  = new GameObject("Generating");
            genGO.transform.SetParent(panelGO.transform, false);
            var genRT  = genGO.AddComponent<RectTransform>();
            genRT.anchorMin        = new Vector2(0f, 0.5f);
            genRT.anchorMax        = new Vector2(1f, 0.5f);
            genRT.pivot            = new Vector2(0.5f, 0.5f);
            genRT.anchoredPosition = new Vector2(0f, 10f * sf);
            genRT.sizeDelta        = new Vector2(0f, 28f * sf);
            var genTxt             = genGO.AddComponent<Text>();
            genTxt.font            = ResolveFont();
            genTxt.fontSize        = Mathf.RoundToInt(16 * sf);
            genTxt.alignment       = TextAnchor.MiddleCenter;
            genTxt.color           = new Color(0.75f, 0.75f, 0.75f, 0.9f);
            genTxt.text            = "Generating highlight reel…";

            // Watch Highlights button
            var btnGO  = new GameObject("WatchButton");
            btnGO.transform.SetParent(panelGO.transform, false);
            var btnRT  = btnGO.AddComponent<RectTransform>();
            btnRT.anchorMin        = new Vector2(0.5f, 0f);
            btnRT.anchorMax        = new Vector2(0.5f, 0f);
            btnRT.pivot            = new Vector2(0.5f, 0f);
            btnRT.anchoredPosition = new Vector2(0f, 20f * sf);
            btnRT.sizeDelta        = new Vector2(280f * sf, 48f * sf);
            var btnImg             = btnGO.AddComponent<Image>();
            btnImg.color           = new Color(0.12f, 0.9f, 1f, 0.95f);
            watchButton            = btnGO.AddComponent<Button>();
            var colors             = watchButton.colors;
            colors.highlightedColor = new Color(0.3f, 1f, 1f, 1f);
            colors.pressedColor     = new Color(0.05f, 0.6f, 0.8f, 1f);
            watchButton.colors      = colors;

            var btnLabelGO = new GameObject("Label");
            btnLabelGO.transform.SetParent(btnGO.transform, false);
            var btnLabelRT = btnLabelGO.AddComponent<RectTransform>();
            btnLabelRT.anchorMin = Vector2.zero;
            btnLabelRT.anchorMax = Vector2.one;
            btnLabelRT.offsetMin = btnLabelRT.offsetMax = Vector2.zero;
            watchLabel             = btnLabelGO.AddComponent<Text>();
            watchLabel.font        = ResolveFont();
            watchLabel.fontSize    = Mathf.RoundToInt(20 * sf);
            watchLabel.fontStyle   = FontStyle.Bold;
            watchLabel.alignment   = TextAnchor.MiddleCenter;
            watchLabel.color       = new Color(0.04f, 0.04f, 0.08f, 1f);
            watchLabel.text        = "Watch Highlights";

            canvas.gameObject.SetActive(false);
        }

        private static Font ResolveFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 20);
            return f;
        }

        [System.Serializable]
        private sealed class RecapUrlResponse
        {
            public string video_url;
            public string job_id;
            public bool   ready;
        }
    }
}
