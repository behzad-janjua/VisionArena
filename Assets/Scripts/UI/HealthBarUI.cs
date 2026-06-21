using System.Collections.Generic;
using KiForge.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace KiForge.UI
{
    public sealed class HealthBarUI : MonoBehaviour
    {
        private const int   BlockCount = 40;
        private const float BlockGap   = 2f;

        private FighterHealthBar playerBar;
        private FighterHealthBar bossBar;

        public void Setup(PlayerCombatController player, BossCombatController boss)
        {
            var canvas = BuildCanvas();

            playerBar = BuildBlockBar(canvas, "Player HP", "YOU",  isLeft: true,
                new Color(0.12f, 0.9f, 1f),   player.MaxHealth);
            bossBar   = BuildBlockBar(canvas, "Boss HP",   "BOSS", isLeft: false,
                new Color(1f, 0.18f, 0.45f), boss.MaxHealth);

            player.Damaged += _ => playerBar.SetHealth(player.Health);
            boss.Damaged   += _ => bossBar.SetHealth(boss.Health);
        }

        private static Canvas BuildCanvas()
        {
            var go     = new GameObject("HUD Canvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static FighterHealthBar BuildBlockBar(Canvas canvas, string label, string nameTag,
            bool isLeft, Color fillColor, float maxHealth)
        {
            const float barWidth  = 560f;
            const float barHeight = 34f;
            const float margin    = 28f;
            const float labelH    = 18f;
            const float labelGap  = 4f;

            // --- Fighter name label ---
            var nameGO = new GameObject(label + "_Name");
            nameGO.transform.SetParent(canvas.transform, false);
            var nameRT       = nameGO.AddComponent<RectTransform>();
            nameRT.sizeDelta = new Vector2(barWidth, labelH);
            if (isLeft)
            {
                nameRT.anchorMin        = new Vector2(0f, 1f);
                nameRT.anchorMax        = new Vector2(0f, 1f);
                nameRT.pivot            = new Vector2(0f, 1f);
                nameRT.anchoredPosition = new Vector2(margin, -margin);
            }
            else
            {
                nameRT.anchorMin        = new Vector2(1f, 1f);
                nameRT.anchorMax        = new Vector2(1f, 1f);
                nameRT.pivot            = new Vector2(1f, 1f);
                nameRT.anchoredPosition = new Vector2(-margin, -margin);
            }
            var nameTxt                    = nameGO.AddComponent<Text>();
            nameTxt.font                   = ResolveFont();
            nameTxt.fontSize               = 13;
            nameTxt.fontStyle              = FontStyle.Bold;
            nameTxt.color                  = new Color(0.82f, 0.82f, 0.82f, 0.9f);
            nameTxt.alignment              = isLeft ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
            nameTxt.horizontalOverflow     = HorizontalWrapMode.Overflow;
            nameTxt.text                   = nameTag;

            // --- Bar root (border tint) ---
            float barY = -(margin + labelH + labelGap);

            var root   = new GameObject(label);
            root.transform.SetParent(canvas.transform, false);
            var rootRT = root.AddComponent<RectTransform>();
            rootRT.sizeDelta = new Vector2(barWidth, barHeight);
            if (isLeft)
            {
                rootRT.anchorMin        = new Vector2(0f, 1f);
                rootRT.anchorMax        = new Vector2(0f, 1f);
                rootRT.pivot            = new Vector2(0f, 1f);
                rootRT.anchoredPosition = new Vector2(margin, barY);
            }
            else
            {
                rootRT.anchorMin        = new Vector2(1f, 1f);
                rootRT.anchorMax        = new Vector2(1f, 1f);
                rootRT.pivot            = new Vector2(1f, 1f);
                rootRT.anchoredPosition = new Vector2(-margin, barY);
            }

            // Root Image = tinted border
            var borderImg = root.AddComponent<Image>();
            borderImg.color = new Color(fillColor.r * 0.45f, fillColor.g * 0.45f, fillColor.b * 0.45f, 0.65f);

            // Dark background (inset 1px)
            var bgGO  = new GameObject("BG");
            bgGO.transform.SetParent(root.transform, false);
            var bgRT  = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = new Vector2(1f, 1f);
            bgRT.offsetMax = new Vector2(-1f, -1f);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.04f, 0.04f, 0.06f, 0.96f);

            // Ghost fill (yellow, behind blocks, drains after damage)
            var ghostGO   = new GameObject("Ghost");
            ghostGO.transform.SetParent(root.transform, false);
            var ghostRect = ghostGO.AddComponent<RectTransform>();
            ghostRect.anchorMin = Vector2.zero;
            ghostRect.anchorMax = Vector2.one;
            ghostRect.offsetMin = new Vector2(3f, 3f);
            ghostRect.offsetMax = new Vector2(-3f, -3f);
            var ghostImg        = ghostGO.AddComponent<Image>();
            ghostImg.color      = new Color(fillColor.r * 0.35f, fillColor.g * 0.35f, fillColor.b * 0.35f, 0.82f);
            ghostImg.type       = Image.Type.Filled;
            ghostImg.fillMethod = Image.FillMethod.Horizontal;
            ghostImg.fillOrigin = isLeft ? 0 : 1;
            ghostImg.fillAmount = 1f;

            // Blocks
            float innerWidth = barWidth - 6f;
            float totalGap   = BlockGap * (BlockCount - 1);
            float blockWidth = (innerWidth - totalGap) / BlockCount;
            float blockH     = barHeight - 6f;

            var blockImages = new List<Image>();
            for (int i = 0; i < BlockCount; i++)
            {
                var blockGO  = new GameObject("Block_" + i);
                blockGO.transform.SetParent(root.transform, false);
                var blockRT  = blockGO.AddComponent<RectTransform>();
                float xOff   = 3f + i * (blockWidth + BlockGap) + blockWidth * 0.5f;
                blockRT.anchorMin        = new Vector2(0f, 0.5f);
                blockRT.anchorMax        = new Vector2(0f, 0.5f);
                blockRT.pivot            = new Vector2(0.5f, 0.5f);
                blockRT.anchoredPosition = new Vector2(xOff, 0f);
                blockRT.sizeDelta        = new Vector2(blockWidth, blockH);
                var blockImg  = blockGO.AddComponent<Image>();
                blockImg.color = fillColor;
                blockImages.Add(blockImg);
            }

            var bar = root.AddComponent<FighterHealthBar>();
            bar.InitBlocks(blockImages, fillColor, maxHealth, isLeft, ghostImg);
            return bar;
        }

        private static Font ResolveFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 13);
            return f;
        }
    }
}
