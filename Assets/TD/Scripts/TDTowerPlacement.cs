using Shared.Graphics;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TD
{
    // Handles user input and mechanics for placing defensive towers onto valid grid cells.
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

        // Configures map, player, settings, and shape generator references.
        public void Initialize(TDMap hexMap, TDPlayer actor, TDSettings config, SharedShapes sprites)
        { map = hexMap; player = actor; settings = config; shapes = sprites; }

        // Listens for placement keystrokes to trigger tower creation at the player's position.
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame) TryPlaceCurrent();
        }

        // Attempts to construct a new tower on the cell currently occupied by the player.
        public bool TryPlaceCurrent() => TryPlace(player.CurrentCell, out _);

        // Instantiates and registers a tower at the given hex cell if the cell is valid and empty.
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

        // Clears all tracked tower instances when the placement manager is destroyed.
        private void OnDestroy() { towers.Clear(); }

        // Connects all existing and future placed towers to the active combat world.
        public void EnableCombat(TDWorld context)
        {
            world=context;
            foreach(var tower in towers.Values) tower.EnableCombat(world);
        }
    }
}
