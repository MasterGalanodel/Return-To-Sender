using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using static Shared.HUD.MenuUI;

namespace Shared.HUD
{
    // Full-screen victory/defeat notice: the ending art fills the screen, with a side panel holding the
    // result text, a difficulty slider for the next run, and Restart / Exit buttons. Built entirely in code
    // (see MenuUI) so it needs no prefab of its own. Runs on unscaled time because the session
    // freezes gameplay (timeScale 0) when the run ends.
    public sealed class EndScreen : MonoBehaviour
    {
        public const float FadeInSeconds = .8f;
        private const int SortingOrder = 900; // above the shared HUD (100), below the opening splash (1000)

        private static readonly Color VictoryColor = AccentColor;
        private static readonly Color DefeatColor = new Color(.88f, .9f, .95f);

        private static EndScreen current;
        private GameSession session;
        private CanvasGroup group;

        // Shows the end screen for the session's outcome; does nothing if one is already up.
        public static void Show(GameSession session, Sprite art, TMP_FontAsset font)
        {
            if (current || !session || !session.HasEnded) return;
            current = NewOverlay("End Screen", SortingOrder, typeof(EndScreen)).GetComponent<EndScreen>();
            current.Build(session, art, font);
        }

        public static void Hide() { if (current) Destroy(current.gameObject); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() { current = null; }

        private void Build(GameSession owner, Sprite art, TMP_FontAsset font)
        {
            session = owner;
            session.Changed += CloseIfRunRestarted;
            group = GetComponent<CanvasGroup>();
            group.alpha = 0;
            EnsureEventSystem();
            bool victory = session.Outcome == GameSession.RunOutcome.Victory;

            CoverArt(transform, art);

            // Notice panel in the art's empty lower-left space (the planet sits centre-right).
            var panel = SidePanel(transform, 430, new Vector2(56, -40));
            var title = Label(panel, "Title", victory ? "VICTORY" : "DEFEAT", font, 54, victory ? VictoryColor : DefeatColor);
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 6;
            Label(panel, "Subtitle", victory
                    ? "A successful visit to Planet Little Compton. Darlington Hall awaits your return, sir."
                    : "Unresolved business on Planet Little Compton. Your stay has been extended.",
                font, 20, TextColor).textWrappingMode = TextWrappingModes.Normal;
            Label(panel, "Stats", $"DAY {session.Day}/{GameSession.TotalDays}   ·   HP {session.Health}", font, 17, MutedColor);
            if (!string.IsNullOrEmpty(session.ResultMessage))
                Label(panel, "Detail", session.ResultMessage, font, 15, MutedColor).textWrappingMode = TextWrappingModes.Normal;

            Spacer(panel, 12);
            DifficultySlider(panel, font); // takes effect from the next run (after Restart)
            Spacer(panel, 14);

            var buttons = ButtonRow(panel);
            var restart = MakeButton(buttons, "Restart", "RESTART", font, victory ? VictoryColor : DefeatColor, Color.black, Restart);
            MakeButton(buttons, "Exit", "EXIT", font, ButtonColor, TextColor, QuitGame);
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(restart.gameObject); // keyboard/gamepad: Enter restarts

            StartCoroutine(FadeIn());
        }

        private void Restart()
        {
            if (!session) return;
            session.ResetForNewGame(); // restores timeScale and clears the run; Changed closes this screen
            SceneManager.LoadScene(0);
        }

        private void CloseIfRunRestarted() { if (session && !session.HasEnded) Destroy(gameObject); }

        private void OnDestroy()
        {
            if (session) session.Changed -= CloseIfRunRestarted;
            if (current == this) current = null;
        }

        private IEnumerator FadeIn()
        {
            group.interactable = false;
            for (float t = 0; t < FadeInSeconds; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.SmoothStep(0, 1, t / FadeInSeconds);
                yield return null;
            }
            group.alpha = 1;
            group.interactable = true;
        }
    }
}
