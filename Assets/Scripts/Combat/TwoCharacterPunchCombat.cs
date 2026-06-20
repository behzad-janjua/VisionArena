using KiForge.Animation;
using KiForge.Effects;
using KiForge.UI;
using UnityEngine;

namespace KiForge.Combat
{
    public sealed class TwoCharacterPunchCombat : MonoBehaviour
    {
        [SerializeField] private int punchDamage = 4;

        private PlayerCombatController player;
        private BossCombatController boss;
        private ArenaEffectsController effects;
        private KiForgeHudController hud;
        private FighterAnimationController playerPuncher;
        private FighterAnimationController bossPuncher;
        private bool fightEnded;

        public void Initialize(PlayerCombatController playerController, BossCombatController bossController, ArenaEffectsController effectsController, KiForgeHudController hudController, FighterAnimationController playerPuncher, FighterAnimationController bossPuncher)
        {
            player = playerController;
            boss = bossController;
            effects = effectsController;
            hud = hudController;
            this.playerPuncher = playerPuncher;
            this.bossPuncher = bossPuncher;

            playerPuncher.Impact += OnPunchImpact;
            bossPuncher.Impact += OnPunchImpact;
            player.Damaged += _ => OnFighterHurt(this.playerPuncher, player.transform);
            boss.Damaged += _ => OnFighterHurt(this.bossPuncher, boss.transform);
            player.Defeated += () => OnFighterDefeated(this.playerPuncher);
            boss.Defeated += () => OnFighterDefeated(this.bossPuncher);
        }

        private void OnDestroy()
        {
            if (playerPuncher != null)
            {
                playerPuncher.Impact -= OnPunchImpact;
            }

            if (bossPuncher != null)
            {
                bossPuncher.Impact -= OnPunchImpact;
            }
        }

        private void OnPunchImpact(FighterAnimationController source, Transform target)
        {
            if (fightEnded || target == null)
            {
                return;
            }

            var appliedDamage = 0;
            if (target.GetComponent<PlayerCombatController>() != null)
            {
                appliedDamage = player.ApplyDamage(punchDamage);
            }
            else if (target.GetComponent<BossCombatController>() != null)
            {
                appliedDamage = boss.ApplyDamage(punchDamage);
            }

            if (appliedDamage <= 0)
            {
                return;
            }

            var origin = source.transform.position + Vector3.up * 0.7f;
            var direction = target.position.x > source.transform.position.x ? Vector2.right : Vector2.left;
            effects.ShowSlash(origin, direction);
            effects.ScreenShake();
            hud.Refresh();
        }

        private void OnFighterHurt(FighterAnimationController fighter, Transform target)
        {
            fighter.PlayPain();
            hud.Refresh();
        }

        private void OnFighterDefeated(FighterAnimationController defeatedFighter)
        {
            fightEnded = true;
            playerPuncher.StopLoop();
            bossPuncher.StopLoop();
            defeatedFighter.PlayDying();
            hud.Refresh();
        }
    }
}
