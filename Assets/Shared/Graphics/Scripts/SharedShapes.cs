using UnityEngine;

namespace Shared.Graphics
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "TD", sourceAssembly: null, sourceClassName: "TDShapes")]
    public sealed class SharedShapes : ScriptableObject
    {
        public Sprite circle;
        public Sprite triangle;
        public Sprite hex;
        [Header("Gameplay sprites")]
        public Sprite player;
        public Sprite enemy;
        public Sprite tower;
        [Header("Enemy rewards")]
        public Sprite buildReward;
        public Sprite claw;
        [Header("Effects")]
        [Tooltip("Enemy destroy animation, in play order (destroyed_sprite_sheet_0 .. _6).")]
        public Sprite[] destroyedFrames;
        public Material unlitMaterial;
        public Material terrainMaterial;

        // Imported art has different pixel sizes; retain aspect ratios while matching gameplay dimensions.
        public static float ActorScale(Sprite sprite, float height)
        {
            if (!sprite) throw new System.InvalidOperationException("A gameplay sprite is missing. Run Tools/Shared/Graphics/Assign Gameplay Sprites.");
            return height / sprite.bounds.size.y;
        }

        public SpriteRenderer CreateActor(string objectName, Sprite sprite, Vector3 position,
            float height, int sortingOrder, Transform parent)
        {
            return Create(objectName, sprite, Color.white, position, ActorScale(sprite, height), sortingOrder, parent);
        }

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
