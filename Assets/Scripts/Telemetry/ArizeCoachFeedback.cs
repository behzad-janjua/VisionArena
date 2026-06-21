using System;
using System.Collections.Generic;
using KiForge.Combat;
using KiForge.Input;
using UnityEngine;
using UnityEngine.UI;

namespace KiForge.Telemetry
{
    /// <summary>
    /// Arize-style fight lab + coaching feedback loop displayed as a Canvas panel
    /// mirroring the Redis sponsor panel on the opposite (top-left) side of the screen.
    /// </summary>
    public sealed class ArizeCoachFeedback : MonoBehaviour
    {
        private readonly Dictionary<AttackType, int> attempts = new Dictionary<AttackType, int>();
        private int totalAttempts;
        private int cleanLands;
        private int blockedLands;
        private int blocksRaised;
        private int hitsTaken;

        private Func<BossDecision> bossDecisionProvider;
        private Text bodyText;
        private string lastBossStrategy = "";

        public string PlayerStyle { get; private set; } = "balanced";
        public string CurrentTip  { get; private set; } = "Land hits, mix your moves, and block to survive.";

        public float Aggression { get; private set; }
        public float Accuracy   { get; private set; }
        public float Variety    { get; private set; }
        public float Defense    { get; private set; }

        public event Action<string> TipChanged;

        public void Initialize(PlayerAttackInput input, Func<BossDecision> bossDecision)
        {
            bossDecisionProvider = bossDecision;
            BuildUI();
            RefreshText();

            if (input != null)
            {
                input.AttackThrown += OnAttackThrown;
                input.Blocked      += _ => { blocksRaised++; Recompute(); };
            }
        }

        private void Update()
        {
            if (bossDecisionProvider == null) return;
            var strategy = bossDecisionProvider().nextStrategy ?? "";
            if (strategy != lastBossStrategy)
            {
                lastBossStrategy = strategy;
                RefreshText();
            }
        }

        private void OnAttackThrown(AttackType type)
        {
            totalAttempts++;
            attempts.TryGetValue(type, out var c);
            attempts[type] = c + 1;
            Recompute();
        }

        public void RecordPlayerLanded(AttackType type, bool blockedByBoss)
        {
            if (blockedByBoss) blockedLands++; else cleanLands++;
            Recompute();
        }

        public void RecordPlayerTookHit()
        {
            hitsTaken++;
            Recompute();
        }

        private void Recompute()
        {
            Aggression = Mathf.Clamp01(totalAttempts / 20f);
            Accuracy   = totalAttempts > 0 ? (float)cleanLands / totalAttempts : 0f;
            Variety    = totalAttempts > 0 ? attempts.Count / 7f : 0f;
            var defenseEvents = blocksRaised + hitsTaken;
            Defense    = defenseEvents > 0 ? (float)blocksRaised / defenseEvents : 0f;

            PlayerStyle = InferStyle();
            var tip = BuildTip();
            if (tip != CurrentTip)
            {
                CurrentTip = tip;
                TipChanged?.Invoke(tip);
            }
            RefreshText();
        }

        private void RefreshText()
        {
            if (bodyText == null) return;
            var strategy = string.IsNullOrEmpty(lastBossStrategy) ? "-" : lastBossStrategy;
            bodyText.text =
                "COACH\n" +
                $"style: {PlayerStyle}   boss reading: {strategy}\n" +
                $"acc {Accuracy:0.00}  var {Variety:0.00}  def {Defense:0.00}  agg {Aggression:0.00}\n" +
                $"tip: {CurrentTip}";
        }

        private string InferStyle()
        {
            var heavy       = Count(AttackType.HeavyPunch) + Count(AttackType.VeryHeavyPunch) + Count(AttackType.Ultimate);
            var quickPunches = Count(AttackType.PunchLeft) + Count(AttackType.PunchRight);

            if (blocksRaised >= Mathf.Max(3, totalAttempts))          return "guard_turtle";
            if (totalAttempts >= 4 && heavy >= totalAttempts * 0.5f)  return "heavy_puncher";
            if (quickPunches >= Mathf.Max(3, totalAttempts * 0.6f))   return "combo_puncher";
            return "balanced";
        }

        private string BuildTip()
        {
            if (totalAttempts >= 4 && Variety < 0.4f)
                return "Mix quick punches (J/K), heavy (U), very-heavy (I).";
            if (totalAttempts >= 4 && Accuracy < 0.4f)
                return "Strike right after the boss commits or whiffs.";
            if (totalAttempts >= 5 && blocksRaised == 0)
                return "Hold ; or ' to block — you're taking free damage.";
            if (totalAttempts >= 5 && Count(AttackType.HeavyPunch) + Count(AttackType.VeryHeavyPunch) == 0)
                return "Hold B for 2s+ (heavy) or 4s+ (very-heavy punch).";
            return "Solid pressure. Keep landing clean hits.";
        }

        private int Count(AttackType type)
        {
            attempts.TryGetValue(type, out var c);
            return c;
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("Coach Canvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 18;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // Panel — top-left, mirroring the Redis panel which sits at top-right
            var panel = new GameObject("Panel");
            panel.transform.SetParent(canvasGO.transform, false);
            var rt        = panel.AddComponent<RectTransform>();
            rt.anchorMin  = new Vector2(0f, 1f);
            rt.anchorMax  = new Vector2(0f, 1f);
            rt.pivot      = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(22f, -78f);
            rt.sizeDelta  = new Vector2(560f, 170f);

            var bg   = panel.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.04f, 0.10f, 0.88f);

            var textGO = new GameObject("Body");
            textGO.transform.SetParent(panel.transform, false);
            var textRT        = textGO.AddComponent<RectTransform>();
            textRT.anchorMin  = Vector2.zero;
            textRT.anchorMax  = Vector2.one;
            textRT.offsetMin  = new Vector2(14f,  10f);
            textRT.offsetMax  = new Vector2(-14f, -10f);

            bodyText                   = textGO.AddComponent<Text>();
            bodyText.font              = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                                      ?? Font.CreateDynamicFontFromOSFont("Arial", 14);
            bodyText.fontSize          = 14;
            bodyText.alignment         = TextAnchor.UpperLeft;
            bodyText.color             = new Color(0.78f, 0.90f, 1f, 1f);
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow   = VerticalWrapMode.Truncate;
        }
    }
}
