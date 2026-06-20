using System;
using UnityEngine;

namespace KiForge.Shared
{
    public static class KiForgeEventNames
    {
        public const string ChargeStart = "CHARGE_START";
        public const string ChargeUpdate = "CHARGE_UPDATE";
        public const string BlastRelease = "BLAST_RELEASE";
        public const string ShieldStart = "SHIELD_START";
        public const string ShieldEnd = "SHIELD_END";
        public const string SlashLeft = "SLASH_LEFT";
        public const string SlashRight = "SLASH_RIGHT";
        public const string Ultimate = "ULTIMATE";
        public const string PoseUpdate = "POSE_UPDATE";
        public const string CombatTelemetry = "COMBAT_TELEMETRY";
        public const string AgentResponse = "AGENT_RESPONSE";

        public static string FromGestureType(GestureEventType type)
        {
            switch (type)
            {
                case GestureEventType.ChargeStart:
                    return ChargeStart;
                case GestureEventType.ChargeUpdate:
                    return ChargeUpdate;
                case GestureEventType.BlastRelease:
                    return BlastRelease;
                case GestureEventType.ShieldStart:
                    return ShieldStart;
                case GestureEventType.ShieldEnd:
                    return ShieldEnd;
                case GestureEventType.SlashLeft:
                    return SlashLeft;
                case GestureEventType.SlashRight:
                    return SlashRight;
                case GestureEventType.Ultimate:
                    return Ultimate;
                default:
                    return string.Empty;
            }
        }
    }

    public enum GestureEventType
    {
        ChargeStart,
        ChargeUpdate,
        BlastRelease,
        ShieldStart,
        ShieldEnd,
        SlashLeft,
        SlashRight,
        Ultimate
    }

    public enum PlayerActionType
    {
        None,
        ChargedBlast,
        Shield,
        SlashLeft,
        SlashRight,
        Ultimate
    }

    [Serializable]
    public struct GestureEvent
    {
        public GestureEventType type;
        public float charge;
        public float holdSeconds;
        public float emgIntensity;
        public Vector2 origin;
        public Vector2 aim;
        public double timestamp;

        public static GestureEvent Create(GestureEventType type, Vector2 origin, Vector2 aim, float charge = 0f, float holdSeconds = 0f)
        {
            return new GestureEvent
            {
                type = type,
                origin = origin,
                aim = aim.sqrMagnitude > 0.001f ? aim.normalized : Vector2.right,
                charge = Mathf.Clamp01(charge),
                holdSeconds = Mathf.Max(0f, holdSeconds),
                emgIntensity = 1f,
                timestamp = Time.realtimeSinceStartupAsDouble
            };
        }
    }

    [Serializable]
    public struct PoseEvent
    {
        public Vector2 wrist;
        public Vector2 bodyCenter;
        public Vector2 aim;
        public float confidence;
        public double timestamp;
    }

    [Serializable]
    public struct CombatTelemetryEvent
    {
        public int round;
        public PlayerActionType playerAction;
        public float chargeTime;
        public float accuracy;
        public int damageDealtByPlayer;
        public int damageDealtByBoss;
        public string bossAction;
        public int bossHealthAfter;
        public int playerHealthAfter;
        public string outcome;
    }

    [Serializable]
    public struct AgentResponseEvent
    {
        public string moveName;
        public string narration;
        public string bossAction;
        public string nextStrategy;
        public string recapPrompt;
        public float counterSuccess;
        public float survivalScore;
    }
}
