using KiForge.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace KiForge.UI
{
    public sealed class KiForgeHudController : MonoBehaviour
    {
        private PlayerCombatController player;
        private BossCombatController boss;
        private Slider playerHealth;
        private Slider bossHealth;
        private Slider charge;
        private Text narration;
        private Text fightLab;
        private RectTransform aimReticle;
        private Camera mainCamera;

        public void BuildRuntimeHud()
        {
            mainCamera = Camera.main;

            var canvasObject = new GameObject("KiForge HUD");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.AddComponent<GraphicRaycaster>();

            playerHealth = CreateSlider(canvasObject.transform, "Player Health", new Vector2(24f, -24f), new Vector2(260f, 18f), Color.cyan);
            bossHealth = CreateSlider(canvasObject.transform, "Boss Health", new Vector2(-284f, -24f), new Vector2(260f, 18f), new Color(1f, 0.18f, 0.45f), TextAnchor.UpperRight);
            charge = CreateSlider(canvasObject.transform, "Charge", new Vector2(24f, -56f), new Vector2(220f, 14f), Color.yellow);

            narration = CreateText(canvasObject.transform, "Narration", new Vector2(0f, 72f), new Vector2(720f, 72f), 22, TextAnchor.MiddleCenter);
            fightLab = CreateText(canvasObject.transform, "Fight Lab", new Vector2(24f, -98f), new Vector2(330f, 130f), 16, TextAnchor.UpperLeft);

            var reticleObject = new GameObject("Aim Reticle");
            reticleObject.transform.SetParent(canvasObject.transform, false);
            aimReticle = reticleObject.AddComponent<RectTransform>();
            aimReticle.sizeDelta = new Vector2(18f, 18f);
            var image = reticleObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.8f);
        }

        public void Bind(PlayerCombatController playerController, BossCombatController bossController)
        {
            player = playerController;
            boss = bossController;
            Refresh();
        }

        public void Refresh()
        {
            if (player != null && playerHealth != null)
            {
                playerHealth.value = player.Health / (float)player.MaxHealth;
            }

            if (boss != null && bossHealth != null)
            {
                bossHealth.value = boss.Health / (float)boss.MaxHealth;
            }
        }

        public void SetCharge(float value)
        {
            if (charge != null)
            {
                charge.value = Mathf.Clamp01(value);
            }
        }

        public void SetNarration(string moveName, string line)
        {
            if (narration != null)
            {
                narration.text = string.IsNullOrEmpty(moveName) ? line : $"{moveName}\n{line}";
            }
        }

        public void SetFightLab(string text)
        {
            if (fightLab != null)
            {
                fightLab.text = text;
            }
        }

        public void SetAimReticle(Vector2 worldPosition)
        {
            if (aimReticle == null || mainCamera == null)
            {
                return;
            }

            aimReticle.position = mainCamera.WorldToScreenPoint(worldPosition);
        }

        private static Slider CreateSlider(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color fillColor, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var rect = root.AddComponent<RectTransform>();
            rect.anchorMin = anchor == TextAnchor.UpperRight ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = anchor == TextAnchor.UpperRight ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var slider = root.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            var background = CreateImage(root.transform, "Background", Color.black);
            background.rectTransform.anchorMin = Vector2.zero;
            background.rectTransform.anchorMax = Vector2.one;
            background.rectTransform.offsetMin = Vector2.zero;
            background.rectTransform.offsetMax = Vector2.zero;

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(root.transform, false);
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;

            var fill = CreateImage(fillArea.transform, "Fill", fillColor);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;
            slider.fillRect = fill.rectTransform;
            slider.targetGraphic = fill;
            return slider;
        }

        private static Text CreateText(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var text = obj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var image = obj.AddComponent<Image>();
            image.color = color;
            return image;
        }
    }
}
