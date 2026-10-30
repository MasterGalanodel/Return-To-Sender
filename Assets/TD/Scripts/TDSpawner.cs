using Shared.HUD;
using UnityEngine;

namespace TD
{
    // Spawns enemies at regular time intervals, capped by the per-day maximum alive at once.
    public sealed class TDSpawner : MonoBehaviour
    {
        private TDWorld world;
        private float elapsed;

        // Connects the spawner to the active world context and spawns the first enemy immediately.
        public void Initialize(TDWorld context) { world=context; elapsed=0; world.SpawnEnemy(); }

        // Advances the spawn countdown timer using delta time.
        private void Update() { Step(Time.deltaTime); }

        // Most enemies allowed alive at once today, from TDSettings.maxEnemiesPerDay.
        public int MaxEnemies => world.Settings.MaxEnemiesOnDay(GameSession.Instance ? GameSession.Instance.Day : 1);

        // Accumulates delta time and triggers enemy spawns whenever the configured spawn interval is reached,
        // as long as fewer than today's maximum are alive.
        public void Step(float deltaTime)
        {
            if (world == null || !world.CanSimulate) return;
            elapsed += Mathf.Max(0, deltaTime);
            while (elapsed >= world.Settings.spawnInterval)
            {
                // At the cap the timer waits, so the next enemy appears as soon as one dies or reaches the Base.
                if (world.Enemies.Count >= MaxEnemies) { elapsed = world.Settings.spawnInterval; return; }
                elapsed -= world.Settings.spawnInterval;
                world.SpawnEnemy();
            }
        }
    }
}
