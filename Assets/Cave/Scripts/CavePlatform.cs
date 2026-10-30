using UnityEngine;

namespace Cave
{
    [DisallowMultipleComponent]
    public sealed class CavePlatform : MonoBehaviour
    {
        [SerializeField] private int stepIndex;
        [SerializeField] private float width = 3;
        public int StepIndex => stepIndex;
        public float Width => width;
        public float SurfaceY => transform.position.y;
        public Vector2 SafePosition => new Vector2(transform.position.x, SurfaceY + CaveExploration.PlayerHalfHeight + .04f);

        public void Configure(int index, float surfaceWidth) { stepIndex = index; width = surfaceWidth; }
    }
}
