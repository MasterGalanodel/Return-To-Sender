using System;
using UnityEngine;
using UnityEngine.Serialization;
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
            [Tooltip("How much one turret of this level adds to the spawn formula: seconds between spawns = day factor ÷ summed weights.")]
            [Min(0)] public float spawnWeight = 1;
            [Tooltip("Barrel tips in the unrotated sprite, 0–1 from its bottom-left corner. Each shot fires one projectile per tip.")]
            public Vector2[] muzzles = { new Vector2(.5f, 1) };
            [Tooltip("Optional mark drawn on top of the turret (e.g. the piercing-shot mark). Leave empty for none.")]
            public Sprite emblem;
            [Tooltip("Where the emblem's centre sits, 0–1 from the turret sprite's bottom-left corner.")]
            public Vector2 emblemCenter = new Vector2(.5f, .43f);
            [Tooltip("World height of the emblem.")]
            [Min(0.01f)] public float emblemHeight = .22f;
            public Color emblemColor = Color.white;
            [Tooltip("World radius around a projectile hit that also takes splash damage. 0 = no splash.")]
            [Min(0)] public float splashRadius;
            [Tooltip("Damage dealt to every other enemy inside the splash radius. The enemy hit directly takes projectileDamage.")]
            [Min(0)] public float splashDamage;
            [Tooltip("Small circles thrown out by each splash.")]
            [Range(4, 10)] public int splashParticles = 6;
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
        [Tooltip("Spawn interval factor per day (element 0 = day 1). Days past the end use the last value. "
            + "Seconds between spawns = factor ÷ summed turret spawnWeight (set per tower level; total counts as at least 1). "
            + "Intervals shorter than a frame spawn several enemies in the same frame.")]
        [FormerlySerializedAs("spawnIntervalPerDay")]
        public float[] spawnIntervalFactor = { 3, 1.5f, .75f, .375f, .1875f, .09375f };
        [Header("Enemy rewards")]
        [Tooltip("Chance a killed enemy drops a build token, per day (element 0 = day 1). Days past the end use the last value.")]
        public float[] buildRewardDropChancePerDay = { .2f, .12f, .07f, .2f, .2f, .2f };
        [Min(0)] public float buildRewardDropRadius = 2;
        [Tooltip("Claw collect radius per day (element 0 = day 1). Days past the end use the last value. "
            + "One press grabs every token inside this radius.")]
        public float[] buildRewardCollectRadiusPerDay = { .6f, .9f, 1.2f, 1.5f, 1.8f, 2.1f };
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
        [Header("Base ship")]
        [Tooltip("Drawn once, centred on the middle of all B (Base) cells. Leave empty for no ship.")]
        public Sprite baseShip;
        [Tooltip("World width of the ship. A hex is 1 wide and columns sit 0.75 apart, so 2.5 spans about 3 tiles and 2 a little over 2.")]
        [Min(0.01f)] public float baseShipWidth = 2f;
        [Header("Road")]
        [Tooltip("Flat-top hex road art, rotated per R cell to follow its neighbours. Leave empty for plain colour.\n"
            + "Straight: road from the upper-left edge to the lower-right edge.\n"
            + "Turn: road from the upper-left edge to the bottom edge.\n"
            + "Junction: Y joining the upper-left, upper-right and bottom edges.")]
        public Sprite roadStraight;
        public Sprite roadTurn;
        public Sprite roadJunction;
        [Header("Audio")]
        [Tooltip("Played once per tower volley (not per barrel).")]
        public AudioClip towerShotSound;
        [Range(0, 1)] public float towerShotVolume = .5f;
        [Tooltip("Same-clip repeats closer than this are skipped so many towers don't stack into noise.")]
        [Min(0)] public float towerShotMinInterval = .06f;
        [Tooltip("Played when an enemy is destroyed by towers.")]
        public AudioClip enemyDeathSound;
        [Range(0, 1)] public float enemyDeathVolume = .8f;

        // Confirms that all speed, range, rate, damage, health, interval, and zoom settings are positive and finite numbers.
        public void Validate()
        {
            float[] values = { playerSpeed, towerRange, towerFireRate, projectileSpeed,
                projectileDamage, projectileLifetime, enemySpeed, enemyHealth, cameraZoom };
            foreach (float value in values)
                if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0)
                    throw new ArgumentException("TD: all speed, range, rate, damage, health, interval and zoom settings must be finite and positive.");
            if (buildRewardDropChancePerDay == null || buildRewardDropChancePerDay.Length == 0
                || Array.Exists(buildRewardDropChancePerDay, chance => float.IsNaN(chance) || chance < 0 || chance > 1)
                || float.IsNaN(buildRewardDropRadius) || float.IsInfinity(buildRewardDropRadius) || buildRewardDropRadius < 0)
                throw new ArgumentException("TD: buildRewardDropChancePerDay needs at least one entry, each between 0 and 1, and drop radius must be finite and nonnegative.");
            if (buildRewardCollectRadiusPerDay == null || buildRewardCollectRadiusPerDay.Length == 0
                || Array.Exists(buildRewardCollectRadiusPerDay, radius => float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0))
                throw new ArgumentException("TD: buildRewardCollectRadiusPerDay needs at least one entry, and every entry must be finite and positive.");
            if (towerDefenseSeconds < 1)
                throw new ArgumentException("TD: towerDefenseSeconds must be at least 1.");
            if (spawnIntervalFactor == null || spawnIntervalFactor.Length == 0
                || Array.Exists(spawnIntervalFactor, factor => float.IsNaN(factor) || float.IsInfinity(factor) || factor <= 0))
                throw new ArgumentException("TD: spawnIntervalFactor needs at least one entry, and every entry must be finite and positive.");
            if (towerLevels == null || towerLevels.Length == 0
                || Array.Exists(towerLevels, level => level == null || !level.sprite || level.cost < 1 || level.muzzles == null || level.muzzles.Length == 0))
                throw new ArgumentException("TD: towerLevels needs at least one level, each with a sprite, a cost of at least 1 and one or more muzzles.");
            if (Array.Exists(towerLevels, level => float.IsNaN(level.spawnWeight) || float.IsInfinity(level.spawnWeight) || level.spawnWeight < 0))
                throw new ArgumentException("TD: tower spawnWeight must be finite and nonnegative.");
            if (Array.Exists(towerLevels, level => float.IsNaN(level.splashRadius) || float.IsInfinity(level.splashRadius) || level.splashRadius < 0
                || float.IsNaN(level.splashDamage) || float.IsInfinity(level.splashDamage) || level.splashDamage < 0))
                throw new ArgumentException("TD: tower splashRadius and splashDamage must be finite and nonnegative.");
        }

        // Build-token drop chance on the given day (1-based); days past the list reuse its last entry.
        public float BuildRewardDropChanceOnDay(int day) =>
            buildRewardDropChancePerDay[Mathf.Clamp(day - 1, 0, buildRewardDropChancePerDay.Length - 1)];

        // Claw collect radius on the given day (1-based); days past the list reuse its last entry.
        public float BuildRewardCollectRadiusOnDay(int day) =>
            buildRewardCollectRadiusPerDay[Mathf.Clamp(day - 1, 0, buildRewardCollectRadiusPerDay.Length - 1)];

        // Spawn interval factor on the given day (1-based); days past the list reuse its last entry.
        public float SpawnIntervalFactorOnDay(int day) => spawnIntervalFactor[Mathf.Clamp(day - 1, 0, spawnIntervalFactor.Length - 1)];

        // Seconds between spawns on the given day: that day's factor ÷ the summed turret spawn weights, so more and stronger
        // turrets mean more enemies. The total counts as at least 1, so an empty base uses the bare factor instead of dividing by zero.
        public float SpawnIntervalOnDay(int day, float turretWeight) => SpawnIntervalFactorOnDay(day) / Mathf.Max(1, turretWeight);
    }
}
