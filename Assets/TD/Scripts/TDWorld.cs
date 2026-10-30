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
        private readonly List<TDRewardPickup> rewards = new List<TDRewardPickup>();
        private readonly TDMap map;
        public IReadOnlyList<TDRewardPickup> Rewards => rewards;
        public IReadOnlyList<TDProjectile> Projectiles => projectiles;
        public IReadOnlyList<TDEnemy> Enemies => enemies;
        public TDSettings Settings { get; }
        public SharedShapes Shapes { get; }
        public Transform ActorsRoot { get; }
        private readonly List<Vector2Int> spawnCells;
        private readonly List<Vector2Int> baseCells;
        public int SpawnedCount { get; private set; }
        public int ExitedCount { get; private set; }
        public int KilledCount { get; private set; }
        public int ShotsFired { get; private set; }
        public int Hits { get; private set; }
        public bool CanSimulate => !GameSession.Instance || GameSession.Instance.IsPhaseActive(GameSession.PlayPhase.TowerDefense);

        // Initializes the simulation world with configuration settings, shape renderers, and the map enemies cross.
        public TDWorld(TDSettings settings, SharedShapes shapes, Transform actorsRoot, TDMap hexMap)
        {
            Settings=settings; Shapes=shapes; ActorsRoot=actorsRoot; map=hexMap;
            baseCells=map.BaseCells();
            spawnCells=map.EdgeCells().FindAll(cell => map.TerrainAt(cell) != TDMap.Base);
            if (baseCells.Count == 0) throw new System.InvalidOperationException("TD: the map has no B (Base) tiles; gameplay startup is disabled.");
            if (spawnCells.Count == 0) throw new System.InvalidOperationException("TD: every map-edge tile is Base, so enemies have nowhere to spawn.");
        }

        // True when the world position lies on a Base cell.
        public bool IsBase(Vector3 world) => map.TerrainAt(map.WorldToLogical(world)) == TDMap.Base;

        // Spawns an enemy on a random map-edge cell, walking straight at a random Base cell.
        public TDEnemy SpawnEnemy()
        {
            Vector3 start = map.Center(spawnCells[Random.Range(0, spawnCells.Count)]);
            Vector3 target = map.Center(baseCells[Random.Range(0, baseCells.Count)]);
            return SpawnEnemy(start, target);
        }

        // Instantiates and tracks a new enemy actor walking straight from start toward target.
        public TDEnemy SpawnEnemy(Vector3 start, Vector3 target)
        {
            var renderer = Shapes.CreateActor("Enemy", Shapes.enemy, start, .4f, 20, ActorsRoot);
            var enemy = renderer.gameObject.AddComponent<TDEnemy>();
            enemy.Initialize(this, start, target);
            enemies.Add(enemy);
            SpawnedCount++;
            return enemy;
        }

        // Removes an enemy from active tracking and increments exited or killed counters.
        // An exited enemy reached the Base and costs 1 HP.
        public void RemoveEnemy(TDEnemy enemy, bool exited, bool killed = false)
        {
            if (!enemies.Remove(enemy)) return;
            if (exited)
            {
                ExitedCount++;
                if (CanSimulate && GameSession.Instance) GameSession.Instance.Damage(1);
            }
            else if (killed)
            {
                KilledCount++;
                if (!CanSimulate) return;
                TDEnemyExplosion.Spawn(this, enemy.transform.position);
                if (Settings.buildRewardDropChance >= 1
                    || (Settings.buildRewardDropChance > 0 && Random.value < Settings.buildRewardDropChance))
                    SpawnBuildReward(enemy.transform.position);
            }
        }

        public TDRewardPickup SpawnBuildReward(Vector3 deathPosition)
        {
            if (!CanSimulate) return null;
            Vector3 position = deathPosition;
            // Reject off-map scatter so every drop remains reachable by the player.
            for (int attempt = 0; attempt < 32; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * Settings.buildRewardDropRadius;
                Vector3 candidate = deathPosition + new Vector3(offset.x, offset.y, 0);
                if (!map || map.ContainsWorld(candidate)) { position = candidate; break; }
            }
            var root = new GameObject("Build Reward (+1)");
            root.transform.SetParent(ActorsRoot, false);
            root.transform.position = position;
            var reward = root.AddComponent<TDRewardPickup>();
            reward.Initialize(this);
            rewards.Add(reward);
            return reward;
        }

        public void RemoveReward(TDRewardPickup reward) { rewards.Remove(reward); }

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
        public void Dispose() { enemies.Clear(); projectiles.Clear(); rewards.Clear(); }

        public void Retreat()
        {
            foreach (var enemy in enemies)
                if (enemy) { enemy.gameObject.SetActive(false); Object.Destroy(enemy.gameObject); }
            foreach (var projectile in projectiles)
                if (projectile) { projectile.gameObject.SetActive(false); Object.Destroy(projectile.gameObject); }
            foreach (var reward in rewards)
                if (reward) { reward.gameObject.SetActive(false); Object.Destroy(reward.gameObject); }
            enemies.Clear();
            projectiles.Clear();
            rewards.Clear();
        }
    }
}
