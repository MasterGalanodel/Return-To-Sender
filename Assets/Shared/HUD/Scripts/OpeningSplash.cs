using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Shared.HUD.MenuUI;

namespace Shared.HUD
{
    // Start screen shown once per app launch: the title art over a panel with the intro text, the difficulty
    // slider (shared with the end screen) and Play / Exit buttons. Gameplay is paused (timeScale 0) until the
    // player presses Play, then the screen fades out to reveal the first Cave. Restarts skip it.
    public sealed class OpeningSplash : MonoBehaviour
    {
        public const float FadeInSeconds = .6f;
        public const float FadeOutSeconds = .6f;
        private const int SortingOrder = 1000; // above the shared HUD (100) and the end screen (900)

        // ␣ (U+2423) is missing from the HUD font, so the space bar is spelled out.
        public const string IntroText =
            "    Radiator’s cooking. Fuel’s running low.\n" +
            "    Gotta top up on Planet Little Compton”.\n\n" +
            "No worries.\n"+
            "<b>← → ↑ ↓</b> and <b>SPACE</b>. That’s all a good driver needs.";

        private static bool shown;
        private CanvasGroup group;
        private float previousTimeScale = 1;
        private bool starting;

        // Spawns the start screen the first time it is requested in this play session; later scene loads skip it.
        public static void ShowOnce(Sprite art)
        {
            if (shown || !art) return;
            shown = true;
            var hud = SharedHud.EnsureExists();
            NewOverlay("Start Screen", SortingOrder, typeof(OpeningSplash))
                .GetComponent<OpeningSplash>().Build(art, hud ? hud.Font : null);
        }

        // Clears the once-per-launch flag so entering Play mode again in the editor shows the screen again.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetShown() { shown = false; }

        private void Build(Sprite art, TMP_FontAsset font)
        {
            previousTimeScale = Time.timeScale > 0 ? Time.timeScale : 1;
            Time.timeScale = 0;
            group = GetComponent<CanvasGroup>();
            group.alpha = 0;
            group.blocksRaycasts = true;
            EnsureEventSystem();

            // The title art has a white background, so match it and fit the logo in the upper part of the screen.
            Stretch(NewUI("Backdrop", transform)).gameObject.AddComponent<Image>().color = Color.white;
            var logo = NewUI("Opening Art", transform);
            logo.anchorMin = new Vector2(.08f, .4f);
            logo.anchorMax = new Vector2(.92f, .98f);
            logo.offsetMin = logo.offsetMax = Vector2.zero;
            var image = logo.gameObject.AddComponent<Image>();
            image.sprite = art;
            image.preserveAspect = true;
            image.raycastTarget = false;

            // Bottom panel: intro text on the left, difficulty + buttons on the right.
            var panel = NewUI("Panel", transform);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, 0);
            panel.anchoredPosition = new Vector2(0, 28);
            panel.sizeDelta = new Vector2(1180, 0);
            panel.gameObject.AddComponent<Image>().color = PanelColor;
            var columns = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
            columns.padding = new RectOffset(36, 36, 26, 26);
            columns.spacing = 40;
            columns.childAlignment = TextAnchor.MiddleLeft;
            columns.childControlWidth = columns.childControlHeight = true;
            columns.childForceExpandWidth = columns.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var intro = Label(panel, "Intro", IntroText, font, 21, TextColor);
            intro.textWrappingMode = TextWrappingModes.Normal;
            intro.lineSpacing = 18;
            var introLayout = intro.gameObject.AddComponent<LayoutElement>();
            introLayout.preferredWidth = 640;
            introLayout.flexibleWidth = 1;

            var controls = NewUI("Controls", panel);
            controls.gameObject.AddComponent<LayoutElement>().preferredWidth = 400;
            var stack = controls.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.spacing = 10;
            stack.childControlWidth = stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;
            DifficultySlider(controls, font);
            Spacer(controls, 8);
            var buttons = ButtonRow(controls);
            var play = MakeButton(buttons, "Play", "PLAY", font, AccentColor, Color.black, Play);
            MakeButton(buttons, "Exit", "EXIT", font, ButtonColor, TextColor, QuitGame);
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(play.gameObject); // Enter / Space plays

            StartCoroutine(FadeIn());
        }

        private void Play()
        {
            if (starting) return;
            starting = true;
            StartCoroutine(FadeOutAndStart());
        }

        private IEnumerator FadeIn()
        {
            group.interactable = false;
            yield return Fade(0, 1, FadeInSeconds);
            group.interactable = true;
        }

        private IEnumerator FadeOutAndStart()
        {
            group.interactable = false;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            yield return Fade(1, 0, FadeOutSeconds);
            if (Time.timeScale == 0) Time.timeScale = previousTimeScale;
            Destroy(gameObject);
        }

        private IEnumerator Fade(float from, float to, float seconds)
        {
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, t / seconds));
                yield return null;
            }
            group.alpha = to;
        }

        // Exiting play mode (or quitting) while the screen is up must not leave the game frozen.
        private void OnDestroy() { if (!starting && Time.timeScale == 0) Time.timeScale = previousTimeScale; }
    }
}
