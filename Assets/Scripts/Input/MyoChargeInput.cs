using System;
using KiForge.Combat;
using UnityEngine;

namespace KiForge.Input
{
    /// <summary>
    /// Hardware abstraction for the MYO armband. The real implementation will be fed
    /// EMG / pose data from the PyoMyo Python bridge over the backend WebSocket
    /// (see backend/myo_listener.py). For the keyboard fallback demo we simulate a
    /// fist contraction with a held key.
    /// </summary>
    public interface IMyoSource
    {
        /// <summary>True while the player is making / holding a fist (muscle contracted).</summary>
        bool IsFistContracted { get; }
    }

    /// <summary>Keyboard stand-in for the MYO: hold the sim key to "make a fist".</summary>
    public sealed class KeyboardMyoSource : IMyoSource
    {
        private readonly KeyCode key;

        public KeyboardMyoSource(KeyCode simulationKey)
        {
            key = simulationKey;
        }

        public bool IsFistContracted => UnityEngine.Input.GetKey(key);
    }

    public enum MyoPunchTier
    {
        Normal,
        Heavy,
        VeryHeavy
    }

    /// <summary>
    /// Translates a held fist contraction into a punch tier:
    ///   * a punch from a resting hand (short contraction) -> normal punch
    ///   * fist held >= heavyHoldSeconds (2s)             -> heavy punch
    ///   * fist held >= veryHeavyHoldSeconds (4s)         -> very heavy punch
    /// Longer holds deal progressively more damage (see AttackTuning).
    /// </summary>
    public sealed class MyoChargeInput : MonoBehaviour
    {
        [SerializeField] private float heavyHoldSeconds = 2f;
        [SerializeField] private float veryHeavyHoldSeconds = 4f;

        private IMyoSource source;
        private bool wasContracted;
        private float holdTime;

        /// <summary>Raised on release with the resolved attack for the hold duration.</summary>
        public event Action<AttackType> Punched;

        /// <summary>Current charge time while a fist is held (0 when resting). For HUD/aura.</summary>
        public float CurrentHoldSeconds => wasContracted ? holdTime : 0f;

        public MyoPunchTier CurrentTier => TierFor(holdTime);

        public void Initialize(IMyoSource myoSource)
        {
            // Defaults to keyboard simulation (hold B) so the demo runs without hardware.
            source = myoSource ?? new KeyboardMyoSource(KeyCode.B);
        }

        private void Update()
        {
            if (source == null)
            {
                return;
            }

            var contracted = source.IsFistContracted;

            if (contracted)
            {
                holdTime += Time.deltaTime;
                wasContracted = true;
                return;
            }

            if (wasContracted)
            {
                // Fist released -> fire the punch tier the hold earned.
                Punched?.Invoke(AttackForTier(TierFor(holdTime)));
                wasContracted = false;
                holdTime = 0f;
            }
        }

        private MyoPunchTier TierFor(float seconds)
        {
            if (seconds >= veryHeavyHoldSeconds)
            {
                return MyoPunchTier.VeryHeavy;
            }

            return seconds >= heavyHoldSeconds ? MyoPunchTier.Heavy : MyoPunchTier.Normal;
        }

        private static AttackType AttackForTier(MyoPunchTier tier)
        {
            switch (tier)
            {
                case MyoPunchTier.VeryHeavy: return AttackType.VeryHeavyPunch;
                case MyoPunchTier.Heavy:     return AttackType.HeavyPunch;
                default:                     return AttackType.PunchRight;
            }
        }
    }
}
