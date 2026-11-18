using Shared.HUD;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Cave
{
    // Runs the Cave phase: builds the day's cave, drives the phase timer, follows the player with the camera and returns to TD.
    [DisallowMultipleComponent, DefaultExecutionOrder(-100)]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "TD", sourceAssembly: null, sourceClassName: "TDCaveExploration")]
    public sealed class CaveExploration : MonoBehaviour
    {
        // ---------- Scenes ----------
        public const string ScenePath = "Assets/Cave/Scenes/CaveExploration.unity";
        public const string TDScenePath = "Assets/TD/Scenes/TD.unity";

        // ---------- Shared dimensions (used by CaveBuilder, CaveWalker, CavePlatform, CaveSettings) ----------
        public const float ShaftHalfWidth = 5.2f;
        public const float PlayerHeight = .95f;
        public const float PlayerHalfHeight = .45f;

        // ---------- Movement defaults (CaveSetup writes these into the scene) ----------
        public const float DefaultWalkSpeed = 5.2f;
        public const float DefaultJumpSpeed = 8.5f;

        // ---------- Controls hint ----------
        private const float ControlsLabelX = 3.3f;          // world X, to the right of the TD door
        private const float ControlsLabelAboveExit = .9f;   // height above the door's base

        // ---------- Camera ----------
        public const float TargetAspect = 16f / 9;          // game view is letterboxed/pillarboxed to this ratio
        private const float CameraZ = -10;
        private const float CameraFloorMargin = 1.2f;       // lowest camera Y = cave floor + this
        private const float CameraCapMargin = .5f;          // lets the view rise this far past the cave top before clamping
        private const float DivingSpeedThreshold = -2;      // vertical speed below this counts as "diving"
        private const float DivingLookAhead = -1.8f;        // camera offset below the player while diving
        private const float ClimbingLookAhead = .8f;        // camera offset above the player otherwise
        private const float CameraSmoothTime = .12f;

        // ---------- Scene references (wired by Tools/Cave/Setup Cave Scene) ----------
        public Camera caveCamera;
        public CaveWalker player;
        public InputActionAsset existingInputActions;
        public CaveSettings settings;
        public CaveBuilder builder;
        public Transform exitGate;
        public Transform controlsLabel;
        public Sprite openingArt; // shown once at game launch, see OpeningSplash
        [NonSerialized] public CavePlatform[] platforms; // index 0 = platform 01 under the TD door
        [Min(.1f)] public float walkSpeed = DefaultWalkSpeed;
        [Min(.1f)] public float jumpSpeed = DefaultJumpSpeed;

        // ---------- Runtime state ----------
        public bool Initialized { get; private set; }
        public bool Transitioning { get; private set; } // true once the TD scene load has started
        public string InitializationError { get; private set; }
        public float BottomY { get; private set; }
        public float TopY { get; private set; }
        public int LayoutSeed { get; private set; }
        private float cameraVelocity;   // SmoothDamp state
        private GameObject overtimeFlash;

        // Validates references, generates today's cave and spawns the player at the TD door.
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
                // A fixed seed in settings overrides the per-run seed (for testing the same caves every run).
                LayoutSeed = settings.seed != 0 ? settings.seed : session.RunSeed;
                var cave = builder.Build(settings, session.Day, LayoutSeed);
                platforms = cave.platforms;
                BottomY = cave.bottomY;
                TopY = cave.topY;
                // TD door floats above platform 01; each dive starts from the door's centre.
                exitGate.position = new Vector3(platforms[0].transform.position.x, cave.exitY, 0);
                if (controlsLabel) controlsLabel.position = new Vector3(ControlsLabelX, cave.exitY + ControlsLabelAboveExit, 0);
                overtimeFlash = builder.BuildOvertimeFlash(caveCamera, settings.overtimeFlashColor);
                player.Initialize(this, existingInputActions);
                Initialized = true;
                Shared.Audio.SharedAudio.PlayBgm(settings.bgm, settings.bgmVolume, 0, settings.bgmLoopSeconds);
                if (openingArt) OpeningSplash.ShowOnce(openingArt);
                player.Launch(cave.spawn, Vector2.zero, 0, 0);
                UpdateCamera(true);
            }
            catch (Exception exception)
            {
                // Keep the error so the HUD/tests can show it instead of silently running a broken cave.
                InitializationError = exception.Message;
                Debug.LogError(exception.Message, this);
            }
        }

        // Advances the phase timer and blinks the red overtime overlay once the cave time limit has passed.
        private void Update()
        {
            if (Initialized && !Transitioning) GameSession.Instance.StepPhase(Time.deltaTime);
            if (overtimeFlash)
            {
                var session = GameSession.Instance;
                // Time since overtime began, wrapped into one flash interval; visible for the first `overtimeFlashDuration` of each.
                bool flash = Initialized && !Transitioning && session && !session.HasEnded && session.IsOvertime
                    && Mathf.Repeat(session.ElapsedSeconds - GameSession.CaveSeconds, settings.overtimeFlashInterval)
                        < settings.overtimeFlashDuration;
                if (overtimeFlash.activeSelf != flash) overtimeFlash.SetActive(flash);
            }
        }

        // Camera follows after physics/movement so it never lags a frame behind the player.
        private void LateUpdate()
        {
            if (Initialized && !Transitioning && !GameSession.Instance.HasEnded) UpdateCamera();
        }

        // Keep the shaft in view: look below while diving, slightly above while climbing; never past the floor or cap.
        public void UpdateCamera(bool snap = false)
        {
            // Letterbox (bars top/bottom) or pillarbox (bars left/right) so the visible area is always TargetAspect.
            float screenAspect = (float)Mathf.Max(1, Screen.width) / Mathf.Max(1, Screen.height);
            if (screenAspect > TargetAspect)
            {
                float width = TargetAspect / screenAspect;
                caveCamera.rect = new Rect((1 - width) / 2, 0, width, 1);
            }
            else
            {
                float height = screenAspect / TargetAspect;
                caveCamera.rect = new Rect(0, (1 - height) / 2, 1, height);
            }
            caveCamera.aspect = TargetAspect;
            float minY = BottomY + CameraFloorMargin, maxY = TopY - caveCamera.orthographicSize + CameraCapMargin;
            // Cave shorter than one screen: lock the camera to the middle.
            if (maxY < minY) minY = maxY = (minY + maxY) / 2;
            float lookAhead = player.Velocity.y < DivingSpeedThreshold ? DivingLookAhead : ClimbingLookAhead;
            float targetY = Mathf.Clamp(player.transform.position.y + lookAhead, minY, maxY);
            float y = snap ? targetY : Mathf.SmoothDamp(caveCamera.transform.position.y, targetY, ref cameraVelocity, CameraSmoothTime,
                Mathf.Infinity, Time.deltaTime);
            if (snap) cameraVelocity = 0;
            caveCamera.transform.position = new Vector3(0, y, CameraZ);
        }

        // Puts the player back on the last platform they safely stood on (e.g. after falling out of bounds).
        public void RecoverPlayer(int lastSafeStep)
        {
            int index = Mathf.Clamp(lastSafeStep, 0, platforms.Length - 1);
            player.TeleportTo(platforms[index].SafePosition);
            UpdateCamera(true);
        }

        // Called by CaveExit when the player re-enters the TD door: ends the cave phase once and loads TD.
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
