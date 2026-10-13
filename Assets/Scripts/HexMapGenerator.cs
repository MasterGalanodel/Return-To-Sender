using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class HexMapGenerator : MonoBehaviour
{
    [Header("Settings")]
    public int viewRadius = 25;

    [Tooltip("Controls clump size:\n0.01–0.05 = very large clumps\n0.06–0.10 = medium clumps (recommended)\n0.12–0.20 = smaller clumps\n0.3+ = almost random")]
    public float noiseScale = 0.08f;

    [Header("Ground Tiles (Rarest → Most Common)")]
    public TileBase[] groundTiles;

    [Header("Resources (Rarest → Most Common)")]
    public ResourceTile[] resources;

    [Header("References")]
    public Tilemap groundTilemap;
    public Tilemap resourceTilemap;
    public Transform player;

    private HashSet<Vector3Int> generatedCells = new HashSet<Vector3Int>();

    [System.Serializable]
    public class ResourceTile
    {
        public TileBase tile;
        [Range(0f, 1f)] public float spawnChance = 0.02f;
    }

    void Start()
    {
        if (player == null)
            player = GameObject.Find("Player").transform;

        if (resourceTilemap == null)
            resourceTilemap = groundTilemap;

        GenerateAroundPlayer();
    }

    void Update()
    {
        GenerateAroundPlayer();
    }

    void GenerateAroundPlayer()
    {
        if (groundTilemap == null || groundTiles == null || groundTiles.Length == 0 || player == null)
            return;

        Vector3Int playerCell = groundTilemap.WorldToCell(player.position);

        for (int x = -viewRadius; x <= viewRadius; x++)
        {
            for (int y = -viewRadius; y <= viewRadius; y++)
            {
                Vector3Int cellPos = new Vector3Int(playerCell.x + x, playerCell.y + y, 0);

                if (generatedCells.Contains(cellPos))
                    continue;

                // Ground
                float noise = Mathf.PerlinNoise((cellPos.x + 1000) * noiseScale,
                                               (cellPos.y + 1000) * noiseScale);

                int index = Mathf.FloorToInt(noise * groundTiles.Length);
                index = Mathf.Clamp(index, 0, groundTiles.Length - 1);

                groundTilemap.SetTile(cellPos, groundTiles[index]);

                // Resources
                TrySpawnResource(cellPos);

                generatedCells.Add(cellPos);
            }
        }
    }

    void TrySpawnResource(Vector3Int cellPos)
    {
        if (resources == null || resources.Length == 0) return;

        foreach (var res in resources)
        {
            if (res.tile == null) continue;

            if (Random.value < res.spawnChance)
            {
                resourceTilemap.SetTile(cellPos, res.tile);
                break;
            }
        }
    }
}