using UnityEngine;

namespace TD
{
    [DefaultExecutionOrder(20)]
    public sealed class TDTower : MonoBehaviour
    {
        [SerializeField] private TDSettings settings;
        public TDSettings Settings => settings;
        private TDWorld world;
        private float cooldown;
        public bool drawRangeGizmo = true;
        public TDEnemy LastTarget { get; private set; }
        public void Initialize(TDSettings config) { settings = config; }
        public void EnableCombat(TDWorld context) { world=context; }
        private void Update() { Step(Time.deltaTime); }
        public void Step(float deltaTime)
        {
            if(world==null) return;
            cooldown=Mathf.Max(0,cooldown-Mathf.Max(0,deltaTime));
            if(cooldown>0) return;
            LastTarget=NearestEnemy();
            if(!LastTarget) return;
            world.FireProjectile(transform.position,LastTarget);
            cooldown=1/settings.towerFireRate;
        }
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
        private void OnDrawGizmosSelected()
        {
            if(!drawRangeGizmo || !settings) return;
            Gizmos.color=Color.yellow;
            Gizmos.DrawWireSphere(transform.position,settings.towerRange);
        }
    }
}
