using System;
using System.Collections.Generic;
using System.IO;
using Shared.HUD;
using TD;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class VerifyEnemyRewards
{
    public static object Main()
    {
        var bootstrap = Object.FindFirstObjectByType<TDBootstrap>();
        if (!EditorApplication.isPlaying || !bootstrap || !bootstrap.Initialized)
            throw new InvalidOperationException("Run this check in the initialized TD scene in Play mode.");
        var checks = new List<string>();
        Action<bool, string> check = (condition, message) => {
            if (!condition) throw new InvalidOperationException(message);
            checks.Add(message);
        };
        var session = GameSession.Instance;
        var randomState = UnityEngine.Random.state;
        var config = Object.Instantiate(bootstrap.settings);
        var root = new GameObject("Temporary Reward Verification");
        var world = new TDWorld(config, bootstrap.shapes, root.transform, bootstrap.Map);
        var collectorRoot = new GameObject("Test Collector");
        collectorRoot.transform.SetParent(root.transform, false);
        var collector = collectorRoot.AddComponent<TDClawCollector>();
        collector.enabled = false;
        collector.Initialize(world);
        Vector3 deathSpot = bootstrap.Player.transform.position;
        Vector3 edgeSpot = bootstrap.Map.Center(new Vector2Int(0, TDMap.Rows / 2));
        int sampledDrops = 0;
        try
        {
            check(Mathf.Approximately(config.buildRewardDropChance, .2f), "Default drop chance is 20%");
            check(Mathf.Approximately(config.buildRewardDropRadius, 2), "Default scatter radius is 2 world units");
            check(Mathf.Approximately(config.buildRewardCollectRadius, .6f), "Collection requires standing within 0.6 units");
            check(AssetDatabase.GetAssetPath(bootstrap.shapes.buildReward) == "Assets/Shared/HUD/Art/build.png", "Drop uses supplied build.png");
            check(AssetDatabase.GetAssetPath(bootstrap.shapes.claw) == "Assets/Shared/Claw/Art/claw.png", "Claw uses supplied claw.png");
            int tokens = session.TowerTokens, health = session.Health;
            config.buildRewardDropChance = 1;
            var enemy = world.SpawnEnemy();
            enemy.transform.position = deathSpot;
            enemy.ApplyDamage(config.enemyHealth - 1);
            check(enemy.IsAlive && world.Rewards.Count == 0, "Nonlethal damage creates no reward");
            enemy.ApplyDamage(1);
            check(!enemy.IsAlive && world.KilledCount == 1 && world.Rewards.Count == 1, "Lethal hit creates one reward with forced 100% chance");
            check(root.GetComponentsInChildren<TDEnemyExplosion>().Length == 1, "Killed enemy creates an explosion");
            enemy.ApplyDamage(100);
            check(world.KilledCount == 1 && world.Rewards.Count == 1, "Repeated lethal damage cannot duplicate drops");
            check(session.Health == health && session.TowerTokens == tokens, "A kill neither damages HP nor awards an uncollected token");
            var reward = world.Rewards[0];
            check(Vector2.Distance(reward.Position, deathSpot) <= 2.0001f && bootstrap.Map.ContainsWorld(reward.Position), "Drop lands within the kill radius on playable terrain");
            collector.transform.position = reward.Position + Vector3.right;
            check(!collector.TryCollect(), "C collection fails when the player is too far away");
            collector.transform.position = reward.Position;
            collector.StepCollection(2);
            check(session.TowerTokens == tokens && reward.IsAvailable, "Walking onto a drop does not collect it automatically");
            check(collector.TryCollect() && collector.IsBusy && reward.IsClaimed, "Nearby collection reserves the reward");
            check(!collector.TryCollect(), "Repeated C presses cannot start a second claw while busy");
            collector.StepCollection(.3f);
            check(session.TowerTokens == tokens, "Token is not credited while the claw descends");
            collector.StepCollection(1);
            check(session.TowerTokens == tokens + 1 && !collector.IsBusy && world.Rewards.Count == 0, "Claw retrieval awards exactly one build token");
            check(!reward.CompleteCollection() && session.TowerTokens == tokens + 1, "Completed reward cannot pay out twice");

            config.buildRewardDropChance = 0;
            var noDrop = world.SpawnEnemy();
            noDrop.ApplyDamage(config.enemyHealth);
            check(world.Rewards.Count == 0, "Zero chance produces no drop");
            int killsBefore = world.KilledCount, explosionsBefore = root.GetComponentsInChildren<TDEnemyExplosion>().Length;
            config.buildRewardDropChance = 1;
            var escaped = world.SpawnEnemy();
            escaped.MoveStep(10000);
            check(world.ExitedCount == 1 && session.Health == health - 1 && world.Rewards.Count == 0
                && world.KilledCount == killsBefore && root.GetComponentsInChildren<TDEnemyExplosion>().Length == explosionsBefore,
                "Escaping enemies cost one HP and create neither explosion nor reward");
            var removed = world.SpawnEnemy();
            Object.DestroyImmediate(removed.gameObject);
            check(world.KilledCount == killsBefore && world.Rewards.Count == 0, "Scene cleanup is not a rewarded kill");

            config.buildRewardDropChance = .2f;
            UnityEngine.Random.InitState(12345);
            for (int i = 0; i < 200; i++)
            {
                var sample = world.SpawnEnemy();
                sample.transform.position = i % 2 == 0 ? deathSpot : edgeSpot;
                sample.ApplyDamage(config.enemyHealth);
            }
            sampledDrops = world.Rewards.Count;
            check(sampledDrops >= 20 && sampledDrops <= 65, "Seeded 200-kill sample has a plausible 20% drop rate");
            foreach (var drop in world.Rewards)
                check(bootstrap.Map.ContainsWorld(drop.Position)
                    && Mathf.Min(Vector2.Distance(drop.Position, deathSpot), Vector2.Distance(drop.Position, edgeSpot)) <= 2.0001f,
                    "Sampled drop stays within radius and playable bounds");
            world.Retreat();
            check(world.Rewards.Count == 0 && world.Enemies.Count == 0, "Retreat clears uncollected rewards and enemies");

            config.buildRewardDropRadius = 0;
            var first = world.SpawnBuildReward(deathSpot);
            var second = world.SpawnBuildReward(deathSpot);
            collector.transform.position = deathSpot;
            check(collector.TryCollect(), "A claw can collect overlapping drops");
            check((first.IsClaimed ? 1 : 0) + (second.IsClaimed ? 1 : 0) == 1, "One press reserves only one overlapping reward");
            collector.StepCollection(.6f);
            collector.CancelCollection();
            check(first.IsAvailable && second.IsAvailable && session.TowerTokens == tokens + 1,
                "Canceled collection releases its drop without paying out");
            check(first.Icon.transform.localPosition.sqrMagnitude < .0001f && second.Icon.transform.localPosition.sqrMagnitude < .0001f,
                "Cancellation restores carried artwork to the original drop spot");
            check(collector.TryCollect(), "Canceled drops can be retried");
            world.Retreat();
            collector.StepCollection(1);
            check(!collector.IsBusy && session.TowerTokens == tokens + 1, "Retreat during retrieval cannot collect a removed drop");
            world.SpawnBuildReward(deathSpot);
            check(collector.TryCollect(), "An available drop starts collection before the phase deadline");
            session.StepPhase(GameSession.TowerDefenseSeconds);
            collector.StepCollection(1);
            check(!collector.IsBusy && session.TowerTokens == tokens + 1, "Phase deadline cancels in-flight collection without a token");
            check(!collector.TryCollect() && world.SpawnBuildReward(deathSpot) == null, "Expired phase blocks collection and new drops");
            int count = world.Rewards.Count;
            var frozen = world.SpawnEnemy();
            frozen.ApplyDamage(config.enemyHealth);
            check(frozen.IsAlive && world.Rewards.Count == count, "Combat cannot award rewards after the phase deadline");
            world.Retreat();
            session.BeginPhase(GameSession.PlayPhase.TowerDefense);
            session.Health = health;
            session.TowerTokens = tokens;
            var report = new { passed = true, checkCount = checks.Count, sampledKills = 200, sampledDrops, checks };
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/EnemyRewardChecks.json", Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
            return report;
        }
        finally
        {
            collector.CancelCollection();
            world.Dispose();
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(config);
            UnityEngine.Random.state = randomState;
        }
    }
}
