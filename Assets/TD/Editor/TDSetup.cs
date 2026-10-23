using Shared.Graphics;
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TD.Editor
{
    public static class TDSetup
    {
        public const string Folder = "Assets/TD";
        public const string ScenePath = Folder + "/Scenes/TD.unity";
        public const string SettingsPath = Folder + "/Data/PrototypeSettings.asset";
        public const string ShapesPath = "Assets/Shared/Graphics/Data/PrototypeShapes.asset";

        [MenuItem("Tools/TD/Setup Prototype")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before running TD setup.");
            Directory.CreateDirectory(Folder + "/Scenes");
            Directory.CreateDirectory(Folder + "/Data");
            Directory.CreateDirectory("Assets/Shared/Graphics/Art");
            AssetDatabase.Refresh();
            var settings = LoadOrCreate<TDSettings>(SettingsPath);
            if (!settings.packagedTerrain)
            {
                settings.packagedTerrain = AssetDatabase.LoadAssetAtPath<TextAsset>(Folder + "/Data/arab_32x18.txt");
                EditorUtility.SetDirty(settings);
            }
            var shapes = LoadOrCreate<SharedShapes>(ShapesPath);
            AssignGameplaySprites(shapes);
            shapes.circle = CreateSprite("Circle", 256, 256, (x, y) => x * x + y * y <= 0.25f);
            shapes.triangle = CreateSprite("Triangle", 256, 256, (x, y) => y >= -0.5f && y <= 0.5f && Mathf.Abs(x) <= (0.5f - y) * 0.5f);
            shapes.hex = CreateSprite("FlatTopHex", 512, 444, (x, y) => Mathf.Abs(y) <= Mathf.Sqrt(3) / 4f && Mathf.Abs(x) <= 0.5f - Mathf.Abs(y) / Mathf.Sqrt(3));
            string terrainMaterialPath = "Assets/Shared/Graphics/Art/HexTerrain.mat";
            shapes.terrainMaterial = AssetDatabase.LoadAssetAtPath<Material>(terrainMaterialPath);
            if (!shapes.terrainMaterial)
            {
                var terrainShader = Shader.Find("TD/Hex Terrain Unlit");
                if (!terrainShader) throw new InvalidOperationException("Hex terrain shader has not imported.");
                shapes.terrainMaterial = new Material(terrainShader);
                AssetDatabase.CreateAsset(shapes.terrainMaterial, terrainMaterialPath);
            }
            string materialPath = "Assets/Shared/Graphics/Art/PrototypeUnlit.mat";
            shapes.unlitMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!shapes.unlitMaterial)
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (!shader) shader = Shader.Find("Sprites/Default");
                if (!shader) throw new InvalidOperationException("No compatible unlit sprite shader is available.");
                shapes.unlitMaterial = new Material(shader);
                AssetDatabase.CreateAsset(shapes.unlitMaterial, materialPath);
            }
            EditorUtility.SetDirty(shapes);

            Scene scene = default;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).path == ScenePath) scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid())
            {
                scene = File.Exists(ScenePath)
                    ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive)
                    : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            }
            SceneManager.SetActiveScene(scene);
            TDBootstrap bootstrap = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var owned = root.GetComponent<TDBootstrap>();
                if (owned && !bootstrap) bootstrap = owned;
                else if (owned) UnityEngine.Object.DestroyImmediate(root);
            }
            if (!bootstrap)
            {
                var root = new GameObject("TD Prototype");
                SceneManager.MoveGameObjectToScene(root, scene);
                bootstrap = root.AddComponent<TDBootstrap>();
            }
            bootstrap.settings = settings;
            bootstrap.shapes = shapes;
            bootstrap.existingInputActions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            var cameras = bootstrap.GetComponentsInChildren<Camera>(true);
            var camera = cameras.Length > 0 ? cameras[0] : new GameObject("Prototype Camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(bootstrap.transform, false);
            for (int i = 1; i < cameras.Length; i++) UnityEngine.Object.DestroyImmediate(cameras[i].gameObject);
            camera.orthographic = true;
            camera.orthographicSize = settings.cameraZoom;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.065f, 0.08f);
            camera.transform.position = new Vector3(0, 0, -10);
            // Isolate rendering from any unrelated additive scene cameras and sprites.
            camera.cullingMask = 1 << 30;
            bootstrap.gameObject.layer = 30;
            bootstrap.prototypeCamera = camera;
            if (bootstrap.stage >= 5 && !camera.GetComponent<TDCameraFollow>()) camera.gameObject.AddComponent<TDCameraFollow>();
            if (bootstrap.stage >= 2)
            {
                bootstrap.InitializeMap();
                if (bootstrap.stage >= 3) bootstrap.InitializeRoute();
                camera.transform.position = new Vector3(bootstrap.Map.WorldBounds.center.x, bootstrap.Map.WorldBounds.center.y, -10);
            }
            EditorUtility.SetDirty(bootstrap);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = bootstrap.gameObject;
            Debug.Log("TD: prototype setup complete (owned scene only).");
        }

        [MenuItem("Tools/Shared/Graphics/Assign Gameplay Sprites")]
        public static void AssignGameplaySprites()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before assigning gameplay sprites.");
            var shapes = AssetDatabase.LoadAssetAtPath<SharedShapes>(ShapesPath);
            if (!shapes) throw new InvalidOperationException("Shared graphics asset is missing. Run Tools/TD/Setup Prototype.");
            AssignGameplaySprites(shapes);
            AssetDatabase.SaveAssets();
        }

        private static void AssignGameplaySprites(SharedShapes shapes)
        {
            shapes.player = GameplaySprite("mc");
            shapes.enemy = GameplaySprite("enemy1");
            shapes.tower = GameplaySprite("castle");
            EditorUtility.SetDirty(shapes);
        }

        private static Sprite GameplaySprite(string name)
        {
            var sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Shared/Graphics/Art/" + name + ".png").OfType<Sprite>().FirstOrDefault();
            if (!sprite) throw new InvalidOperationException("Missing gameplay sprite: " + name);
            return sprite;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static Texture2D CreateHexFillTexture()
        {
            string texturePath = "Assets/Shared/Graphics/Art/HexWhite.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (existing) return existing;
            var texture = new Texture2D(64, 56, TextureFormat.RGBA32, false);
            var pixels = new Color32[64 * 56];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.textureType = TextureImporterType.Default;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        }

        private static Sprite CreateSprite(string name, int width, int height, Func<float, float, bool> inside)
        {
            string path = "Assets/Shared/Graphics/Art/" + name + ".png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                var pixels = new Color32[width * height];
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                        pixels[y * width + x] = inside((x + 0.5f - width / 2f) / width, (y + 0.5f - height / 2f) / width)
                            ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = width;
            importer.spriteImportMode = SpriteImportMode.Single;
            var importSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(importSettings);
            importSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(importSettings);
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
