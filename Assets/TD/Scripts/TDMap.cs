using Shared.Graphics;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TD
{
    public sealed class TDMap : MonoBehaviour
    {
        public const int Columns = 32;
        public const int Rows = 18;
        [SerializeField] private Tilemap terrain;
        private string[] data;
        [SerializeField] private Tile[] tiles = new Tile[4];
        public Tilemap Terrain => terrain;
        public Bounds WorldBounds { get; private set; }
        public string SourceUsed { get; private set; }

        public static string[] Parse(string text, string source)
        {
            if (text == null) throw new FormatException($"TD map '{source}': no text provided.");
            string normalized = text.Replace("\r\n", "\n");
            if (normalized.EndsWith("\n", StringComparison.Ordinal)) normalized = normalized.Substring(0, normalized.Length - 1);
            string[] lines = normalized.Split('\n');
            if (lines.Length != Rows) throw new FormatException($"TD map '{source}': expected {Rows} rows, found {lines.Length}.");
            for (int r = 0; r < Rows; r++)
            {
                if (lines[r].Length != Columns)
                    throw new FormatException($"TD map '{source}': row {r + 1}, column {Math.Min(lines[r].Length + 1, Columns + 1)}: expected {Columns} characters, found {lines[r].Length}.");
                for (int c = 0; c < Columns; c++)
                    if ("RDWC".IndexOf(lines[r][c]) < 0)
                        throw new FormatException($"TD map '{source}': invalid character '{lines[r][c]}' at row {r + 1}, column {c + 1}; allowed R/D/W/C.");
            }
            return lines;
        }

        public static string[] Load(TDSettings settings, out string source)
        {
#if UNITY_EDITOR
            if (settings.preferExternalMapInEditor && File.Exists(settings.externalMapPath))
            {
                source = settings.externalMapPath;
                return Parse(File.ReadAllText(source), source);
            }
#endif
            source = "packaged TextAsset: arab_32x18";
            if (!settings.packagedTerrain) throw new FormatException("TD: packaged terrain TextAsset is missing.");
            return Parse(settings.packagedTerrain.text, source);
        }

        public void Initialize(string[] rows, string source, SharedShapes shapes)
        {
            data = rows;
            SourceUsed = source;
            var grid = GetComponent<Grid>();
            if (!grid) grid = gameObject.AddComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.Hexagon;
            grid.cellSwizzle = GridLayout.CellSwizzle.YXZ;
            grid.cellSize = new Vector3(Mathf.Sqrt(3) / 2f, 1, 0);
            if (!terrain)
            {
                var go = new GameObject("Terrain Tilemap", typeof(Tilemap), typeof(TilemapRenderer));
                go.layer = 30;
                go.transform.SetParent(transform, false);
                terrain = go.GetComponent<Tilemap>();
            }
            // Hex cells are already centered at CellToWorld: the rectangular .5 anchor is wrong.
            terrain.tileAnchor = Vector3.zero;
            terrain.ClearAllTiles();
            var renderer = terrain.GetComponent<TilemapRenderer>();
            renderer.sharedMaterial = shapes.terrainMaterial;
            renderer.sortingOrder = 0;
            Color[] colors = { new Color32(132, 116, 96, 255), new Color32(225, 194, 112, 255),
                new Color32(53, 132, 190, 255), new Color32(88, 58, 37, 255) };
            Sprite hexSprite = shapes.hex;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (!tiles[i]) tiles[i] = ScriptableObject.CreateInstance<Tile>();
                tiles[i].sprite = hexSprite;
                tiles[i].color = colors[i];
                tiles[i].colliderType = Tile.ColliderType.None;
            }
            bool first = true;
            Bounds bounds = default;
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Columns; c++)
                {
                    var cell = new Vector2Int(c, r);
                    terrain.SetTile(ToUnityCell(cell), tiles["RDWC".IndexOf(data[r][c])]);
                    Vector3 center = Center(cell);
                    var extents = new Vector3(.5f, Mathf.Sqrt(3) / 4, 0);
                    Vector3 min = center - extents;
                    Vector3 max = center + extents;
                    if (first) { bounds = new Bounds(center, Vector3.zero); first = false; }
                    bounds.Encapsulate(min);
                    bounds.Encapsulate(max);
                }
            terrain.CompressBounds();
            WorldBounds = bounds;
            Debug.Log($"TD: map loaded from {source}; {Columns} x {Rows}, 576 hex cells.", this);
        }

        public bool IsValid(Vector2Int cell) => cell.x >= 0 && cell.x < Columns && cell.y >= 0 && cell.y < Rows;
        public char TerrainAt(Vector2Int cell) => IsValid(cell) && data != null ? data[cell.y][cell.x] : '\0';
        public static Vector3Int ToUnityCell(Vector2Int logical) => new Vector3Int(-logical.y, logical.x, 0);
        public static Vector2Int FromUnityCell(Vector3Int cell) => new Vector2Int(cell.y, -cell.x);
        public Vector3 Center(Vector2Int cell) => terrain.GetCellCenterWorld(ToUnityCell(cell));
        public Vector2Int WorldToLogical(Vector3 world) => FromUnityCell(terrain.WorldToCell(world));
        public bool ContainsWorld(Vector3 world) => IsValid(WorldToLogical(world));

        private void OnDestroy()
        {
            foreach (Tile tile in tiles)
                if (tile) { if (Application.isPlaying) Destroy(tile); else DestroyImmediate(tile); }
        }
    }
}
