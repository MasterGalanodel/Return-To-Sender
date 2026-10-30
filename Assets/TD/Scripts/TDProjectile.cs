using System;
using UnityEngine;

namespace TD
{
    // Guides a homing projectile toward an enemy target and applies damage upon continuous swept collision detection.
    [DefaultExecutionOrder(30)]
    public sealed class TDProjectile : MonoBehaviour
    {
        public const float Radius = .065f;
        private TDWorld world;
        private TDEnemy target;
        private float age;
        private int spawnFrame;
        public bool Finished { get; private set; }

        // Sets the simulation context, locked target enemy, and birth frame for collision evaluation.
        public void Initialize(TDWorld context, TDEnemy selected) { world=context; target=selected; spawnFrame=Time.frameCount; }

        // Advances projectile flight each frame based on delta time.
        private void Update() { Step(Time.deltaTime); }

        // Translates the projectile toward its target, checks swept circular collisions against active enemies, and triggers damage on hit.
        public void Step(float deltaTime)
        {
            if (Finished) return;
            if (!target || !target.IsAlive) { Finish(); return; }
            age += Mathf.Max(0, deltaTime);
            if (age >= world.Settings.projectileLifetime) { Finish(); return; }
            Vector3 start=transform.position;
            Vector3 delta=target.transform.position-start;
            Vector3 end=start+delta.normalized*world.Settings.projectileSpeed*Mathf.Max(0,deltaTime);
            TDEnemy hit=null;
            float earliest=float.PositiveInfinity;
            foreach (var enemy in world.Enemies)
            {
                if (!enemy || !enemy.IsAlive) continue;
                Vector2 enemyStart=enemy.MovedFrame==Time.frameCount && spawnFrame!=Time.frameCount ? enemy.PreviousPosition : enemy.transform.position;
                if (SweepCircle((Vector2)start-enemyStart,(Vector2)end-(Vector2)enemy.transform.position,
                    Radius+TDEnemy.Radius,out float fraction) && fraction<earliest)
                { earliest=fraction; hit=enemy; }
            }
            if (hit)
            {
                transform.position=Vector3.Lerp(start,end,earliest);
                // Retire before damage: simultaneous projectiles cannot apply this hit twice.
                Finish();
                world.RecordHit();
                hit.ApplyDamage(world.Settings.projectileDamage);
            }
            else transform.position=end;
        }

        // Performs continuous 2D swept circle intersection testing between moving positions.
        public static bool SweepCircle(Vector2 relativeStart, Vector2 relativeEnd, float radius, out float fraction)
        {
            double sx=relativeStart.x, sy=relativeStart.y;
            double dx=relativeEnd.x-sx, dy=relativeEnd.y-sy;
            double c=sx*sx+sy*sy-(double)radius*radius;
            fraction=0;
            if(c<=0) return true;
            double a=dx*dx+dy*dy;
            if(a<1e-16) return false;
            double b=sx*dx+sy*dy;
            double discriminant=b*b-a*c;
            if(discriminant<0) return false;
            double t=(-b-Math.Sqrt(discriminant))/a;
            if(t<0 || t>1) return false;
            fraction=(float)t;
            return true;
        }

        // Unregisters the projectile from active simulation and destroys its GameObject.
        private void Finish()
        {
            if(Finished) return;
            Finished=true;
            world.RemoveProjectile(this);
            Destroy(gameObject);
        }

        // Ensures the projectile is cleanly removed from the simulation context if destroyed externally.
        private void OnDestroy() { if(!Finished && world!=null) world.RemoveProjectile(this); Finished=true; }
    }
}
