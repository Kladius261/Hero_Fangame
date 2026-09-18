using UnityEngine;
using HeroFangame.Camera;
using HeroFangame.Core;

namespace HeroFangame.Enemy
{
    /// <summary>
    /// Basic robot: Chase / Frozen / Dead state machine. Chases the player
    /// within aggroRange, accumulates Freeze Breath exposure (IFreezable),
    /// and shows a progressively intensifying frost visual as exposure
    /// rises even before freezing. Once exposure crosses freezeThreshold it
    /// freezes solid (stops moving, tints frost, pops back a short, fixed
    /// distance away from the player, and pulses a subtle camera shake)
    /// and takes bonus damage while frozen (IDamageModifier). A solid
    /// freeze has no auto-thaw
    /// timer: the robot stays frozen indefinitely until either (a) it takes
    /// any damage, which breaks the ice (dealing that hit's normal, frozen-
    /// bonus damage first) and frees it back into Chase, or (b) it is hit
    /// again by Freeze Breath, which frees it immediately instead of
    /// re-freezing it (Freeze Breath acts as a toggle on an already-frozen
    /// target). Any active freeze exposure (even below the freeze threshold)
    /// halts chase movement immediately, so the robot visibly stops in its
    /// tracks for as long as Freeze Breath is chilling it, resuming the
    /// chase once exposure decays back to zero. Briefly suspends chase
    /// movement after any hit (knockbackRecoveryTime) so an attack's
    /// physics knockback impulse can actually separate it from the player
    /// instead of being overwritten by chase steering on the very next
    /// physics step. Requires Damageable + DamageFlashAndDestroy on the
    /// same GameObject.
    /// </summary>
    [RequireComponent(typeof(Damageable))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class EnemyRobot : MonoBehaviour, IFreezable, IDamageModifier
    {
        private enum State { Chase, Frozen, Dead }

        [Header("Chase")]
        [SerializeField] private Transform player;
        [SerializeField] private float aggroRange = 8f;
        [SerializeField] private float moveSpeed = 2.5f;

        [Header("Knockback")]
        [SerializeField] private float knockbackRecoveryTime = 0.35f;

        [Header("Freeze")]
        [SerializeField] private float freezeThreshold = 25f;
        [SerializeField] private float frozenDamageMultiplier = 2f;
        [SerializeField] private Color frozenTint = new Color(0.6f, 0.85f, 1f);
        [SerializeField] private float freezeExposureDecayPerSecond = 10f;
        [SerializeField] private EnemyFreezeVisualEffect freezeVisual;
        [SerializeField] private float freezeKnockbackDistance = 0.4f;
        [SerializeField] private float freezeShakeDuration = 0.12f;

        private Rigidbody2D rb;
        private SpriteRenderer spriteRenderer;
        private Damageable damageable;

        private State state = State.Chase;
        private float freezeExposure;
        private Color baseColor;
        private float knockbackTimeRemaining;

        public bool IsFrozen => state == State.Frozen;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            damageable = GetComponent<Damageable>();

            if (spriteRenderer != null)
            {
                baseColor = spriteRenderer.color;
            }

            if (player == null)
            {
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    player = playerObj.transform;
                }
            }
        }

        private void OnEnable()
        {
            damageable.OnDeath += HandleDeath;
            damageable.OnDamaged += HandleDamaged;
        }

        private void OnDisable()
        {
            damageable.OnDeath -= HandleDeath;
            damageable.OnDamaged -= HandleDamaged;
        }

        private void Update()
        {
            if (state == State.Dead)
            {
                return;
            }

            if (state == State.Frozen)
            {
                // Solid-frozen has no auto-thaw timer: stays frozen until
                // damaged or hit again by Freeze Breath.
                return;
            }

            // Chase state: exposure decays when not actively being hit.
            if (freezeExposure > 0f)
            {
                freezeExposure = Mathf.Max(0f, freezeExposure - freezeExposureDecayPerSecond * Time.deltaTime);
            }

            freezeVisual?.SetChillLevel(freezeExposure / freezeThreshold);

            if (knockbackTimeRemaining > 0f)
            {
                knockbackTimeRemaining -= Time.deltaTime;
            }
        }

