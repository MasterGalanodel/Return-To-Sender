using UnityEngine;

namespace TD
{
    // Plays the destroyed_sprite_sheet frames once where an enemy died, independent of the destroyed enemy object.
    public sealed class TDEnemyExplosion : MonoBehaviour
    {
        private const float FramesPerSecond = 14;
        // World height of a whole frame cell; the burst itself fills roughly the middle half of it.
        private const float FrameHeight = 1.4f;
        private SpriteRenderer frame;
        private Sprite[] frames;
        private TDWorld world;
        private float elapsed;

        public static TDEnemyExplosion Spawn(TDWorld context, Vector3 position)
        {
            Sprite[] frames = context.Shapes.destroyedFrames;
            if (frames == null || frames.Length == 0)
                throw new System.InvalidOperationException("Destroy animation frames are missing. Run Tools/Shared/Graphics/Assign Gameplay Sprites.");
            var renderer = context.Shapes.CreateActor("Enemy Explosion", frames[0], position, FrameHeight, 40, context.ActorsRoot);
            var burst = renderer.gameObject.AddComponent<TDEnemyExplosion>();
            burst.world = context;
            burst.frame = renderer;
            burst.frames = frames;
            return burst;
        }

        private void Update()
        {
            if (!world.CanSimulate) { Destroy(gameObject); return; }
            Step(Time.deltaTime);
        }

        public void Step(float deltaTime)
        {
            elapsed += Mathf.Max(0, deltaTime);
            int index = Mathf.FloorToInt(elapsed * FramesPerSecond);
            if (index >= frames.Length) { Destroy(gameObject); return; }
            frame.sprite = frames[index];
        }
    }
}
