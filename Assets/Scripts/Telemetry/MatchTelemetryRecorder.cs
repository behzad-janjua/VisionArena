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
            var heavyPunches = 0;
            var guards = 0;
            var comboPunches = 0;
            var totalCharge = 0f;

            foreach (var evt in events)
            {
                if (evt.playerAction == PlayerActionType.HeavyPunch || evt.playerAction == PlayerActionType.VeryHeavyPunch)
                {
                    heavyPunches++;
                    totalCharge += evt.chargeTime;
                }
                else if (evt.playerAction == PlayerActionType.Guard)
                {
                    guards++;
                }
                else if (evt.playerAction == PlayerActionType.LeftPunch || evt.playerAction == PlayerActionType.RightPunch)
                {
                    comboPunches++;
                }
            }

            if (heavyPunches > 0 && totalCharge / heavyPunches >= 2.2f)
            {
                return "heavy_puncher";
            }

            if (guards >= Mathf.Max(2, comboPunches + heavyPunches))
            {
                return "guard_turtle";
            }

            if (comboPunches >= Mathf.Max(2, heavyPunches))
            {
                return "combo_puncher";
            }

            return "balanced";
        }
    }
}
