using KiForge.Shared;
using UnityEngine;

namespace KiForge.UI
{
    /// <summary>
    /// Plays Deepgram TTS audio for commentator narration.
    /// Audio playback is disabled until the Unity Audio module is re-enabled in Package Manager.
    /// </summary>
    public sealed class CommentatorAudioPlayer : MonoBehaviour
    {
        private KiForgeEventBus eventBus;

        public void Setup(KiForgeEventBus bus)
        {
            eventBus = bus;
            eventBus.AgentResponseReceived += OnAgentResponse;
        }

        private void OnDestroy()
        {
            if (eventBus != null)
                eventBus.AgentResponseReceived -= OnAgentResponse;
        }

        private void OnAgentResponse(AgentResponseEvent response)
        {
            // Audio playback disabled — Unity Audio module not enabled.
        }
    }
}
