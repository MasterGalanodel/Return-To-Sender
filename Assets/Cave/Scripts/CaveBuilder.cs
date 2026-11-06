using Shared.Graphics;
using Shared.HUD;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Cave
{
    // Generates the shaft, platforms and pickups at runtime from CaveSettings. Same seed + day = same cave.
    [DisallowMultipleComponent]
    public sealed class CaveBuilder : MonoBehaviour
    {
        public const int CaveLayer = 30;
        private const string GeneratedName = "Generated";
        private const float RestWidth = 4.4f;
        private const float WallOuterX = 7.2f;

        [Header("Art (assigned by Tools/Cave/Setup Cave Scene)")]
        public Sprite solid;
        public Sprite rockTriangle;
        public Sprite[] platformArt;
        public Sprite groundArt;
        public Sprite faithfulArt;
        public Sprite healthIcon;
        public Sprite buildIcon;
        public Material unlitMaterial;
        public TMP_FontAsset font;
        public PhysicsMaterial2D wallMaterial;

        public struct Result
        {
            public CavePlatform[] platforms;
            public float bottomY;      // cave floor surface
            public float topY;         // underside of the shaft cap
            public float exitY;        // base of the TD door
            public Vector2 spawn;      // centre of the TD door: each dive starts there
        }

        public bool HasArt => solid && rockTriangle && platformArt != null && platformArt.Length > 0 && platformArt[0]
            && groundArt && healthIcon && buildIcon && unlitMaterial && font;

        public Result Build(CaveSettings settings, int day, int seed)
        {
            // Legacy editor-built geometry from before runtime generation.
            DestroyChild("Shaft");
            DestroyChild("Platforms");
            DestroyChild(GeneratedName);
            var root = Child(transform, GeneratedName);
            var rng = new System.Random(unchecked(seed * 486187739 + day * 16777619));
            var layout = settings.ForDay(day);
            int layers = Mathf.Max(2, layout.platformLayers);
            float half = CaveExploration.ShaftHalfWidth;
            float laneEdge = -half + settings.freeFallLaneWidth; // platforms stay right of this line

            var platforms = new List<CavePlatform>();
            var platformLayer = new List<int>();
            var platformsRoot = Child(root, "Platforms");
            float y = 0, prevX = 0, floorY = 0;
            for (int layer = 0; layer < layers; layer++)
            {
                if (layer > 0) y -= Range(rng, settings.layerSpacing);
                bool top = layer == 0, floor = layer == layers - 1;
                bool rest = settings.restPlatformEvery > 0 && layer % settings.restPlatformEvery == 0;
                string label = (layer + 1).ToString("00");
                if (floor)
                {
                    floorY = y;
                    platforms.Add(Floor(platformsRoot, platforms.Count, y, label));
                    platformLayer.Add(layer);
                    continue;
                }
                float x = 0, width = Mathf.Min(RestWidth, half - laneEdge - .2f);
                if (!top && !rest)
                {
                    width = Range(rng, settings.platformWidth);
                    x = prevX + Range(rng, -settings.maxSideStep, settings.maxSideStep);
                }
                x = Mathf.Clamp(x, laneEdge + width / 2, half - width / 2 - .1f);
                platforms.Add(Ledge(platformsRoot, platforms.Count, x, y, width, label, true));
                platformLayer.Add(layer);
                if (!top && !rest && rng.NextDouble() < settings.secondPlatformChance)
                {
                    float width2 = Range(rng, settings.platformWidth);
                    float min2 = laneEdge + width2 / 2, max2 = half - width2 / 2 - .1f;
                    float gap = width / 2 + width2 / 2 + .8f;
                    // Opposite side of the first platform, whichever side has room.
                    float from = x + gap, to = max2;
                    if (to <= from) { from = min2; to = x - gap; }
                    if (to > from)
                    {
                        platforms.Add(Ledge(platformsRoot, platforms.Count, Range(rng, from, to), y, width2, label, false));
                        platformLayer.Add(layer);
                    }
                }
                prevX = x;
            }

            // Pickups: spread one per platform first, then double up, never on 01 or the floor.
            var candidates = new List<int>();
            for (int i = 0; i < platforms.Count; i++)
                if (platformLayer[i] != 0 && platformLayer[i] != layers - 1) candidates.Add(i);
            Shuffle(rng, candidates);
            var kinds = new List<GameSession.PickupType>();
            for (int i = 0; i < layout.healthPickups; i++) kinds.Add(GameSession.PickupType.Health);
            for (int i = 0; i < layout.buildPickups; i++) kinds.Add(GameSession.PickupType.Build);
            Shuffle(rng, kinds);
            var assigned = new Dictionary<int, List<GameSession.PickupType>>();
            int placed = 0;
            for (int round = 0; round < settings.maxPickupsPerPlatform && placed < kinds.Count; round++)
                foreach (int index in candidates)
                {
                    if (placed >= kinds.Count) break;
                    int fit = Mathf.Max(1, Mathf.FloorToInt((platforms[index].Width - .3f) / .9f));
                    if (!assigned.TryGetValue(index, out var list)) assigned[index] = list = new List<GameSession.PickupType>();
                    if (list.Count > round || list.Count >= fit) continue;
                    list.Add(kinds[placed++]);
                }
            if (placed < kinds.Count)
                Debug.LogWarning($"Cave day {day}: only room for {placed} of {kinds.Count} pickups. Add layers or raise maxPickupsPerPlatform.", this);
            int pickupId = 0;
            for (int i = 0; i < platforms.Count; i++)
            {
                if (!assigned.TryGetValue(i, out var list)) continue;
                for (int j = 0; j < list.Count; j++)
                {
                    bool build = list[j] == GameSession.PickupType.Build;
                    float offset = (j - (list.Count - 1) / 2f) * .9f;
                    Pickup(platforms[i].transform, "Pickup " + j, offset, pickupId++, list[j],
                        build ? layout.buildPerPickup : layout.healthPerPickup);
                }
            }

            if (day >= settings.faithfulFromDay && faithfulArt) FaithfulOne(platforms[0], settings);

            float exitY = settings.exitHeightAbovePlatform01;
            float capBottom = exitY + 2.7f;
            float shaftBottom = floorY - 3.5f;
            var shaft = Child(root, "Shaft");
            var rockDark = new Color(.025f, .045f, .07f);
            Box(shaft, "Background", new Vector2(-WallOuterX, shaftBottom), new Vector2(WallOuterX, capBottom + 2),
                new Color(.045f, .09f, .15f), -30, false);
            Box(shaft, "Right Wall", new Vector2(half, shaftBottom), new Vector2(WallOuterX, capBottom + 2), rockDark, -5, true);
            Box(shaft, "Left Wall", new Vector2(-WallOuterX, shaftBottom), new Vector2(-half, capBottom + 2), rockDark, -5, true);
            Box(shaft, "Shaft Cap", new Vector2(-WallOuterX, capBottom), new Vector2(WallOuterX, capBottom + 1.5f), rockDark, -5, true);
            Box(shaft, "Exit Ledge", new Vector2(-.9f, exitY - .14f), new Vector2(.9f, exitY), new Color(.12f, .5f, .55f), 7, false);
            int rockIndex = 0;
            for (float ry = capBottom - 2; ry > shaftBottom; ry -= 7)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var rock = Sprite(shaft, "Rock " + rockIndex, rockTriangle,
                        rockIndex % 2 == 0 ? new Color(.06f, .12f, .18f) : new Color(.045f, .085f, .13f), -4);
                    rock.transform.localPosition = new Vector3(side * 5.6f, ry, 0);
                    rock.transform.localScale = new Vector3(1.6f, 5.5f, 1);
                    rock.transform.localRotation = Quaternion.Euler(0, 0, side < 0 ? -90 : 90);
                    rockIndex++;
                }
            }

            return new Result
            {
                platforms = platforms.ToArray(),
                bottomY = floorY,
                topY = capBottom,
                exitY = exitY,
                spawn = new Vector2(0, exitY + .8f)
            };
        }

        // Red screen stroke + tint parented to the camera, shown by CaveExploration while overtime flashes.
        public GameObject BuildOvertimeFlash(Camera cam, Color color)
        {
            var existing = cam.transform.Find("Overtime Flash");
            if (existing) Destroy(existing.gameObject);
            var root = Child(cam.transform, "Overtime Flash");
            root.localPosition = new Vector3(0, 0, 1);
            float h = cam.orthographicSize, w = h * 16f / 9, t = .32f;
            var tint = color; tint.a *= .2f;
            Box(root, "Tint", new Vector2(-w, -h), new Vector2(w, h), tint, 200, false);
            Box(root, "Top", new Vector2(-w, h - t), new Vector2(w, h), color, 201, false);
            Box(root, "Bottom", new Vector2(-w, -h), new Vector2(w, -h + t), color, 201, false);
            Box(root, "Left", new Vector2(-w, -h), new Vector2(-w + t, h), color, 201, false);
            Box(root, "Right", new Vector2(w - t, -h), new Vector2(w, h), color, 201, false);
            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        // NPC on the left end of platform 01, pointing out over the free-fall lane, with her hint below.
        private void FaithfulOne(CavePlatform top, CaveSettings settings)
        {
            var npc = Child(top.transform, "Faithful One");
            float scale = settings.faithfulHeight / faithfulArt.bounds.size.y;
            npc.localPosition = new Vector3(-top.Width / 2 + .35f, 0, 0);
            var art = Sprite(npc, "Art", faithfulArt, Color.white, 12);
            art.transform.localScale = Vector3.one * scale;
            art.transform.localPosition = new Vector3(-faithfulArt.bounds.center.x * scale, -faithfulArt.bounds.min.y * scale - .02f, 0);
            if (string.IsNullOrWhiteSpace(settings.faithfulHint)) return;
            var hint = Text(npc, "Hint", settings.faithfulHint, 1.7f, new Vector2(-.45f, -.75f), new Vector2(3, .7f), new Color(.86f, .84f, .78f));
            hint.outlineWidth = .25f;
            hint.outlineColor = new Color32(10, 8, 6, 255);
        }

        private CavePlatform Floor(Transform parent, int index, float y, string label)
        {
            var step = Child(parent, "Layer " + label + " Floor");
            step.localPosition = new Vector3(0, y, 0);
            var platform = step.gameObject.AddComponent<CavePlatform>();
            float half = CaveExploration.ShaftHalfWidth;
            platform.Configure(index, half * 2);
            // cave_ground.png spans wall to wall; its ridge sits on the walkable surface.
            float span = WallOuterX * 2;
            var ground = Sprite(step, "Ground", groundArt, Color.white, 6);
            float scale = span / groundArt.bounds.size.x;
            ground.transform.localScale = Vector3.one * scale;
            ground.transform.localPosition = new Vector3(-groundArt.bounds.center.x * scale,
                .08f - groundArt.bounds.max.y * scale, 0);
            var collider = step.gameObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(span, 3.5f);
            collider.offset = new Vector2(0, -1.75f);
            collider.sharedMaterial = wallMaterial;
            Text(step, "Step Label", label, 2.4f, new Vector2(-half + .5f, -.35f), new Vector2(1, .45f), new Color(1, .88f, .65f));
            return platform;
        }

        private CavePlatform Ledge(Transform parent, int index, float x, float y, float width, string label, bool showLabel)
        {
            var step = Child(parent, "Layer " + label + (showLabel ? "" : " B"));
            step.localPosition = new Vector3(x, y, 0);
            var platform = step.gameObject.AddComponent<CavePlatform>();
            platform.Configure(index, width);
            var art = platformArt[index % platformArt.Length] ? platformArt[index % platformArt.Length] : platformArt[0];
            var rock = Sprite(step, "Rock", art, Color.white, 5);
            float scale = width / art.bounds.size.x;
            rock.transform.localScale = Vector3.one * scale;
            rock.transform.localPosition = new Vector3(0, -art.bounds.size.y * scale / 2, 0);
            var surface = step.gameObject.AddComponent<BoxCollider2D>();
            surface.size = new Vector2(width * .9f, .18f);
            surface.offset = new Vector2(0, -.09f);
            surface.usedByEffector = true;
            var effector = step.gameObject.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.useOneWayGrouping = true;
            effector.surfaceArc = 170;
            if (showLabel)
                Text(step, "Step Label", label, 2.4f, new Vector2(-width / 2 + .45f, -.35f), new Vector2(1, .45f), new Color(1, .88f, .65f));
            return platform;
        }

        private void Pickup(Transform platform, string name, float x, int id, GameSession.PickupType kind, int amount)
        {
            bool build = kind == GameSession.PickupType.Build;
            var pickup = Child(platform, name);
            pickup.localPosition = new Vector3(x, .48f, 0);
            var icon = Sprite(pickup, "Icon", build ? buildIcon : healthIcon, Color.white, 15);
            icon.transform.localScale = Vector3.one * SharedShapes.ActorScale(icon.sprite, .48f);
            var trigger = pickup.gameObject.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = .34f;
            pickup.gameObject.AddComponent<CavePickup>().Configure("pickup-" + id, kind, amount, icon.transform);
            Text(pickup, "Reward Label", build ? $"+{amount} BUILD" : $"+{amount} HP", 2.2f, new Vector2(0, .48f), new Vector2(2, .4f),
                build ? new Color(.55f, 1, .82f) : new Color(1, .64f, .6f));
        }

        private void Box(Transform parent, string name, Vector2 min, Vector2 max, Color color, int sorting, bool solidCollider)
        {
            var renderer = Sprite(parent, name, solid, color, sorting);
            Vector2 size = max - min;
            renderer.transform.localPosition = (min + max) / 2;
            renderer.transform.localScale = new Vector3(size.x / solid.bounds.size.x, size.y / solid.bounds.size.y, 1);
            if (!solidCollider) return;
            var collider = renderer.gameObject.AddComponent<BoxCollider2D>();
            collider.size = solid.bounds.size;
            collider.sharedMaterial = wallMaterial;
        }

        private SpriteRenderer Sprite(Transform parent, string name, Sprite sprite, Color color, int sorting)
        {
            var renderer = Child(parent, name).gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = unlitMaterial;
            renderer.color = color;
            renderer.sortingOrder = sorting;
            return renderer;
        }

        private TextMeshPro Text(Transform parent, string name, string value, float size, Vector2 position, Vector2 bounds, Color color)
        {
            var text = Child(parent, name).gameObject.AddComponent<TextMeshPro>();
            text.transform.localPosition = position;
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = bounds;
            text.GetComponent<MeshRenderer>().sortingOrder = 20;
            return text;
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name) { layer = CaveLayer };
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private void DestroyChild(string name)
        {
            var child = transform.Find(name);
            if (!child) return;
            child.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
        }

        private static float Range(System.Random rng, Vector2 range) => Range(rng, range.x, range.y);
        private static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
        private static void Shuffle<T>(System.Random rng, List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
