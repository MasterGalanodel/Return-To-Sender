using Shared.HUD;
using UnityEngine;

namespace TD
{
    // Spawns enemies at a regular time interval that shrinks each day (TDSettings.spawnIntervalPerDay).
    public sealed class TDSpawner : MonoBehaviour
    {
        private TDWorld world;
        private float elapsed;

        // Connects the spawner to the active world context and spawns the first enemy immediately.
        public void Initialize(TDWorld context) { world=context; elapsed=0; world.SpawnEnemy(); }

        // Advances the spawn countdown timer using delta time.
        private void Update() { Step(Time.deltaTime); }

        // Seconds between spawns today, from TDSettings.spawnIntervalPerDay.
        public float SpawnInterval => world.Settings.SpawnIntervalOnDay(GameSession.Instance ? GameSession.Instance.Day : 1);

        // Accumulates delta time and spawns one enemy per elapsed interval. When the interval is shorter than
        // the frame, several enemies spawn in the same frame so the overall rate stays exact.
        public void Step(float deltaTime)
        {
            if (world == null || !world.CanSimulate) return;
            float interval = SpawnInterval;
            elapsed += Mathf.Max(0, deltaTime);
            while (elapsed >= interval)
            {
                elapsed -= interval;
                world.SpawnEnemy();
            }
        }
    }
}
