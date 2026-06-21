using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KiForge.UI
{
    public sealed class FighterHealthBar : MonoBehaviour
    {
        private readonly List<Image> blocks = new List<Image>();
        private Color brightColor;
        private Color dimColor;
        private float maxHp;
        private bool  isLeft;

        private Image  ghost;
        private float  ghostFill = 1f;
        private Coroutine drainRoutine;

        private RectTransform fillRect;

        public void InitBlocks(List<Image> blockImages, Color bright, float max, bool left, Image ghostImage)
        {
            blocks.Clear();
            blocks.AddRange(blockImages);
            brightColor = bright;
            dimColor    = new Color(bright.r * 0.1f, bright.g * 0.1f, bright.b * 0.1f, 0.25f);
            maxHp       = max;
            isLeft      = left;
            ghost       = ghostImage;
        }

        public void InitDirect(RectTransform fill, float max)
        {
            fillRect = fill;
            maxHp    = max;
        }

        private void Start() => SetHealth(maxHp);

        public void SetHealth(float health)
        {
            float ratio = Mathf.Clamp01(health / maxHp);

            if (blocks.Count > 0)
            {
                int lit = Mathf.RoundToInt(ratio * blocks.Count);
                for (int i = 0; i < blocks.Count; i++)
                {
                    if (blocks[i] == null) continue;
                    bool on = isLeft ? (i < lit) : (i >= blocks.Count - lit);
                    blocks[i].color = on ? brightColor : dimColor;
                }

                if (ghost != null && ratio < ghostFill)
                {
                    if (drainRoutine != null) StopCoroutine(drainRoutine);
                    drainRoutine = StartCoroutine(DrainGhost(ratio));
                }
            }
            else if (fillRect != null)
            {
                float w = fillRect.rect.width;
                if (w > 0f)
                    fillRect.localPosition = new Vector3(w * ratio - w, 0f, 0f);
            }
        }

        private IEnumerator DrainGhost(float targetFill)
        {
            yield return new WaitForSeconds(0.42f);

            float startFill = ghostFill;
            float elapsed   = 0f;
            const float duration = 0.55f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t     = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - (1f - t) * (1f - t);
                ghostFill        = Mathf.Lerp(startFill, targetFill, eased);
                ghost.fillAmount = ghostFill;
                yield return null;
            }

            ghostFill        = targetFill;
            ghost.fillAmount = ghostFill;
        }
    }
}
