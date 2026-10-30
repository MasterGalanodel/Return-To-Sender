using UnityEngine;
using UnityEngine.InputSystem;

namespace TD
{
    // One press reserves the nearest adjacent reward; its token is credited after retrieval.
    public sealed class TDClawCollector : MonoBehaviour
    {
        private const float DescendSeconds = .28f;
        private const float GripSeconds = .12f;
        private const float ReturnSeconds = .42f;
        private static readonly Vector3 GripOffset = Vector3.up * .34f;
        private TDWorld world;
        private TDRewardPickup target;
        private SpriteRenderer claw;
        private Vector3 grabPosition, startPosition;
        private float elapsed;
        public bool IsBusy => target != null;
        public TDRewardPickup Target => target;

        public void Initialize(TDWorld context) { world = context; }

        private void Update()
        {
            if (world == null) return;
            var nearest = !IsBusy && world.CanSimulate ? FindNearestReward() : null;
            foreach (var reward in world.Rewards)
                if (reward) reward.SetHighlighted(reward == nearest);
            if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame) TryCollect();
            StepCollection(Time.deltaTime);
        }

        private TDRewardPickup FindNearestReward()
        {
            TDRewardPickup nearest = null;
            float bestSquared = world.Settings.buildRewardCollectRadius * world.Settings.buildRewardCollectRadius;
            foreach (var reward in world.Rewards)
            {
                if (!reward || !reward.IsAvailable) continue;
                float distanceSquared = ((Vector2)(reward.Position - transform.position)).sqrMagnitude;
                if (distanceSquared > bestSquared) continue;
                nearest = reward;
                bestSquared = distanceSquared;
            }
            return nearest;
        }

        public bool TryCollect()
        {
            if (world == null || !world.CanSimulate || IsBusy || !Shared.HUD.GameSession.Instance) return false;
            var reward = FindNearestReward();
            if (!reward || !reward.TryClaim()) return false;
            target = reward;
            elapsed = 0;
            grabPosition = reward.Position + GripOffset;
            startPosition = grabPosition + Vector3.up * 1.2f;
            claw = world.Shapes.CreateActor("Collection Claw", world.Shapes.claw,
                startPosition, .85f, 60, world.ActorsRoot);
            return true;
        }

        public void StepCollection(float deltaTime)
        {
            if (!target || !target.isActiveAndEnabled || !world.CanSimulate) { CancelCollection(); return; }
            if (deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            elapsed += deltaTime;
            if (elapsed < DescendSeconds)
                claw.transform.position = Vector3.Lerp(startPosition, grabPosition,
                    Mathf.SmoothStep(0, 1, elapsed / DescendSeconds));
            else if (elapsed < DescendSeconds + GripSeconds)
            {
                claw.transform.position = grabPosition;
                float grip = (elapsed - DescendSeconds) / GripSeconds;
                float scale = Shared.Graphics.SharedShapes.ActorScale(world.Shapes.claw, .85f);
                claw.transform.localScale = new Vector3(scale * Mathf.Lerp(1, .8f, grip), scale, scale);
            }
            else
            {
                float progress = Mathf.Clamp01((elapsed - DescendSeconds - GripSeconds) / ReturnSeconds);
                Vector3 destination = transform.position + GripOffset;
                claw.transform.position = Vector3.Lerp(grabPosition, destination, Mathf.SmoothStep(0, 1, progress))
                    + Vector3.up * (.55f * Mathf.Sin(progress * Mathf.PI));
                target.Icon.transform.position = claw.transform.position - GripOffset;
                if (progress >= 1)
                {
                    if (!target.CompleteCollection()) target.Release();
                    target = null;
                    ClearClaw();
                }
            }
        }

        private void ClearClaw()
        {
            if (claw) { claw.gameObject.SetActive(false); Destroy(claw.gameObject); }
            claw = null;
        }

        public void CancelCollection()
        {
            if (target) target.Release();
            target = null;
            ClearClaw();
        }

        private void OnDisable() { CancelCollection(); }
    }
}
