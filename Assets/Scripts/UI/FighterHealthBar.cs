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
        private bool isLeft;

        private RectTransform fillRect; // fallback for non-block mode

        public void InitBlocks(List<Image> blockImages, Color bright, float max, bool left)
        {
            blocks.Clear();
            blocks.AddRange(blockImages);
            brightColor = bright;
            dimColor    = new Color(bright.r * 0.15f, bright.g * 0.15f, bright.b * 0.15f, 0.5f);
            maxHp       = max;
            isLeft      = left;
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
                    // Player: first lit blocks from left; Boss: first lit blocks from right
                    bool on = isLeft ? (i < lit) : (i >= blocks.Count - lit);
                    blocks[i].color = on ? brightColor : dimColor;
                }
            }
            else if (fillRect != null)
            {
                float w = fillRect.rect.width;
                if (w > 0f)
                    fillRect.localPosition = new Vector3(w * ratio - w, 0f, 0f);
            }
        }
    }
}
