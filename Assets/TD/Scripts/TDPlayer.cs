using UnityEngine;
using UnityEngine.InputSystem;

namespace TD
{
    public sealed class TDPlayer : MonoBehaviour
    {
        private TDMap map;
        private TDSettings settings;
        private InputAction move;
        public bool drawCurrentCellGizmo = true;
        public Vector2Int CurrentCell => map.WorldToLogical(transform.position);
        public Vector2 LastInput { get; private set; }

        public void Initialize(TDMap hexMap, TDSettings config, InputActionAsset existingInput)
        {
            map = hexMap;
            settings = config;
            var original = existingInput ? existingInput.FindAction("Player/Move") : null;
            if (original == null) throw new System.InvalidOperationException("TD: the existing Player/Move Input System action is missing.");
            move = original.Clone();
            move.Enable();
        }

        private void Update()
        {
            LastInput = move.ReadValue<Vector2>();
            MoveStep(LastInput, Time.deltaTime);
        }

        public void MoveStep(Vector2 input, float deltaTime)
        {
            Vector2 displacement = Vector2.ClampMagnitude(input, 1) * settings.playerSpeed * Mathf.Max(0, deltaTime);
            int steps = Mathf.Max(1, Mathf.CeilToInt(displacement.magnitude / .15f));
            Vector3 step = new Vector3(displacement.x, displacement.y, 0) / steps;
            for (int i = 0; i < steps; i++)
            {
                Vector3 position = transform.position;
                Vector3 next = position + step;
                if (map.ContainsWorld(next)) transform.position = next;
                else
                {
                    // Resolve axes independently to permit sliding along a staggered boundary.
                    next = transform.position + new Vector3(step.x, 0, 0);
                    if (map.ContainsWorld(next)) transform.position = next;
                    next = transform.position + new Vector3(0, step.y, 0);
                    if (map.ContainsWorld(next)) transform.position = next;
                }
            }
        }

        private void OnDestroy() { move?.Disable(); move?.Dispose(); }
        private void OnDrawGizmos()
        {
            if (!drawCurrentCellGizmo || !map || !map.Terrain) return;
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(map.Center(CurrentCell), .25f);
        }
    }
}
