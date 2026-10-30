using Shared.Graphics;
using TD.Editor;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cave.Editor
{
    public static class CaveSetup
    {
        private const string SolidPath = "Assets/Cave/Art/CaveSolid.png";

        [MenuItem("Tools/Cave/Setup Cave Scene")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before cave setup.");
            if (!File.Exists(CaveExploration.TDScenePath)) throw new InvalidOperationException("Create the TD prototype scene first.");
            var shapes = AssetDatabase.LoadAssetAtPath<SharedShapes>(TDSetup.ShapesPath);
            if (!shapes || !shapes.circle || !shapes.triangle || !shapes.unlitMaterial)
                throw new InvalidOperationException("Shared shape assets are missing. Run the prototype setup first.");
            Sprite solid = SolidSprite();
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = default;
            for (int i=0;i<SceneManager.sceneCount;i++)
                if (SceneManager.GetSceneAt(i).path == CaveExploration.ScenePath) scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid())
                scene = File.Exists(CaveExploration.ScenePath)
                    ? EditorSceneManager.OpenScene(CaveExploration.ScenePath,OpenSceneMode.Additive)
                    : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            CaveExploration cave = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var candidate = root.GetComponent<CaveExploration>();
                if (candidate && !cave) cave = candidate;
            }
            if (!cave)
            {
                var root = new GameObject("Cave Exploration",typeof(CaveExploration));
                SceneManager.MoveGameObjectToScene(root,scene);
                cave = root.GetComponent<CaveExploration>();
            }
            var environment = Child(cave.transform,"Cave Environment");
            Rect(environment,"Floor",solid,shapes,new Vector2(12,-4.4f),new Vector2(26,2.6f),Color.black,0);
            Rect(environment,"Ceiling",solid,shapes,new Vector2(12,4.5f),new Vector2(26,2.4f),Color.black,0);
            for (int i=0;i<8;i++)
            {
                var tooth = Sprite(environment,"Ceiling Rock "+i,shapes.triangle,shapes,Color.black,0);
                tooth.transform.localPosition = new Vector3(.6f+i*3.4f,3.55f+(i%3)*.15f,0);
                tooth.transform.localScale = new Vector3(3.8f,1.6f+(i%2)*.5f,1);
                tooth.transform.localRotation = Quaternion.Euler(0,0,180);
            }
            Rect(environment,"Exit Opening",solid,shapes,new Vector2(22.95f,0),new Vector2(.7f,6.4f),new Color(.16f,.3f,.47f),-2);
            var arrow = Sprite(environment,"Exit Arrow",shapes.triangle,shapes,new Color(.48f,.72f,.94f),2);
            arrow.transform.localPosition = new Vector3(22.5f,-1.9f,0);
            arrow.transform.localScale = Vector3.one*.4f;
            arrow.transform.localRotation = Quaternion.Euler(0,0,-90);

            var player = Sprite(cave.transform,"Cave Player",shapes.circle,shapes,Color.white,30);
            player.transform.localPosition = new Vector3(1.3f,CaveExploration.PlayerY,0);
            player.transform.localScale = Vector3.one*.42f;
            var ring = Sprite(player.transform,"Black Outline",shapes.circle,shapes,new Color(.08f,.08f,.08f),29);
            ring.transform.localPosition = Vector3.zero;
            ring.transform.localScale = Vector3.one*(.48f/.42f);
            cave.player = GetOrAdd<CaveWalker>(player.gameObject);
            var cameraObject = Child(cave.transform,"Cave Camera");
            var camera = GetOrAdd<Camera>(cameraObject.gameObject);
            camera.orthographic = true;
            camera.orthographicSize = 5;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(16,34,59,255);
            camera.cullingMask = 1 << 30;
            camera.transform.localPosition = new Vector3(5f*16/9,0,-10);
            cave.caveCamera = camera;
            cave.existingInputActions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            EditorUtility.SetDirty(cave);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene,CaveExploration.ScenePath);

            // Cave is the entry point; retain every unrelated build scene and its enabled state.
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(CaveExploration.ScenePath,true),
                new EditorBuildSettingsScene(CaveExploration.TDScenePath,true)
            };
            foreach (var entry in EditorBuildSettings.scenes)
                if (entry.path != CaveExploration.ScenePath && entry.path != CaveExploration.TDScenePath) scenes.Add(entry);
            EditorBuildSettings.scenes = scenes.ToArray();
            if (previous.IsValid() && previous != scene && !previous.isDirty) EditorSceneManager.CloseScene(previous,true);
            Selection.activeGameObject = cave.gameObject;
            Debug.Log("Cave ready: A/D or arrows, walk to the right-hand exit to enter TD. Shared HUD values persist.");
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        { var component=go.GetComponent<T>();return component ? component : go.AddComponent<T>(); }
        private static Transform Child(Transform parent,string name)
        {
            var existing=parent.Find(name);
            if (existing) return existing;
            var child=new GameObject(name);
            child.layer=30;
            child.transform.SetParent(parent,false);
            return child.transform;
        }
        private static SpriteRenderer Sprite(Transform parent,string name,Sprite sprite,SharedShapes shapes,Color color,int sorting)
        {
            var child=Child(parent,name);
            var renderer=GetOrAdd<SpriteRenderer>(child.gameObject);
            renderer.sprite=sprite;renderer.sharedMaterial=shapes.unlitMaterial;renderer.color=color;renderer.sortingOrder=sorting;
            return renderer;
        }
        private static void Rect(Transform parent,string name,Sprite sprite,SharedShapes shapes,Vector2 position,Vector2 size,Color color,int sorting)
        {
            var renderer=Sprite(parent,name,sprite,shapes,color,sorting);
            renderer.transform.localPosition=position;
            renderer.transform.localScale=new Vector3(size.x,size.y,1);
        }
        private static Sprite SolidSprite()
        {
            if (!File.Exists(SolidPath))
            {
                var texture=new Texture2D(16,16,TextureFormat.RGBA32,false);
                var pixels=new Color32[256];
                for(int i=0;i<pixels.Length;i++)pixels[i]=new Color32(255,255,255,255);
                texture.SetPixels32(pixels);texture.Apply();
                File.WriteAllBytes(SolidPath,texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(SolidPath);
                var importer=(TextureImporter)AssetImporter.GetAtPath(SolidPath);
                importer.textureType=TextureImporterType.Sprite;
                importer.spritePixelsPerUnit=16;
                importer.mipmapEnabled=false;
                importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(SolidPath);
        }
    }
}
