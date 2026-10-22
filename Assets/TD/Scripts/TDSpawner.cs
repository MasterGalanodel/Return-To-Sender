using UnityEngine;

namespace TD
{
    // Automatically spawns enemies at the start of the road at regular time intervals.
    public sealed class TDSpawner : MonoBehaviour
    {
        private TDWorld world;
        private float elapsed;

        // Connects the spawner to the active world context and spawns the first enemy immediately.
        public void Initialize(TDWorld context) { world=context; elapsed=0; world.SpawnEnemy(); }

        // Advances the spawn countdown timer using delta time.
        private void Update() { Step(Time.deltaTime); }

        // Accumulates delta time and triggers enemy spawns whenever the configured spawn interval is reached.
        public void Step(float deltaTime)
        {
            elapsed += Mathf.Max(0, deltaTime);
            while (elapsed >= world.Settings.spawnInterval)
            { elapsed -= world.Settings.spawnInterval; world.SpawnEnemy(); }
        }
    }
}
