using UnityEngine;

namespace Cave
{
    [DisallowMultipleComponent]
    public sealed class CaveExit : MonoBehaviour
    {
        [SerializeField] private CaveExploration cave;
        [Tooltip("Seconds after the cave starts before the TD door accepts the player.")]
        [SerializeField, Min(0)] private float openDelay = 5f;
        private bool armed; // the dive starts inside the door, so it only counts after the player has left it once
        private float openAt;
        private SpriteRenderer frame;
        private Color frameColor;

        public void Configure(CaveExploration owner) { cave = owner; }
        private bool IsOpen => Time.time >= openAt;

        private void Start()
        {
            openAt = Time.time + openDelay;
            var frameTransform = transform.Find("Frame");
            if (frameTransform && frameTransform.TryGetComponent(out frame))
            {
                frameColor = frame.color;
                frame.color = Color.Lerp(frameColor, Color.gray, .75f); // dimmed while locked
            }
        }

        private void Update()
        {
            if (!frame || !IsOpen) return;
            frame.color = frameColor;
            frame = null; // restored once; stop updating
        }

        private void OnTriggerEnter2D(Collider2D other) => TryExit(other);

        // Stay as well as Enter, so a player already standing in the door leaves as soon as it unlocks.
        private void OnTriggerStay2D(Collider2D other) => TryExit(other);

        private void TryExit(Collider2D other)
        {
            if (armed && IsOpen && cave && other.GetComponent<CaveWalker>()) cave.ReachExit();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponent<CaveWalker>()) armed = true;
        }
    }
}
