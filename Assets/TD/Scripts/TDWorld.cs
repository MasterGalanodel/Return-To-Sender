using Shared.Graphics;
using Shared.HUD;
using System.Collections.Generic;
using UnityEngine;

namespace TD
{
    // Manages active enemies, projectiles, combat statistics, and actor lifecycles within the combat simulation.
    public sealed class TDWorld
    {
        private readonly List<TDEnemy> enemies = new List<TDEnemy>();
        private readonly List<TDProjectile> projectiles = new List<TDProjectile>();
        public IReadOnlyList<TDProjectile> Projectiles => projectiles;
        public IReadOnlyList<TDEnemy> Enemies => enemies;
        public TDSettings Settings { get; }
        public SharedShapes Shapes { get; }
        public Transform ActorsRoot { get; }
        public Vector3[] Route { get; }
        public int SpawnedCount { get; private set; }
        public int ExitedCount { get; private set; }
        public int KilledCount { get; private set; }
        public int ShotsFired { get; private set; }
        public int Hits { get; private set; }
        public bool CanSimulate => !GameSession.Instance || GameSession.Instance.IsPhaseActive(GameSession.PlayPhase.TowerDefense);

        // Initializes the simulation world with configuration settings, shape renderers, and the enemy route.
        public TDWorld(TDSettings settings, SharedShapes shapes, Transform actorsRoot, Vector3[] route)
        { Settings=settings; Shapes=shapes; ActorsRoot=actorsRoot; Route=route; }

        // Instantiates and tracks a new enemy actor at the beginning of the road route.
        public TDEnemy SpawnEnemy()
        {
            var renderer = Shapes.CreateActor("Enemy", Shapes.enemy, Route[0], .4f, 20, ActorsRoot);
            var enemy = renderer.gameObject.AddComponent<TDEnemy>();
            enemy.Initialize(this);
            enemies.Add(enemy);
            SpawnedCount++;
            return enemy;
        }

        // Removes an enemy from active tracking and increments exited or killed counters.
        public void RemoveEnemy(TDEnemy enemy, bool exited)
        {
            if (!enemies.Remove(enemy)) return;
            if (exited)
            {
                ExitedCount++;
                if (CanSimulate && GameSession.Instance) GameSession.Instance.Damage(1);
            }
            else KilledCount++;
        }

        // Instantiates and tracks a homing projectile directed at a specific target enemy.
        public TDProjectile FireProjectile(Vector3 position, TDEnemy target)
        {
            var renderer=Shapes.Create("Projectile",Shapes.circle,Color.yellow,position,.13f,25,ActorsRoot);
            var projectile=renderer.gameObject.AddComponent<TDProjectile>();
            projectile.Initialize(this,target);
            projectiles.Add(projectile);
            ShotsFired++;
            return projectile;
        }

        // Increments the total count of successful projectile impacts.
        public void RecordHit() { Hits++; }

        // Removes a finished or expired projectile from the active simulation list.
        public void RemoveProjectile(TDProjectile projectile) { projectiles.Remove(projectile); }

        // Clears all active enemy and projectile tracking lists upon world teardown.
        public void Dispose() { enemies.Clear(); projectiles.Clear(); }

        public void Retreat()
        {
            foreach (var enemy in enemies)
                if (enemy) { enemy.gameObject.SetActive(false); Object.Destroy(enemy.gameObject); }
            foreach (var projectile in projectiles)
                if (projectile) { projectile.gameObject.SetActive(false); Object.Destroy(projectile.gameObject); }
            enemies.Clear();
            projectiles.Clear();
        }
    }
}
