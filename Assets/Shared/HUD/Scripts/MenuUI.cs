using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Shared.HUD
{
    // Shared code-built UI pieces for the full-screen menus (OpeningSplash start screen and EndScreen):
    // overlay canvas, cover art, side panel, labels, buttons and the difficulty slider.
    public static class MenuUI
    {
        public static readonly Color PanelColor = new Color(.02f, .03f, .05f, .82f);
        public static readonly Color TextColor = new Color(.86f, .89f, .93f);
        public static readonly Color MutedColor = new Color(.58f, .63f, .7f);
        public static readonly Color AccentColor = new Color(1f, .78f, .32f);
        public static readonly Color TrackColor = new Color(1, 1, 1, .14f);
        public static readonly Color ButtonColor = new Color(1, 1, 1, .1f);

        // Creates a GameObject with a screen-space overlay canvas (1280x720 reference) plus the given components.
        public static GameObject NewOverlay(string name, int sortingOrder, params System.Type[] extra)
        {
            var types = new System.Type[4 + extra.Length];
            types[0] = typeof(Canvas); types[1] = typeof(CanvasScaler); types[2] = typeof(GraphicRaycaster); types[3] = typeof(CanvasGroup);
            extra.CopyTo(types, 4);
            var root = new GameObject(name, types) { layer = 5 };
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = .5f;
            return root;
        }

        // Black backdrop plus art scaled to cover the whole screen (cropping the bottom rather than letterboxing).
        public static void CoverArt(Transform parent, Sprite art)
        {
            Stretch(NewUI("Backdrop", parent)).gameObject.AddComponent<Image>().color = Color.black;
            if (!art) return;
            var artRect = NewUI("Art", parent);
            artRect.anchorMin = artRect.anchorMax = new Vector2(.5f, .5f);
            artRect.pivot = new Vector2(.5f, 1f);
            var image = artRect.gameObject.AddComponent<Image>();
            image.sprite = art;
            image.raycastTarget = false;
            var fitter = artRect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = art.rect.width / art.rect.height;
        }

        // Dark auto-height panel anchored to the left edge, laying its children out vertically.
        public static RectTransform SidePanel(Transform parent, float width, Vector2 offset)
        {
            var panel = NewUI("Panel", parent);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0, .5f);
            panel.anchoredPosition = offset;
            panel.sizeDelta = new Vector2(width, 0);
            panel.gameObject.AddComponent<Image>().color = PanelColor;
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 28, 30);
            layout.spacing = 10;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return panel;
        }

        // Horizontal row of equal-width buttons.
        public static RectTransform ButtonRow(Transform parent)
        {
            var buttons = NewUI("Buttons", parent);
            buttons.gameObject.AddComponent<LayoutElement>().preferredHeight = 52;
            var row = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 14;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = true;
            return buttons;
        }

        // Difficulty multiplier slider bound to GameSession.Difficulty. The track is logarithmic so 1x sits in the
        // middle, with 0.25x and 4x an equal distance either side. TDSpawner divides the enemy spawn interval by it.
        public static Slider DifficultySlider(Transform parent, TMP_FontAsset font)
        {
            var header = NewUI("DifficultyHeader", parent);
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
            var headerRow = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            headerRow.childControlWidth = headerRow.childControlHeight = true;
            headerRow.childForceExpandWidth = headerRow.childForceExpandHeight = true;
            Label(header, "Label", "DIFFICULTY", font, 16, TextColor).characterSpacing = 3;
            var valueText = Label(header, "Value", "", font, 18, AccentColor);
            valueText.alignment = TextAlignmentOptions.MidlineRight;
            valueText.fontStyle = FontStyles.Bold;

            var sliderRect = NewUI("DifficultySlider", parent);
            sliderRect.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;
            var track = NewUI("Track", sliderRect);
            track.anchorMin = new Vector2(0, .5f); track.anchorMax = new Vector2(1, .5f);
            track.sizeDelta = new Vector2(0, 6);
            track.gameObject.AddComponent<Image>().color = TrackColor;
            var fillArea = NewUI("Fill Area", sliderRect);
            fillArea.anchorMin = new Vector2(0, .5f); fillArea.anchorMax = new Vector2(1, .5f);
            fillArea.sizeDelta = new Vector2(0, 6);
            var fill = Stretch(NewUI("Fill", fillArea));
            fill.gameObject.AddComponent<Image>().color = AccentColor;
            var handleArea = Stretch(NewUI("Handle Area", sliderRect));
            handleArea.offsetMin = new Vector2(9, 0); handleArea.offsetMax = new Vector2(-9, 0);
            var handle = NewUI("Handle", handleArea);
            handle.sizeDelta = new Vector2(18, 0);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = Color.white;

            var slider = sliderRect.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = Mathf.Log(GameSession.MinDifficulty, 2);
            slider.maxValue = Mathf.Log(GameSession.MaxDifficulty, 2);
            slider.SetValueWithoutNotify(Mathf.Log(GameSession.Difficulty, 2));
            void Apply(float value)
            {
                // Snap to 0.05 steps so the readout stays tidy, and to exactly 1 near the middle.
                GameSession.Difficulty = Mathf.Round(value * 20) / 20f;
                valueText.text = $"×{GameSession.Difficulty:0.00}";
            }
            slider.onValueChanged.AddListener(v => Apply(Mathf.Pow(2, v)));
            Apply(GameSession.Difficulty);

            var ticks = NewUI("Ticks", parent);
            ticks.gameObject.AddComponent<LayoutElement>().preferredHeight = 16;
            TickLabel(ticks, "×0.25", 0, font);
            TickLabel(ticks, "×1", .5f, font);
            TickLabel(ticks, "×4", 1, font);
            return slider;
        }

        public static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // Scenes don't ship an EventSystem (the HUD is display-only), so create one for the buttons.
        public static void EnsureEventSystem()
        {
            if (EventSystem.current) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        public static RectTransform NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static RectTransform Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static void Spacer(Transform parent, float height)
        {
            NewUI("Spacer", parent).gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        }

        public static TextMeshProUGUI Label(Transform parent, string name, string value, TMP_FontAsset font, float size, Color color)
        {
            var text = NewUI(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (font) text.font = font;
            text.fontSize = size;
            text.color = color;
            text.text = value;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            return text;
        }

        private static void TickLabel(Transform parent, string value, float x, TMP_FontAsset font)
        {
            var text = Label(parent, value, value, font, 13, MutedColor);
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(x, 0); rect.anchorMax = new Vector2(x, 1);
            rect.pivot = new Vector2(x, .5f);
            rect.sizeDelta = new Vector2(60, 0);
            rect.anchoredPosition = Vector2.zero;
            text.alignment = x <= 0 ? TextAlignmentOptions.MidlineLeft
                : x >= 1 ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.Center;
        }

        public static Button MakeButton(Transform parent, string name, string caption, TMP_FontAsset font,
            Color background, Color textColor, UnityEngine.Events.UnityAction onClick)
        {
            var rect = NewUI(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = background;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = new Color(.85f, .85f, .85f, 1);
            colors.highlightedColor = colors.selectedColor = Color.white;
            colors.pressedColor = new Color(.65f, .65f, .65f, 1);
            button.colors = colors;
            button.onClick.AddListener(onClick);
            var text = Label(rect, "Text", caption, font, 20, textColor);
            Stretch(text.rectTransform);
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 4;
            return button;
        }
    }
}
