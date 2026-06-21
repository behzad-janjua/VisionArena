using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace KiForge.UI
{
    /// <summary>
    /// Always-on sponsor panel for the Redis-backed memory surface.
    /// Shows the live data exposed by /demo/redis without requiring a prefab.
    /// </summary>
    public sealed class RedisSponsorPanelUI : MonoBehaviour
    {
        private const string BaseUrl = "http://127.0.0.1:8000/demo/redis";
        private const float PollInterval = 2f;

        private string playerId = "demo_player";
        private GameObject panel;
        private Text bodyText;
        private bool visible = true;
        private float pollTimer;

        public void Setup(string id = "demo_player")
        {
            playerId = string.IsNullOrWhiteSpace(id) ? "demo_player" : id;
            BuildUI();
            SetVisible(true);
            pollTimer = 0f;
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.R))
                SetVisible(!visible);

            if (!visible)
                return;

            pollTimer -= Time.deltaTime;
            if (pollTimer <= 0f)
            {
                pollTimer = PollInterval;
                StartCoroutine(FetchAndRefresh());
            }
        }

        private void SetVisible(bool show)
        {
            visible = show;
            if (panel != null)
                panel.SetActive(show);
        }

        private IEnumerator FetchAndRefresh()
        {
            using var req = UnityWebRequest.Get($"{BaseUrl}?player_id={UnityWebRequest.EscapeURL(playerId)}");
            req.timeout = 3;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                bodyText.text = "REDIS MEMORY\nBackend offline - start FastAPI on :8000";
                yield break;
            }

            ParseAndDisplay(req.downloadHandler.text);
        }

        private void ParseAndDisplay(string json)
        {
            var backend = Extract(json, "backend");
            var memory = Extract(json, "using_memory_store");
            var vector = Extract(json, "vector_search");
            var phase = Extract(json, "boss_phase");
            var cooldowns = Compact(ExtractBlock(json, "active_cooldowns"), 88);
            var moves = Compact(ExtractArray(json, "move_names"), 88);
            var recall = Compact(ExtractArray(json, "memory_recall"), 88);
            var stream = ExtractArray(json, "match_stream");
            var streamCount = CountArrayItems(stream);

            bodyText.text =
                "REDIS MEMORY  [R]\n" +
                $"backend: {backend}  fallback: {memory}  vector: {vector}\n" +
                $"boss phase: {phase}  stream events: {streamCount}\n" +
                $"cooldowns: {cooldowns}\n" +
                $"move names: {moves}\n" +
                $"recall: {recall}";
        }

        private static string Extract(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return "-";
            var idx = FindValueStart(json, key);
            if (idx < 0 || idx >= json.Length) return "-";

            if (json[idx] == '"')
            {
                var end = json.IndexOf('"', idx + 1);
                return end < 0 ? "-" : json.Substring(idx + 1, end - idx - 1);
            }

            var endIdx = idx;
            while (endIdx < json.Length && json[endIdx] != ',' && json[endIdx] != '}' && json[endIdx] != '\n')
                endIdx++;
            return json.Substring(idx, endIdx - idx).Trim();
        }

        private static string ExtractBlock(string json, string key) => ExtractDelimited(json, key, '{', '}');
        private static string ExtractArray(string json, string key) => ExtractDelimited(json, key, '[', ']');

        private static string ExtractDelimited(string json, string key, char open, char close)
        {
            var idx = FindValueStart(json, key);
            if (idx < 0 || idx >= json.Length || json[idx] != open)
                return "";

            var depth = 0;
            var inString = false;
            for (var i = idx; i < json.Length; i++)
            {
                if (json[i] == '"' && (i == 0 || json[i - 1] != '\\'))
                    inString = !inString;
                if (inString)
                    continue;

                if (json[i] == open) depth++;
                else if (json[i] == close)
                {
                    depth--;
                    if (depth == 0)
                        return json.Substring(idx, i - idx + 1);
                }
            }
            return "";
        }

        private static int FindValueStart(string json, string key)
        {
            var needle = $"\"{key}\"";
            var idx = json.IndexOf(needle);
            if (idx < 0) return -1;
            idx += needle.Length;
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == ':')) idx++;
            return idx;
        }

        private static string Compact(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value == "{}" || value == "[]")
                return "none";
            value = value.Replace("\"", "").Replace("{", "").Replace("}", "").Replace("[", "").Replace("]", "");
            value = value.Replace(",", ", ");
            while (value.Contains("  "))
                value = value.Replace("  ", " ");
            return value.Length > max ? value.Substring(0, max) + "..." : value;
        }

        private static int CountArrayItems(string arrayJson)
        {
            if (string.IsNullOrEmpty(arrayJson) || arrayJson == "[]")
                return 0;

            var count = 0;
            var depth = 0;
            var inString = false;
            for (var i = 0; i < arrayJson.Length; i++)
            {
                if (arrayJson[i] == '"' && (i == 0 || arrayJson[i - 1] != '\\'))
                    inString = !inString;
                if (inString)
                    continue;
                if (arrayJson[i] == '{')
                {
                    if (depth == 0) count++;
                    depth++;
                }
                else if (arrayJson[i] == '}')
                {
                    depth--;
                }
            }
            return count;
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("Redis Sponsor Canvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 18;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            panel = new GameObject("Panel");
            panel.transform.SetParent(canvasGO.transform, false);
            var rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-22f, -78f);
            rt.sizeDelta = new Vector2(560f, 170f);

            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.08f, 0.06f, 0.88f);

            var textGO = new GameObject("Body");
            textGO.transform.SetParent(panel.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(14f, 10f);
            textRT.offsetMax = new Vector2(-14f, -10f);

            bodyText = textGO.AddComponent<Text>();
            bodyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                            ?? Font.CreateDynamicFontFromOSFont("Arial", 14);
            bodyText.fontSize = 14;
            bodyText.alignment = TextAnchor.UpperLeft;
            bodyText.color = new Color(0.78f, 1f, 0.86f, 1f);
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Truncate;
            bodyText.text = "REDIS MEMORY\nConnecting...";
        }
    }
}
