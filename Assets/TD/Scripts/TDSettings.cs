using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TD
{
    // ScriptableObject asset defining balance parameters and map configurations for Tower Defense gameplay.
    [CreateAssetMenu(menuName = "TD/Prototype Settings")]
    public sealed class TDSettings : ScriptableObject
    {
        [Header("Player")][Min(0.01f)] public float playerSpeed = 4;
        [Header("Tower")][Min(0.01f)] public float towerRange = 3;
        [Min(0.01f)] public float towerFireRate = 1;
        [Min(0.01f)] public float projectileSpeed = 8;
        [Min(0.01f)] public float projectileDamage = 1;
        [Min(0.01f)] public float projectileLifetime = 8;
        [Header("Enemy")][Min(0.01f)] public float enemySpeed = 1;
        [Min(0.01f)] public float enemyHealth = 3;
        [Min(0.01f)] public float spawnInterval = 2;
        [Header("Difficulty")]
        [Tooltip("Length of each TD phase in seconds.")]
        [Min(1)] public int towerDefenseSeconds = 60;
        [Tooltip("Most enemies alive at the same time, per day (element 0 = day 1). Days past the end use the last value.")]
        public int[] maxEnemiesPerDay = { 3, 6, 9, 12, 15, 18 };
        [Header("Enemy rewards")]
        [Range(0, 1)] public float buildRewardDropChance = .2f;
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

        // Confirms that all speed, range, rate, damage, health, interval, and zoom settings are positive and finite numbers.
        public void Validate()
        {
            float[] values = { playerSpeed, towerRange, towerFireRate, projectileSpeed,
                projectileDamage, projectileLifetime, enemySpeed, enemyHealth, spawnInterval, cameraZoom,
                buildRewardCollectRadius };
            foreach (float value in values)
                if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0)
                    throw new ArgumentException("TD: all speed, range, rate, damage, health, interval and zoom settings must be finite and positive.");
            if (float.IsNaN(buildRewardDropChance) || buildRewardDropChance < 0 || buildRewardDropChance > 1
                || float.IsNaN(buildRewardDropRadius) || float.IsInfinity(buildRewardDropRadius) || buildRewardDropRadius < 0)
                throw new ArgumentException("TD: reward chance must be between 0 and 1, and drop radius must be finite and nonnegative.");
            if (towerDefenseSeconds < 1)
                throw new ArgumentException("TD: towerDefenseSeconds must be at least 1.");
            if (maxEnemiesPerDay == null || maxEnemiesPerDay.Length == 0 || Array.Exists(maxEnemiesPerDay, count => count < 1))
                throw new ArgumentException("TD: maxEnemiesPerDay needs at least one entry, and every entry must be at least 1.");
        }

        // Most enemies allowed alive at once on the given day (1-based); days past the list reuse its last entry.
        public int MaxEnemiesOnDay(int day) => maxEnemiesPerDay[Mathf.Clamp(day - 1, 0, maxEnemiesPerDay.Length - 1)];
    }
}
