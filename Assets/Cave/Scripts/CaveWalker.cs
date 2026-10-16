using UnityEngine;
using UnityEngine.InputSystem;

namespace Cave
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "TD", sourceAssembly: null, sourceClassName: "TDCaveWalker")]
    public sealed class CaveWalker : MonoBehaviour
    {
        private CaveExploration cave;
        private InputAction move;
        public void Initialize(CaveExploration owner, InputActionAsset existingInput)
        {
            var original = existingInput.FindAction("Player/Move");
            if (original == null) throw new System.InvalidOperationException("Cave needs the existing Player/Move action.");
            cave = owner;
            move = original.Clone();
            move.Enable();
        }
        private void Update()
        {
            if (move == null || !cave.Initialized || cave.Transitioning) return;
            MoveStep(move.ReadValue<Vector2>().x, Time.deltaTime);
        }
        public void MoveStep(float horizontal, float deltaTime)
        {
            if (!cave || cave.Transitioning) return;
            float x = transform.position.x + Mathf.Clamp(horizontal,-1,1) * cave.walkSpeed * Mathf.Max(0,deltaTime);
            x = Mathf.Clamp(x,.7f,CaveExploration.ExitX);
            transform.position = new Vector3(x,CaveExploration.PlayerY,0);
            if (x >= CaveExploration.ExitX) cave.ReachExit();
        }
        private void OnDestroy() { move?.Disable(); move?.Dispose(); }
    }
}
