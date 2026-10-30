using UnityEngine;

namespace Shared.Graphics
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "TD", sourceAssembly: null, sourceClassName: "TDShapes")]
    public sealed class SharedShapes : ScriptableObject
    {
        public Sprite circle;
        public Sprite triangle;
        public Sprite hex;
        public Material unlitMaterial;
        public Material terrainMaterial;

        public SpriteRenderer Create(string objectName, Sprite sprite, Color color,
            Vector3 position, float size, int sortingOrder, Transform parent)
        {
            var go = new GameObject(objectName);
            go.layer = 30;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * size;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = unlitMaterial;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }
    }
}
