using System;
using UnityEngine;

namespace TD
{
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
        [Header("Camera")][Min(0.01f)] public float cameraZoom = 3.9f;
        [Header("Terrain")]
        public TextAsset packagedTerrain;
        public bool preferExternalMapInEditor = true;
        public string externalMapPath = @"E:\Miles\assets\arab_32x18.txt";

        public void Validate()
        {
            float[] values = { playerSpeed, towerRange, towerFireRate, projectileSpeed,
                projectileDamage, projectileLifetime, enemySpeed, enemyHealth, spawnInterval, cameraZoom };
            foreach (float value in values)
                if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0)
                    throw new ArgumentException("TD: all speed, range, rate, damage, health, interval and zoom settings must be finite and positive.");
        }
    }
}
