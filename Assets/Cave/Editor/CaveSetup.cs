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
            // Replace the old flat corridor without touching unrelated scene roots.
            foreach (string name in new [] { "Floor", "Ceiling", "Exit Opening", "Exit Arrow" }) RemoveChild(environment, name);
            for (int i = 0; i < 8; i++) RemoveChild(environment, "Ceiling Rock " + i);
            var shaft = Child(environment, "Shaft");
            float shaftHeight = -CaveExploration.BottomY + 12;
            float middleY = CaveExploration.BottomY / 2;
            Rect(shaft, "Background", solid, shapes, new Vector2(0, middleY), new Vector2(20, shaftHeight), new Color(.045f, .09f, .15f), -30);
            for (int side = -1; side <= 1; side += 2)
            {
                var wall = Rect(shaft, side < 0 ? "Left Wall" : "Right Wall", solid, shapes,
                    new Vector2(side * 6.2f, middleY), new Vector2(2, shaftHeight), new Color(.025f, .045f, .07f), -5);
                var wallCollider = GetOrAdd<BoxCollider2D>(wall.gameObject);
                wallCollider.size = Vector2.one;
                wallCollider.sharedMaterial = frictionless;
                for (int i = 0; i < 15; i++)
                {
                    var rock = Sprite(shaft, (side < 0 ? "Left" : "Right") + " Rock " + i, shapes.triangle, shapes,
                        i % 2 == 0 ? new Color(.06f, .12f, .18f) : new Color(.045f, .085f, .13f), -4);
                    rock.transform.localPosition = new Vector3(side * 5.6f, 2 - i * 7, 0);
                    rock.transform.localScale = new Vector3(1.6f, 5.5f, 1);
                    rock.transform.localRotation = Quaternion.Euler(0, 0, side < 0 ? -90 : 90);
                }
            }
            Rect(shaft, "Shaft Cap", solid, shapes, new Vector2(0, 4.8f), new Vector2(15, 1.5f), new Color(.025f, .045f, .07f), -5);
            var platformsRoot = Child(environment, "Platforms");
            cave.platforms = new CavePlatform[CaveExploration.StepCount];
            for (int i = 0; i < CaveExploration.StepCount; i++)
            {
                bool rest = i == 0 || i == CaveExploration.StepCount - 1 || i % 10 == 0;
                float x = rest ? 0 : i % 4 == 1 ? 2.7f : i % 4 == 3 ? -2.7f : 0;
                float width = rest ? 4.4f : 3.1f + (i % 3) * .15f;
                var step = Child(platformsRoot, "Step " + (i + 1).ToString("00"));
                step.localPosition = new Vector3(x, -i * CaveExploration.StepSpacing, 0);
                step.localScale = Vector3.one;
                var platform = GetOrAdd<CavePlatform>(step.gameObject);
                platform.Configure(i, width);
                cave.platforms[i] = platform;
                var art = platformArt[i % 2];
                var rock = Sprite(step, "Rock", art, shapes, Color.white, 5);
                float scale = width / art.bounds.size.x;
                rock.transform.localScale = Vector3.one * scale;
                rock.transform.localPosition = new Vector3(0, -art.bounds.size.y * scale / 2, 0);
                var surface = GetOrAdd<BoxCollider2D>(step.gameObject);
                surface.size = new Vector2(width * .9f, .18f);
                surface.offset = new Vector2(0, -.09f);
                surface.usedByEffector = true;
                var effector = GetOrAdd<PlatformEffector2D>(step.gameObject);
                effector.useOneWay = true;
                effector.useOneWayGrouping = true;
                effector.surfaceArc = 170;
                Text(step, "Step Label", (i + 1).ToString("00"), font, 2.4f,
                    new Vector2(-width / 2 + .45f, -.35f), new Vector2(1, .45f), new Color(1, .88f, .65f));
                if (i > 0 && i < CaveExploration.StepCount - 1 && i % 3 == 0)
                {
                    bool build = i % 9 == 0;
                    var pickup = Child(step, "Pickup");
                    pickup.localPosition = new Vector3(0, .48f, 0);
                    var icon = Sprite(pickup, "Icon", build ? buildArt : healthArt, shapes, Color.white, 15);
                    icon.transform.localPosition = Vector3.zero;
                    icon.transform.localScale = Vector3.one * SharedShapes.ActorScale(icon.sprite, .48f);
                    var trigger = GetOrAdd<CircleCollider2D>(pickup.gameObject);
                    trigger.isTrigger = true;
                    trigger.radius = .34f;
                    GetOrAdd<CavePickup>(pickup.gameObject).Configure("step-" + i,
                        build ? GameSession.PickupType.Build : GameSession.PickupType.Health, build ? 1 : 5, icon.transform);
                    Text(pickup, "Reward Label", build ? "+1 BUILD" : "+5 HP", font, 2.2f,
                        new Vector2(0, .48f), new Vector2(2, .4f), build ? new Color(.55f, 1, .82f) : new Color(1, .64f, .6f));
                }
            }
            Text(environment, "Controls", "A/D   MOVE\nSPACE   JUMP\nS + SPACE   DROP", font, 1.8f,
                new Vector2(-3.7f, 1.1f), new Vector2(3, 1.1f), new Color(.64f, .79f, .87f));
            var gate = Child(environment, "TD Exit");
            gate.localPosition = new Vector3(1.3f, CaveExploration.BottomY, 0);
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

            var player = Sprite(cave.transform, "Cave Player", shapes.player, shapes, Color.white, 30);
            player.transform.localPosition = cave.platforms[0].SafePosition;
            float playerScale = SharedShapes.ActorScale(shapes.player, CaveExploration.PlayerHeight);
            player.transform.localScale = Vector3.one * playerScale;
            RemoveChild(player.transform, "Black Outline");
            var body = GetOrAdd<Rigidbody2D>(player.gameObject);
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 2.8f;
            body.linearDamping = 0;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var playerCollider = GetOrAdd<BoxCollider2D>(player.gameObject);
            playerCollider.size = new Vector2(.43f, CaveExploration.PlayerHalfHeight * 2) / playerScale;
            playerCollider.offset = Vector2.zero;
            playerCollider.sharedMaterial = frictionless;
            cave.player = GetOrAdd<CaveWalker>(player.gameObject);
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
            Selection.activeGameObject = cave.gameObject;
            Debug.Log("Vertical Cave ready: 40 one-way platforms, 8 health pickups (+5) and 4 build pickups (+1) per day. A/D move, Space jump, Down + Space drop.");
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
