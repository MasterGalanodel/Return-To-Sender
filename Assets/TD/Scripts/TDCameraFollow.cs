using UnityEngine;

namespace TD
{
    [RequireComponent(typeof(Camera))]
    public sealed class TDCameraFollow : MonoBehaviour
    {
        private Camera view;
        private Transform player;
        private TDMap map;
        private float fixedZ;

        public void Initialize(Transform target, TDMap hexMap, TDSettings settings)
        {
            view = GetComponent<Camera>();
            player = target;
            map = hexMap;
            fixedZ = -10;
            view.orthographicSize = settings.cameraZoom;
            ApplyViewport();
            FollowNow();
        }

        private void LateUpdate()
        {
            if (!player || !map) return;
            ApplyViewport();
            FollowNow();
        }

        private void ApplyViewport()
        {
            const float desiredAspect = 16f / 9;
            float screenAspect = (float)Mathf.Max(1, Screen.width) / Mathf.Max(1, Screen.height);
            if (screenAspect > desiredAspect)
            {
                float width = desiredAspect / screenAspect;
                view.rect = new Rect((1-width)/2, 0, width, 1);
            }
            else
            {
                float height = screenAspect / desiredAspect;
                view.rect = new Rect(0, (1-height)/2, 1, height);
            }
            view.aspect = desiredAspect;
        }

        public void FollowNow()
        {
            Vector2 center = ClampCenter(player.position, map.WorldBounds, view.orthographicSize, view.aspect);
            transform.position = new Vector3(center.x, center.y, fixedZ);
        }

        public static Vector2 ClampCenter(Vector2 desired, Bounds bounds, float halfHeight, float aspect)
        {
            float halfWidth = halfHeight * aspect;
            float x = bounds.size.x <= halfWidth*2 ? bounds.center.x : Mathf.Clamp(desired.x, bounds.min.x+halfWidth, bounds.max.x-halfWidth);
            float y = bounds.size.y <= halfHeight*2 ? bounds.center.y : Mathf.Clamp(desired.y, bounds.min.y+halfHeight, bounds.max.y-halfHeight);
            return new Vector2(x, y);
        }
    }
}
