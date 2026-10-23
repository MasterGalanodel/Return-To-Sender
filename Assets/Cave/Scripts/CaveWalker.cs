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
        private float lastGroundedAt = -1;
        private float restoreCollisionsAt;
        private bool dropBuffered, jumpReleased;
        private int lastSafeStep;
        public bool IsGrounded { get; private set; }
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
            actor = GetComponent<SpriteRenderer>();
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
            if (!CanMove) return;
            Vector2 input = move.ReadValue<Vector2>();
            horizontalInput = input.x;
            if (jump.WasPressedThisFrame()) QueueJump(input.y < -.5f);
            if (jump.WasReleasedThisFrame()) jumpReleased = true;
            if (actor && Mathf.Abs(horizontalInput) > .05f) actor.flipX = horizontalInput < 0;
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
                lastGroundedAt = Time.time;
                lastSafeStep = ground.StepIndex;
            }
            Vector2 velocity = body.linearVelocity;
            if (jumpBufferedUntil >= Time.time)
            {
                if (dropBuffered && ground)
                {
                    DropThrough(ground.GetComponent<Collider2D>());
                    velocity.y = -2;
                    jumpBufferedUntil = -1;
                    lastGroundedAt = -1;
                    IsGrounded = false;
                }
                else if (!dropBuffered && Time.time - lastGroundedAt <= .1f && ignoredPlatforms.Count == 0)
                {
                    velocity.y = cave.jumpSpeed;
                    jumpBufferedUntil = -1;
                    lastGroundedAt = -1;
                    IsGrounded = false;
                }
            }
            if (jumpReleased && velocity.y > 0) velocity.y *= .5f;
            jumpReleased = false;
            velocity.x = Mathf.MoveTowards(velocity.x, Mathf.Clamp(horizontalInput, -1, 1) * cave.walkSpeed, 42 * Time.fixedDeltaTime);
            velocity.y = Mathf.Max(-12, velocity.y);
            body.linearVelocity = velocity;
            if (body.position.y < CaveExploration.BottomY - 4) cave.RecoverPlayer(lastSafeStep);
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
                if (!force && (Time.time < restoreCollisionsAt || bodyCollider.bounds.max.y > platform.bounds.min.y)) continue;
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
            lastGroundedAt = -1;
            IsGrounded = false;
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
