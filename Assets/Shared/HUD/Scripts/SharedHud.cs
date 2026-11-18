using TMPro;
using UnityEngine;

namespace Shared.HUD
{
    // Displays persistent player stats on screen and adjusts layout to match the device safe area.
    [RequireComponent(typeof(GameSession))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "TD", sourceAssembly: null, sourceClassName: "TDHud")]
    public sealed class SharedHud : MonoBehaviour
    {
        public const string ResourceName = "SharedHud";
        [SerializeField] private TextMeshProUGUI healthValue;
        [SerializeField] private TextMeshProUGUI towerValue;
        [SerializeField] private TextMeshProUGUI dayValue;
        [SerializeField] private TextMeshProUGUI timerValue;
        [SerializeField] private RectTransform resultPanel;
        [SerializeField] private TextMeshProUGUI resultTitle;
        [SerializeField] private TextMeshProUGUI resultDetail;
        [SerializeField] private RectTransform safeArea;
        [SerializeField] private UnityEngine.UI.Image healthIcon;
        [SerializeField] private Sprite healthCritical;
        [SerializeField] private Sprite healthHealing;
        [SerializeField] private Sprite healthGood;
        [Header("End screen")]
        [SerializeField] private Sprite victoryArt;
        [SerializeField] private Sprite defeatArt;
        public const int HealthCriticalBelow = 60;
        public const int HealthGoodAbove = 80;
        private GameSession session;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;
        private float buildWarningUntil;
        private static readonly Color WarningColor = new Color(1, .25f, .23f);
        private static readonly Color BuildColor = new Color(.46f, .89f, .76f);

        // HUD font, reused by the code-built start and end screens.
        public TMP_FontAsset Font => resultTitle ? resultTitle.font : null;

        // Automatically ensures the shared HUD is instantiated after scene load during play mode.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartSharedHud() { EnsureExists(); }

        // Returns the existing HUD instance or loads and instantiates the SharedHud prefab from Resources.
        public static SharedHud EnsureExists()
        {
            if (GameSession.Instance) return GameSession.Instance.GetComponent<SharedHud>();
            var prefab = Resources.Load<GameObject>(ResourceName);
            if (!prefab)
            {
                Debug.LogError("Shared HUD: prefab is missing. Run Tools/Shared/HUD/Setup Shared HUD.");
                return null;
            }
            return Instantiate(prefab).GetComponent<SharedHud>();
        }

        // Assigns UI text element references and the safe area container, typically used during scene setup.
        public void Configure(TextMeshProUGUI health, TextMeshProUGUI towers,
            RectTransform safeAreaRect, TextMeshProUGUI days, TextMeshProUGUI timer,
            RectTransform result, TextMeshProUGUI title, TextMeshProUGUI detail)
        {
            healthValue = health; towerValue = towers; safeArea = safeAreaRect;
            dayValue = days; timerValue = timer; resultPanel = result; resultTitle = title; resultDetail = detail;
        }

        // Assigns the health icon Image and the sprites shown for critical (<60), healing (60-80) and good (>80) health.
        public void ConfigureHealthIcons(UnityEngine.UI.Image icon, Sprite critical, Sprite healing, Sprite good)
        {
            healthIcon = icon; healthCritical = critical; healthHealing = healing; healthGood = good;
        }

        // Assigns the full-screen art used by the victory and defeat end screens.
        public void ConfigureEndScreen(Sprite victory, Sprite defeat) { victoryArt = victory; defeatArt = defeat; }

        // Picks the health icon sprite for the given HP.
        private Sprite HealthSprite(int hp)
        {
            if (hp < HealthCriticalBelow) return healthCritical;
            if (hp > HealthGoodAbove) return healthGood;
            return healthHealing;
        }

        // Subscribes to session state changes, renders the initial values, and applies the current safe area.
        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            session = GetComponent<GameSession>();
            session.Changed += Refresh;
            session.BuildDenied += WarnBuild;
            Refresh();
            UpdateSafeArea();
        }

        // Unsubscribes from session change notifications when disabled.
        private void OnDisable()
        {
            if (!session) return;
            session.Changed -= Refresh;
            session.BuildDenied -= WarnBuild;
        }

        private void WarnBuild() { buildWarningUntil = Time.unscaledTime + 1.2f; }

        // Updates all on-screen UI text elements with the latest health and token values from the session.
        private void Refresh()
        {
            if (!session) return;
            if (healthValue) healthValue.SetText("{0}", session.Health);
            if (healthIcon)
            {
                var sprite = HealthSprite(session.Health);
                if (sprite && healthIcon.sprite != sprite) healthIcon.sprite = sprite;
            }
            if (towerValue) towerValue.SetText("{0}", session.TowerTokens);
            if (dayValue) dayValue.SetText("DAY {0}/6", session.Day);
            if (timerValue)
            {
                string phase = session.Phase == GameSession.PlayPhase.TowerDefense ? "TD" : "CAVE";
                timerValue.text = $"{phase} {Mathf.FloorToInt(session.ElapsedSeconds)}/{session.PhaseDurationSeconds}s";
                timerValue.color = session.IsOvertime ? WarningColor : Color.white;
            }
            // The full-screen end screen replaces the small result panel; the panel stays as a fallback without art.
            var endArt = session.Outcome == GameSession.RunOutcome.Victory ? victoryArt : defeatArt;
            if (session.HasEnded && endArt) EndScreen.Show(session, endArt, resultTitle ? resultTitle.font : null);
            else if (!session.HasEnded) EndScreen.Hide();
            if (resultPanel) resultPanel.gameObject.SetActive(session.HasEnded && !endArt);
            if (resultTitle) resultTitle.text = session.Outcome == GameSession.RunOutcome.Victory ? "VICTORY" : "GAME OVER";
            if (resultDetail) resultDetail.text = $"Day {session.Day}/6  |  HP {session.Health}\n{session.ResultMessage}";
        }

        // Detects screen resolution or safe area changes each frame to keep UI anchors aligned.
        private void LateUpdate()
        {
            if (lastSafeArea != Screen.safeArea || lastScreenSize != new Vector2Int(Screen.width, Screen.height)) UpdateSafeArea();
            if (session && towerValue)
            {
                towerValue.color = !session.HasEnded && Time.unscaledTime < buildWarningUntil
                    ? Color.Lerp(WarningColor, Color.white, .5f + .5f * Mathf.Sin(Time.unscaledTime * 24))
                    : session.TowerTokens == 0 ? WarningColor : BuildColor;
            }
        }

        // Adjusts RectTransform anchors to constrain HUD content within the physical screen safe area.
        private void UpdateSafeArea()
        {
            if (!safeArea || Screen.width <= 0 || Screen.height <= 0) return;
            lastSafeArea = Screen.safeArea;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            safeArea.anchorMin = new Vector2(lastSafeArea.xMin / Screen.width, lastSafeArea.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(lastSafeArea.xMax / Screen.width, lastSafeArea.yMax / Screen.height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }
    }
}
