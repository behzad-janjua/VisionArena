using KiForge.Shared;
using UnityEngine;

namespace KiForge.Telemetry
{
    public sealed class MockAgentClient : MonoBehaviour
    {
        [SerializeField] private bool mockMode = true;

        private KiForgeEventBus eventBus;
        private MatchTelemetryRecorder telemetry;

        public void Initialize(KiForgeEventBus bus, MatchTelemetryRecorder recorder)
        {
            eventBus = bus;
            telemetry = recorder;
            eventBus.CombatTelemetryRecorded += OnCombatTelemetry;
        }

        private void OnDestroy()
        {
            if (eventBus != null)
            {
                eventBus.CombatTelemetryRecorded -= OnCombatTelemetry;
            }
        }

        private void OnCombatTelemetry(CombatTelemetryEvent evt)
        {
            if (!mockMode)
            {
                return;
            }

            var style = telemetry.InferPlayerStyle();
            var moveName = evt.playerAction == PlayerActionType.VeryHeavyPunch ? "Very Heavy Punch" :
                evt.playerAction == PlayerActionType.HeavyPunch ? "Heavy Punch" :
                evt.playerAction == PlayerActionType.Guard ? "Guard" : "Quick Punch";

            eventBus.PublishAgentResponse(new AgentResponseEvent
            {
                moveName = moveName,
                narration = $"{moveName} lands for {evt.damageDealtByPlayer} damage. The boss studies the pattern.",
                bossAction = style == "heavy_puncher" ? "pressure" : style == "guard_turtle" ? "heavy_counter" : "dodge",
                nextStrategy = style,
                recapPrompt = "Create a 7-second boxing-game recap in a neon arena with a clean heavy-punch finish.",
                counterSuccess = style == "balanced" ? 0.42f : 0.68f,
                survivalScore = evt.bossHealthAfter > 0 ? 0.6f : 0.2f
            });
        }
    }
}
