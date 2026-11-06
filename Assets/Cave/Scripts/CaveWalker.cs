using System.Collections.Generic;
using Shared.HUD;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cave
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "TD", sourceAssembly: null, sourceClassName: "TDCaveWalker")]
    public sealed class CaveWalker : MonoBehaviour
    {
        [Header("Animation (row 1 of character_sprite_sheet.png)")]
        public SpriteRenderer visual;
        public Sprite[] idleFrames;
        public Sprite[] moveFrames;
        [Min(.1f)] public float idleFps = 6;
        [Min(.1f)] public float moveFps = 12;
        [Header("Jet fire (fire.png, drawn behind the player at the backpack)")]
        public SpriteRenderer jetFire;
        public Vector2 backpackOffset = new Vector2(-.24f, -.02f);
        [Min(.05f)] public float fireHeight = .5f;
        private CaveExploration cave;
        private InputAction move, jump;
        private Rigidbody2D body;
        private BoxCollider2D bodyCollider;
        private SpriteRenderer actor;
        private ContactFilter2D groundFilter;
        private readonly RaycastHit2D[] groundHits = new RaycastHit2D[12];
        private readonly List<Collider2D> ignoredPlatforms = new List<Collider2D>();
        private float horizontalInput;
        private float jumpBufferedUntil = -1;
        private float restoreCollisionsAt;
        private float steeringLockedUntil = -1;
        private bool dropBuffered, jumpReleased, boostUp, boostDown, facingLeft;
        private float visualScale = 1;
        private int lastSafeStep;
        public bool IsGrounded { get; private set; }
        public bool IsBoosting => boostUp || boostDown;
        public int LastSafeStep => lastSafeStep;
        public Vector2 Velocity => body ? body.linearVelocity : Vector2.zero;
        public Rigidbody2D Body => body;
        private bool CanMove => cave && cave.Initialized && !cave.Transitioning && GameSession.Instance
            && GameSession.Instance.IsPhaseActive(GameSession.PlayPhase.Cave);

        public void Initialize(CaveExploration owner, InputActionAsset existingInput)
        {
            var originalMove = existingInput.FindAction("Player/Move");
            var originalJump = existingInput.FindAction("Player/Jump");
            if (originalMove == null || originalJump == null)
                throw new System.InvalidOperationException("Cave needs the existing Player/Move and Player/Jump actions.");
            cave = owner;
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<BoxCollider2D>();
            actor = visual ? visual : GetComponentInChildren<SpriteRenderer>();
            if (idleFrames != null && idleFrames.Length > 0 && idleFrames[0])
                visualScale = CaveExploration.PlayerHeight / idleFrames[0].bounds.size.y;
            if (jetFire) jetFire.enabled = false;
            move = originalMove.Clone();
            jump = originalJump.Clone();
            move.Enable();
            jump.Enable();
            groundFilter = new ContactFilter2D();
            groundFilter.SetLayerMask(1 << 30);
            groundFilter.useTriggers = false;
            body.linearVelocity = Vector2.zero;
            body.simulated = true;
        }

        private void Update()
        {
            if (!CanMove) { boostUp = boostDown = false; Animate(); return; }
            Vector2 input = move.ReadValue<Vector2>();
            horizontalInput = input.x;
            if (jump.WasPressedThisFrame()) QueueJump(input.y < -.5f);
            if (jump.WasReleasedThisFrame()) jumpReleased = true;
            bool held = jump.IsPressed();
            boostDown = held && input.y < -.5f;
            boostUp = held && !boostDown;
            if (Mathf.Abs(horizontalInput) > .05f) facingLeft = horizontalInput < 0;
            Animate();
        }

        // Idle frames when still, run frames when moving sideways; feet stay on the collider's base.
        private void Animate()
        {
            if (!actor) return;
            bool moving = CanMove && (Mathf.Abs(horizontalInput) > .05f || Mathf.Abs(Velocity.x) > .4f);
            var frames = moving && moveFrames != null && moveFrames.Length > 0 ? moveFrames : idleFrames;
            if (frames != null && frames.Length > 0)
            {
                var frame = frames[(int)(Time.time * (moving ? moveFps : idleFps)) % frames.Length];
                if (frame)
                {
                    actor.sprite = frame;
                    actor.transform.localScale = Vector3.one * visualScale;
                    actor.transform.localPosition = new Vector3(0,
                        -CaveExploration.PlayerHalfHeight - frame.bounds.min.y * visualScale, 0);
                }
            }
            actor.flipX = facingLeft;
            if (!jetFire) return;
            jetFire.enabled = IsBoosting;
            if (!IsBoosting) return;
            // Space: flame points down (pushes up). S + Space: flame points up (pushes down).
            float flicker = 1 + .12f * Mathf.Sin(Time.time * 47) + .06f * Mathf.Sin(Time.time * 83);
            float size = fireHeight / jetFire.sprite.bounds.size.y;
            jetFire.flipY = boostUp;
            jetFire.transform.localScale = new Vector3(size * (2 - flicker), size * flicker, 1);
            float side = facingLeft ? -1 : 1;
            jetFire.transform.localPosition = new Vector3(backpackOffset.x * side,
                backpackOffset.y + (boostUp ? -fireHeight * .45f : fireHeight * .35f), 0);
        }

        public void QueueJump(bool dropThrough = false)
        {
            if (!CanMove) return;
            jumpBufferedUntil = Time.time + .14f;
            dropBuffered = dropThrough;
        }

        private void FixedUpdate()
        {
            if (!body) return;
            RestoreIgnoredPlatforms(false);
            if (!CanMove) { body.linearVelocity = Vector2.zero; return; }
            CavePlatform ground = FindGround();
            IsGrounded = ground != null;
            if (IsGrounded)
            {
                lastSafeStep = ground.StepIndex;
            }
            Vector2 velocity = body.linearVelocity;
            if (jumpBufferedUntil >= Time.time)
            {
                if (dropBuffered && ground && ground.GetComponent<PlatformEffector2D>())
                {
                    DropThrough(ground.GetComponent<Collider2D>());
                    velocity.y = -2;
                    jumpBufferedUntil = -1;
                    IsGrounded = false;
                }
                else if (dropBuffered && ground) jumpBufferedUntil = -1; // solid cave floor: nothing to drop through
                else if (!dropBuffered)
                {
                    // Each press refreshes lift, including while falling or already airborne.
                    velocity.y = cave.jumpSpeed;
                    jumpBufferedUntil = -1;
                    IsGrounded = false;
                }
            }
            if (jumpReleased && velocity.y > 0) velocity.y *= .5f;
            jumpReleased = false;
            var settings = cave.settings;
            float maxFall = 12;
            if (boostUp && settings)
                velocity.y = Mathf.Min(velocity.y + settings.boostUpAcceleration * Time.fixedDeltaTime,
                    Mathf.Max(velocity.y, settings.maxBoostRiseSpeed));
            else if (boostDown && settings && !IsGrounded)
            {
                velocity.y -= settings.boostDownAcceleration * Time.fixedDeltaTime;
                maxFall = settings.maxBoostFallSpeed;
            }
            if (Time.time >= steeringLockedUntil)
                velocity.x = Mathf.MoveTowards(velocity.x, Mathf.Clamp(horizontalInput, -1, 1) * cave.walkSpeed, 42 * Time.fixedDeltaTime);
            velocity.y = Mathf.Max(-maxFall, velocity.y);
            body.linearVelocity = velocity;
            if (body.position.y < cave.BottomY - 4) cave.RecoverPlayer(lastSafeStep);
        }

        private CavePlatform FindGround()
        {
            if (body.linearVelocity.y > .1f || ignoredPlatforms.Count > 0) return null;
            int count = body.Cast(Vector2.down, groundFilter, groundHits, .09f);
            for (int i = 0; i < count; i++)
                if (groundHits[i].normal.y > .5f && groundHits[i].collider.TryGetComponent<CavePlatform>(out var platform))
                    return platform;
            return null;
        }

        private void DropThrough(Collider2D platform)
        {
            Physics2D.IgnoreCollision(bodyCollider, platform, true);
            if (!ignoredPlatforms.Contains(platform)) ignoredPlatforms.Add(platform);
            restoreCollisionsAt = Time.time + .3f;
        }

        private void RestoreIgnoredPlatforms(bool force)
        {
            if (!bodyCollider) { ignoredPlatforms.Clear(); return; }
            for (int i = ignoredPlatforms.Count - 1; i >= 0; i--)
            {
                var platform = ignoredPlatforms[i];
                if (!platform) { ignoredPlatforms.RemoveAt(i); continue; }
                if (!force && (Time.time < restoreCollisionsAt || bodyCollider.bounds.Intersects(platform.bounds))) continue;
                Physics2D.IgnoreCollision(bodyCollider, platform, false);
                ignoredPlatforms.RemoveAt(i);
            }
        }

        public void TeleportTo(Vector2 position)
        {
            RestoreIgnoredPlatforms(true);
            body.position = position;
            body.linearVelocity = Vector2.zero;
            horizontalInput = 0;
            jumpBufferedUntil = -1;
            dropBuffered = jumpReleased = false;
            IsGrounded = false;
            steeringLockedUntil = -1;
        }

        // Places the player and throws them with a velocity that steering cannot cancel for lockSeconds.
        public void Launch(Vector2 position, Vector2 velocity, float lockSeconds, int safeStep)
        {
            TeleportTo(position);
            lastSafeStep = safeStep;
            body.linearVelocity = velocity;
            steeringLockedUntil = Time.time + lockSeconds;
            if (actor) actor.flipX = velocity.x < 0;
        }

        public void StopMoving()
        {
            if (body) { body.linearVelocity = Vector2.zero; body.simulated = false; }
            enabled = false;
        }

        private void OnDestroy()
        {
            RestoreIgnoredPlatforms(true);
            move?.Disable(); move?.Dispose();
            jump?.Disable(); jump?.Dispose();
        }
    }
}
