using Shared.HUD;
using UnityEngine;

namespace TD
{
    // Tints the whole TD view red for a moment whenever the shared HP drops during the TD phase.
    // Lives on the TD camera as a camera-filling quad; it runs on unscaled time so the flash still
    // finishes when the killing blow pauses the game.
    [RequireComponent(typeof(Camera))]
    public sealed class TDDamageFlash : MonoBehaviour
    {
        public const float Duration = .2f;
        [Range(0, 1)] public float peakAlpha = .45f;
        public Color color = new Color(1, 0, 0);
        private Camera view;
        private SpriteRenderer overlay;
        private GameSession session;
        private float remaining;

        // Builds the overlay quad with the given sprite material; safe to call again.
        public void Initialize(Material material)
        {
            view = GetComponent<Camera>();
            if (!overlay)
            {
                var go = new GameObject("Damage Flash");
                // Layer 30 is the only layer the TD camera renders.
                go.layer = 30;
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0, 0, Mathf.Max(view.nearClipPlane + .01f, 1));
                overlay = go.AddComponent<SpriteRenderer>();
                var texture = Texture2D.whiteTexture;
                overlay.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), texture.width);
                overlay.sortingOrder = 1000;
            }
            if (material) overlay.sharedMaterial = material;
            overlay.enabled = false;
            Subscribe();
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            if (session) session.Damaged -= OnDamaged;
            session = null;
            remaining = 0;
            if (overlay) overlay.enabled = false;
        }

        private void Subscribe()
        {
            if (session == GameSession.Instance) return;
            if (session) session.Damaged -= OnDamaged;
            session = GameSession.Instance;
            if (session) session.Damaged += OnDamaged;
        }

        private void OnDamaged(int amount)
        {
            if (!overlay || !session.IsPhaseActive(GameSession.PlayPhase.TowerDefense) && !session.HasEnded) return;
            remaining = Duration;
        }

        private void LateUpdate()
        {
            Subscribe();
            if (!overlay) return;
            if (remaining <= 0) { overlay.enabled = false; return; }
            // Refit every frame: zoom and aspect may change while the flash is showing.
            float height = view.orthographic ? view.orthographicSize * 2
                : 2 * overlay.transform.localPosition.z * Mathf.Tan(view.fieldOfView * .5f * Mathf.Deg2Rad);
            overlay.transform.localScale = new Vector3(height * view.aspect * 1.05f, height * 1.05f, 1);
            overlay.enabled = true;
            overlay.color = new Color(color.r, color.g, color.b, peakAlpha * remaining / Duration);
            remaining -= Time.unscaledDeltaTime;
        }
    }
}
