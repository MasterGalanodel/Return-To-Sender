using UnityEngine;

namespace TD
{
    [DefaultExecutionOrder(10)]
    public sealed class TDEnemy : MonoBehaviour
    {
        public const float Radius = .2f;
        private TDWorld world;
        private int nextWaypoint;
        private bool retired;
        public float Health { get; private set; }
        public bool IsAlive => !retired && Health > 0;
        public int NextWaypoint => nextWaypoint;
        public Vector3 PreviousPosition { get; private set; }
        public int MovedFrame { get; private set; } = -1;

        public void Initialize(TDWorld context)
        {
            world = context;
            Health = world.Settings.enemyHealth;
            nextWaypoint = 1;
            transform.position = world.Route[0];
        }

        private void Update() { MoveStep(Time.deltaTime); }
        public void MoveStep(float deltaTime)
        {
            if (!IsAlive) return;
            PreviousPosition=transform.position;
            MovedFrame=Time.frameCount;
            float budget = world.Settings.enemySpeed * Mathf.Max(0, deltaTime);
            while (budget > 0 && nextWaypoint < world.Route.Length)
            {
                Vector3 waypoint = world.Route[nextWaypoint];
                Vector3 delta = waypoint - transform.position;
                float distance = delta.magnitude;
                if (distance <= budget || distance < .000001f)
                { transform.position = waypoint; budget -= distance; nextWaypoint++; }
                else { transform.position += delta / distance * budget; budget = 0; }
            }
            if (nextWaypoint >= world.Route.Length) Retire(true);
        }

        public void ApplyDamage(float damage)
        {
            if (!IsAlive || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return;
            Health = Mathf.Max(0, Health-damage);
            if (Health <= 0) Retire(false);
        }

        private void Retire(bool exited)
        {
            if (retired) return;
            retired = true;
            world.RemoveEnemy(this, exited);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (!retired && world != null) world.RemoveEnemy(this, false);
            retired = true;
        }
    }
}
