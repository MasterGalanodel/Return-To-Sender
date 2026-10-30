using Shared.HUD;
using UnityEngine;

namespace Cave
{
    [DisallowMultipleComponent]
    public sealed class CavePickup : MonoBehaviour
    {
        [SerializeField] private string pickupId;
        [SerializeField] private GameSession.PickupType kind;
        [SerializeField, Min(1)] private int amount = 1;
        [SerializeField] private Transform icon;
        private Vector3 iconOrigin;
        public string PickupId => pickupId;
        public GameSession.PickupType Kind => kind;
        public int Amount => amount;

        public void Configure(string id, GameSession.PickupType type, int value, Transform visual)
        { pickupId = id; kind = type; amount = value; icon = visual; }

        private void Start()
        {
            if (icon) iconOrigin = icon.localPosition;
            var session = GameSession.Instance;
            if (session && session.HasCollectedCavePickup(pickupId)) gameObject.SetActive(false);
        }

        private void Update()
        {
            if (icon) icon.localPosition = iconOrigin + Vector3.up * (.06f * Mathf.Sin(Time.time * 3 + transform.position.y));
        }

        private void OnTriggerEnter2D(Collider2D other) { TryCollect(other); }
        private void OnTriggerStay2D(Collider2D other) { TryCollect(other); }

        private void TryCollect(Collider2D other)
        {
            if (!other.GetComponent<CaveWalker>()) return;
            var session = GameSession.Instance;
            if (session && session.TryCollectCavePickup(pickupId, kind, amount)) gameObject.SetActive(false);
        }
    }
}
