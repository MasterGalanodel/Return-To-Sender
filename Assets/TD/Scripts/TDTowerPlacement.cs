using Shared.Graphics;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TD
{
    public sealed class TDTowerPlacement : MonoBehaviour
    {
        private TDMap map;
        private TDPlayer player;
        private TDSettings settings;
        private SharedShapes shapes;
        private TDWorld world;
        private readonly Dictionary<Vector2Int, TDTower> towers = new Dictionary<Vector2Int, TDTower>();
        public int TowerCount => towers.Count;
        public IEnumerable<TDTower> Towers => towers.Values;

        public void Initialize(TDMap hexMap, TDPlayer actor, TDSettings config, SharedShapes sprites)
        { map = hexMap; player = actor; settings = config; shapes = sprites; }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame) TryPlaceCurrent();
        }

        public bool TryPlaceCurrent() => TryPlace(player.CurrentCell, out _);
        public bool TryPlace(Vector2Int cell, out TDTower tower)
        {
            tower = null;
            if (!map.IsValid(cell) || towers.ContainsKey(cell)) return false;
            var renderer = shapes.Create("Tower " + cell, shapes.triangle, Color.white, map.Center(cell), .6f, 10, transform);
            tower = renderer.gameObject.AddComponent<TDTower>();
            tower.Initialize(settings);
            if(world!=null) tower.EnableCombat(world);
            towers.Add(cell, tower);
            return true;
        }

        private void OnDestroy() { towers.Clear(); }
        public void EnableCombat(TDWorld context)
        {
            world=context;
            foreach(var tower in towers.Values) tower.EnableCombat(world);
        }
    }
}
