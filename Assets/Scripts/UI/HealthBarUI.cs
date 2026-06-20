using System.Collections.Generic;
using KiForge.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace KiForge.UI
{
    public sealed class HealthBarUI : MonoBehaviour
    {
        private const int   BlockCount = 40;
        private const float BlockGap   = 3f;

        private FighterHealthBar playerBar;
        private FighterHealthBar bossBar;

        public void Setup(PlayerCombatController player, BossCombatController boss)
        {
            var canvas = BuildCanvas();

            playerBar = BuildBlockBar(canvas, "Player HP", isLeft: true,
                new Color(0.12f, 0.9f, 1f), player.MaxHealth);
            bossBar   = BuildBlockBar(canvas, "Boss HP",   isLeft: false,
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
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution  = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight   = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static FighterHealthBar BuildBlockBar(Canvas canvas, string label,
            bool isLeft, Color fillColor, float maxHealth)
        {
            // Root container — anchored to the corner
            float barWidth  = 520f;
            float barHeight = 22f;

            var root   = new GameObject(label);
            root.transform.SetParent(canvas.transform, false);
            var rootRT = root.AddComponent<RectTransform>();
            rootRT.sizeDelta = new Vector2(barWidth, barHeight);

            if (isLeft)
            {
                rootRT.anchorMin        = new Vector2(0f, 1f);
                rootRT.anchorMax        = new Vector2(0f, 1f);
                rootRT.pivot            = new Vector2(0f, 1f);
                rootRT.anchoredPosition = new Vector2(24f, -24f);
            }
            else
            {
                rootRT.anchorMin        = new Vector2(1f, 1f);
                rootRT.anchorMax        = new Vector2(1f, 1f);
                rootRT.pivot            = new Vector2(1f, 1f);
                rootRT.anchoredPosition = new Vector2(-24f, -24f);
            }

            // Dark background
            var bgGO   = new GameObject("BG");
            bgGO.transform.SetParent(root.transform, false);
            var bgRect = bgGO.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;
            var bgImg  = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.05f, 0.05f, 0.05f, 0.9f);

            // Build blocks
            float totalGap   = BlockGap * (BlockCount - 1);
            float blockWidth = (barWidth - totalGap - 6f) / BlockCount;
            float blockH     = barHeight - 6f;

            var blockImages = new List<Image>();
            for (int i = 0; i < BlockCount; i++)
            {
                var blockGO   = new GameObject("Block_" + i);
                blockGO.transform.SetParent(root.transform, false);
                var blockRT   = blockGO.AddComponent<RectTransform>();
                float xOffset = 3f + i * (blockWidth + BlockGap) + blockWidth * 0.5f;
                blockRT.anchorMin        = new Vector2(0f, 0.5f);
                blockRT.anchorMax        = new Vector2(0f, 0.5f);
                blockRT.pivot            = new Vector2(0.5f, 0.5f);
                blockRT.anchoredPosition = new Vector2(xOffset, 0f);
                blockRT.sizeDelta        = new Vector2(blockWidth, blockH);

                var blockImg   = blockGO.AddComponent<Image>();
                blockImg.type   = Image.Type.Simple;
                blockImg.color  = fillColor;
                blockImages.Add(blockImg);
            }

            var bar = root.AddComponent<FighterHealthBar>();
            bar.InitBlocks(blockImages, fillColor, maxHealth, isLeft);
            return bar;
        }

    }
}
