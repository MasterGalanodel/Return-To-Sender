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
    // Editor tool that builds (or rebuilds) the Cave scene hierarchy, wires references and registers build scenes.
    public static class CaveSetup
    {
        // ---------- Asset paths ----------
        private const string DataFolder = "Assets/Cave/Data";
        public const string SettingsPath = DataFolder + "/CaveSettings.asset";
        private const string SolidPath = "Assets/Cave/Art/CaveSolid.png";
        private const string PhysicsPath = "Assets/Cave/Art/CavePlayer.physicsMaterial2D";
        private const string SharedArtFolder = "Assets/Shared/Graphics/Art/";
        private const string SheetPath = SharedArtFolder + "character_sprite_sheet.png";
        private const string SheetFramePrefix = "character_sprite_sheet_";
        private static readonly string[] PlatformArtPaths = { SharedArtFolder + "platform1.png", SharedArtFolder + "platform2.png" };
        private const string HealthIconPath = SharedArtFolder + "health_icon.png";
        private const string BuildIconPath = SharedArtFolder + "build_icon.png";
        private const string GroundArtPath = SharedArtFolder + "cave_ground.png";
        private const string FaithfulArtPath = SharedArtFolder + "faithful_one.png";
        private const string JetFirePath = SharedArtFolder + "backpack_flame.png";
        private const string OpeningArtPath = SharedArtFolder + "opening.png";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string FontResource = "Fonts & Materials/LiberationSans SDF";

        // ---------- Hierarchy ----------
        private const int CaveLayer = 30; // every cave object lives on this layer so only the cave camera renders it
        private const string RootName = "Cave Exploration";
        private const string EnvironmentName = "Cave Environment";
        private const string ControlsName = "Controls";
        private const string GateName = "TD Exit";
        private const string PlayerName = "Cave Player";
        private const string CameraName = "Cave Camera";
        // Objects from older (static) cave layouts that must be cleared before CaveBuilder generates at runtime.
        private static readonly string[] LegacyChildren = { "Floor", "Ceiling", "Exit Opening", "Exit Arrow", "Shaft", "Platforms", "Generated" };
        private const string LegacyCeilingRockPrefix = "Ceiling Rock ";
        private const int LegacyCeilingRockCount = 8;

        // ---------- Controls hint ----------
        private const float ControlsFontSize = 1.8f;
        private static readonly Vector2 ControlsPosition = new Vector2(3.4f, 2.8f);
        private static readonly Vector2 ControlsBounds = new Vector2(3.6f, 2);
        private static readonly Color ControlsColor = new Color(.64f, .79f, .87f);

        // ---------- TD exit gate ----------
        private static readonly Vector2 GateFramePosition = new Vector2(0, .85f);
        private static readonly Vector2 GateFrameSize = new Vector2(1.1f, 1.7f);
        private static readonly Color GateFrameColor = new Color(.12f, .72f, .78f);
        private const int GateFrameSorting = 8;
        private static readonly Vector2 GateOpeningPosition = new Vector2(0, .8f);
        private static readonly Vector2 GateOpeningSize = new Vector2(.78f, 1.5f);
        private static readonly Color GateOpeningColor = new Color(.035f, .12f, .2f);
        private const int GateOpeningSorting = 9;
        private const float ExitLabelFontSize = 2.4f;
        private static readonly Vector2 ExitLabelPosition = new Vector2(0, 1.05f);
        private static readonly Vector2 ExitLabelBounds = new Vector2(1, .7f);
        private const float ExitCaptionFontSize = 2;
        private static readonly Vector2 ExitCaptionPosition = new Vector2(0, 2.1f);
        private static readonly Vector2 ExitCaptionBounds = new Vector2(3, .5f);
        private static readonly Color ExitCaptionColor = new Color(.6f, 1, 1);
        private static readonly Vector2 ExitTriggerSize = new Vector2(.9f, 1.5f);
        private static readonly Vector2 ExitTriggerOffset = new Vector2(0, .8f);

        // ---------- Player ----------
        private const float PlayerSpawnAboveExit = .8f; // spawn roughly at the door's centre
        private const int IdleFirstFrame = 0, RunFirstFrame = 4, FramesPerAnimation = 4;
        private const int PlayerVisualSorting = 30;
        private const int JetFireSorting = 29;
        private const float PlayerGravityScale = .56f; // low gravity for the "diving" feel
        private const float PlayerColliderWidth = .43f;

        // ---------- Camera ----------
        private const float CameraOrthographicSize = 4.6f;
        private static readonly Color CameraBackground = new Color(.045f, .09f, .15f);
        private static readonly Vector3 CameraLocalPosition = new Vector3(0, -1.6f, -10);

        // ---------- Text / solid sprite ----------
        private const int TextSorting = 20;
        private const int SolidTextureSize = 16; // also used as pixels-per-unit, so the solid sprite is exactly 1x1 unit

        // Loads the shared CaveSettings asset, creating it (and the Data folder) on first run.
        private static CaveSettings LoadOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<CaveSettings>(SettingsPath);
            if (settings) return settings;
            if (!AssetDatabase.IsValidFolder(DataFolder)) AssetDatabase.CreateFolder("Assets/Cave", "Data");
            settings = ScriptableObject.CreateInstance<CaveSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        // Entry point: idempotently builds the Cave scene; safe to re-run after changing art or constants.
        [MenuItem("Tools/Cave/Setup Cave Scene")]
        public static void Setup()
        {
            // Validate prerequisites before touching any scene.
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before cave setup.");
            if (!File.Exists(CaveExploration.TDScenePath)) throw new InvalidOperationException("Create the TD prototype scene first.");
            var shapes = AssetDatabase.LoadAssetAtPath<SharedShapes>(TDSetup.ShapesPath);
            if (!shapes || !shapes.player || !shapes.triangle || !shapes.unlitMaterial)
                throw new InvalidOperationException("Shared graphics assets are missing. Run Tools/Shared/Graphics/Assign Gameplay Sprites.");
            var platformArt = PlatformArtPaths.Select(LoadSprite).ToArray();
            var healthArt = LargestSprite(HealthIconPath);
            var buildArt = LargestSprite(BuildIconPath);
            var font = Resources.Load<TMP_FontAsset>(FontResource);
            if (!font) throw new InvalidOperationException("The bundled TMP font is missing.");
            Sprite solid = SolidSprite();
            // Zero-friction material stops the player sticking to walls when pushing against them mid-air.
            var frictionless = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(PhysicsPath);
            if (!frictionless)
            {
                frictionless = new PhysicsMaterial2D("Cave Player") { friction = 0, bounciness = 0 };
                AssetDatabase.CreateAsset(frictionless, PhysicsPath);
            }

            // Reuse the cave scene if already open, else open it additively, else create a new empty one.
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
                var root = new GameObject(RootName, typeof(CaveExploration));
                SceneManager.MoveGameObjectToScene(root, scene);
                cave = root.GetComponent<CaveExploration>();
            }

            // --- Environment: shaft, platforms and pickups are generated at runtime per day by CaveBuilder. ---
            var environment = Child(cave.transform, EnvironmentName);
            foreach (string name in LegacyChildren)
                RemoveChild(environment, name);
            for (int i = 0; i < LegacyCeilingRockCount; i++) RemoveChild(environment, LegacyCeilingRockPrefix + i);
            var builder = GetOrAdd<CaveBuilder>(environment.gameObject);
            builder.solid = solid;
            builder.rockTriangle = shapes.triangle;
            builder.platformArt = platformArt;
            builder.groundArt = LoadSprite(GroundArtPath);
            builder.faithfulArt = LoadSprite(FaithfulArtPath);
            builder.healthIcon = healthArt;
            builder.buildIcon = buildArt;
            builder.unlitMaterial = shapes.unlitMaterial;
            builder.font = font;
            builder.wallMaterial = frictionless;
            EditorUtility.SetDirty(builder);
            cave.builder = builder;
            cave.settings = LoadOrCreateSettings();
            Text(environment, ControlsName, $"<color=#FFC266>CLIMB BACK TO THIS TD DOOR\nWITHIN {GameSession.CaveSeconds}s OR LOSE HP</color>", font,
                ControlsFontSize, ControlsPosition, ControlsBounds, ControlsColor);
            cave.controlsLabel = environment.Find(ControlsName);

            // --- TD exit gate: frame + dark opening + labels + trigger that returns the player to TD. ---
            var gate = Child(environment, GateName);
            // Placeholder; CaveExploration moves the door above platform 01 once the day's cave is generated.
            gate.localPosition = new Vector3(0, cave.settings.exitHeightAbovePlatform01, 0);
            Rect(gate, "Frame", solid, shapes, GateFramePosition, GateFrameSize, GateFrameColor, GateFrameSorting);
            Rect(gate, "Opening", solid, shapes, GateOpeningPosition, GateOpeningSize, GateOpeningColor, GateOpeningSorting);
            Text(gate, "Exit Label", "EXIT", font, ExitLabelFontSize, ExitLabelPosition, ExitLabelBounds, Color.white);
            Text(gate, "Exit Caption", "CAVE EXIT", font, ExitCaptionFontSize, ExitCaptionPosition, ExitCaptionBounds, ExitCaptionColor);
            var exitTrigger = GetOrAdd<BoxCollider2D>(gate.gameObject);
            exitTrigger.isTrigger = true;
            exitTrigger.size = ExitTriggerSize;
            exitTrigger.offset = ExitTriggerOffset;
            GetOrAdd<CaveExit>(gate.gameObject).Configure(cave);
            cave.exitGate = gate;

            // --- Player: animated sprite child, jet-fire child, rigidbody + collider, CaveWalker controller. ---
            var player = Child(cave.transform, PlayerName);
            player.localPosition = new Vector3(0, cave.settings.exitHeightAbovePlatform01 + PlayerSpawnAboveExit, 0); // runtime: TD door centre
            player.localScale = Vector3.one;
            // Clean up leftovers from the old single-sprite player setup.
            var oldRenderer = player.GetComponent<SpriteRenderer>();
            if (oldRenderer) UnityEngine.Object.DestroyImmediate(oldRenderer);
            RemoveChild(player, "Black Outline");
            // Row 1 of the sheet: frames 0-3 idle, 4-7 run (all facing right).
            var sheet = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().ToDictionary(sp => sp.name);
            Sprite Frame(int i) => sheet.TryGetValue(SheetFramePrefix + i, out var sp) ? sp
                : throw new InvalidOperationException($"Missing {SheetFramePrefix}{i} in {SheetPath}.");
            var idle = Enumerable.Range(IdleFirstFrame, FramesPerAnimation).Select(Frame).ToArray();
            var run = Enumerable.Range(RunFirstFrame, FramesPerAnimation).Select(Frame).ToArray();
            var visual = Sprite(player, "Visual", idle[0], shapes, Color.white, PlayerVisualSorting);
            // Scale the sprite to PlayerHeight and align its feet with the collider bottom (same maths CaveWalker uses in play).
            float visualScale = CaveExploration.PlayerHeight / idle[0].bounds.size.y;
            visual.transform.localScale = Vector3.one * visualScale;
            visual.transform.localPosition = new Vector3(0, -CaveExploration.PlayerHalfHeight - idle[0].bounds.min.y * visualScale, 0);
            var fire = Sprite(player, "Jet Fire", LoadSprite(JetFirePath), shapes, Color.white, JetFireSorting);
            fire.enabled = false;
            var body = GetOrAdd<Rigidbody2D>(player.gameObject);
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = PlayerGravityScale;
            body.linearDamping = 0;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var playerCollider = GetOrAdd<BoxCollider2D>(player.gameObject);
            playerCollider.size = new Vector2(PlayerColliderWidth, CaveExploration.PlayerHalfHeight * 2);
            playerCollider.offset = Vector2.zero;
            playerCollider.sharedMaterial = frictionless;
            cave.player = GetOrAdd<CaveWalker>(player.gameObject);
            cave.player.visual = visual;
            cave.player.idleFrames = idle;
            cave.player.moveFrames = run;
            cave.player.jetFire = fire;
            EditorUtility.SetDirty(cave.player);
            cave.walkSpeed = CaveExploration.DefaultWalkSpeed;
            cave.jumpSpeed = CaveExploration.DefaultJumpSpeed;

            // --- Camera: orthographic, renders only the cave layer. ---
            var cameraObject = Child(cave.transform, CameraName);
            var camera = GetOrAdd<Camera>(cameraObject.gameObject);
            camera.orthographic = true;
            camera.orthographicSize = CameraOrthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = CameraBackground;
            camera.cullingMask = 1 << CaveLayer;
            camera.transform.localPosition = CameraLocalPosition;
            cave.caveCamera = camera;
            cave.openingArt = LoadSprite(OpeningArtPath);
            cave.existingInputActions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(InputActionsPath);

            // --- Save and register build scenes: Cave first, TD second, then any others kept in their order. ---
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

        // Returns the existing component of type T, adding one if missing (keeps setup re-runnable).
        private static T GetOrAdd<T>(GameObject go) where T : Component
        { var component = go.GetComponent<T>(); return component ? component : go.AddComponent<T>(); }

        // Finds a named child or creates it on the cave layer.
        private static Transform Child(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing) return existing;
            var child = new GameObject(name);
            child.layer = CaveLayer;
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        // Deletes a named child if it exists.
        private static void RemoveChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child) UnityEngine.Object.DestroyImmediate(child.gameObject);
        }

        // Creates/updates a child SpriteRenderer using the shared unlit material.
        private static SpriteRenderer Sprite(Transform parent, string name, Sprite sprite, SharedShapes shapes, Color color, int sorting)
        {
            var child = Child(parent, name);
            var renderer = GetOrAdd<SpriteRenderer>(child.gameObject);
            renderer.sprite = sprite; renderer.sharedMaterial = shapes.unlitMaterial; renderer.color = color; renderer.sortingOrder = sorting;
            return renderer;
        }

        // Sprite child scaled so it covers exactly `size` world units, regardless of the sprite's native size.
        private static SpriteRenderer Rect(Transform parent, string name, Sprite sprite, SharedShapes shapes, Vector2 position, Vector2 size, Color color, int sorting)
        {
            var renderer = Sprite(parent, name, sprite, shapes, color, sorting);
            renderer.transform.localPosition = position;
            renderer.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1);
            return renderer;
        }

        // Creates/updates a centred, non-wrapping world-space TextMeshPro label.
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
            text.GetComponent<MeshRenderer>().sortingOrder = TextSorting;
        }

        // The pickup PNGs may contain a tiny stray slice; use the largest sprite sub-asset.
        private static Sprite LargestSprite(string path)
        {
            var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .OrderByDescending(s => s.rect.width * s.rect.height).FirstOrDefault();
            if (!sprite) throw new InvalidOperationException("Missing Cave sprite: " + path);
            return sprite;
        }

        // Loads the first sprite sub-asset at a path, failing loudly if absent.
        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            if (!sprite) throw new InvalidOperationException("Missing Cave sprite: " + path);
            return sprite;
        }

        // Returns a plain white 1x1-unit sprite, generating and importing the PNG the first time.
        private static Sprite SolidSprite()
        {
            if (!File.Exists(SolidPath))
            {
                var texture = new Texture2D(SolidTextureSize, SolidTextureSize, TextureFormat.RGBA32, false);
                var pixels = new Color32[SolidTextureSize * SolidTextureSize];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
                texture.SetPixels32(pixels); texture.Apply();
                File.WriteAllBytes(SolidPath, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(SolidPath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(SolidPath);
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = SolidTextureSize;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(SolidPath);
        }
    }
}
