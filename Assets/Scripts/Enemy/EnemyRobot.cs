using UnityEngine;
using HeroFangame.Core;

namespace HeroFangame.Enemy
{
    /// <summary>
    /// Basic robot: Chase / Frozen / Dead state machine. Chases the player
    /// within aggroRange, accumulates Freeze Breath exposure (IFreezable) and
    /// freezes solid once the threshold is crossed (stops moving, tints
    /// frost, auto-thaws), and takes bonus damage while frozen (IDamageModifier).
    /// Briefly suspends chase movement after any hit (knockbackRecoveryTime)
    /// so an attack's physics knockback impulse can actually separate it from
    /// the player instead of being overwritten by chase steering on the very
    /// next physics step. Requires Damageable + DamageFlashAndDestroy on the
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
        [SerializeField] private float freezeThreshold = 100f;
        [SerializeField] private float freezeDuration = 3f;
        [SerializeField] private float frozenDamageMultiplier = 2f;
        [SerializeField] private Color frozenTint = new Color(0.6f, 0.85f, 1f);
        [SerializeField] private float freezeExposureDecayPerSecond = 10f;

        private Rigidbody2D rb;
        private SpriteRenderer spriteRenderer;
        private Damageable damageable;

        private State state = State.Chase;
        private float freezeExposure;
        private float frozenTimeRemaining;
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
                frozenTimeRemaining -= Time.deltaTime;
                if (frozenTimeRemaining <= 0f)
                {
                    Thaw();
                }
                return;
            }

            // Chase state: exposure decays when not actively being hit.
            if (freezeExposure > 0f)
            {
                freezeExposure = Mathf.Max(0f, freezeExposure - freezeExposureDecayPerSecond * Time.deltaTime);
            }

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
            if (state == State.Chase)
            {
                knockbackTimeRemaining = knockbackRecoveryTime;
            }
        }

        public void AddFreezeExposure(float amount)
        {
            if (state == State.Dead || state == State.Frozen)
            {
                return;
            }

            freezeExposure += amount;
            if (freezeExposure >= freezeThreshold)
            {
                Freeze();
            }
        }

        private void Freeze()
        {
            state = State.Frozen;
            frozenTimeRemaining = freezeDuration;
            rb.linearVelocity = Vector2.zero;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = frozenTint;
            }
        }

        private void Thaw()
        {
            state = State.Chase;
            freezeExposure = 0f;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = baseColor;
            }
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
