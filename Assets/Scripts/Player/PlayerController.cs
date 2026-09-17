using UnityEngine;

namespace HeroFangame.Player
{
    /// <summary>
    /// Free 2D movement (no gravity), persistent facing direction, and a
    /// flight-dash burst that replaces a traditional dash. Movement and
    /// flight never consume Power.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 6f;

        [Header("Flight Dash")]
        [SerializeField] private float flightSpeed = 20f;
        [SerializeField] private float flightDuration = 0.15f;
        [SerializeField] private float flightCooldown = 0.5f;

        private Rigidbody2D rb;
        private PlayerInputHandler input;

        public Vector2 Facing { get; private set; } = Vector2.right;
        public bool IsFlying { get; private set; }

        private float flightTimeRemaining;
        private float flightCooldownRemaining;
        private Vector2 flightDirection;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            input = GetComponent<PlayerInputHandler>();
        }

        private void OnEnable()
        {
            input.OnFlight += HandleFlightInput;
        }

        private void OnDisable()
        {
            input.OnFlight -= HandleFlightInput;
        }

        private void Update()
        {
            Vector2 move = input.MoveInput;
            if (move.sqrMagnitude > 0.0001f)
            {
                Facing = move.normalized;
            }

            if (flightCooldownRemaining > 0f)
            {
                flightCooldownRemaining -= Time.deltaTime;
            }

            if (IsFlying)
            {
                flightTimeRemaining -= Time.deltaTime;
                if (flightTimeRemaining <= 0f)
                {
                    IsFlying = false;
                }
            }
        }

        private void FixedUpdate()
        {
            if (IsFlying)
            {
                rb.linearVelocity = flightDirection * flightSpeed;
            }
            else
            {
                rb.linearVelocity = input.MoveInput.normalized * moveSpeed;
            }
        }

        private void HandleFlightInput()
        {
            if (IsFlying || flightCooldownRemaining > 0f)
            {
                return;
            }

            Vector2 dir = input.MoveInput.sqrMagnitude > 0.0001f ? input.MoveInput.normalized : Facing;
            flightDirection = dir;
            IsFlying = true;
            flightTimeRemaining = flightDuration;
            flightCooldownRemaining = flightCooldown;
        }
    }
}
