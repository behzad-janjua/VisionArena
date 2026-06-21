using KiForge.Shared;
using UnityEngine;

namespace KiForge.Input
{
    /// <summary>
    /// IMyoSource driven by CHARGE_START / HEAVY_PUNCH_RELEASE events from the
    /// Python MYO bridge over the backend WebSocket. Plug this into MyoChargeInput
    /// instead of KeyboardMyoSource when real hardware is connected.
    /// </summary>
    public sealed class WebSocketMyoSource : IMyoSource
    {
        private bool fistContracted;

        public bool IsFistContracted => fistContracted;

        public WebSocketMyoSource(KiForgeEventBus bus)
        {
            bus.GestureReceived += OnGesture;
        }

        private void OnGesture(GestureEvent evt)
        {
            switch (evt.type)
            {
                case GestureEventType.ChargeStart:
                    fistContracted = true;
                    break;
                case GestureEventType.HeavyPunchRelease:
                case GestureEventType.GuardStart:  // any non-fist pose clears it
                case GestureEventType.GuardEnd:
                    fistContracted = false;
                    break;
            }
        }
    }
}
