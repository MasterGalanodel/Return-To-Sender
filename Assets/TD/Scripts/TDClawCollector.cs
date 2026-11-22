using System.Collections.Generic;  // should be reimplemented by the claw prefab later - Miles
using UnityEngine;

namespace TD
{
    // One press reserves every available reward within today's collect radius; each gets its own claw,
    // and each token is credited after its claw brings it back.
    public sealed class TDClawCollector : MonoBehaviour
    {
        private const float DescendSeconds = .28f;
        private const float GripSeconds = .12f;
        private const float ReturnSeconds = .42f;
        private static readonly Vector3 GripOffset = Vector3.up * .34f;

        private sealed class Grab
        {
            public TDRewardPickup target;
            public SpriteRenderer claw;
            public Vector3 grabPosition, startPosition;
            public float elapsed;
        }

        private TDWorld world;
        private readonly List<Grab> grabs = new List<Grab>();
        private readonly List<TDRewardPickup> inRange = new List<TDRewardPickup>();
        public bool IsBusy => grabs.Count > 0;

        public void Initialize(TDWorld context) { world = context; }

        // Collect radius for the current day.
        public float CurrentRadius
        {
            get
            {
                var session = Shared.HUD.GameSession.Instance;
                return world.Settings.BuildRewardCollectRadiusOnDay(session ? session.Day : 1);
            }
        }

        private void Update()
        {
            if (world == null) return;
            if (!IsBusy && world.CanSimulate) FindRewardsInRange(inRange); else inRange.Clear();
            foreach (var reward in world.Rewards)
                if (reward) reward.SetHighlighted(inRange.Contains(reward));
            StepCollection(Time.deltaTime);
        }

        private void FindRewardsInRange(List<TDRewardPickup> results)
        {
            results.Clear();
            float radius = CurrentRadius;
            float radiusSquared = radius * radius;
            foreach (var reward in world.Rewards)
            {
                if (!reward || !reward.IsAvailable) continue;
                if (((Vector2)(reward.Position - transform.position)).sqrMagnitude <= radiusSquared) results.Add(reward);
            }
        }

        public bool TryCollect()
        {
            if (world == null || !world.CanSimulate || IsBusy || !Shared.HUD.GameSession.Instance) return false;
            FindRewardsInRange(inRange);
            foreach (var reward in inRange)
            {
                if (!reward || !reward.TryClaim()) continue;
                var grab = new Grab { target = reward, grabPosition = reward.Position + GripOffset };
                grab.startPosition = grab.grabPosition + Vector3.up * 1.2f;
                grab.claw = world.Shapes.CreateActor("Collection Claw", world.Shapes.claw,
                    grab.startPosition, .85f, 60, world.ActorsRoot);
                grabs.Add(grab);
            }
            inRange.Clear();
            return IsBusy;
        }

        public void StepCollection(float deltaTime)
        {
            if (!IsBusy) return;
            if (!world.CanSimulate) { CancelCollection(); return; }
            if (deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            for (int i = grabs.Count - 1; i >= 0; i--)
                if (StepGrab(grabs[i], deltaTime)) grabs.RemoveAt(i);
        }

        // Advances one claw; returns true once it is finished (collected or lost its target).
        private bool StepGrab(Grab grab, float deltaTime)
        {
            if (!grab.target || !grab.target.isActiveAndEnabled) { EndGrab(grab, false); return true; }
            grab.elapsed += deltaTime;
            var claw = grab.claw;
            if (grab.elapsed < DescendSeconds)
                claw.transform.position = Vector3.Lerp(grab.startPosition, grab.grabPosition,
                    Mathf.SmoothStep(0, 1, grab.elapsed / DescendSeconds));
            else if (grab.elapsed < DescendSeconds + GripSeconds)
            {
                claw.transform.position = grab.grabPosition;
                float grip = (grab.elapsed - DescendSeconds) / GripSeconds;
                float scale = Shared.Graphics.SharedShapes.ActorScale(world.Shapes.claw, .85f);
                claw.transform.localScale = new Vector3(scale * Mathf.Lerp(1, .8f, grip), scale, scale);
            }
            else
            {
                float progress = Mathf.Clamp01((grab.elapsed - DescendSeconds - GripSeconds) / ReturnSeconds);
                Vector3 destination = transform.position + GripOffset;
                claw.transform.position = Vector3.Lerp(grab.grabPosition, destination, Mathf.SmoothStep(0, 1, progress))
                    + Vector3.up * (.55f * Mathf.Sin(progress * Mathf.PI));
                grab.target.Icon.transform.position = claw.transform.position - GripOffset;
                if (progress >= 1) { EndGrab(grab, true); return true; }
            }
            return false;
        }

        private static void EndGrab(Grab grab, bool complete)
        {
            if (grab.target && !(complete && grab.target.CompleteCollection())) grab.target.Release();
            grab.target = null;
            if (grab.claw) { grab.claw.gameObject.SetActive(false); Destroy(grab.claw.gameObject); }
            grab.claw = null;
        }

        public void CancelCollection()
        {
            foreach (var grab in grabs) EndGrab(grab, false);
            grabs.Clear();
        }

        private void OnDisable() { CancelCollection(); }
    }
}
