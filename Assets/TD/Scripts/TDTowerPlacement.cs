using Shared.Graphics;
using Shared.HUD;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TD
{
    // Handles user input for building towers on Fence (F) cells and upgrading the tower the player stands on.
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

        // Builds a first-level tower on an empty Fence cell, or upgrades the tower already standing
        // there to its next level. Each step costs that level's tokens; nothing changes if they fall short.
        public bool TryPlace(Vector2Int cell, out TDTower tower)
        {
            tower = null;
            var session = GameSession.Instance;
            if (!session || !session.IsPhaseActive(GameSession.PlayPhase.TowerDefense)) return false;
            if (towers.TryGetValue(cell, out TDTower existing))
            {
                if (!CanUpgrade(cell)) return false;
                int next = existing.Level + 1;
                if (!session.TrySpendBuildTokens(settings.towerLevels[next].cost)) return false;
                existing.SetLevel(next);
                session.RememberTower(cell, next);
                tower = existing;
                return true;
            }
            if (!CanBuildOn(cell)) return false;
            if (!session.TrySpendBuildTokens(settings.towerLevels[0].cost)) return false;
            tower = CreateTower(cell, 0);
            session.RememberTower(cell, 0);
            return true;
        }

        // Restoring previously paid-for towers must not consume tokens a second time.
        public void RestoreTowers()
        {
            var session = GameSession.Instance;
            if (!session) return;
            foreach (var cell in session.TowerCells)
                if (CanBuildOn(cell)) CreateTower(cell, Mathf.Min(session.TowerLevelAt(cell), settings.towerLevels.Length - 1));
        }

        // Towers may only stand on unoccupied Fence cells.
        public bool CanBuildOn(Vector2Int cell) => map.TerrainAt(cell) == TDMap.Fence && !towers.ContainsKey(cell);

        // True when a tower stands on the cell and has a higher level left to buy.
        public bool CanUpgrade(Vector2Int cell) => towers.TryGetValue(cell, out TDTower tower) && tower.Level + 1 < settings.towerLevels.Length;

        private TDTower CreateTower(Vector2Int cell, int level)
        {
            var renderer = shapes.Create("Tower " + cell, settings.towerLevels[level].sprite, Color.white, map.Center(cell), 1, 10, transform);
            var tower = renderer.gameObject.AddComponent<TDTower>();
            tower.Initialize(settings, level);
            if(world!=null) tower.EnableCombat(world);
            towers.Add(cell, tower);
            return tower;
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