        private void FixedUpdate()
        {
            if (state != State.Chase || player == null)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }

            if (knockbackTimeRemaining > 0f)
            {
                // Currently reeling from a hit: leave the physics-driven
                // knockback velocity alone (it decays via linear damping)
                // instead of instantly overwriting it with chase movement,
                // so a knockback actually creates visible separation.
                return;
            }

            if (freezeExposure > 0f)
            {
                // Actively being chilled by Freeze Breath: hold still
                // instead of chasing. This makes any exposure visibly halt
                // the robot right away, not just once it fully solidifies
                // into the Frozen state.
                rb.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 toPlayer = (Vector2)(player.position - transform.position);
            if (toPlayer.magnitude <= aggroRange)
            {
                rb.linearVelocity = toPlayer.normalized * moveSpeed;
            }
            else
            {
                rb.linearVelocity = Vector2.zero;
            }
        }

        private void HandleDamaged(int amount)
        {
            if (state == State.Dead)
            {
                return;
            }

            if (state == State.Frozen)
            {
                // Any damage breaks the ice and frees the robot (the hit
                // itself already applied its frozen-bonus damage, since
                // Damageable computes/applies damage before firing
                // OnDamaged).
                Thaw();
            }

            knockbackTimeRemaining = knockbackRecoveryTime;
        }

        public void AddFreezeExposure(float amount, bool isNewActivation)
        {
            if (state == State.Dead)
            {
                return;
            }

            if (state == State.Frozen)
            {
                // Freeze Breath toggles an already-frozen target: a fresh
                // press frees it instead of re-freezing it. A continuing
                // hold that happened to freeze this target mid-stream is
                // NOT a fresh press, so it must not immediately free it
                // again — that would look like the ice melting on its own.
                if (isNewActivation)
                {
                    Thaw();
                }
                return;
            }

            freezeExposure += amount;
            freezeVisual?.SetChillLevel(freezeExposure / freezeThreshold);
            if (freezeExposure >= freezeThreshold)
            {
                Freeze();
            }
        }

        private void Freeze()
        {
            state = State.Frozen;
            rb.linearVelocity = Vector2.zero;
            ApplyFreezeKnockback();
            if (spriteRenderer != null)
            {
                spriteRenderer.color = frozenTint;
            }
            freezeVisual?.PlayFreezeIn();
            CameraShake.GetOrCreate()?.Pulse(freezeShakeDuration);
        }

        /// <summary>
        /// A one-time, fixed-distance pop away from the player at the
        /// instant of freezing, applied directly via rb.position rather
        /// than a physics impulse — FixedUpdate() hard-zeroes velocity
        /// every step while Frozen (to keep it immovable until damaged),
        /// which would otherwise cancel an AddForce-based knockback before
        /// it ever produced visible movement.
        /// </summary>
        private void ApplyFreezeKnockback()
        {
            if (player == null || freezeKnockbackDistance <= 0f)
            {
                return;
            }

            Vector2 away = (Vector2)transform.position - (Vector2)player.position;
            if (away.sqrMagnitude < 0.0001f)
            {
                return;
            }

            rb.position += away.normalized * freezeKnockbackDistance;
        }

        private void Thaw()
        {
            state = State.Chase;
            freezeExposure = 0f;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = baseColor;
            }
            freezeVisual?.PlayThaw();
        }

        public float GetDamageMultiplier()
        {
            return state == State.Frozen ? frozenDamageMultiplier : 1f;
        }

        private void HandleDeath()
        {
            state = State.Dead;
            rb.linearVelocity = Vector2.zero;
        }
    }
}
