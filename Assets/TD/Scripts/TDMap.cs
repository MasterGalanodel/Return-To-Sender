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
        // Terrain letters in tile order: Road, Desert, Water, C, Base, Fence. Base is where enemies head;
        // Fence cells (drawn with fence art) are the only cells towers may stand on.
        public const string TerrainKinds = "RDWCBF";
        public const char Base = 'B';
        public const char Fence = 'F';
        [SerializeField] private Tilemap terrain;
        [SerializeField] private Tilemap desert;
        [SerializeField] private Tilemap decor;
        [SerializeField] private SpriteRenderer baseShip;
        private readonly List<Tile> desertTiles = new List<Tile>();
        private readonly List<Tile> decorTiles = new List<Tile>();
        // Edges a road sprite joins in its unrotated art, numbered clockwise from the top edge:
        // 0 top, 1 upper-right, 2 lower-right, 3 bottom, 4 lower-left, 5 upper-left.
        private static readonly int[][] RoadShapeEdges = { new[] { 5, 2 }, new[] { 5, 3 }, new[] { 5, 1, 3 } };
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
                        throw new FormatException($"TD map '{source}': invalid character '{lines[r][c]}' at row {r + 1}, column {c + 1}; allowed R/D/W/C/B/F.");
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
                        throw new FormatException($"TD map '{source}': invalid character '{ch}' at line {l + 1}, character {i + 1}; allowed R/D/W/C/B/F or space.");
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
                new Color32(53, 132, 190, 255), new Color32(88, 58, 37, 255), new Color32(76, 175, 80, 255),
                new Color32(150, 84, 88, 255) };
            // Scenes saved before B or F existed serialized a shorter array.
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
            int decorCount = PaintDecor(settings, shapes, out int roads, out int fences);
            PlaceBaseShip(settings, shapes);
            Debug.Log($"TD: map loaded from {source}; {Columns} x {Rows}, {Columns * Rows} hex cells, {variants} desert variant tiles, "
                + $"{roads} road tiles, {fences} fence tiles ({decorCount} decor).", this);
        }

        // Roads follow their R neighbours; the base's outer ring of B cells gets random fence art.
        private int PaintDecor(TDSettings settings, SharedShapes shapes, out int roads, out int fences)
        {
            roads = fences = 0;
            foreach (Tile tile in decorTiles) DestroyTile(tile);
            decorTiles.Clear();
            Sprite[] roadSprites = settings ? new[] { settings.roadStraight, settings.roadTurn, settings.roadJunction } : new Sprite[3];
            Sprite[] fenceSprites = settings && settings.fenceSprites != null
                ? Array.FindAll(settings.fenceSprites, s => s) : new Sprite[0];
            if (Array.TrueForAll(roadSprites, s => !s) && fenceSprites.Length == 0)
            {
                if (decor) decor.ClearAllTiles();
                return 0;
            }
            if (!decor)
            {
                var go = new GameObject("Decor Tilemap", typeof(Tilemap), typeof(TilemapRenderer));
                go.layer = 30;
                go.transform.SetParent(transform, false);
                decor = go.GetComponent<Tilemap>();
            }
            decor.tileAnchor = Vector3.zero;
            decor.ClearAllTiles();
            var renderer = decor.GetComponent<TilemapRenderer>();
            renderer.sharedMaterial = shapes.unlitMaterial;
            renderer.sortingOrder = 1;
            // One fitted tile per (sprite, rotation) pair, shared by every cell that uses it.
            var cache = new Dictionary<(Sprite, int), Tile>();
            var random = new System.Random(settings.fenceSeed);
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Columns; c++)
                {
                    var cell = new Vector2Int(c, r);
                    char kind = data[r][c];
                    if (kind == 'R')
                    {
                        if (!PickRoad(cell, roadSprites, out int shape, out int turns)) continue;
                        decor.SetTile(ToUnityCell(cell), FittedTile(cache, roadSprites[shape], turns));
                        roads++;
                    }
                    else if (kind == Fence && fenceSprites.Length > 0)
                    {
                        decor.SetTile(ToUnityCell(cell), FittedTile(cache, fenceSprites[random.Next(fenceSprites.Length)], 0));
                        fences++;
                    }
                }
            decor.CompressBounds();
            return roads + fences;
        }

        // Puts the landed ship over the middle of the Base: above the ground art, below towers and actors.
        private void PlaceBaseShip(TDSettings settings, SharedShapes shapes)
        {
            Sprite sprite = settings ? settings.baseShip : null;
            List<Vector2Int> cells = BaseCells();
            if (!sprite || cells.Count == 0)
            {
                if (baseShip) DestroyObject(baseShip.gameObject);
                baseShip = null;
                return;
            }
            Vector3 middle = Vector3.zero;
            foreach (var cell in cells) middle += Center(cell);
            middle /= cells.Count;
            if (!baseShip)
            {
                var go = new GameObject("Base Ship", typeof(SpriteRenderer));
                go.layer = 30;
                go.transform.SetParent(transform, false);
                baseShip = go.GetComponent<SpriteRenderer>();
            }
            baseShip.sprite = sprite;
            baseShip.sharedMaterial = shapes.unlitMaterial;
            baseShip.sortingOrder = 5;
            float scale = settings.baseShipWidth / sprite.bounds.size.x;
            baseShip.transform.localScale = new Vector3(scale, scale, 1);
            // Centre the art itself, whatever pivot the sprite was sliced with.
            Vector3 offset = sprite.bounds.center * scale;
            baseShip.transform.position = new Vector3(middle.x - offset.x, middle.y - offset.y, middle.z);
        }

        private static void DestroyObject(GameObject go)
        {
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }

        // Chooses the road shape and clockwise 60° turns that best match this cell's links: road
        // neighbours must connect, while map exits and the base are welcome ends for a road.
        // Hand-drawn maps include sharp bends and two-wide roads no single tile matches, so each
        // candidate is scored and the closest fit wins (ties go to the simpler shape).
        private bool PickRoad(Vector2Int cell, Sprite[] sprites, out int shape, out int turns)
        {
            Vector2Int[] neighbours = Neighbours(cell);
            int roadMask = 0, openMask = 0;
            for (int edge = 0; edge < 6; edge++)
            {
                char kind = TerrainAt(neighbours[edge]);
                if (kind == 'R') roadMask |= 1 << edge;
                else if (kind == '\0' || kind == Base || kind == Fence) openMask |= 1 << edge;
            }
            shape = turns = -1;
            int best = int.MinValue;
            for (int s = 0; s < RoadShapeEdges.Length; s++)
            {
                if (!sprites[s]) continue;
                for (int k = 0; k < 6; k++)
                {
                    int score = -(RoadShapeEdges[s].Length - 2);
                    foreach (int baseEdge in RoadShapeEdges[s])
                    {
                        int bit = 1 << ((baseEdge + k) % 6);
                        score += (roadMask & bit) != 0 ? 4 : (openMask & bit) != 0 ? 1 : -3;
                    }
                    if (score > best) { best = score; shape = s; turns = k; }
                }
            }
            return shape >= 0;
        }

        // The six cells sharing an edge with this one, indexed by edge clockwise from the top.
        // Worked out from world positions so it never depends on the grid's odd-column convention.
        private Vector2Int[] Neighbours(Vector2Int cell)
        {
            var result = new Vector2Int[6];
            for (int i = 0; i < 6; i++) result[i] = new Vector2Int(-1, -1);
            Vector3 center = Center(cell);
            float spacing = GetComponent<Grid>().cellSize.x;
            for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                {
                    if (dr == 0 && dc == 0) continue;
                    var other = new Vector2Int(cell.x + dc, cell.y + dr);
                    Vector3 delta = Center(other) - center;
                    if (Mathf.Abs(delta.magnitude - spacing) > spacing * .05f) continue;
                    float angle = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
                    result[((Mathf.RoundToInt(angle / 60) % 6) + 6) % 6] = other;
                }
            return result;
        }

        // Stretches the sprite to fill the flat-top hex exactly, then turns it clockwise in 60° steps.
        private Tile FittedTile(Dictionary<(Sprite, int), Tile> cache, Sprite sprite, int turns)
        {
            if (cache.TryGetValue((sprite, turns), out Tile tile)) return tile;
            Vector2 size = sprite.bounds.size;
            var scale = new Vector3(1 / size.x, GetComponent<Grid>().cellSize.x / size.y, 1);
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = sprite.name + (turns != 0 ? " @" + turns * 60 : "");
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            tile.transform = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, -60 * turns), scale);
            cache[(sprite, turns)] = tile;
            decorTiles.Add(tile);
            return tile;
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

        // Lists every Base cell, where enemies head.
        public List<Vector2Int> BaseCells() => CellsOf(Base);

        // Lists every Fence cell, the only cells towers may stand on.
        public List<Vector2Int> FenceCells() => CellsOf(Fence);

        private List<Vector2Int> CellsOf(char kind)
        {
            var cells = new List<Vector2Int>();
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Columns; c++)
                    if (data != null && data[r][c] == kind) cells.Add(new Vector2Int(c, r));
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
            foreach (Tile tile in decorTiles) DestroyTile(tile);
        }
    }
}
