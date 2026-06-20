using System;
using System.Collections.Generic;
using KiForge.Combat;
using KiForge.Input;
using UnityEngine;

namespace KiForge.Telemetry
{
    /// <summary>
    /// Arize-style fight lab + coaching feedback loop. Every player action and outcome is
    /// "traced", scored against simple evals, and turned into a coaching tip that helps the
    /// player improve. The loop is: player acts -> traces/evals -> tip surfaces -> player adapts.
    ///
    /// PLACEHOLDER: in the full build these traces/evals are emitted to Arize Phoenix
    /// (see backend/fight_tracing.py + fight_lab.py) and the coaching tips are returned by the
    /// CoachAgent (backend/agents/coach_agent.py). Here we compute them locally so the loop is
    /// demoable without the backend. The public surface (PlayerStyle, CurrentTip, eval scores)
    /// is what the boss AI and HUD consume, and would be unchanged by a real integration.
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

        public string PlayerStyle { get; private set; } = "balanced";
        public string CurrentTip { get; private set; } = "Land hits, mix your moves, and block to survive.";

        // Arize-style eval scores (0..1).
        public float Aggression { get; private set; }
        public float Accuracy { get; private set; }
        public float Variety { get; private set; }
        public float Defense { get; private set; }

        /// <summary>Raised whenever the coaching tip changes, so a HUD could surface it.</summary>
        public event Action<string> TipChanged;

        public void Initialize(PlayerAttackInput input, Func<BossDecision> bossDecision)
        {
            bossDecisionProvider = bossDecision;
            if (input != null)
            {
                input.AttackThrown += OnAttackThrown;
                input.Blocked += _ => { blocksRaised++; Recompute(); };
            }
        }

        private void OnAttackThrown(AttackType type)
        {
            totalAttempts++;
            attempts.TryGetValue(type, out var c);
            attempts[type] = c + 1;
            Recompute();
        }

        /// <summary>Called from the combat resolver when a player strike connects.</summary>
        public void RecordPlayerLanded(AttackType type, bool blockedByBoss)
        {
            if (blockedByBoss)
            {
                blockedLands++;
            }
            else
            {
                cleanLands++;
            }

            Recompute();
        }

        /// <summary>Called when the player takes a hit (for the defense eval).</summary>
        public void RecordPlayerTookHit()
        {
            hitsTaken++;
            Recompute();
        }

        private void Recompute()
        {
            // --- Eval scoring (the part Arize would compute from traces) ---
            Aggression = Mathf.Clamp01(totalAttempts / 20f);
            Accuracy = totalAttempts > 0 ? (float)cleanLands / totalAttempts : 0f;
            Variety = totalAttempts > 0 ? attempts.Count / 7f : 0f; // 7 attack types
            var defenseEvents = blocksRaised + hitsTaken;
            Defense = defenseEvents > 0 ? (float)blocksRaised / defenseEvents : 0f;

            PlayerStyle = InferStyle();
            UpdateTip();
        }

        private string InferStyle()
        {
            var heavy = Count(AttackType.HeavyPunch) + Count(AttackType.VeryHeavyPunch) + Count(AttackType.Ultimate);
            var kicks = Count(AttackType.KickLeft) + Count(AttackType.KickRight);

            if (blocksRaised >= Mathf.Max(3, totalAttempts))
            {
                return "shield_turtle";
            }

            if (totalAttempts >= 4 && heavy >= totalAttempts * 0.5f)
            {
                return "patient_charger";
            }

            if (kicks >= Mathf.Max(3, totalAttempts * 0.6f))
            {
                return "slash_spammer";
            }

            return "balanced";
        }

        private void UpdateTip()
        {
            var tip = BuildTip();
            if (tip != CurrentTip)
            {
                CurrentTip = tip;
                TipChanged?.Invoke(tip);
            }
        }

        private string BuildTip()
        {
            // Coaching aimed at the player's weakest dimension.
            if (totalAttempts >= 4 && Variety < 0.4f)
            {
                return "You're predictable — mix punches (J/K), kicks (U/I) and heavies.";
            }

            if (totalAttempts >= 4 && Accuracy < 0.4f)
            {
                return "The boss is reading you. Strike right after it commits or whiffs.";
            }

            if (totalAttempts >= 5 && blocksRaised == 0)
            {
                return "Hold ; or ' to block — you're taking free damage.";
            }

            if (totalAttempts >= 5 && Count(AttackType.HeavyPunch) + Count(AttackType.VeryHeavyPunch) == 0)
            {
                return "Hold the MYO fist (B) 2s+ for a heavy, 4s+ for a very-heavy punch.";
            }

            return "Solid pressure. Keep landing clean hits and punish the boss's guard.";
        }

        private int Count(AttackType type)
        {
            attempts.TryGetValue(type, out var c);
            return c;
        }

        private void OnGUI()
        {
            const int w = 320;
            var rect = new Rect(12, 12, w, 150);
            GUI.Box(rect, "AI FIGHT LAB  (Arize feedback)");

            var style = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 12 };
            var y = 34f;
            GUI.Label(new Rect(20, y, w - 16, 20), $"Player style: {PlayerStyle}", style); y += 18;
            GUI.Label(new Rect(20, y, w - 16, 20),
                $"acc {Accuracy:0.00}  var {Variety:0.00}  def {Defense:0.00}  agg {Aggression:0.00}", style); y += 18;

            if (bossDecisionProvider != null)
            {
                var d = bossDecisionProvider();
                if (!string.IsNullOrEmpty(d.nextStrategy))
                {
                    GUI.Label(new Rect(20, y, w - 16, 20), $"Boss strategy: {d.nextStrategy}", style); y += 18;
                }
            }

            GUI.Label(new Rect(20, y, w - 16, 60), $"Coach: {CurrentTip}", style);
        }
    }
}
