using System;
using System.Collections.Generic;
using UnityEngine;

namespace TD
{
    public static class TDRoadPathfinder
    {
        public static IEnumerable<Vector2Int> Neighbors(Vector2Int cell)
        {
            yield return cell + Vector2Int.up;
            yield return cell + Vector2Int.down;
            int stagger = (cell.x & 1) == 0 ? 1 : -1;
            yield return new Vector2Int(cell.x - 1, cell.y);
            yield return new Vector2Int(cell.x - 1, cell.y + stagger);
            yield return new Vector2Int(cell.x + 1, cell.y);
            yield return new Vector2Int(cell.x + 1, cell.y + stagger);
        }

        public static List<Vector2Int> FindRoute(Func<Vector2Int, char> terrainAt)
        {
            var queue = new Queue<Vector2Int>();
            var previous = new Dictionary<Vector2Int, Vector2Int>();
            var starts = new HashSet<Vector2Int>();
            for (int r = 0; r < TDMap.Rows; r++)
            {
                var cell = new Vector2Int(0, r);
                if (terrainAt(cell) != 'R') continue;
                queue.Enqueue(cell);
                starts.Add(cell);
                previous.Add(cell, cell);
            }
            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();
                if (cell.x == TDMap.Columns - 1)
                {
                    var route = new List<Vector2Int> {cell};
                    while (!starts.Contains(cell)) {cell = previous[cell]; route.Add(cell);}
                    route.Reverse();
                    return route;
                }
                foreach (var neighbor in Neighbors(cell))
                {
                    if (neighbor.x < 0 || neighbor.x >= TDMap.Columns || neighbor.y < 0 || neighbor.y >= TDMap.Rows)
                        continue;
                    if (terrainAt(neighbor) != 'R' || previous.ContainsKey(neighbor)) continue;
                    previous.Add(neighbor, cell);
                    queue.Enqueue(neighbor);
                }
            }
            throw new InvalidOperationException("TD: no connected road route from a left-edge R tile to a right-edge R tile; gameplay startup is disabled.");
        }
    }
}
