using Shared.HUD;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Cave
{
    [DisallowMultipleComponent, DefaultExecutionOrder(-100)]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "TD", sourceAssembly: null, sourceClassName: "TDCaveExploration")]
    public sealed class CaveExploration : MonoBehaviour
    {
        public const string ScenePath = "Assets/Cave/Scenes/CaveExploration.unity";
        public const string TDScenePath = "Assets/TD/Scenes/TD.unity";
        public const int StepCount = 40;
        public const float StepSpacing = 2.4f;
        public const float ShaftHalfWidth = 5.2f;
        public const float BottomY = -(StepCount - 1) * StepSpacing;
        public const float PlayerHeight = .95f;
        public const float PlayerHalfHeight = .45f;
        public Camera caveCamera;
        public CaveWalker player;
        public InputActionAsset existingInputActions;
        public CavePlatform[] platforms;
        public Transform exitGate;
        [Min(.1f)] public float walkSpeed = 5.2f;
        [Min(.1f)] public float jumpSpeed = 8.5f;
        public bool Initialized { get; private set; }
        public bool Transitioning { get; private set; }
        public string InitializationError { get; private set; }
        private float cameraVelocity;

        private void Start()
        {
            try
            {
                if (!caveCamera || !player || !existingInputActions || !exitGate || platforms == null || platforms.Length != StepCount)
                    throw new InvalidOperationException("Vertical Cave references are missing. Run Tools/Cave/Setup Cave Scene.");
                if (walkSpeed <= 0 || jumpSpeed <= 0 || float.IsNaN(walkSpeed) || float.IsInfinity(walkSpeed))
                    throw new InvalidOperationException("Cave movement speeds must be positive and finite.");
                if (!Application.CanStreamedLevelBeLoaded(TDScenePath))
                    throw new InvalidOperationException("TD scene is not enabled in Build Settings. Run Tools/Cave/Setup Cave Scene.");
                SharedHud.EnsureExists();
                GameSession.Instance.BeginPhase(GameSession.PlayPhase.Cave);
                player.Initialize(this, existingInputActions);
                Initialized = true;
                UpdateCamera(true);
            }
            catch (Exception exception)
            {
                InitializationError = exception.Message;
                Debug.LogError(exception.Message, this);
            }
        }

        private void Update()
        {
            if (Initialized && !Transitioning) GameSession.Instance.StepPhase(Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (Initialized && !Transitioning && !GameSession.Instance.HasEnded) UpdateCamera();
        }

        // Keep the shaft in view and show the next ledges below the player's feet.
        public void UpdateCamera(bool snap = false)
        {
            const float aspect = 16f / 9;
            float screenAspect = (float)Mathf.Max(1, Screen.width) / Mathf.Max(1, Screen.height);
            if (screenAspect > aspect)
            {
                float width = aspect / screenAspect;
                caveCamera.rect = new Rect((1 - width) / 2, 0, width, 1);
            }
            else
            {
                float height = screenAspect / aspect;
                caveCamera.rect = new Rect(0, (1 - height) / 2, 1, height);
            }
            caveCamera.aspect = aspect;
            float targetY = Mathf.Clamp(player.transform.position.y - 2.1f, BottomY + 1.2f, -1.6f);
            float y = snap ? targetY : Mathf.SmoothDamp(caveCamera.transform.position.y, targetY, ref cameraVelocity, .12f,
                Mathf.Infinity, Time.deltaTime);
            if (snap) cameraVelocity = 0;
            caveCamera.transform.position = new Vector3(0, y, -10);
        }

        public void RecoverPlayer(int lastSafeStep)
        {
            int index = Mathf.Clamp(lastSafeStep, 0, platforms.Length - 1);
            player.TeleportTo(platforms[index].SafePosition);
            UpdateCamera(true);
        }

        public void ReachExit()
        {
            if (!Initialized || Transitioning || !GameSession.Instance.IsPhaseActive(GameSession.PlayPhase.Cave)) return;
            GameSession.Instance.FinishCave();
            Transitioning = true;
            player.StopMoving();
            SceneManager.LoadSceneAsync(TDScenePath, LoadSceneMode.Single);
        }
    }
}
