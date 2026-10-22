using Shared.HUD;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Cave
{
    // Manages the cave exploration phase, handling scene initialization, camera framing, and the transition into Tower Defense.
    [DisallowMultipleComponent]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "TD", sourceAssembly: null, sourceClassName: "TDCaveExploration")]
    public sealed class CaveExploration : MonoBehaviour
    {
        public const string ScenePath = "Assets/Cave/Scenes/CaveExploration.unity";
        public const string TDScenePath = "Assets/TD/Scenes/TD.unity";
        public const float RoomWidth = 24f;
        public const float FloorY = -3.1f;
        public const float PlayerY = FloorY + .24f;
        public const float ExitX = 22.5f;
        public Camera caveCamera;
        public CaveWalker player;
        public InputActionAsset existingInputActions;
        [Min(.1f)] public float walkSpeed = 4f;
        public bool Initialized { get; private set; }
        public bool Transitioning { get; private set; }
        public string InitializationError { get; private set; }

        // Validates scene references and build settings, ensures the shared HUD exists, initializes the player, and frames the initial camera view.
        private void Start()
        {
            try
            {
                if (!caveCamera || !player || !existingInputActions)
                    throw new InvalidOperationException("Cave references are missing. Run Tools/Cave/Setup Cave Scene.");
                if (walkSpeed <= 0 || float.IsNaN(walkSpeed) || float.IsInfinity(walkSpeed))
                    throw new InvalidOperationException("Cave walk speed must be positive and finite.");
                if (!Application.CanStreamedLevelBeLoaded(TDScenePath))
                    throw new InvalidOperationException("TD scene is not enabled in Build Settings. Run Tools/Cave/Setup Cave Scene.");
                SharedHud.EnsureExists();
                player.Initialize(this, existingInputActions);
                Initialized = true;
                UpdateCamera();
            }
            catch (Exception exception)
            {
                InitializationError = exception.Message;
                Debug.LogError(exception.Message, this);
            }
        }

        // Updates the camera position after movement each frame while exploration is actively running.
        private void LateUpdate() { if (Initialized && !Transitioning) UpdateCamera(); }

        // Configures letterboxing/pillarboxing for a fixed 16:9 aspect ratio and clamps camera tracking within room bounds.
        public void UpdateCamera()
        {
            const float aspect = 16f / 9;
            float screenAspect = (float)Mathf.Max(1, Screen.width) / Mathf.Max(1, Screen.height);
            if (screenAspect > aspect)
            {
                float width = aspect / screenAspect;
                caveCamera.rect = new Rect((1-width)/2, 0, width, 1);
            }
            else
            {
                float height = screenAspect / aspect;
                caveCamera.rect = new Rect(0, (1-height)/2, 1, height);
            }
            caveCamera.aspect = aspect;
            float halfWidth = caveCamera.orthographicSize * caveCamera.aspect;
            float center = halfWidth*2 >= RoomWidth ? RoomWidth/2 : Mathf.Clamp(player.transform.position.x, halfWidth, RoomWidth-halfWidth);
            caveCamera.transform.position = new Vector3(center, 0, -10);
        }

        // Disables the player and initiates an asynchronous scene transition to the Tower Defense scene when the exit is reached.
        public void ReachExit()
        {
            if (!Initialized || Transitioning) return;
            Transitioning = true;
            player.enabled = false;
            // The HUD/session lives in DontDestroyOnLoad and retains its current values.
            SceneManager.LoadSceneAsync(TDScenePath, LoadSceneMode.Single);
        }
    }
}
