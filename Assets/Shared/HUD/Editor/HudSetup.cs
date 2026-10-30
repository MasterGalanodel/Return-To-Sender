using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Shared.HUD.Editor
{
    public static class HudSetup
    {
        public const string PrefabPath = "Assets/Shared/HUD/Resources/SharedHud.prefab";

        [MenuItem("Tools/Shared/HUD/Setup Shared HUD")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play before HUD setup.");
            Directory.CreateDirectory("Assets/Shared/HUD/Resources");
            AssetDatabase.Refresh();
            var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (!font) throw new System.InvalidOperationException("Import bundled TMP essentials before HUD setup.");
            var healthIcon = LoadIcon("health");
            var buildIcon = LoadIcon("build");
            // Load and edit the owned prefab in place on subsequent runs.
            bool existing = File.Exists(PrefabPath);
            var root = existing ? PrefabUtility.LoadPrefabContents(PrefabPath)
                : new GameObject("Shared HUD", typeof(RectTransform), typeof(Canvas));
            try
            {
                root.layer = 5;
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                var scaler = GetOrAdd<UnityEngine.UI.CanvasScaler>(root);
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);
                scaler.matchWidthOrHeight = .5f;
                GetOrAdd<UnityEngine.UI.GraphicRaycaster>(root).enabled = false;
                var group = GetOrAdd<CanvasGroup>(root);
                group.blocksRaycasts = false;
                group.interactable = false;
                GetOrAdd<GameSession>(root);
                var hud = GetOrAdd<SharedHud>(root);
                var safe = Child(root.transform, "SafeArea");
                safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one;
                safe.offsetMin = safe.offsetMax = Vector2.zero;
                var panel = Child(safe, "TopCenterStats");
                panel.anchorMin = panel.anchorMax = new Vector2(.5f, 1);
                panel.pivot = new Vector2(.5f, 1);
                panel.anchoredPosition = new Vector2(0, -18);
                panel.sizeDelta = new Vector2(400, 150);
                var background = GetOrAdd<UnityEngine.UI.Image>(panel.gameObject);
                background.color = new Color(.035f, .055f, .075f, .94f);
                background.raycastTarget = false;
                var layout = GetOrAdd<UnityEngine.UI.VerticalLayoutGroup>(panel.gameObject);
                layout.padding = new RectOffset(18, 18, 13, 13);
                layout.spacing = 6;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
                var header = Child(panel, "DayAndTime");
                header.SetAsFirstSibling();
                GetOrAdd<UnityEngine.UI.LayoutElement>(header.gameObject).preferredHeight = 28;
                var headerLayout = GetOrAdd<UnityEngine.UI.HorizontalLayoutGroup>(header.gameObject);
                headerLayout.childControlWidth = headerLayout.childControlHeight = true;
                headerLayout.childForceExpandWidth = true;
                headerLayout.childForceExpandHeight = true;
                headerLayout.spacing = 12;
                var days = Text(header, "Day", "DAY 1/6", font, 20, Color.white);
                days.alignment = TextAlignmentOptions.MidlineLeft;
                var timer = Text(header, "Time", $"CAVE 0/{GameSession.CaveSeconds}s", font, 20, Color.white);
                timer.alignment = TextAlignmentOptions.MidlineRight;
                var health = Row(panel, "Health", "HEALTH", "50", new Color(1, .52f, .48f), font, healthIcon);
                var towers = Row(panel, "TowerTokens", "BUILD TOKENS", "2", new Color(.46f, .89f, .76f), font, buildIcon);
                var obsoleteUpdates = panel.Find("UpdateTokens");
                if (obsoleteUpdates) Object.DestroyImmediate(obsoleteUpdates.gameObject);
                var result = Child(safe, "RunResult");
                result.anchorMin = result.anchorMax = result.pivot = new Vector2(.5f, .5f);
                result.anchoredPosition = Vector2.zero;
                result.sizeDelta = new Vector2(680, 180);
                var resultBackground = GetOrAdd<UnityEngine.UI.Image>(result.gameObject);
                resultBackground.color = new Color(.035f, .055f, .075f, .96f);
                resultBackground.raycastTarget = false;
                var resultLayout = GetOrAdd<UnityEngine.UI.VerticalLayoutGroup>(result.gameObject);
                resultLayout.padding = new RectOffset(24, 24, 20, 20);
                resultLayout.spacing = 8;
                resultLayout.childControlWidth = resultLayout.childControlHeight = true;
                resultLayout.childForceExpandWidth = true;
                resultLayout.childForceExpandHeight = false;
                var title = Text(result, "Title", "GAME OVER", font, 40, Color.white);
                title.alignment = TextAlignmentOptions.Center;
                title.fontStyle = FontStyles.Bold;
                GetOrAdd<UnityEngine.UI.LayoutElement>(title.gameObject).preferredHeight = 52;
                var detail = Text(result, "Detail", "", font, 21, new Color(.82f, .86f, .9f));
                detail.alignment = TextAlignmentOptions.Center;
                detail.textWrappingMode = TextWrappingModes.Normal;
                GetOrAdd<UnityEngine.UI.LayoutElement>(detail.gameObject).preferredHeight = 76;
                result.gameObject.SetActive(false);
                hud.Configure(health, towers, safe, days, timer, result, title, detail);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                if (existing) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Shared HUD: prefab ready (50 health, 2 build tokens).");
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        { var component = go.GetComponent<T>(); return component ? component : go.AddComponent<T>(); }
        private static RectTransform Child(Transform parent, string name)
        {
            var found = parent.Find(name);
            if (found) return found.GetComponent<RectTransform>();
            var child = new GameObject(name, typeof(RectTransform));
            child.layer = 5;
            child.transform.SetParent(parent, false);
            return child.GetComponent<RectTransform>();
        }
        private static Sprite LoadIcon(string name)
        {
            // The supplied PNGs contain sliced sprites; load their sprite sub-assets without changing the imports.
            var sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Shared/HUD/Art/" + name + ".png").OfType<Sprite>().FirstOrDefault();
            if (!sprite) throw new System.InvalidOperationException("Missing HUD sprite: " + name);
            return sprite;
        }

        private static TextMeshProUGUI Row(Transform parent, string name, string label, string value, Color color, TMP_FontAsset font, Sprite icon = null)
        {
            var row = Child(parent, name);
            GetOrAdd<UnityEngine.UI.LayoutElement>(row.gameObject).preferredHeight = 36;
            var layout = GetOrAdd<UnityEngine.UI.HorizontalLayoutGroup>(row.gameObject);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = true;
            layout.spacing = 8;
            if (icon)
            {
                var iconRect = Child(row, "Icon");
                iconRect.SetAsFirstSibling();
                var image = GetOrAdd<UnityEngine.UI.Image>(iconRect.gameObject);
                image.sprite = icon;
                image.preserveAspect = true;
                image.raycastTarget = false;
                var iconSize = GetOrAdd<UnityEngine.UI.LayoutElement>(iconRect.gameObject);
                iconSize.preferredWidth = iconSize.minWidth = 32;
                iconSize.preferredHeight = 32;
                iconSize.flexibleWidth = 0;
            }
            var title = Text(row, "Label", label, font, 16, new Color(.82f, .86f, .9f));
            title.alignment = TextAlignmentOptions.MidlineLeft;
            var labelSize = GetOrAdd<UnityEngine.UI.LayoutElement>(title.gameObject);
            labelSize.preferredWidth = 200; labelSize.flexibleWidth = 1;
            var number = Text(row, "Value", value, font, 26, color);
            number.fontStyle = FontStyles.Bold;
            number.alignment = TextAlignmentOptions.MidlineRight;
            GetOrAdd<UnityEngine.UI.LayoutElement>(number.gameObject).preferredWidth = 58;
            return number;
        }
        private static TextMeshProUGUI Text(Transform parent, string name, string value, TMP_FontAsset font, int size, Color color)
        {
            var rect = Child(parent, name);
            var text = GetOrAdd<TextMeshProUGUI>(rect.gameObject);
            text.font = font;
            text.fontSize = size;
            text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = color;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }
    }
}
