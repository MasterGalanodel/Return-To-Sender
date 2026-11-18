using UnityEngine;

namespace TD
{
    // Short splash effect: a handful of small circles thrown out from the impact point to the edge of the
    // splash radius, shrinking and fading as they go. Independent of the projectile that caused it.
    public sealed class TDSplashBurst : MonoBehaviour
    {
        private const float Lifetime = .4f;
        private const int SortingOrder = 41;
        private TDWorld world;
        private Transform[] dots;
        private SpriteRenderer[] renderers;
        private Vector3[] directions;
        private float[] sizes;
        private float radius;
        private float elapsed;

        public static TDSplashBurst Spawn(TDWorld context, Vector3 position, float radius, int count)
        {
            count = Mathf.Clamp(count, 4, 10);
            var root = new GameObject("Splash Burst");
            root.layer = 30;
            root.transform.SetParent(context.ActorsRoot, false);
            root.transform.position = position;
            var burst = root.AddComponent<TDSplashBurst>();
            burst.world = context;
            burst.radius = radius;
            burst.dots = new Transform[count];
            burst.renderers = new SpriteRenderer[count];
            burst.directions = new Vector3[count];
            burst.sizes = new float[count];
            // Evenly spaced with jitter so the ring reads as a burst rather than a perfect star.
            float start = Random.value * 360;
            for (int i = 0; i < count; i++)
            {
                float angle = (start + i * 360f / count + Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
                burst.directions[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * Random.Range(.7f, 1f);
                burst.sizes[i] = Random.Range(.06f, .12f);
                var renderer = context.Shapes.Create("Splash Dot", context.Shapes.circle, TDWorld.SplashColor,
                    position, burst.sizes[i], SortingOrder, root.transform);
                burst.dots[i] = renderer.transform;
                burst.renderers[i] = renderer;
            }
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
            float t = elapsed / Lifetime;
            if (t >= 1) { Destroy(gameObject); return; }
            // Fast start, slow finish.
            float travel = 1 - (1 - t) * (1 - t);
            Color color = Color.Lerp(TDWorld.SplashColor, new Color(1f, .95f, .6f), t);
            color.a = 1 - t * t;
            for (int i = 0; i < dots.Length; i++)
            {
                dots[i].localPosition = directions[i] * (radius * travel);
                dots[i].localScale = Vector3.one * (sizes[i] * (1 - .7f * t));
                renderers[i].color = color;
            }
        }
    }
}
