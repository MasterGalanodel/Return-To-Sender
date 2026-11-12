using Shared.HUD;
using UnityEngine;

namespace TD
{
    // Spawns enemies at a regular time interval: the day's TDSettings.spawnIntervalFactor × current turret levels.
    public sealed class TDSpawner : MonoBehaviour
    {
        private TDWorld world;
        private float elapsed;

        // Connects the spawner to the active world context and spawns the first enemy immediately.
        public void Initialize(TDWorld context) { world=context; elapsed=0; world.SpawnEnemy(); }

        // Advances the spawn countdown timer using delta time.
        private void Update() { Step(Time.deltaTime); }

        // Seconds between spawns today: TDSettings.spawnIntervalFactor for the day × current turret levels.
        public float SpawnInterval
        {
            get
            {
                var session = GameSession.Instance;
                return world.Settings.SpawnIntervalOnDay(session ? session.Day : 1, CurrentTurretLevels(session));
            }
        }

        // Sum of every standing turret's level, 1-based (a level-1 turret counts 1, level-2 counts 2).
        private int CurrentTurretLevels(GameSession session)
        {
            if (!session) return 0;
            int maxLevel = world.Settings.towerLevels.Length - 1;
            int total = 0;
            foreach (var cell in session.TowerCells)
                total += Mathf.Clamp(session.TowerLevelAt(cell), 0, maxLevel) + 1;
            return total;
        }

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
