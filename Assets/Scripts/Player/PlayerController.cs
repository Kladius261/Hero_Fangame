using System.Collections.Generic;
using UnityEngine;

namespace HeroFangame.Player
{
    /// <summary>
    /// Free 2D movement (no gravity) and persistent facing direction. Also
    /// exposes a thin "flight mode" API (movement speed multiplier) and a
    /// velocity override hook driven entirely by FlightAbility — this class
    /// stays a dumb movement executor and owns none of the flight/charge
    /// state itself.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float flightMoveSpeedMultiplier = 2f;
        [SerializeField] private float grabStanceMoveSpeedMultiplier = 0.75f;

        private Rigidbody2D rb;
        private PlayerInputHandler input;

        // Multiple abilities (Heat Vision, Freeze Breath, ...) can each want
        // to lock movement at once. Tracking requesters individually (rather
        // than a single shared bool) means one ability releasing its lock in
        // a given frame can never stomp a different ability's still-active
        // lock, regardless of Update() execution order between them.
        private readonly HashSet<object> movementLockers = new HashSet<object>();

        public Vector2 Facing { get; private set; } = Vector2.right;
        public bool IsMovementLocked => movementLockers.Count > 0;
        public bool IsFlightMode { get; private set; }
        public bool IsGrabStance { get; private set; }

        public void LockMovement(object requester)
        {
            movementLockers.Add(requester);
        }

        public void UnlockMovement(object requester)
        {
            movementLockers.Remove(requester);
        }

        public void SetFlightMode(bool active)
        {
            IsFlightMode = active;
        }

        /// <summary>
        /// Toggled by PlayerGrabAbility while an object is mounted on the
        /// player. Deliberately NOT implemented via LockMovement — grab
        /// stance must still allow movement (just slower), unlike Heat
        /// Vision/Freeze Breath/beam-lock which fully freeze movement.
        /// </summary>
        public void SetGrabStance(bool active)
        {
            IsGrabStance = active;
        }

        private Vector2? velocityOverride;

        /// <summary>
        /// Drives movement directly (e.g. FlightAbility's Charge), bypassing
        /// normal input-driven movement entirely. Pass null to release the
        /// override and return to normal movement resolution.
        /// </summary>
        public void SetVelocityOverride(Vector2? velocity)
        {
            velocityOverride = velocity;
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            input = GetComponent<PlayerInputHandler>();
        }

        private void Update()
        {
            Vector2 move = input.MoveInput;
            if (!IsMovementLocked && move.sqrMagnitude > 0.0001f)
            {
                Facing = move.normalized;
            }
        }

        private void FixedUpdate()
        {
            if (velocityOverride.HasValue)
            {
                rb.linearVelocity = velocityOverride.Value;
                return;
            }

            if (IsMovementLocked)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }

            float speed = moveSpeed * (IsFlightMode ? flightMoveSpeedMultiplier : 1f) * (IsGrabStance ? grabStanceMoveSpeedMultiplier : 1f);
            rb.linearVelocity = input.MoveInput.normalized * speed;
        }
    }
}
