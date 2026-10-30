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
        [SerializeField] private TextMeshProUGUI updateValue;
        [SerializeField] private RectTransform safeArea;
        private GameSession session;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;

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
        public void Configure(TextMeshProUGUI health, TextMeshProUGUI towers, TextMeshProUGUI updates, RectTransform safeAreaRect)
        { healthValue = health; towerValue = towers; updateValue = updates; safeArea = safeAreaRect; }

        // Subscribes to session state changes, renders the initial values, and applies the current safe area.
        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            session = GetComponent<GameSession>();
            session.Changed += Refresh;
            Refresh();
            UpdateSafeArea();
        }

        // Unsubscribes from session change notifications when disabled.
        private void OnDisable() { if (session) session.Changed -= Refresh; }

        // Updates all on-screen UI text elements with the latest health and token values from the session.
        private void Refresh()
        {
            if (!healthValue || !towerValue || !updateValue) return;
            healthValue.SetText("{0}", session.Health);
            towerValue.SetText("{0}", session.TowerTokens);
            updateValue.SetText("{0}", session.UpdateTokens);
        }

        // Detects screen resolution or safe area changes each frame to keep UI anchors aligned.
        private void LateUpdate()
        {
            if (lastSafeArea != Screen.safeArea || lastScreenSize != new Vector2Int(Screen.width, Screen.height)) UpdateSafeArea();
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
