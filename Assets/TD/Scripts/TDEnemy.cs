using UnityEngine;

namespace TD
{
    // Walks in a straight line from its spawn point toward a Base cell, ignoring terrain, tracks health,
    // and handles destruction upon death or upon touching any Base cell.
    [DefaultExecutionOrder(10)]
    public sealed class TDEnemy : MonoBehaviour
    {
        public const float Radius = .2f;
        // Movement is sub-stepped so a large frame step cannot skip over a thin corner of the Base.
        private const float BaseCheckStep = .1f;
        private TDWorld world;
        private bool retired;
        public float Health { get; private set; }
        public bool IsAlive => !retired && Health > 0;
        public Vector3 Target { get; private set; }
        public Vector3 PreviousPosition { get; private set; }
        public int MovedFrame { get; private set; } = -1;

        // Initializes enemy health, spawn position, and the Base point it walks toward.
        public void Initialize(TDWorld context, Vector3 start, Vector3 target)
        {
            world = context;
            Health = world.Settings.enemyHealth;
            transform.position = start;
            Target = target;
        }

        // Steps enemy movement toward the Base each frame using delta time.
        private void Update() { MoveStep(Time.deltaTime); }

        // Moves straight toward the target within the movement budget and retires the enemy on its first Base cell.
        public void MoveStep(float deltaTime)
        {
            if (!IsAlive || !world.CanSimulate) return;
            PreviousPosition=transform.position;
            MovedFrame=Time.frameCount;
            float budget = world.Settings.enemySpeed * Mathf.Max(0, deltaTime);
            while (budget > 0)
            {
                Vector3 delta = Target - transform.position;
                float distance = delta.magnitude;
                float step = Mathf.Min(budget, BaseCheckStep);
                if (distance <= step) { transform.position = Target; budget = 0; }
                else { transform.position += delta / distance * step; budget -= step; }
                if (world.IsBase(transform.position)) { Retire(true); return; }
            }
        }

        // Reduces enemy health by the specified damage amount and destroys the enemy when health reaches zero.
        public void ApplyDamage(float damage)
        {
            if (!IsAlive || !world.CanSimulate || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return;
            Health = Mathf.Max(0, Health-damage);
            if (Health <= 0) Retire(false);
        }

        // Removes the enemy from active world simulation and destroys its GameObject.
        private void Retire(bool exited)
        {
            if (retired) return;
            retired = true;
            world.RemoveEnemy(this, exited, !exited);
            Destroy(gameObject);
        }

        // Ensures the enemy is cleanly unregistered from the world if destroyed unexpectedly.
        private void OnDestroy()
        {
            if (!retired && world != null) world.RemoveEnemy(this, false);
            retired = true;
        }
    }
}
