using UnityEngine;
using HeroFangame.Camera;
using HeroFangame.Core;

namespace HeroFangame.Enemy
{
    /// <summary>
    /// Basic robot: Wander / Frozen / Dead state machine. Ignores the
    /// player entirely and roams the level in random directions, changing
    /// heading every minWanderInterval-maxWanderInterval seconds and
    /// bouncing off walls/obstacles (reflecting off the contact normal,
    /// with a little jitter to avoid stable ping-pong loops at corners).
    /// It never reacts to the player, even when attacked or bumped into.
    /// It also accumulates Freeze Breath exposure (IFreezable), and shows
    /// a progressively intensifying frost visual as exposure rises even
    /// before freezing. Once exposure crosses freezeThreshold it freezes
    /// solid (stops moving, tints frost, pops back a short, fixed distance
    /// away from the player, and pulses a subtle camera shake) and takes
    /// bonus damage while frozen (IDamageModifier). A solid freeze has no
    /// auto-thaw timer: the robot stays frozen indefinitely until either
    /// (a) it takes any damage, which breaks the ice (dealing that hit's
    /// normal, frozen-bonus damage first), plays a shatter flash/VFX, pops
    /// the robot away from the player and pulses the camera by the same
    /// amount as the initial freeze, and frees it back into wandering
    /// (with a freshly-picked random direction), or (b) it is
    /// hit again by Freeze Breath, which frees it immediately instead of
    /// re-freezing it (Freeze Breath acts as a toggle on an already-frozen
    /// target). Any active freeze exposure (even below the freeze threshold)
    /// halts wander movement immediately, so the robot visibly stops in its
    /// tracks for as long as Freeze Breath is chilling it, resuming
    /// wandering once exposure decays back to zero. Briefly suspends
    /// wander movement after any hit (knockbackRecoveryTime) so an
    /// attack's physics knockback impulse can actually separate it from
    /// the player instead of being overwritten by wander steering on the
    /// very next physics step. Requires Damageable + DamageFlashAndDestroy
    /// on the same GameObject.
    /// </summary>
    [RequireComponent(typeof(Damageable))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class EnemyRobot : MonoBehaviour, IFreezable, IDamageModifier
    {
        private enum State { Wander, Frozen, Dead }

        [Header("Wander")]
        [SerializeField] private Transform player;
        [SerializeField] private float wanderSpeed = 2.5f;
        [SerializeField] private float minWanderInterval = 1.0f;
        [SerializeField] private float maxWanderInterval = 3.5f;

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
        [SerializeField] private float iceBreakHitStopDuration = 0.2f;

        private Rigidbody2D rb;
        private SpriteRenderer spriteRenderer;
        private Damageable damageable;
        private HitSquashEffect squashEffect;

        private State state = State.Wander;
        private float freezeExposure;
        private Color baseColor;
        private float knockbackTimeRemaining;
        private Vector2 wanderDirection;
        private float wanderTimer;

        public bool IsFrozen => state == State.Frozen;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            // Flight's liftoff/Charge knockback (and simply being rammed at
            // Flight's 2x move speed) can push this rigidbody hard enough
            // against the arena's thin 1-unit-thick walls to tunnel straight
            // through them in a single physics step under Discrete detection
            // — the same tunneling risk the player's own rigidbody already
            // guards against while flying (see FlightAbility.EnterFlight).
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            damageable = GetComponent<Damageable>();
            squashEffect = GetComponent<HitSquashEffect>();

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

            PickNewWanderDirection();
            // Stagger the first direction change across enemies so a
            // scene full of them (all Awake()'d the same frame) doesn't
            // re-roll direction in visible lockstep.
            wanderTimer = Random.Range(0f, wanderTimer);
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

            // Wander state: exposure decays when not actively being hit.
            if (freezeExposure > 0f)
            {
                freezeExposure = Mathf.Max(0f, freezeExposure - freezeExposureDecayPerSecond * Time.deltaTime);
            }

            freezeVisual?.SetChillLevel(freezeExposure / freezeThreshold);

            if (knockbackTimeRemaining > 0f)
            {
                knockbackTimeRemaining -= Time.deltaTime;
            }

            wanderTimer -= Time.deltaTime;
            if (wanderTimer <= 0f)
            {
                PickNewWanderDirection();
            }
        }

        private void FixedUpdate()
        {
            if (state != State.Wander)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }

            if (knockbackTimeRemaining > 0f)
            {
                // Currently reeling from a hit: leave the physics-driven
                // knockback velocity alone (it decays via linear damping)
                // instead of instantly overwriting it with wander movement,
                // so a knockback actually creates visible separation.
                return;
            }

            if (freezeExposure > 0f)
            {
                // Actively being chilled by Freeze Breath: hold still
                // instead of wandering. This makes any exposure visibly
                // halt the robot right away, not just once it fully
                // solidifies into the Frozen state.
                rb.linearVelocity = Vector2.zero;
                return;
            }

            rb.linearVelocity = wanderDirection * wanderSpeed;
        }

