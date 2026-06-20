using System.Collections.Generic;
using KiForge.Shared;
using UnityEngine;

namespace KiForge.Telemetry
{
    public sealed class MatchTelemetryRecorder : MonoBehaviour
    {
        private readonly List<CombatTelemetryEvent> events = new List<CombatTelemetryEvent>();
        private KiForgeEventBus eventBus;

        public IReadOnlyList<CombatTelemetryEvent> Events => events;

        public void Initialize(KiForgeEventBus bus)
        {
            eventBus = bus;
        }

        public void Record(CombatTelemetryEvent evt)
        {
            events.Add(evt);
        }

        public string InferPlayerStyle()
        {
            var blasts = 0;
            var shields = 0;
            var slashes = 0;
            var totalCharge = 0f;

            foreach (var evt in events)
            {
                if (evt.playerAction == PlayerActionType.ChargedBlast)
                {
                    blasts++;
                    totalCharge += evt.chargeTime;
                }
                else if (evt.playerAction == PlayerActionType.Shield)
                {
                    shields++;
                }
                else if (evt.playerAction == PlayerActionType.SlashLeft || evt.playerAction == PlayerActionType.SlashRight)
                {
                    slashes++;
                }
            }

            if (blasts > 0 && totalCharge / blasts >= 2.2f)
            {
                return "patient_charger";
            }

            if (shields >= Mathf.Max(2, slashes + blasts))
            {
                return "shield_turtle";
            }

            if (slashes >= Mathf.Max(2, blasts))
            {
                return "slash_spammer";
            }

            return "balanced";
        }
    }
}
