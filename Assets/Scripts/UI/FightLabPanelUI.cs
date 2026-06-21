using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace KiForge.UI
{
    /// <summary>
    /// Tab-toggled overlay that shows live boss AI state from /demo/fight-lab.
    /// Top section: player style + boss strategy weights + adaptation.
    /// Bottom section: the latest deterministic local eval, optionally exported to Arize.
    /// </summary>
    public sealed class FightLabPanelUI : MonoBehaviour
    {
        private const string FightLabUrl = "http://127.0.0.1:8000/demo/fight-lab?player_id=demo_player";
        private const float PollInterval  = 2f;

        private GameObject panel;
        private Text aiStatsText;
        private Text traceText;
        private bool visible;
        private float pollTimer;

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Tab))
                SetVisible(!visible);

            if (visible)
            {
                pollTimer -= Time.deltaTime;
                if (pollTimer <= 0f)
                {
                    pollTimer = PollInterval;
                    StartCoroutine(FetchAndRefresh());
                }
            }
        }

        public void Setup()
        {
            BuildUI();
            SetVisible(false);
        }

        private void SetVisible(bool show)
        {
            visible = show;
            panel.SetActive(show);
            if (show)
            {
                pollTimer = 0f; // immediate fetch on open
            }
        }

        private IEnumerator FetchAndRefresh()
        {
            using var req = UnityWebRequest.Get(FightLabUrl);
            req.timeout = 3;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                aiStatsText.text = "Backend offline — start the FastAPI server.";
                traceText.text   = "";
                yield break;
            }

            ParseAndDisplay(req.downloadHandler.text);
        }

        // --- Parsing --- //
        // JsonUtility can't deserialize dictionaries, so we extract string values
        // with a simple key-search helper to avoid a Newtonsoft dependency.

        private void ParseAndDisplay(string json)
        {
            var style         = Extract(json, "player_style");
            var move          = Extract(json, "most_common_player_move");
            var adapt         = Extract(json, "boss_adaptation");
            var beforeRaw     = Extract(json, "boss_counter_success_before");
            var afterRaw      = Extract(json, "boss_counter_success_after");

            // Strategy weights live inside the "strategy_weights" sub-object.
            var weightsRaw = ExtractBlock(json, "strategy_weights");
            var pressure   = Extract(weightsRaw, "pressure");
            var dodge      = Extract(weightsRaw, "dodge");
            var jab        = Extract(weightsRaw, "jab");
            var guard      = Extract(weightsRaw, "guard");
            var heavy      = Extract(weightsRaw, "heavy_counter");

            aiStatsText.text =
                $"PLAYER STYLE:  {style}\n" +
                $"TOP MOVE:      {move}\n" +
                $"BOSS ADAPT:    {Truncate(adapt, 80)}\n\n" +
                $"LOCAL EVAL  counter_success:  baseline {Pct(beforeRaw)}  ->  adapted {Pct(afterRaw)}\n\n" +
                $"STRATEGY WEIGHTS\n" +
                $"  Pressure {Pct(pressure)}  Dodge {Pct(dodge)}  Jab {Pct(jab)}" +
                $"  Guard {Pct(guard)}  Heavy {Pct(heavy)}";

            // Fight Lab trace section
            var traceRaw = ExtractBlock(json, "last_trace");
            if (string.IsNullOrEmpty(traceRaw) || traceRaw == "{}" || traceRaw == "{ }")
            {
                traceText.text = "No Fight Lab trace yet - land a hit to record a local eval.";
            }
            else
            {
                var tEvt      = Extract(traceRaw, "event");
                var tRound    = Extract(traceRaw, "round");
                var tOutcome  = Extract(traceRaw, "outcome");
                var tCounter  = Extract(traceRaw, "counter_success");
                var tMode     = Extract(traceRaw, "learning_mode");
                var tBoss     = Extract(traceRaw, "boss_action");
                traceText.text =
                    $"LAST FIGHT LAB EVAL  [{tMode}]\n" +
                    $"  Round {tRound}  player: {tEvt}  boss: {tBoss}\n" +
                    $"  Outcome: {tOutcome}  counter_success: {tCounter}";
            }
        }

        // Extracts the string/number value for a given JSON key (flat only).
        private static string Extract(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return "—";
            var needle = $"\"{key}\"";
            var idx = json.IndexOf(needle);
            if (idx < 0) return "—";
            idx += needle.Length;
            // skip whitespace and colon
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == ':')) idx++;
            if (idx >= json.Length) return "—";

            if (json[idx] == '"')
            {
                var end = json.IndexOf('"', idx + 1);
                return end < 0 ? "—" : json.Substring(idx + 1, end - idx - 1);
            }
            // number
            var numEnd = idx;
            while (numEnd < json.Length && json[numEnd] != ',' && json[numEnd] != '}' && json[numEnd] != '\n')
                numEnd++;
            return json.Substring(idx, numEnd - idx).Trim();
        }

        // Extracts the raw JSON block for a nested object key.
        private static string ExtractBlock(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return "";
            var needle = $"\"{key}\"";
            var idx    = json.IndexOf(needle);
            if (idx < 0) return "";
            idx += needle.Length;
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == ':')) idx++;
            if (idx >= json.Length || json[idx] != '{') return "";
            var depth = 0;
            var start = idx;
            for (var i = idx; i < json.Length; i++)
            {
                if (json[i] == '{') depth++;
                else if (json[i] == '}') { depth--; if (depth == 0) return json.Substring(start, i - start + 1); }
            }
            return "";
        }

        private static string Pct(string raw)
        {
            if (float.TryParse(raw, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var v))
                return $"{v * 100f:0}%";
            return "—";
        }

        private static string Truncate(string s, int max)
            => s != null && s.Length > max ? s.Substring(0, max) + "…" : s ?? "—";

        // --- UI construction --- //

        private void BuildUI()
        {
            var canvasGO = new GameObject("FightLab Canvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution  = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight   = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // Main panel — centred
            panel = new GameObject("Panel");
            panel.transform.SetParent(canvasGO.transform, false);
            var panelRT        = panel.AddComponent<RectTransform>();
            panelRT.anchorMin  = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax  = new Vector2(0.5f, 0.5f);
            panelRT.pivot      = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta  = new Vector2(780f, 460f);
            panelRT.anchoredPosition = Vector2.zero;

            // Background
            var bg      = panel.AddComponent<Image>();
            bg.color    = new Color(0.04f, 0.04f, 0.08f, 0.93f);

            // Title bar strip
            var titleGO = MakeChild(panel.transform, "TitleBar", 0f, 420f, 780f, 40f);
            titleGO.AddComponent<Image>().color = new Color(0f, 0.55f, 1f, 0.85f);
            var titleLblGO = MakeChild(titleGO.transform, "TitleLabel", 0f, 0f, 780f, 40f);
            var titleLbl = AddText(titleLblGO, 18, FontStyle.Bold, TextAnchor.MiddleCenter);
            titleLbl.text  = "AI FIGHT LAB  [Tab to close]";
            titleLbl.color = Color.white;

            // Hint: local eval is always present; Arize export is optional.
            var hintGO  = MakeChild(panel.transform, "Hint", 0f, 400f, 780f, 20f);
            var hintTxt = AddText(hintGO, 12, FontStyle.Normal, TextAnchor.MiddleRight);
            hintTxt.text  = "source: deterministic local eval + optional Arize export  ";
            hintTxt.color = new Color(0.5f, 0.7f, 1f, 0.7f);

            // Divider
            var divGO   = MakeChild(panel.transform, "Div", 0f, 180f, 760f, 1f);
            divGO.AddComponent<Image>().color = new Color(0.2f, 0.4f, 0.8f, 0.5f);

            // AI Stats section (top half)
            var aiGO  = MakeChild(panel.transform, "AIStats", 10f, 195f, 760f, 200f);
            aiStatsText        = AddText(aiGO, 15, FontStyle.Normal, TextAnchor.UpperLeft);
            aiStatsText.color  = new Color(0.85f, 0.95f, 1f, 1f);
            aiStatsText.text   = "Connecting to backend…";
            aiStatsText.horizontalOverflow = HorizontalWrapMode.Overflow;
            aiStatsText.verticalOverflow   = VerticalWrapMode.Overflow;

            // Trace section (bottom half)
            var traceGO = MakeChild(panel.transform, "Trace", 10f, 20f, 760f, 155f);
            traceText        = AddText(traceGO, 14, FontStyle.Normal, TextAnchor.UpperLeft);
            traceText.color  = new Color(0.6f, 0.9f, 0.6f, 1f);
            traceText.text   = "";
            traceText.horizontalOverflow = HorizontalWrapMode.Overflow;
            traceText.verticalOverflow   = VerticalWrapMode.Overflow;
        }

        private static GameObject MakeChild(Transform parent, string name,
            float xPad, float yAnchor, float w, float h)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt  = go.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 0f);
            rt.anchorMax        = new Vector2(0f, 0f);
            rt.pivot            = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(xPad, yAnchor);
            rt.sizeDelta        = new Vector2(w, h);
            return go;
        }

        private static Text AddText(GameObject go, int size, FontStyle style, TextAnchor anchor)
        {
            var txt   = go.AddComponent<Text>();
            txt.font  = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                     ?? Font.CreateDynamicFontFromOSFont("Arial", size);
            txt.fontSize  = size;
            txt.fontStyle = style;
            txt.alignment = anchor;
            return txt;
        }
    }
}
