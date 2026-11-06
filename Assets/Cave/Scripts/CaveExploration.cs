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
        public const float ShaftHalfWidth = 5.2f;
        public const float PlayerHeight = .95f;
        public const float PlayerHalfHeight = .45f;
        public Camera caveCamera;
        public CaveWalker player;
        public InputActionAsset existingInputActions;
        public CaveSettings settings;
        public CaveBuilder builder;
        public Transform exitGate;
        public Transform controlsLabel;
        public Sprite openingArt; // shown once at game launch, see OpeningSplash
        [NonSerialized] public CavePlatform[] platforms;
        [Min(.1f)] public float walkSpeed = 5.2f;
        [Min(.1f)] public float jumpSpeed = 8.5f;
        public bool Initialized { get; private set; }
        public bool Transitioning { get; private set; }
        public string InitializationError { get; private set; }
        public float BottomY { get; private set; }
        public float TopY { get; private set; }
        public int LayoutSeed { get; private set; }
        private float cameraVelocity;
        private GameObject overtimeFlash;

        private void Start()
        {
            try
            {
                if (!caveCamera || !player || !existingInputActions || !exitGate || !settings || !builder || !builder.HasArt)
                    throw new InvalidOperationException("Vertical Cave references are missing. Run Tools/Cave/Setup Cave Scene.");
                if (walkSpeed <= 0 || jumpSpeed <= 0 || float.IsNaN(walkSpeed) || float.IsInfinity(walkSpeed))
                    throw new InvalidOperationException("Cave movement speeds must be positive and finite.");
                if (!Application.CanStreamedLevelBeLoaded(TDScenePath))
                    throw new InvalidOperationException("TD scene is not enabled in Build Settings. Run Tools/Cave/Setup Cave Scene.");
                SharedHud.EnsureExists();
                var session = GameSession.Instance;
                session.BeginPhase(GameSession.PlayPhase.Cave);
                LayoutSeed = settings.seed != 0 ? settings.seed : session.RunSeed;
                var cave = builder.Build(settings, session.Day, LayoutSeed);
                platforms = cave.platforms;
                BottomY = cave.bottomY;
                TopY = cave.topY;
                // TD door floats above platform 01; each dive starts from the door's centre.
                exitGate.position = new Vector3(platforms[0].transform.position.x, cave.exitY, 0);
                if (controlsLabel) controlsLabel.position = new Vector3(3.3f, cave.exitY + .9f, 0); // beside the TD door
                overtimeFlash = builder.BuildOvertimeFlash(caveCamera, settings.overtimeFlashColor);
                player.Initialize(this, existingInputActions);
                Initialized = true;
                if (openingArt) OpeningSplash.ShowOnce(openingArt);
                player.Launch(cave.spawn, Vector2.zero, 0, 0);
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
            if (overtimeFlash)
            {
                var session = GameSession.Instance;
                bool flash = Initialized && !Transitioning && session && !session.HasEnded && session.IsOvertime
                    && Mathf.Repeat(session.ElapsedSeconds - GameSession.CaveSeconds, settings.overtimeFlashInterval)
                        < settings.overtimeFlashDuration;
                if (overtimeFlash.activeSelf != flash) overtimeFlash.SetActive(flash);
            }
        }

        private void LateUpdate()
        {
            if (Initialized && !Transitioning && !GameSession.Instance.HasEnded) UpdateCamera();
        }

        // Keep the shaft in view: look below while diving, slightly above while climbing; never past the floor or cap.
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
            float minY = BottomY + 1.2f, maxY = TopY - caveCamera.orthographicSize + .5f;
            if (maxY < minY) minY = maxY = (minY + maxY) / 2;
            float targetY = Mathf.Clamp(player.transform.position.y + (player.Velocity.y < -2 ? -1.8f : .8f), minY, maxY);
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
