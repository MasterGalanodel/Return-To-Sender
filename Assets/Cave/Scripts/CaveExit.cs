using UnityEngine;

namespace Cave
{
    [DisallowMultipleComponent]
    public sealed class CaveExit : MonoBehaviour
    {
        [SerializeField] private CaveExploration cave;
        public void Configure(CaveExploration owner) { cave = owner; }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (cave && other.GetComponent<CaveWalker>()) cave.ReachExit();
        }
    }
}
