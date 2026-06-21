using System;
using System.Collections;
using KiForge.Animation;
using KiForge.Input;
using UnityEngine;

namespace KiForge.Combat
{
    /// <summary>
    /// Drives the AI boss (right fighter). Every <see cref="decisionInterval"/> it asks the
    /// <see cref="IBossAgent"/> for a decision, then executes it: approach + strike, raise a
    /// guard, or slip backwards. Movement closes the gap so the boss actually fights.
    /// </summary>
    public sealed class BossAgentController : MonoBehaviour
    {
        [SerializeField] private float decisionInterval = 1.3f;
        [SerializeField] private float attackRange = 2.4f;
        [SerializeField] private float walkSpeed = 3.0f;
        [SerializeField] private float leftBound = -4.5f;
        [SerializeField] private float rightBound = 4.5f;
        [SerializeField] private float blockHoldSeconds = 0.8f;
        [SerializeField] private float dodgeSeconds = 0.5f;

        private FighterAnimationController fighter;
        private Transform player;
        private BossCombatController bossHealth;
        private PlayerCombatController playerHealth;
        private IBossAgent agent;
        private Func<string> styleProvider;

        private float groundY;
        private float retreatTimer;
        private float blockReleaseTime;
        private bool blocking;

        private AttackType lastPlayerAttack;
        private float lastPlayerAttackTime = -999f;

        public BossDecision CurrentDecision { get; private set; }

        public void Initialize(
            FighterAnimationController bossFighter,
            Transform playerTransform,
            BossCombatController bossCombat,
            PlayerCombatController playerCombat,
            IBossAgent bossAgent,
            Func<string> playerStyleProvider,
            PlayerAttackInput playerInput)
        {
            fighter = bossFighter;
            player = playerTransform;
            bossHealth = bossCombat;
            playerHealth = playerCombat;
            agent = bossAgent;
            styleProvider = playerStyleProvider;
            groundY = transform.position.y;

            if (playerInput != null)
            {
                playerInput.AttackThrown += OnPlayerAttack;
            }

            StartCoroutine(BrainLoop());
        }

        private void OnDestroy()
        {
            // Note: PlayerAttackInput lives on the player object; no explicit unsubscribe needed
            // because both are destroyed together at scene teardown.
        }

        private void OnPlayerAttack(AttackType type)
        {
            lastPlayerAttack = type;
            lastPlayerAttackTime = Time.time;
        }

        private IEnumerator BrainLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(decisionInterval);

                if (fighter == null || fighter.IsDefeated || bossHealth.IsDefeated || playerHealth.IsDefeated)
                {
                    yield break;
                }

                if (blocking || fighter.IsAttacking)
                {
                    continue;
                }

                var context = new BossDecisionContext
                {
                    playerStyle = styleProvider != null ? styleProvider() : "balanced",
                    lastPlayerAttack = lastPlayerAttack,
                    playerAttackedRecently = Time.time - lastPlayerAttackTime < 1.5f,
                    inRange = InRange(),
                    bossHealth01 = bossHealth.MaxHealth > 0 ? (float)bossHealth.Health / bossHealth.MaxHealth : 0f,
                    playerHealth01 = playerHealth.MaxHealth > 0 ? (float)playerHealth.Health / playerHealth.MaxHealth : 0f
                };

                var decision = agent.DecideAction(context);
                CurrentDecision = decision;
                Execute(decision);
            }
        }

        private void Execute(BossDecision decision)
        {
            switch (decision.kind)
            {
                case BossActionKind.Block:
                    blocking = true;
                    blockReleaseTime = Time.time + blockHoldSeconds;
                    fighter.StartBlock(decision.blockLeft);
                    break;
                case BossActionKind.Dodge:
                    retreatTimer = dodgeSeconds;
                    break;
                default: // Attack
                    if (InRange())
                    {
                        fighter.Attack(decision.attack);
                    }
                    // Out of range: movement in Update closes the distance before the next tick.
                    break;
            }
        }

        private bool InRange()
        {
            return player != null && Mathf.Abs(player.position.x - transform.position.x) <= attackRange;
        }

        private void Update()
        {
            if (fighter == null || fighter.IsDefeated || player == null)
            {
                return;
            }

            if (blocking && Time.time >= blockReleaseTime)
            {
                blocking = false;
                fighter.StopBlock();
            }

            if (fighter.IsAttacking || blocking)
            {
                return;
            }

            var dx = player.position.x - transform.position.x;
            var dist = Mathf.Abs(dx);
            var h = 0f;

            if (retreatTimer > 0f)
            {
                retreatTimer -= Time.deltaTime;
                h = -Mathf.Sign(dx); // slip away from the player
            }
            else if (dist > attackRange * 0.85f)
            {
                h = Mathf.Sign(dx); // close the gap
            }

            if (Mathf.Abs(h) < 0.01f)
            {
                fighter.StopLocomotion();
                return;
            }

            var newX = Mathf.Clamp(transform.position.x + h * walkSpeed * Time.deltaTime, leftBound, rightBound);
            var newPos = new Vector3(newX, groundY, transform.position.z);
            fighter.SetHome(newPos);
            transform.position = newPos;
            fighter.SetLocomotion(h);
        }
    }
}
