using Shared.Graphics;
using System.Collections.Generic;
using UnityEngine;

namespace TD
{
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

        public TDWorld(TDSettings settings, SharedShapes shapes, Transform actorsRoot, Vector3[] route)
        { Settings=settings; Shapes=shapes; ActorsRoot=actorsRoot; Route=route; }

        public TDEnemy SpawnEnemy()
        {
            var renderer = Shapes.Create("Enemy", Shapes.circle, Color.black, Route[0], .4f, 20, ActorsRoot);
            var enemy = renderer.gameObject.AddComponent<TDEnemy>();
            enemy.Initialize(this);
            enemies.Add(enemy);
            SpawnedCount++;
            return enemy;
        }

        public void RemoveEnemy(TDEnemy enemy, bool exited)
        {
            if (!enemies.Remove(enemy)) return;
            if (exited) ExitedCount++; else KilledCount++;
        }

        public TDProjectile FireProjectile(Vector3 position, TDEnemy target)
        {
            var renderer=Shapes.Create("Projectile",Shapes.circle,Color.yellow,position,.13f,25,ActorsRoot);
            var projectile=renderer.gameObject.AddComponent<TDProjectile>();
            projectile.Initialize(this,target);
            projectiles.Add(projectile);
            ShotsFired++;
            return projectile;
        }

        public void RecordHit() { Hits++; }
        public void RemoveProjectile(TDProjectile projectile) { projectiles.Remove(projectile); }
        public void Dispose() { enemies.Clear(); projectiles.Clear(); }
    }
}
