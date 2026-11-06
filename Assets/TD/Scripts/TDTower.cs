using Shared.Graphics;
using UnityEngine;

namespace TD
{
    // Turret that swings toward the nearest enemy in range and, once on aim, fires one homing
    // projectile from each barrel. Its level (TDSettings.towerLevels) sets the art and barrel count.
    [DefaultExecutionOrder(20)]
    public sealed class TDTower : MonoBehaviour
    {
        // Fire only when the barrel points within this many degrees of the target.
        private const float AimTolerance = 15;
        [SerializeField] private TDSettings settings;
        public TDSettings Settings => settings;
        private TDWorld world;
        private float cooldown;
        public bool drawRangeGizmo = true;
        public TDEnemy LastTarget { get; private set; }
        public int Level { get; private set; }
        private TDSettings.TowerLevel Stats => settings.towerLevels[Level];

        // Assigns the configuration parameters and the starting upgrade level.
        public void Initialize(TDSettings config, int level = 0)
        {
            settings = config;
            SetLevel(level);
        }

        // Swaps in the level's art at its configured size; the current aim is kept.
        public void SetLevel(int level)
        {
            Level = Mathf.Clamp(level, 0, settings.towerLevels.Length - 1);
            var renderer = GetComponent<SpriteRenderer>();
            renderer.sprite = Stats.sprite;
            transform.localScale = Vector3.one * SharedShapes.ActorScale(Stats.sprite, Stats.height);
        }

        // Links the tower to the active world simulation to begin acquiring targets and firing.
        public void EnableCombat(TDWorld context) { world=context; }

        // Steps tower aiming and firing each frame based on delta time.
        private void Update() { Step(Time.deltaTime); }

        // Turns toward the closest enemy in range and fires every barrel when cooled down and on aim.
        public void Step(float deltaTime)
        {
            if(world==null || !world.CanSimulate) return;
            deltaTime=Mathf.Max(0,deltaTime);
            cooldown=Mathf.Max(0,cooldown-deltaTime);
            LastTarget=NearestEnemy();
            if(!LastTarget) return;
            Vector2 toTarget=LastTarget.transform.position-transform.position;
            // The art points up (+Y), so subtract 90° from the direction's angle.
            float wanted=Mathf.Atan2(toTarget.y,toTarget.x)*Mathf.Rad2Deg-90;
            float current=transform.eulerAngles.z;
            float turned=Mathf.MoveTowardsAngle(current,wanted,settings.towerTurnSpeed*deltaTime);
            transform.rotation=Quaternion.Euler(0,0,turned);
            if(cooldown>0 || Mathf.Abs(Mathf.DeltaAngle(turned,wanted))>AimTolerance) return;
            foreach(Vector2 muzzle in Stats.muzzles) world.FireProjectile(MuzzleWorld(muzzle),LastTarget);
            cooldown=1/settings.towerFireRate;
        }

        // Converts a barrel tip given in 0–1 sprite coordinates into a world position, following the turret's rotation.
        public Vector3 MuzzleWorld(Vector2 uv)
        {
            Bounds bounds=Stats.sprite.bounds;
            Vector3 local=bounds.min+Vector3.Scale(bounds.size,new Vector3(uv.x,uv.y,0));
            Vector3 point=transform.TransformPoint(local);
            point.z=transform.position.z;
            return point;
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
