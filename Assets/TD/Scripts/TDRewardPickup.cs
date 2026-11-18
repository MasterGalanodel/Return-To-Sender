using Shared.HUD;
using UnityEngine;

namespace TD
{
    // A world-space token that only the player's claw can reserve and collect.
    public sealed class TDRewardPickup : MonoBehaviour
    {
        private TDWorld world;
        private SpriteRenderer icon;
        private SpriteRenderer halo;
        private bool claimed, collected;
        private Vector3 iconOrigin;
        public bool IsAvailable => !claimed && !collected && isActiveAndEnabled;
        public bool IsClaimed => claimed;
        public SpriteRenderer Icon => icon;
        public Vector3 Position => transform.position;

        public void Initialize(TDWorld context)
        {
            world = context;
            halo = world.Shapes.Create("Reward Glow", world.Shapes.circle,
                new Color(1, .77f, .22f, .3f), Position, .46f, 32, transform);
            icon = world.Shapes.CreateActor("Build Token", world.Shapes.buildReward,
                Position, .3f, 33, transform);
            iconOrigin = icon.transform.localPosition;
        }

        private void Update()
        {
            if (claimed || !world.CanSimulate) return;
            icon.transform.localPosition = iconOrigin + Vector3.up * (.035f * Mathf.Sin(Time.time * 4));
        }

        public void SetHighlighted(bool highlighted)
        {
            if (halo) halo.color = highlighted ? new Color(1, .88f, .3f, .7f) : new Color(1, .77f, .22f, .3f);
        }

        public bool TryClaim()
        {
            if (!IsAvailable || !world.CanSimulate) return false;
            claimed = true;
            icon.transform.localPosition = iconOrigin;
            halo.enabled = false;
            return true;
        }

        public void Release()
        {
            if (collected) return;
            claimed = false;
            icon.transform.localPosition = iconOrigin;
            halo.enabled = true;
            SetHighlighted(false);
        }

        public bool CompleteCollection()
        {
            var session = GameSession.Instance;
            if (!claimed || collected || !isActiveAndEnabled || !world.CanSimulate || !session) return false;
            collected = true;
            session.TowerTokens += 1;
            world.RemoveReward(this);
            gameObject.SetActive(false);
            Destroy(gameObject);
            return true;
        }

        private void OnDestroy() { world?.RemoveReward(this); }
    }
}