        /// <summary>
        /// Picks a fresh random heading and resets the wander timer to a
        /// random duration within [minWanderInterval, maxWanderInterval].
        /// Built from an angle rather than Random.insideUnitCircle to
        /// guarantee a unit-length result every time (insideUnitCircle can
        /// return a near-zero vector that normalizes to Vector2.zero).
        /// </summary>
        private void PickNewWanderDirection()
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            wanderDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            wanderTimer = Random.Range(minWanderInterval, maxWanderInterval);
        }

        /// <summary>
        /// Bounces off walls/obstacles/other enemies by reflecting the
        /// current wander direction off the contact normal, with a small
        /// random jitter so it doesn't settle into a stable ping-pong loop
        /// at corners or against another enemy. Never reacts to the
        /// player: a contact-normal reflect off the player's collider
        /// would, by construction, point away from them, which would read
        /// as evasive behavior even though nothing here is actually aware
        /// of the player. Bumping into the player still physically pushes
        /// both bodies via Unity's own collision solver, same as today.
        /// </summary>
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (state != State.Wander)
            {
                return;
            }

            if (collision.collider.CompareTag("Player"))
            {
                return;
            }

            Vector2 normal = collision.GetContact(0).normal;
            Vector2 reflected = Vector2.Reflect(wanderDirection, normal);

            float jitter = Random.Range(-15f, 15f) * Mathf.Deg2Rad;
            float cos = Mathf.Cos(jitter);
            float sin = Mathf.Sin(jitter);
            wanderDirection = new Vector2(
                reflected.x * cos - reflected.y * sin,
                reflected.x * sin + reflected.y * cos
            ).normalized;
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
                // OnDamaged). Breaking free gets the same pop-away
                // knockback and camera shake as freezing did, so the
                // moment reads with equal physicality in both directions.
                Thaw(shattered: true);
                Vector2 breakDirection = ApplyKnockbackAwayFromPlayer(freezeKnockbackDistance);
                CameraShake.GetOrCreate()?.Pulse(freezeShakeDuration);
                HitStop.GetOrCreate()?.Trigger(iceBreakHitStopDuration);
                squashEffect?.PlaySquash(breakDirection);
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
                    Thaw(shattered: false);
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
            ApplyKnockbackAwayFromPlayer(freezeKnockbackDistance);
            if (spriteRenderer != null)
            {
                spriteRenderer.color = frozenTint;
            }
            freezeVisual?.PlayFreezeIn();
            CameraShake.GetOrCreate()?.Pulse(freezeShakeDuration);
        }

        /// <summary>
        /// A one-time, fixed-distance pop away from the player, applied
        /// directly via rb.position rather than a physics impulse. Used
        /// both at the instant of freezing (FixedUpdate() hard-zeroes
        /// velocity every step while Frozen, which would otherwise cancel
        /// an AddForce-based knockback before it ever produced visible
        /// movement) and at the instant of shattering free, so both
        /// transitions read with the same physical punch. Returns the
        /// away-from-player direction that was applied (Vector2.zero if
        /// nothing was applied), so callers can reuse it to orient other
        /// feedback like the shatter squash.
        /// </summary>
        private Vector2 ApplyKnockbackAwayFromPlayer(float distance)
        {
            if (player == null || distance <= 0f)
            {
                return Vector2.zero;
            }

            Vector2 away = (Vector2)transform.position - (Vector2)player.position;
            if (away.sqrMagnitude < 0.0001f)
            {
                return Vector2.zero;
            }

            Vector2 direction = away.normalized;
            rb.position += direction * distance;
            return direction;
        }

        private void Thaw(bool shattered)
        {
            state = State.Wander;
            freezeExposure = 0f;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = baseColor;
            }
            if (shattered)
            {
                freezeVisual?.PlayShatter();
            }
            else
            {
                freezeVisual?.PlayThaw();
            }
            // Pick a fresh heading so a just-thawed robot doesn't walk
            // straight back toward wherever it got frozen.
            PickNewWanderDirection();
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
