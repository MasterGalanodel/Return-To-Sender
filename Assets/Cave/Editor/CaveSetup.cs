using Shared.Graphics;
using Shared.HUD;
using TD.Editor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cave.Editor
{
    public static class CaveSetup
    {
        private const string SolidPath = "Assets/Cave/Art/CaveSolid.png";
        private const string PhysicsPath = "Assets/Cave/Art/CavePlayer.physicsMaterial2D";
        private const string SheetPath = "Assets/Shared/Graphics/Art/character_sprite_sheet.png";
        public const string SettingsPath = "Assets/Cave/Data/CaveSettings.asset";

        private static CaveSettings LoadOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<CaveSettings>(SettingsPath);
            if (settings) return settings;
            if (!AssetDatabase.IsValidFolder("Assets/Cave/Data")) AssetDatabase.CreateFolder("Assets/Cave", "Data");
            settings = ScriptableObject.CreateInstance<CaveSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        [MenuItem("Tools/Cave/Setup Cave Scene")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before cave setup.");
            if (!File.Exists(CaveExploration.TDScenePath)) throw new InvalidOperationException("Create the TD prototype scene first.");
            var shapes = AssetDatabase.LoadAssetAtPath<SharedShapes>(TDSetup.ShapesPath);
            if (!shapes || !shapes.player || !shapes.triangle || !shapes.unlitMaterial)
                throw new InvalidOperationException("Shared graphics assets are missing. Run Tools/Shared/Graphics/Assign Gameplay Sprites.");
            var platformArt = new [] { LoadSprite("Assets/Shared/Graphics/Art/platform1.png"), LoadSprite("Assets/Shared/Graphics/Art/platform2.png") };
            var healthArt = LoadSprite("Assets/Shared/HUD/Art/health.png");
            var buildArt = LoadSprite("Assets/Shared/HUD/Art/build.png");
            var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (!font) throw new InvalidOperationException("The bundled TMP font is missing.");
            Sprite solid = SolidSprite();
            var frictionless = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(PhysicsPath);
            if (!frictionless)
            {
                frictionless = new PhysicsMaterial2D("Cave Player") { friction = 0, bounciness = 0 };
                AssetDatabase.CreateAsset(frictionless, PhysicsPath);
            }
            Scene scene = default;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).path == CaveExploration.ScenePath) scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid())
                scene = File.Exists(CaveExploration.ScenePath)
                    ? EditorSceneManager.OpenScene(CaveExploration.ScenePath, OpenSceneMode.Additive)
                    : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var cave = scene.GetRootGameObjects().Select(g => g.GetComponent<CaveExploration>()).FirstOrDefault(c => c);
            if (!cave)
            {
                var root = new GameObject("Cave Exploration", typeof(CaveExploration));
                SceneManager.MoveGameObjectToScene(root, scene);
                cave = root.GetComponent<CaveExploration>();
            }
            var environment = Child(cave.transform, "Cave Environment");
            // Shaft, platforms and pickups are now generated at runtime per day by CaveBuilder.
            foreach (string name in new [] { "Floor", "Ceiling", "Exit Opening", "Exit Arrow", "Shaft", "Platforms", "Generated" })
                RemoveChild(environment, name);
            for (int i = 0; i < 8; i++) RemoveChild(environment, "Ceiling Rock " + i);
            var builder = GetOrAdd<CaveBuilder>(environment.gameObject);
            builder.solid = solid;
            builder.rockTriangle = shapes.triangle;
            builder.platformArt = platformArt;
            builder.groundArt = LoadSprite("Assets/Shared/Graphics/Art/cave_ground.png");
            builder.faithfulArt = LoadSprite("Assets/Shared/Graphics/Art/faithful_one.png");
            builder.healthIcon = healthArt;
            builder.buildIcon = buildArt;
            builder.unlitMaterial = shapes.unlitMaterial;
            builder.font = font;
            builder.wallMaterial = frictionless;
            EditorUtility.SetDirty(builder);
            cave.builder = builder;
            cave.settings = LoadOrCreateSettings();
            Text(environment, "Controls", "A/D   MOVE\nSPACE   JUMP / BOOST UP\nS + SPACE   DROP / BOOST DOWN\n"
                + $"<color=#FFC266>CLIMB BACK TO THIS TD DOOR\nWITHIN {GameSession.CaveSeconds}s OR LOSE HP</color>", font, 1.8f,
                new Vector2(3.4f, 2.8f), new Vector2(3.6f, 2), new Color(.64f, .79f, .87f));
            cave.controlsLabel = environment.Find("Controls");
            var gate = Child(environment, "TD Exit");
            // Placeholder; CaveExploration moves the door above platform 01 once the day's cave is generated.
            gate.localPosition = new Vector3(0, cave.settings.exitHeightAbovePlatform01, 0);
            Rect(gate, "Frame", solid, shapes, new Vector2(0, .85f), new Vector2(1.1f, 1.7f), new Color(.12f, .72f, .78f), 8);
            Rect(gate, "Opening", solid, shapes, new Vector2(0, .8f), new Vector2(.78f, 1.5f), new Color(.035f, .12f, .2f), 9);
            Text(gate, "Exit Label", "TD", font, 4.2f, new Vector2(0, 1.05f), new Vector2(1, .7f), Color.white);
            Text(gate, "Exit Caption", "TOWER DEFENSE", font, 2, new Vector2(0, 2.1f), new Vector2(3, .5f), new Color(.6f, 1, 1));
            var exitTrigger = GetOrAdd<BoxCollider2D>(gate.gameObject);
            exitTrigger.isTrigger = true;
            exitTrigger.size = new Vector2(.9f, 1.5f);
            exitTrigger.offset = new Vector2(0, .8f);
            GetOrAdd<CaveExit>(gate.gameObject).Configure(cave);
            cave.exitGate = gate;

            var player = Child(cave.transform, "Cave Player");
            player.localPosition = new Vector3(0, cave.settings.exitHeightAbovePlatform01 + .8f, 0); // runtime: TD door centre
            player.localScale = Vector3.one;
            var oldRenderer = player.GetComponent<SpriteRenderer>();
            if (oldRenderer) UnityEngine.Object.DestroyImmediate(oldRenderer);
            RemoveChild(player, "Black Outline");
            // Row 1 of the sheet: frames 0-3 idle, 4-7 run (all facing right).
            var sheet = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().ToDictionary(sp => sp.name);
            Sprite Frame(int i) => sheet.TryGetValue("character_sprite_sheet_" + i, out var sp) ? sp
                : throw new InvalidOperationException($"Missing character_sprite_sheet_{i} in {SheetPath}.");
            var idle = new [] { Frame(0), Frame(1), Frame(2), Frame(3) };
            var run = new [] { Frame(4), Frame(5), Frame(6), Frame(7) };
            var visual = Sprite(player, "Visual", idle[0], shapes, Color.white, 30);
            float visualScale = CaveExploration.PlayerHeight / idle[0].bounds.size.y; // same sizing CaveWalker uses in play
            visual.transform.localScale = Vector3.one * visualScale;
            visual.transform.localPosition = new Vector3(0, -CaveExploration.PlayerHalfHeight - idle[0].bounds.min.y * visualScale, 0);
            var fire = Sprite(player, "Jet Fire", LoadSprite("Assets/Shared/Graphics/Art/fire.png"), shapes, Color.white, 29);
            fire.enabled = false;
            var body = GetOrAdd<Rigidbody2D>(player.gameObject);
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = .28f;
            body.linearDamping = 0;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var playerCollider = GetOrAdd<BoxCollider2D>(player.gameObject);
            playerCollider.size = new Vector2(.43f, CaveExploration.PlayerHalfHeight * 2);
            playerCollider.offset = Vector2.zero;
            playerCollider.sharedMaterial = frictionless;
            cave.player = GetOrAdd<CaveWalker>(player.gameObject);
            cave.player.visual = visual;
            cave.player.idleFrames = idle;
            cave.player.moveFrames = run;
            cave.player.jetFire = fire;
            EditorUtility.SetDirty(cave.player);
            cave.walkSpeed = 5.2f;
            cave.jumpSpeed = 8.5f;
            var cameraObject = Child(cave.transform, "Cave Camera");
            var camera = GetOrAdd<Camera>(cameraObject.gameObject);
            camera.orthographic = true;
            camera.orthographicSize = 4.6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.045f, .09f, .15f);
            camera.cullingMask = 1 << 30;
            camera.transform.localPosition = new Vector3(0, -1.6f, -10);
            cave.caveCamera = camera;
            cave.openingArt = LoadSprite("Assets/Shared/Graphics/Art/opening.png");
            cave.existingInputActions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            EditorUtility.SetDirty(cave);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, CaveExploration.ScenePath);
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(CaveExploration.ScenePath, true),
                new EditorBuildSettingsScene(CaveExploration.TDScenePath, true)
            };
            foreach (var entry in EditorBuildSettings.scenes)
                if (entry.path != CaveExploration.ScenePath && entry.path != CaveExploration.TDScenePath) scenes.Add(entry);
            EditorBuildSettings.scenes = scenes.ToArray();
            Selection.activeObject = cave.settings;
            Debug.Log("Cave ready: layouts are generated per day at runtime from " + SettingsPath
                + " (layers, health and build pickups per day). The player drops out of the TD door above platform 01, dives down the clear left lane and climbs back.");
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        { var component = go.GetComponent<T>(); return component ? component : go.AddComponent<T>(); }
        private static Transform Child(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing) return existing;
            var child = new GameObject(name);
            child.layer = 30;
            child.transform.SetParent(parent, false);
            return child.transform;
        }
        private static void RemoveChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child) UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
        private static SpriteRenderer Sprite(Transform parent, string name, Sprite sprite, SharedShapes shapes, Color color, int sorting)
        {
            var child = Child(parent, name);
            var renderer = GetOrAdd<SpriteRenderer>(child.gameObject);
            renderer.sprite = sprite; renderer.sharedMaterial = shapes.unlitMaterial; renderer.color = color; renderer.sortingOrder = sorting;
            return renderer;
        }
        private static SpriteRenderer Rect(Transform parent, string name, Sprite sprite, SharedShapes shapes, Vector2 position, Vector2 size, Color color, int sorting)
        {
            var renderer = Sprite(parent, name, sprite, shapes, color, sorting);
            renderer.transform.localPosition = position;
            renderer.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1);
            return renderer;
        }
        private static void Text(Transform parent, string name, string value, TMP_FontAsset font, float size,
            Vector2 position, Vector2 bounds, Color color)
        {
            var child = Child(parent, name);
            var text = GetOrAdd<TextMeshPro>(child.gameObject);
            text.transform.localPosition = position;
            text.text = value; text.font = font; text.fontSize = size; text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = bounds;
            text.GetComponent<MeshRenderer>().sortingOrder = 20;
        }
        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            if (!sprite) throw new InvalidOperationException("Missing Cave sprite: " + path);
            return sprite;
        }
        private static Sprite SolidSprite()
        {
            if (!File.Exists(SolidPath))
            {
                var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
                var pixels = new Color32[256];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
                texture.SetPixels32(pixels); texture.Apply();
                File.WriteAllBytes(SolidPath, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(SolidPath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(SolidPath);
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 16;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(SolidPath);
        }
    }
}
