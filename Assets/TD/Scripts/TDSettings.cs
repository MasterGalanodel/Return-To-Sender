using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TD
{
    // ScriptableObject asset defining balance parameters and map configurations for Tower Defense gameplay.
    [CreateAssetMenu(menuName = "TD/Prototype Settings")]
    public sealed class TDSettings : ScriptableObject
    {
        // One step of the tower upgrade ladder. Element 0 is what B builds on an empty F cell;
        // pressing B on a tower buys the next element.
        [Serializable]
        public sealed class TowerLevel
        {
            [Tooltip("Turret art with the barrel pointing straight up; the sprite pivot is where it spins.")]
            public Sprite sprite;
            [Tooltip("Build tokens spent to reach this level (on top of earlier levels).")]
            [Min(1)] public int cost = 1;
            [Tooltip("World height of the whole sprite image.")]
            [Min(0.01f)] public float height = .8f;
            [Tooltip("Barrel tips in the unrotated sprite, 0–1 from its bottom-left corner. Each shot fires one projectile per tip.")]
            public Vector2[] muzzles = { new Vector2(.5f, 1) };
        }

        [Header("Player")][Min(0.01f)] public float playerSpeed = 4;
        [Header("Tower")][Min(0.01f)] public float towerRange = 3;
        [Min(0.01f)] public float towerFireRate = 1;
        [Tooltip("Degrees per second a turret swings toward its target. It only fires once roughly on aim.")]
        [Min(1)] public float towerTurnSpeed = 540;
        public TowerLevel[] towerLevels =
        {
            new TowerLevel { cost = 1, height = .8f, muzzles = new[] { new Vector2(.52f, .97f) } },
            new TowerLevel { cost = 2, height = .75f, muzzles = new[] { new Vector2(.323f, .98f), new Vector2(.692f, .98f) } },
        };
        [Min(0.01f)] public float projectileSpeed = 8;
        [Min(0.01f)] public float projectileDamage = 1;
        [Min(0.01f)] public float projectileLifetime = 8;
        [Header("Enemy")][Min(0.01f)] public float enemySpeed = 1;
        [Min(0.01f)] public float enemyHealth = 3;
        [Header("Difficulty")]
        [Tooltip("Length of each TD phase in seconds.")]
        [Min(1)] public int towerDefenseSeconds = 60;
        [Tooltip("Seconds between enemy spawns, per day (element 0 = day 1). Days past the end use the last value. "
            + "Intervals shorter than a frame spawn several enemies in the same frame.")]
        public float[] spawnIntervalPerDay = { 3, 1.5f, .75f, .375f, .1875f, .09375f };
        [Header("Enemy rewards")]
        [Tooltip("Chance a killed enemy drops a build token on day 1.")]
        [Range(0, 1)] public float buildRewardDropChance = .2f;
        [Tooltip("Each new day multiplies the drop chance by this (0.6 = day 2 is 60% of day 1, day 3 is 60% of day 2, ...).")]
        [Range(0, 1)] public float buildRewardDropChanceDailyMultiplier = .6f;
        [Min(0)] public float buildRewardDropRadius = 2;
        [Min(0.01f)] public float buildRewardCollectRadius = .6f;
        [Header("Camera")][Min(0.01f)] public float cameraZoom = 3.9f;
        [Header("Terrain")]
        public TextAsset packagedTerrain;
        public bool preferExternalMapInEditor = true;
        public string externalMapPath = @"E:\Miles\assets\arab_32x18.txt";
        [Header("Desert variants")]
        [Tooltip("Tiles drawn over D cells, picked per cell from Perlin noise. Leave empty for plain colour.")]
        public TileBase[] desertTiles;
        [Tooltip("0.01–0.05 = very large clumps, 0.06–0.10 = medium, 0.12+ = small")]
        [Min(0.001f)] public float desertNoiseScale = .08f;
        public Vector2 desertNoiseOffset = new Vector2(1000, 1000);
        [Header("Base fence")]
        [Tooltip("Drawn over every F (Fence) cell, one picked at random per cell. Leave empty for plain colour.")]
        public Sprite[] fenceSprites;
        [Tooltip("Change to reshuffle which fence variant each boundary cell gets.")]
        public int fenceSeed = 1;
        [Header("Road")]
        [Tooltip("Flat-top hex road art, rotated per R cell to follow its neighbours. Leave empty for plain colour.\n"
            + "Straight: road from the upper-left edge to the lower-right edge.\n"
            + "Turn: road from the upper-left edge to the bottom edge.\n"
            + "Junction: Y joining the upper-left, upper-right and bottom edges.")]
        public Sprite roadStraight;
        public Sprite roadTurn;
        public Sprite roadJunction;

        // Confirms that all speed, range, rate, damage, health, interval, and zoom settings are positive and finite numbers.
        public void Validate()
        {
            float[] values = { playerSpeed, towerRange, towerFireRate, projectileSpeed,
                projectileDamage, projectileLifetime, enemySpeed, enemyHealth, cameraZoom,
                buildRewardCollectRadius };
            foreach (float value in values)
                if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0)
                    throw new ArgumentException("TD: all speed, range, rate, damage, health, interval and zoom settings must be finite and positive.");
            if (float.IsNaN(buildRewardDropChance) || buildRewardDropChance < 0 || buildRewardDropChance > 1
                || float.IsNaN(buildRewardDropChanceDailyMultiplier) || buildRewardDropChanceDailyMultiplier < 0 || buildRewardDropChanceDailyMultiplier > 1
                || float.IsNaN(buildRewardDropRadius) || float.IsInfinity(buildRewardDropRadius) || buildRewardDropRadius < 0)
                throw new ArgumentException("TD: reward chance and its daily multiplier must be between 0 and 1, and drop radius must be finite and nonnegative.");
            if (towerDefenseSeconds < 1)
                throw new ArgumentException("TD: towerDefenseSeconds must be at least 1.");
            if (spawnIntervalPerDay == null || spawnIntervalPerDay.Length == 0
                || Array.Exists(spawnIntervalPerDay, interval => float.IsNaN(interval) || float.IsInfinity(interval) || interval <= 0))
                throw new ArgumentException("TD: spawnIntervalPerDay needs at least one entry, and every entry must be finite and positive.");
            if (towerLevels == null || towerLevels.Length == 0
                || Array.Exists(towerLevels, level => level == null || !level.sprite || level.cost < 1 || level.muzzles == null || level.muzzles.Length == 0))
                throw new ArgumentException("TD: towerLevels needs at least one level, each with a sprite, a cost of at least 1 and one or more muzzles.");
        }

        // Build-token drop chance on the given day (1-based): day 1 uses buildRewardDropChance, each later day scales it again.
        public float BuildRewardDropChanceOnDay(int day) =>
            buildRewardDropChance * Mathf.Pow(buildRewardDropChanceDailyMultiplier, Mathf.Max(0, day - 1));

        // Seconds between spawns on the given day (1-based); days past the list reuse its last entry.
        public float SpawnIntervalOnDay(int day) => spawnIntervalPerDay[Mathf.Clamp(day - 1, 0, spawnIntervalPerDay.Length - 1)];
    }
}
