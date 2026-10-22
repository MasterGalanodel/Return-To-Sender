using UnityEngine;
using UnityEngine.InputSystem;

namespace Cave
{
    // Handles side-scrolling player movement and boundary clamping within the cave exploration scene.
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "TD", sourceAssembly: null, sourceClassName: "TDCaveWalker")]
    public sealed class CaveWalker : MonoBehaviour
    {
        private CaveExploration cave;
        private InputAction move;

        // Clones and enables the shared movement input action, then links the walker to the cave manager.
        public void Initialize(CaveExploration owner, InputActionAsset existingInput)
        {
            var original = existingInput.FindAction("Player/Move");
            if (original == null) throw new System.InvalidOperationException("Cave needs the existing Player/Move action.");
            cave = owner;
            move = original.Clone();
            move.Enable();
        }

        // Polls horizontal movement input each frame and moves the player when exploration is active.
        private void Update()
        {
            if (move == null || !cave.Initialized || cave.Transitioning) return;
            MoveStep(move.ReadValue<Vector2>().x, Time.deltaTime);
        }

        // Applies horizontal movement clamped to room boundaries and triggers the exit transition once the threshold is reached.
        public void MoveStep(float horizontal, float deltaTime)
        {
            if (!cave || cave.Transitioning) return;
            float x = transform.position.x + Mathf.Clamp(horizontal,-1,1) * cave.walkSpeed * Mathf.Max(0,deltaTime);
            x = Mathf.Clamp(x,.7f,CaveExploration.ExitX);
            transform.position = new Vector3(x,CaveExploration.PlayerY,0);
            if (x >= CaveExploration.ExitX) cave.ReachExit();
        }

        // Disables and disposes the cloned input action when the player object is destroyed.
        private void OnDestroy() { move?.Disable(); move?.Dispose(); }
    }
}
