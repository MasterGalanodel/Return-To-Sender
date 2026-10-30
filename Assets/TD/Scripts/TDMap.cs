using Shared.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TD
{
    // Manages hex tilemap creation, grid conversions between logical cells and Unity space, and terrain query operations.
    public sealed class TDMap : MonoBehaviour
    {
        public const int Columns = 32;
        public const int Rows = 18;
        // Terrain letters in tile order: Road, Desert, Water, C, Base. Base is where towers stand and enemies head.
        public const string TerrainKinds = "RDWCB";
        public const char Base = 'B';
        [SerializeField] private Tilemap terrain;
        [SerializeField] private Tilemap desert;
        private readonly List<Tile> desertTiles = new List<Tile>();
        private string[] data;
        [SerializeField] private Tile[] tiles = new Tile[TerrainKinds.Length];
        public Tilemap Terrain => terrain;
        public Bounds WorldBounds { get; private set; }
        public string SourceUsed { get; private set; }

        // Validates map text in either layout and returns one compact string per logical row.
        // Spaced layout (2 * Rows lines) is a picture of the flat-top grid: columns sit two characters
        // apart, and each row takes two lines because Unity draws odd columns half a hex higher.
        //   line 2r:     "  D   D   D"  odd columns 1, 3, 5 ... of row r
        //   line 2r + 1: "D   D   D  "  even columns 0, 2, 4 ... of row r
        // Compact layout (Rows lines of Columns characters, no spaces) is still accepted for older maps.
        public static string[] Parse(string text, string source)
        {
            if (text == null) throw new FormatException($"TD map '{source}': no text provided.");
            string normalized = text.Replace("\r\n", "\n");
            if (normalized.EndsWith("\n", StringComparison.Ordinal)) normalized = normalized.Substring(0, normalized.Length - 1);
            string[] lines = normalized.Split('\n');
            if (lines.Length == Rows) return ParseCompact(lines, source);
            if (lines.Length == Rows * 2) return ParseSpaced(lines, source);
            throw new FormatException($"TD map '{source}': expected {Rows * 2} lines (spaced) or {Rows} lines (compact), found {lines.Length}.");
        }

        private static string[] ParseCompact(string[] lines, string source)
        {
            for (int r = 0; r < Rows; r++)
            {
                if (lines[r].Length != Columns)
                    throw new FormatException($"TD map '{source}': row {r + 1}, column {Math.Min(lines[r].Length + 1, Columns + 1)}: expected {Columns} characters, found {lines[r].Length}.");
                for (int c = 0; c < Columns; c++)
                    if (TerrainKinds.IndexOf(lines[r][c]) < 0)
                        throw new FormatException($"TD map '{source}': invalid character '{lines[r][c]}' at row {r + 1}, column {c + 1}; allowed R/D/W/C/B.");
            }
            return lines;
        }

        private static string[] ParseSpaced(string[] lines, string source)
        {
            var cells = new char[Rows, Columns];
            for (int l = 0; l < lines.Length; l++)
            {
                int r = l / 2;
                bool oddColumns = (l & 1) == 0;
                string line = lines[l];
                for (int i = 0; i < line.Length; i++)
                {
                    char ch = line[i];
                    if (ch == ' ') continue;
                    if (TerrainKinds.IndexOf(ch) < 0)
                        throw new FormatException($"TD map '{source}': invalid character '{ch}' at line {l + 1}, character {i + 1}; allowed R/D/W/C/B or space.");
                    int c = i / 2;
                    if ((i & 1) == 1 || ((c & 1) == 1) != oddColumns || c >= Columns)
                        throw new FormatException($"TD map '{source}': line {l + 1}, character {i + 1} is not a hex position; line {l + 1} holds {(oddColumns ? "odd" : "even")} columns at characters {(oddColumns ? 3 : 1)}, {(oddColumns ? 7 : 5)}, {(oddColumns ? 11 : 9)} ...");
                    cells[r, c] = ch;
                }
                for (int c = oddColumns ? 1 : 0; c < Columns; c += 2)
                    if (cells[r, c] == '\0')
                        throw new FormatException($"TD map '{source}': line {l + 1} is missing column {c + 1} at character {c * 2 + 1}.");
            }
            var rows = new string[Rows];
            for (int r = 0; r < Rows; r++)
            {
                var row = new char[Columns];
                for (int c = 0; c < Columns; c++) row[c] = cells[r, c];
                rows[r] = new string(row);
            }
            return rows;
        }

        // Retrieves map data from either an external file in the editor or the packaged TextAsset.
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

        // Sets up hexagonal grid geometry, generates tile assets, populates the tilemap, and calculates world bounds.
        public void Initialize(string[] rows, string source, SharedShapes shapes, TDSettings settings)
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
                new Color32(53, 132, 190, 255), new Color32(88, 58, 37, 255), new Color32(76, 175, 80, 255) };
            // Scenes saved before B existed serialized a shorter array.
            if (tiles == null || tiles.Length != TerrainKinds.Length) Array.Resize(ref tiles, TerrainKinds.Length);
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
                    terrain.SetTile(ToUnityCell(cell), tiles[TerrainKinds.IndexOf(data[r][c])]);
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
            int variants = PaintDesert(settings, shapes);
            Debug.Log($"TD: map loaded from {source}; {Columns} x {Rows}, {Columns * Rows} hex cells, {variants} desert variant tiles.", this);
        }

        // Layers noise-picked desert art over D cells, keyed by raw Unity cell so the pattern is stable.
        // The flat D colour underneath still shows through any transparent edges of the art.
        private int PaintDesert(TDSettings settings, SharedShapes shapes)
        {
            TileBase[] variants = settings ? settings.desertTiles : null;
            if (variants == null || Array.TrueForAll(variants, t => !t))
            {
                if (desert) desert.ClearAllTiles();
                return 0;
            }
            if (!desert)
            {
                var go = new GameObject("Desert Tilemap", typeof(Tilemap), typeof(TilemapRenderer));
                go.layer = 30;
                go.transform.SetParent(transform, false);
                desert = go.GetComponent<Tilemap>();
            }
            desert.tileAnchor = Vector3.zero;
            desert.ClearAllTiles();
            var renderer = desert.GetComponent<TilemapRenderer>();
            renderer.sharedMaterial = shapes.unlitMaterial;
            renderer.sortingOrder = 1;
            Tile[] fitted = FitDesertTiles(variants);
            int painted = 0;
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Columns; c++)
                {
                    if (data[r][c] != 'D') continue;
                    Vector3Int cell = ToUnityCell(new Vector2Int(c, r));
                    float noise = Mathf.PerlinNoise((cell.x + settings.desertNoiseOffset.x) * settings.desertNoiseScale,
                        (cell.y + settings.desertNoiseOffset.y) * settings.desertNoiseScale);
                    // Perlin output bunches around 0.5; stretching the middle keeps every variant in use.
                    int index = Mathf.Clamp(Mathf.FloorToInt(Mathf.InverseLerp(.25f, .75f, noise) * variants.Length), 0, variants.Length - 1);
                    if (!fitted[index]) continue;
                    desert.SetTile(cell, fitted[index]);
                    painted++;
                }
            desert.CompressBounds();
            return painted;
        }

        // Imported tile art is not cell-sized, and AutoTile locks per-cell transforms, so each variant is
        // copied into a runtime Tile stretched to fill the flat-top hex exactly (no seams between
        // neighbours). Rule/animated tiles keep their default sprite.
        private Tile[] FitDesertTiles(TileBase[] variants)
        {
            foreach (Tile tile in desertTiles) DestroyTile(tile);
            desertTiles.Clear();
            Vector2 hexSize = new Vector2(1, GetComponent<Grid>().cellSize.x);
            var fitted = new Tile[variants.Length];
            for (int i = 0; i < variants.Length; i++)
            {
                if (!variants[i]) continue;
                desert.SetTile(Vector3Int.zero, variants[i]);
                Sprite sprite = desert.GetSprite(Vector3Int.zero);
                desert.SetTile(Vector3Int.zero, null);
                if (!sprite) continue;
                Vector2 size = sprite.bounds.size;
                var scale = new Vector3(hexSize.x / size.x, hexSize.y / size.y, 1);
                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.name = variants[i].name;
                tile.sprite = sprite;
                tile.colliderType = Tile.ColliderType.None;
                tile.transform = Matrix4x4.Scale(scale);
                fitted[i] = tile;
                desertTiles.Add(tile);
            }
            return fitted;
        }

        private static void DestroyTile(Tile tile)
        {
            if (tile) { if (Application.isPlaying) Destroy(tile); else DestroyImmediate(tile); }
        }

        // Checks if the specified coordinate falls within the map column and row limits.
        public bool IsValid(Vector2Int cell) => cell.x >= 0 && cell.x < Columns && cell.y >= 0 && cell.y < Rows;

        // Returns the terrain type character at the given logical cell, or null character if outside bounds.
        public char TerrainAt(Vector2Int cell) => IsValid(cell) && data != null ? data[cell.y][cell.x] : '\0';

        // Lists every cell on the outer border of the map, where enemies spawn.
        public List<Vector2Int> EdgeCells()
        {
            var cells = new List<Vector2Int>();
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Columns; c++)
                    if (c == 0 || c == Columns - 1 || r == 0 || r == Rows - 1) cells.Add(new Vector2Int(c, r));
            return cells;
        }

        // Lists every Base cell, the only cells towers may stand on.
        public List<Vector2Int> BaseCells()
        {
            var cells = new List<Vector2Int>();
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Columns; c++)
                    if (data != null && data[r][c] == Base) cells.Add(new Vector2Int(c, r));
            return cells;
        }

        // Maps logical column and row coordinates into Unity's swizzled tilemap cell coordinates.
        public static Vector3Int ToUnityCell(Vector2Int logical) => new Vector3Int(-logical.y, logical.x, 0);

        // Converts Unity tilemap cell coordinates back into logical grid coordinates.
        public static Vector2Int FromUnityCell(Vector3Int cell) => new Vector2Int(cell.y, -cell.x);

        // Returns the world-space center position of the specified logical hex cell.
        public Vector3 Center(Vector2Int cell) => terrain.GetCellCenterWorld(ToUnityCell(cell));

        // Converts a world-space position to its corresponding logical hex grid cell.
        public Vector2Int WorldToLogical(Vector3 world) => FromUnityCell(terrain.WorldToCell(world));

        // Determines whether a world-space point lies within the playable map boundaries.
        public bool ContainsWorld(Vector3 world) => IsValid(WorldToLogical(world));

        // Cleans up dynamically created ScriptableObject tile instances to prevent memory leaks.
        private void OnDestroy()
        {
            foreach (Tile tile in tiles) DestroyTile(tile);
            foreach (Tile tile in desertTiles) DestroyTile(tile);
        }
    }
}
