using UnityEngine;

namespace TD
{
    // Defensive structure that locates the nearest enemy in range and fires homing projectiles.
    [DefaultExecutionOrder(20)]
    public sealed class TDTower : MonoBehaviour
    {
        [SerializeField] private TDSettings settings;
        public TDSettings Settings => settings;
        private TDWorld world;
        private float cooldown;
        public bool drawRangeGizmo = true;
        public TDEnemy LastTarget { get; private set; }

        // Assigns the configuration parameters governing range, fire rate, and projectile stats.
        public void Initialize(TDSettings config) { settings = config; }

        // Links the tower to the active world simulation to begin acquiring targets and firing.
        public void EnableCombat(TDWorld context) { world=context; }

        // Steps tower firing logic each frame based on delta time.
        private void Update() { Step(Time.deltaTime); }

        // Updates firing cooldown, searches for the closest enemy within range, and launches a projectile when ready.
        public void Step(float deltaTime)
        {
            if(world==null || !world.CanSimulate) return;
            cooldown=Mathf.Max(0,cooldown-Mathf.Max(0,deltaTime));
            if(cooldown>0) return;
            LastTarget=NearestEnemy();
            if(!LastTarget) return;
            world.FireProjectile(transform.position,LastTarget);
            cooldown=1/settings.towerFireRate;
        }

        // Finds the closest alive enemy within the tower's targeting radius.
        public TDEnemy NearestEnemy()
        {
            TDEnemy nearest=null;
            float best=settings.towerRange*settings.towerRange;
            foreach(var enemy in world.Enemies)
            {
                if(!enemy || !enemy.IsAlive) continue;
                float distance=(enemy.transform.position-transform.position).sqrMagnitude;
                if(distance<=best && (nearest==null || distance<best)) {nearest=enemy;best=distance;}
            }
            return nearest;
        }

        // Draws a yellow wireframe circle in the Scene view to visualize the tower's targeting range.
        private void OnDrawGizmosSelected()
        {
            if(!drawRangeGizmo || !settings) return;
            Gizmos.color=Color.yellow;
            Gizmos.DrawWireSphere(transform.position,settings.towerRange);
        }
    }
}
