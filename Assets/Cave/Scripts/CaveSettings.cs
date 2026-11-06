using System;
using UnityEngine;

namespace Cave
{
    // Designer-facing balance for the Cave: one entry per day plus shared layout rules for the random generator.
    [CreateAssetMenu(menuName = "Cave/Cave Settings")]
    public sealed class CaveSettings : ScriptableObject
    {
        [Serializable]
        public sealed class DayLayout
        {
            [Tooltip("Number of platform layers (steps) from platform 01 under the TD door down to the cave floor, inclusive.")]
            [Min(2)] public int platformLayers = 30;
            [Tooltip("Heart pickups scattered on random platforms.")]
            [Min(0)] public int healthPickups = 8;
            [Tooltip("Build-token pickups scattered on random platforms.")]
            [Min(0)] public int buildPickups = 4;
            [Min(1)] public int healthPerPickup = 1;
            [Min(1)] public int buildPerPickup = 1;
        }

        [Header("Randomness")]
        [Tooltip("0 = every new run rolls a fresh set of six caves. Any other value = the same six caves every run (useful for testing). "
            + "Reloading the Cave on the same day always shows the same layout.")]
        public int seed;

        [Header("Per day (element 0 = day 1; days past the end reuse the last entry)")]
        public DayLayout[] days =
        {
            new DayLayout { platformLayers = 15, healthPickups = 6, buildPickups = 2 },
            new DayLayout { platformLayers = 20, healthPickups = 7, buildPickups = 3 },
            new DayLayout { platformLayers = 25, healthPickups = 8, buildPickups = 3 },
            new DayLayout { platformLayers = 30, healthPickups = 8, buildPickups = 4 },
            new DayLayout { platformLayers = 35, healthPickups = 9, buildPickups = 4 },
            new DayLayout { platformLayers = 40, healthPickups = 10, buildPickups = 5 },
        };

        [Header("Layout")]
        [Tooltip("Random vertical gap between consecutive layers (min, max).")]
        public Vector2 layerSpacing = new Vector2(2.1f, 2.7f);
        [Tooltip("Random platform width (min, max).")]
        public Vector2 platformWidth = new Vector2(2.6f, 3.6f);
        [Tooltip("Width of the strip along the left wall kept free of platforms, so the player can drop straight to the floor.")]
        [Min(0)] public float freeFallLaneWidth = 2.2f;
        [Tooltip("Largest horizontal jump between a platform and the one in the layer above.")]
        [Min(0)] public float maxSideStep = 4.2f;
        [Tooltip("Chance that a layer gets a second platform on the opposite side.")]
        [Range(0, 1)] public float secondPlatformChance = .25f;
        [Tooltip("Every Nth layer is a wide, centred rest ledge. 0 = none.")]
        [Min(0)] public int restPlatformEvery = 10;
        [Tooltip("Most pickups allowed on one platform.")]
        [Min(1)] public int maxPickupsPerPlatform = 2;

        [Header("TD exit")]
        [Tooltip("How far above platform 01's surface the TD door's base floats.")]
        [Min(0)] public float exitHeightAbovePlatform01 = 1.9f;

        [Header("Faithful One (stands on the left end of platform 01)")]
        [Tooltip("First day she appears. 2 = from the second day on.")]
        [Min(1)] public int faithfulFromDay = 2;
        [Min(.1f)] public float faithfulHeight = .85f;
        [TextArea] public string faithfulHint = "The depths of Ashina\nare over there";

        [Header("Jet boost (hold Space = up, hold S + Space = down)")]
        [Min(0)] public float boostUpAcceleration = 12;
        [Min(0)] public float maxBoostRiseSpeed = 11;
        [Min(0)] public float boostDownAcceleration = 24;
        [Min(0)] public float maxBoostFallSpeed = 22;

        [Header("Overtime warning (after GameSession.CaveSeconds)")]
        [Min(.05f)] public float overtimeFlashInterval = 1;
        [Min(.01f)] public float overtimeFlashDuration = .3f;
        public Color overtimeFlashColor = new Color(1, .08f, .05f, .9f);

        public DayLayout ForDay(int day)
        {
            if (days == null || days.Length == 0) return new DayLayout();
            return days[Mathf.Clamp(day - 1, 0, days.Length - 1)];
        }

        private void OnValidate()
        {
            layerSpacing.x = Mathf.Max(.8f, layerSpacing.x);
            layerSpacing.y = Mathf.Max(layerSpacing.x, layerSpacing.y);
            freeFallLaneWidth = Mathf.Clamp(freeFallLaneWidth, 0, CaveExploration.ShaftHalfWidth * 2 - 2);
            float room = CaveExploration.ShaftHalfWidth * 2 - freeFallLaneWidth - .2f;
            platformWidth.x = Mathf.Clamp(platformWidth.x, 1, room);
            platformWidth.y = Mathf.Clamp(platformWidth.y, platformWidth.x, room);
        }
    }
}
