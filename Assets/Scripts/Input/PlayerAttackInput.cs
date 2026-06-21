using System;
using KiForge.Animation;
using KiForge.Combat;
using UnityEngine;

namespace KiForge.Input
{
    /// <summary>
    /// Human player attack controls (left fighter). Keyboard is the MYO/CV fallback:
    ///   J / K   = punch left / right
    ///   U / I   = heavy punch / very-heavy punch
    ///   ; / '   = hold to guard left / right
    ///   B (hold)= MYO fist charge -> release for normal / heavy / very-heavy punch
    /// All actions funnel through <see cref="AttackThrown"/> / <see cref="Blocked"/> so
    /// the boss AI and Arize coach can observe the player's style.
    /// </summary>
    public sealed class PlayerAttackInput : MonoBehaviour
    {
        private FighterAnimationController fighter;
        private MyoChargeInput myo;
        private bool blocking;

        /// <summary>Raised whenever the player commits to an attack (keyboard or MYO).</summary>
        public event Action<AttackType> AttackThrown;

        /// <summary>Raised when the player raises a block (true = left, false = right).</summary>
        public event Action<bool> Blocked;

        public void Initialize(FighterAnimationController fighterController, MyoChargeInput myoChargeInput)
        {
            fighter = fighterController;
            myo = myoChargeInput;

            if (myo != null)
            {
                myo.Punched += OnMyoPunch;
            }
        }

        private void OnDestroy()
        {
            if (myo != null)
            {
                myo.Punched -= OnMyoPunch;
            }
        }

        private void Update()
        {
            if (fighter == null || fighter.IsDefeated)
            {
                return;
            }

            HandleBlocking();

            // Blocking and attacking are mutually exclusive.
            if (blocking)
            {
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.J)) Throw(AttackType.PunchLeft);
            else if (UnityEngine.Input.GetKeyDown(KeyCode.K)) Throw(AttackType.PunchRight);
            else if (UnityEngine.Input.GetKeyDown(KeyCode.U)) Throw(AttackType.HeavyPunch);
            else if (UnityEngine.Input.GetKeyDown(KeyCode.I)) Throw(AttackType.VeryHeavyPunch);
        }

        private void HandleBlocking()
        {
            var left = UnityEngine.Input.GetKey(KeyCode.Semicolon);
            var right = UnityEngine.Input.GetKey(KeyCode.Quote);

            if (left || right)
            {
                if (!blocking)
                {
                    blocking = true;
                    fighter.StartBlock(left);
                    Blocked?.Invoke(left);
                }
            }
            else if (blocking)
            {
                blocking = false;
                fighter.StopBlock();
            }
        }

        /// <summary>
        /// Commit an attack from an external input source (CV gesture, MYO, network).
        /// Respects the same block/defeat gating as keyboard input.
        /// </summary>
        public void ThrowExternal(AttackType type)
        {
            if (fighter == null || fighter.IsDefeated || blocking)
            {
                return;
            }

            Throw(type);
        }

        private void OnMyoPunch(AttackType type)
        {
            if (fighter == null || fighter.IsDefeated || blocking)
            {
                return;
            }

            Throw(type);
        }

        private void Throw(AttackType type)
        {
            fighter.Attack(type);
            AttackThrown?.Invoke(type);
        }
    }
}
