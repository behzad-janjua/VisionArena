using UnityEngine;

namespace KiForge.Combat
{
    public enum BossActionKind
    {
        Attack,
        Block,
        Dodge
    }

    /// <summary>
    /// Snapshot of the fight handed to the boss "brain" each decision tick. Mirrors the
    /// EnemyAgent input described in plan.md (player style, punch timing, health gap).
    /// </summary>
    public struct BossDecisionContext
    {
        public string playerStyle;
        public AttackType lastPlayerAttack;
        public bool playerAttackedRecently;
        public bool inRange;
        public float bossHealth01;
        public float playerHealth01;
    }

    /// <summary>
    /// The boss "brain" output. Mirrors the EnemyAgent output in plan.md
    /// (move_name, boss_reaction/reasoning, next_strategy).
    /// </summary>
    public struct BossDecision
    {
        public BossActionKind kind;
        public AttackType attack;
        public bool blockLeft;
        public string moveName;
        public string reasoning;
        public string nextStrategy;
    }

    /// <summary>
    /// The seam where a real Fetch.ai uAgent / ASI:One EnemyAgent would plug in. The game
    /// calls <see cref="DecideAction"/> every turn; a networked implementation would forward
    /// the context to the backend (backend/agents/enemy_agent.py) and await the response.
    /// </summary>
    public interface IBossAgent
    {
        BossDecision DecideAction(BossDecisionContext context);
    }

    /// <summary>
    /// Local placeholder for the EnemyAgent. It adapts <see cref="StrategyWeights"/> to the
    /// observed player style and weighted-picks an action. Swap this out for a networked
    /// agent client without touching <see cref="BossAgentController"/>.
    /// </summary>
    public sealed class PlaceholderBossAgent : IBossAgent
    {
        private readonly StrategyWeights weights = new StrategyWeights();
        private string lastStyle = "";

        public StrategyWeights Weights => weights;

        public BossDecision DecideAction(BossDecisionContext context)
        {
            // ---------------------------------------------------------------
            // PLACEHOLDER: a real EnemyAgent decision happens here. Replace the
            // weighted roll below with a call to the Fetch.ai agent over the
            // backend WebSocket and map its JSON response onto BossDecision.
            //   request  = context (player_style, punch timing, accuracy, health gap)
            //   response = { move_name, boss_reaction, next_strategy }
            // Local Fight Lab evals feed the strategy update; Arize export is optional.
            // ---------------------------------------------------------------
            weights.AdaptForStyle(context.playerStyle);
            lastStyle = context.playerStyle;

            // Out of range: close the distance rather than swing at air.
            if (!context.inRange)
            {
                return new BossDecision
                {
                    kind = BossActionKind.Attack,
                    attack = AttackType.KickRight,
                    moveName = "Closing Step",
                    reasoning = "Player out of reach; stepping in with punch pressure.",
                    nextStrategy = StrategyLabel(context.playerStyle)
                };
            }

            var roll = Random.value;
            var dodge = weights.dodge;
            var guard = dodge + weights.guard;
            var heavyCounter = guard + weights.heavyCounter;
            var jab = heavyCounter + weights.jab;
            // remaining probability mass (pressure) -> close-range punch pressure.

            if (roll < dodge)
            {
                return Decision(BossActionKind.Dodge, AttackType.PunchRight,
                    "Slip Counter", "Backing off to bait a whiff.", context.playerStyle);
            }

            if (roll < guard)
            {
                return new BossDecision
                {
                    kind = BossActionKind.Block,
                    blockLeft = Random.value < 0.5f,
                    moveName = "Iron Guard",
                    reasoning = "Reading an incoming strike; raising guard.",
                    nextStrategy = StrategyLabel(context.playerStyle)
                };
            }

            if (roll < heavyCounter)
            {
                var heavy = context.bossHealth01 < 0.4f ? AttackType.HeavyPunch : AttackType.VeryHeavyPunch;
                return Decision(BossActionKind.Attack, heavy,
                    "Heavy Counter", "Player guards too much; timing a heavy counter punch.", context.playerStyle);
            }

            if (roll < jab)
            {
                return Decision(BossActionKind.Attack, AttackType.PunchLeft,
                    "Check Jab", "Testing the player's timing with a quick jab.", context.playerStyle);
            }

            // Default: aggressive close-range punches.
            var pressure = PickPressureAttack();
            return Decision(BossActionKind.Attack, pressure,
                "Punch Pressure", "Closing in to deny the player room.", context.playerStyle);
        }

        private BossDecision Decision(BossActionKind kind, AttackType attack, string move, string reason, string style)
        {
            return new BossDecision
            {
                kind = kind,
                attack = attack,
                moveName = move,
                reasoning = reason,
                nextStrategy = StrategyLabel(style)
            };
        }

        private static AttackType PickPressureAttack()
        {
            var r = Random.value;
            if (r < 0.4f) return AttackType.PunchLeft;
            if (r < 0.8f) return AttackType.PunchRight;
            return AttackType.HeavyPunch;
        }

        private static string StrategyLabel(string style)
        {
            switch (style)
            {
                case "heavy_puncher": return "Pressure long punch windups";
                case "guard_turtle":  return "Bait guard, then heavy counter";
                case "combo_puncher": return "Dodge punch strings and jab back";
                default:                return "Probe and adapt";
            }
        }
    }
}
