using UnityEngine;

namespace Cave
{
    [DisallowMultipleComponent]
    public sealed class CaveExit : MonoBehaviour
    {
        [SerializeField] private CaveExploration cave;
        private bool armed; // the dive starts inside the door, so it only counts after the player has left it once
        public void Configure(CaveExploration owner) { cave = owner; }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (armed && cave && other.GetComponent<CaveWalker>()) cave.ReachExit();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponent<CaveWalker>()) armed = true;
        }
    }
}
