using System;

namespace KiForge.Shared
{
    public sealed class KiForgeEventBus
    {
        public event Action<GestureEvent> GestureReceived;
        public event Action<PoseEvent> PoseReceived;
        public event Action<CombatTelemetryEvent> CombatTelemetryRecorded;
        public event Action<AgentResponseEvent> AgentResponseReceived;

        public void PublishGesture(GestureEvent gesture)
        {
            GestureReceived?.Invoke(gesture);
        }

        public void PublishPose(PoseEvent pose)
        {
            PoseReceived?.Invoke(pose);
        }

        public void PublishCombatTelemetry(CombatTelemetryEvent telemetry)
        {
            CombatTelemetryRecorded?.Invoke(telemetry);
        }

        public void PublishAgentResponse(AgentResponseEvent response)
        {
            AgentResponseReceived?.Invoke(response);
        }
    }
}
