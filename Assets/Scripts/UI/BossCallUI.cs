using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace KiForge.UI
{
    /// <summary>
    /// Full-screen phone-number entry screen that triggers an outbound Vapi call
    /// from the boss before the arena loads. Uses OnGUI (immediate-mode) so input
    /// works out of the box — no Canvas/RectTransform setup required.
    /// </summary>
    public sealed class BossCallUI : MonoBehaviour
    {
        private const string BackendBase = "http://127.0.0.1:8000";
        private const float  PollInterval = 2f;
        private const float  HardTimeout  = 120f;

        private static readonly Color BossRed   = new Color(1f,  0.18f, 0.45f, 1f);
        private static readonly Color BossRedDim = new Color(1f,  0.18f, 0.45f, 0.45f);
        private static readonly Color CardBg    = new Color(0.08f, 0.08f, 0.12f, 1f);
        private static readonly Color OverlayBg = new Color(0.04f, 0.04f, 0.06f, 0.97f);
        private static readonly Color FieldBg   = new Color(0.14f, 0.14f, 0.18f, 1f);
        private static readonly Color DimText   = new Color(1f,  1f,  1f,  0.55f);
        private static readonly Color MutedText = new Color(0.6f, 0.6f, 0.6f, 1f);

        private Action onComplete;
        private string playerId  = "demo_player";
        private string phone     = "+1";
        private string callId    = "";
        private string statusMsg = "";
        private bool   calling;
        private bool   callEnded;
        private float  pulse;

        // ------------------------------------------------------------------ //
        // Lazy-built GUIStyles (must happen inside OnGUI after skin is ready)
        // ------------------------------------------------------------------ //
        private bool       stylesReady;
        private GUIStyle   titleStyle, subtitleStyle, mutedStyle, fieldStyle,
                           btnStyle, errorStyle, callingStyle, cardStyle, skipStyle;
        private Texture2D  overlayTex, cardTex, fieldTex, btnTex, btnHoverTex, clearTex;

        // ------------------------------------------------------------------ //

        public void Setup(string _backendBase, string pid, Action onDone)
        {
            playerId   = pid;
            onComplete = onDone;
        }

        private void Update()
        {
            if (calling && !callEnded)
                pulse += Time.deltaTime * 2.5f;
        }

        // ------------------------------------------------------------------ //
        // Rendering
        // ------------------------------------------------------------------ //

        private void OnGUI()
        {
            EnsureStyles();

            float sw = Screen.width, sh = Screen.height;

            // Full-screen overlay
            GUI.DrawTexture(new Rect(0, 0, sw, sh), overlayTex);

            float cw = 900f, ch = calling ? 520f : 680f;
            float cx = (sw - cw) * 0.5f, cy = (sh - ch) * 0.5f;

            GUI.Box(new Rect(cx, cy, cw, ch), GUIContent.none, cardStyle);

            if (calling)
                RenderCallingState(cx, cy, cw);
            else
                RenderIdleState(cx, cy, cw);
        }

        private void RenderIdleState(float cx, float cy, float cw)
        {
            const float pad = 70f;
            float inner = cw - pad * 2f;
            float y = cy + 52f;

            GUI.Label(new Rect(cx + pad, y, inner, 88f), "KIFORGE ARENA", titleStyle);
            y += 96f;

            GUI.Label(new Rect(cx + pad, y, inner, 56f),
                "The boss wants to talk before the fight.", subtitleStyle);
            y += 70f;

            GUI.Label(new Rect(cx + pad, y, inner, 44f), "YOUR PHONE NUMBER", mutedStyle);
            y += 50f;

            GUI.SetNextControlName("PhoneField");
            phone = GUI.TextField(new Rect(cx + pad, y, inner, 96f), phone, 16, fieldStyle);
            y += 114f;

            if (!string.IsNullOrEmpty(statusMsg))
            {
                GUI.Label(new Rect(cx + pad, y, inner, 48f), statusMsg, errorStyle);
                y += 56f;
            }

            if (GUI.Button(new Rect(cx + pad + 80f, y, inner - 160f, 100f),
                    "CALL THE BOSS", btnStyle))
                OnCallClicked();

            // Focus the input field on first render
            if (Event.current.type == EventType.Layout)
                GUI.FocusControl("PhoneField");

            // Skip button — fixed to the bottom of the card so it stays out of the way
            float skipY = cy + (calling ? 520f : 680f) - 68f;
            if (GUI.Button(new Rect(cx + pad, skipY, inner, 48f),
                    "skip — enter arena without calling", skipStyle))
                SkipToArena();
        }

        private void RenderCallingState(float cx, float cy, float cw)
        {
            const float pad = 70f;
            float inner = cw - pad * 2f;

            if (callEnded)
            {
                GUI.Label(new Rect(cx + pad, cy + 210f, inner, 100f),
                    "ENTERING THE ARENA", titleStyle);
                return;
            }

            float alpha = Mathf.Lerp(0.35f, 1f, (Mathf.Sin(pulse) + 1f) * 0.5f);
            var prev = GUI.color;
            GUI.color = new Color(BossRed.r, BossRed.g, BossRed.b, alpha);
            GUI.Label(new Rect(cx + pad, cy + 110f, inner, 104f), "☎  INCOMING CALL", callingStyle);
            GUI.color = prev;

            GUI.Label(new Rect(cx + pad, cy + 250f, inner, 60f),
                "Answer your phone — the boss is calling.", subtitleStyle);
            GUI.Label(new Rect(cx + pad, cy + 318f, inner, 52f),
                "The fight starts when the call ends.", mutedStyle);
        }

        // ------------------------------------------------------------------ //
        // Networking
        // ------------------------------------------------------------------ //

        private void OnCallClicked()
        {
            if (!ValidPhone(phone))
            {
                statusMsg = "Enter a valid number — e.g. +12125551234";
                return;
            }
            statusMsg = "";
            calling   = true;
            StartCoroutine(TriggerCall(phone));
        }

        private IEnumerator TriggerCall(string ph)
        {
            var url  = $"{BackendBase}/boss-call";
            var json = $"{{\"phone\":\"{ph}\",\"player_id\":\"{playerId}\"}}";

            using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 12;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    calling   = false;
                    statusMsg = $"Backend unreachable: {req.error}";
                    yield break;
                }

                var resp = JsonUtility.FromJson<CallResponse>(req.downloadHandler.text);
                callId = resp?.call_id ?? "";

                if (string.IsNullOrEmpty(callId))
                {
                    calling   = false;
                    statusMsg = "No call ID returned from backend.";
                    yield break;
                }
            }

            // Mock mode (no VAPI key) — short dramatic pause then proceed
            if (callId.StartsWith("mock_"))
            {
                yield return new WaitForSeconds(3f);
                FinishCall();
                yield break;
            }

            StartCoroutine(PollStatus());
        }

        private IEnumerator PollStatus()
        {
            float elapsed = 0f;
            while (elapsed < HardTimeout)
            {
                yield return new WaitForSeconds(PollInterval);
                elapsed += PollInterval;

                using (var req = UnityWebRequest.Get($"{BackendBase}/boss-call/{callId}/status"))
                {
                    req.timeout = 5;
                    yield return req.SendWebRequest();

                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        var resp = JsonUtility.FromJson<StatusResponse>(req.downloadHandler.text);
                        if (resp != null && resp.status == "ended")
                        {
                            FinishCall();
                            yield break;
                        }
                    }
                }
            }
            FinishCall(); // hard timeout — start anyway
        }

        private void SkipToArena()
        {
            Destroy(gameObject);
            onComplete?.Invoke();
        }

        private void FinishCall()
        {
            callEnded = true;
            StartCoroutine(DelayThenStart());
        }

        private IEnumerator DelayThenStart()
        {
            yield return new WaitForSeconds(1.2f);
            Destroy(gameObject);
            onComplete?.Invoke();
        }

        // ------------------------------------------------------------------ //
        // Style bootstrap — called inside OnGUI after GUI.skin is available
        // ------------------------------------------------------------------ //

        private void EnsureStyles()
        {
            if (stylesReady) return;
            stylesReady = true;

            overlayTex  = Tex(OverlayBg);
            cardTex     = Tex(CardBg);
            fieldTex    = Tex(FieldBg);
            btnTex      = Tex(BossRed);
            btnHoverTex = Tex(new Color(1f, 0.35f, 0.6f, 1f));
            clearTex    = Tex(Color.clear);

            cardStyle = new GUIStyle { normal = { background = cardTex } };

            titleStyle = new GUIStyle
            {
                fontSize  = 72,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = BossRed },
            };

            subtitleStyle = new GUIStyle
            {
                fontSize  = 40,
                alignment = TextAnchor.MiddleCenter,
                wordWrap  = true,
                normal    = { textColor = DimText },
            };

            mutedStyle = new GUIStyle
            {
                fontSize  = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = MutedText },
            };

            fieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize  = 52,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white, background = fieldTex },
                focused   = { textColor = Color.white, background = fieldTex },
                hover     = { textColor = Color.white, background = fieldTex },
                active    = { textColor = Color.white, background = fieldTex },
            };

            btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 46,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white, background = btnTex },
                hover     = { textColor = Color.white, background = btnHoverTex },
                active    = { textColor = Color.white, background = btnTex },
                focused   = { textColor = Color.white, background = btnTex },
            };

            errorStyle = new GUIStyle
            {
                fontSize  = 34,
                alignment = TextAnchor.MiddleCenter,
                wordWrap  = true,
                normal    = { textColor = new Color(1f, 0.4f, 0.4f, 1f) },
            };

            callingStyle = new GUIStyle
            {
                fontSize  = 76,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = BossRed },
            };

            skipStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 28,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = MutedText, background = clearTex },
                hover     = { textColor = DimText,   background = clearTex },
                active    = { textColor = Color.white, background = clearTex },
                focused   = { textColor = MutedText,  background = clearTex },
            };
        }

        private static Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private static bool ValidPhone(string p)
        {
            if (string.IsNullOrEmpty(p) || !p.StartsWith("+") || p.Length < 8 || p.Length > 16)
                return false;
            foreach (var c in p.Substring(1))
                if (!char.IsDigit(c)) return false;
            return true;
        }

        [Serializable] private sealed class CallResponse  { public string call_id; public string status; }
        [Serializable] private sealed class StatusResponse { public string call_id; public string status; }
    }
}
