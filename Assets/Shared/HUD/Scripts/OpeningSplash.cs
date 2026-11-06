using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Shared.HUD
{
    // Full-screen opening image shown once per app launch: fades in, holds, then fades out to reveal the game.
    // Gameplay is paused (timeScale 0) while it is visible so the first phase timer doesn't run underneath it.
    public sealed class OpeningSplash : MonoBehaviour
    {
        public const float FadeInSeconds = .6f;
        public const float HoldSeconds = 2f;
        public const float FadeOutSeconds = .6f;
        private static bool shown;
        private CanvasGroup group;

        // Spawns the splash the first time it is requested in this play session; later scene loads skip it.
        public static void ShowOnce(Sprite art)
        {
            if (shown || !art) return;
            shown = true;
            var root = new GameObject("Opening Splash", typeof(Canvas), typeof(CanvasGroup), typeof(OpeningSplash));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000; // above the shared HUD (100)
            var splash = root.GetComponent<OpeningSplash>();
            splash.group = root.GetComponent<CanvasGroup>();
            splash.group.alpha = 0;
            splash.group.blocksRaycasts = true;
            Stretch(new GameObject("Backdrop", typeof(RectTransform), typeof(Image)), root.transform)
                .GetComponent<Image>().color = Color.black;
            var image = Stretch(new GameObject("Opening Art", typeof(RectTransform), typeof(Image)), root.transform)
                .GetComponent<Image>();
            image.sprite = art;
            image.preserveAspect = true;
            splash.StartCoroutine(splash.Play());
        }

        // Clears the once-per-launch flag so entering Play mode again in the editor shows the splash again.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetShown() { shown = false; }

        private static GameObject Stretch(GameObject go, Transform parent)
        {
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return go;
        }

        private IEnumerator Play()
        {
            float previousScale = Time.timeScale;
            Time.timeScale = 0;
            yield return Fade(0, 1, FadeInSeconds);
            yield return new WaitForSecondsRealtime(HoldSeconds);
            yield return Fade(1, 0, FadeOutSeconds);
            if (Time.timeScale == 0) Time.timeScale = previousScale > 0 ? previousScale : 1;
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
    }
}
