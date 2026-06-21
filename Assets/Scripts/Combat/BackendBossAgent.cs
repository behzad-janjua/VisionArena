using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace KiForge.Combat
{
    /// <summary>
    /// Network-backed boss brain. It asks the backend/Agentverse Battle Agent for
    /// decisions, caches the latest response, and falls back to the local agent if
    /// the endpoint is unavailable. This keeps the fight playable while making the
    /// sponsor-track agent part of the real combat loop.
    /// </summary>
    public sealed class BackendBossAgent : MonoBehaviour, IBossAgent
    {
        [SerializeField] private string endpoint = "http://127.0.0.1:8000/agent/combat";
        [SerializeField] private float requestTimeoutSeconds = 0.75f;
        [SerializeField] private bool useBackend = true;

        private readonly PlaceholderBossAgent fallback = new PlaceholderBossAgent();

        private BossDecision latestDecision;
        private BossDecisionContext latestContext;
        private bool hasBackendDecision;
        private bool requestInFlight;
        private int round = 1;

        public bool IsConnected { get; private set; }
        public string LastStatus { get; private set; } = "Agent idle";
        public string Endpoint => endpoint;

        public void Initialize(string agentEndpoint = null)
        {
            if (!string.IsNullOrWhiteSpace(agentEndpoint))
            {
                endpoint = agentEndpoint;
            }
        }

        public BossDecision DecideAction(BossDecisionContext context)
        {
            latestContext = context;

            if (useBackend && !requestInFlight)
            {
                StartCoroutine(RequestDecision(context));
            }

            if (hasBackendDecision)
            {
                return latestDecision;
            }

            var local = fallback.DecideAction(context);
            local.reasoning = $"Local fallback: {local.reasoning}";
            return local;
        }

        private IEnumerator RequestDecision(BossDecisionContext context)
        {
            requestInFlight = true;

            var payload = BuildPayload(context);
            var body = JsonUtility.ToJson(payload);
            using (var request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = Mathf.Max(1, Mathf.CeilToInt(requestTimeoutSeconds));

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = JsonUtility.FromJson<AgentResponsePayload>(request.downloadHandler.text);
                        latestDecision = MapResponse(response, latestContext);
                        hasBackendDecision = true;
                        IsConnected = true;
                        LastStatus = $"Agent: {response.boss_action} / {response.move_name}";
                    }
                    catch (Exception ex)
                    {
                        IsConnected = false;
                        LastStatus = $"Agent parse failed: {ex.Message}";
                        Debug.LogWarning($"[BackendBossAgent] Could not parse agent response: {ex.Message}");
                    }
                }
                else
                {
                    IsConnected = false;
                    LastStatus = $"Agent offline: {request.error}";
                    Debug.LogWarning($"[BackendBossAgent] Request failed: {request.error}");
                }
            }

            round++;
            requestInFlight = false;
        }

        private CombatTelemetryPayload BuildPayload(BossDecisionContext context)
        {
            var action = PlayerActionLabel(context);
            var chargeTime = action == "heavy_punch" || action == "very_heavy_punch" ? 2.4f : 0f;
            var bossHp = Mathf.RoundToInt(context.bossHealth01 * AttackTuning.MaxHealth);
            var playerHp = Mathf.RoundToInt(context.playerHealth01 * AttackTuning.MaxHealth);

            return new CombatTelemetryPayload
            {
                round = round,
                player_action = action,
                charge_time = chargeTime,
                accuracy = context.playerAttackedRecently ? 0.78f : 0.5f,
                damage_dealt_by_player = context.playerAttackedRecently ? AttackTuning.Damage(context.lastPlayerAttack) : 0,
                damage_dealt_by_boss = 0,
                boss_action = "unity_decision_request",
                boss_health_after = bossHp,
                player_health_after = playerHp,
                outcome = context.playerAttackedRecently ? "player_pressure" : "neutral"
            };
        }

        private static string PlayerActionLabel(BossDecisionContext context)
        {
            if (!context.playerAttackedRecently)
            {
                switch (context.playerStyle)
                {
                    case "heavy_puncher":
                        return "heavy_punch";
                    case "guard_turtle":
                        return "guard";
                    case "combo_puncher":
                        return "right_punch";
                    default: return "idle";
                }
            }

            switch (context.lastPlayerAttack)
            {
                case AttackType.Ultimate:
                    return "very_heavy_punch";
                case AttackType.HeavyPunch:
                    return "heavy_punch";
                case AttackType.VeryHeavyPunch:
                    return "very_heavy_punch";
                case AttackType.PunchLeft:
                    return "left_punch";
                case AttackType.PunchRight:
                    return "right_punch";
                case AttackType.KickLeft:
                case AttackType.KickRight:
                    return "right_punch";
                default:
                    return "idle";
            }
        }

        private static BossDecision MapResponse(AgentResponsePayload response, BossDecisionContext context)
        {
            var action = (response.boss_action ?? "pressure").ToLowerInvariant();
            var moveName = string.IsNullOrWhiteSpace(response.move_name) ? "Agent Counter" : response.move_name;
            var strategy = string.IsNullOrWhiteSpace(response.next_strategy) ? "Agent pressure" : response.next_strategy;
            var narration = string.IsNullOrWhiteSpace(response.narration) ? "Backend Battle Agent chose this counter." : response.narration;

            if (action.Contains("dodge"))
            {
                return Decision(BossActionKind.Dodge, AttackType.PunchRight, moveName, narration, strategy);
            }

            if (action.Contains("block"))
            {
                return new BossDecision
                {
                    kind = BossActionKind.Block,
                    attack = AttackType.PunchRight,
                    blockLeft = UnityEngine.Random.value < 0.5f,
                    moveName = moveName,
                    reasoning = narration,
                    nextStrategy = strategy
                };
            }

            if (action.Contains("heavy_counter"))
            {
                var heavy = context.bossHealth01 < 0.35f ? AttackType.HeavyPunch : AttackType.VeryHeavyPunch;
                return Decision(BossActionKind.Attack, heavy, moveName, narration, strategy);
            }

            if (action.Contains("jab"))
            {
                return Decision(BossActionKind.Attack, AttackType.PunchLeft, moveName, narration, strategy);
            }

            // pressure/default: fast close-range punch pressure.
            var attack = context.inRange ? AttackType.PunchRight : AttackType.HeavyPunch;
            return Decision(BossActionKind.Attack, attack, moveName, narration, strategy);
        }

        private static BossDecision Decision(BossActionKind kind, AttackType attack, string moveName, string reasoning, string strategy)
        {
            return new BossDecision
            {
                kind = kind,
                attack = attack,
                moveName = moveName,
                reasoning = reasoning,
                nextStrategy = strategy
            };
        }

        [Serializable]
        private sealed class CombatTelemetryPayload
        {
            public int round;
            public string player_action;
            public float charge_time;
            public float accuracy;
            public int damage_dealt_by_player;
            public int damage_dealt_by_boss;
            public string boss_action;
            public int boss_health_after;
            public int player_health_after;
            public string outcome;
        }

        [Serializable]
        private sealed class AgentResponsePayload
        {
            public string move_name;
            public string narration;
            public string boss_action;
            public string next_strategy;
            public float counter_success;
            public float survival_score;
        }
    }
}
