using UnityEngine;

namespace TD
{
    public sealed class TDSpawner : MonoBehaviour
    {
        private TDWorld world;
        private float elapsed;
        public void Initialize(TDWorld context) { world=context; elapsed=0; world.SpawnEnemy(); }
        private void Update() { Step(Time.deltaTime); }
        public void Step(float deltaTime)
        {
            elapsed += Mathf.Max(0, deltaTime);
            while (elapsed >= world.Settings.spawnInterval)
            { elapsed -= world.Settings.spawnInterval; world.SpawnEnemy(); }
        }
    }
}
