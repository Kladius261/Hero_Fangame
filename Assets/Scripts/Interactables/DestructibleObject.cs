using System.Collections;
using UnityEngine;
using HeroFangame.Combat;
using HeroFangame.Core;
using HeroFangame.Camera;
using HeroFangame.Enemy;

namespace HeroFangame.Interactables
{
    /// <summary>
    /// Stationary destructible prop. Takes damage from punches/Heat Vision/
    /// Freeze Breath like any Damageable (see Damageable + DamageFlashAndDestroy
    /// on this prefab), and additionally:
    /// - Can be frozen by Freeze Breath (IFreezable) and takes bonus damage
    ///   while frozen (IDamageModifier), mirroring EnemyRobot's freeze state
    ///   machine almost exactly. Unlike EnemyRobot it never moves on its own
    ///   (no wander/chase) and has no Rigidbody2D, so the short "pop away from
    ///   the player" on breaking free is done via a direct transform.position
    ///   offset rather than a physics-driven one.
    /// - Can be grabbed and thrown by the player (see PlayerGrabAbility).
    ///   While grabbed it's parented onto the player and its collider is
    ///   disabled; on throw it flies a fixed distance over a fixed duration
    ///   (always covering the full distance, regardless of obstacles) and,
    ///   on landing, damages itself, deals Charge-Crash-style outward AOE
    ///   damage/knockback to nearby enemies/destructibles, and plays the same
    ///   camera shake/hit-stop game feel as Flight's Charge-Crash. If it was
    ///   frozen at the moment it lands, the ice also shatters (same shatter
    ///   VFX/sound as a normal ice-break), on top of the usual landing impact.
    /// </summary>
    [RequireComponent(typeof(Damageable))]
    public class DestructibleObject : MonoBehaviour, IFreezable, IDamageModifier
    {
        private enum FreezeState { Normal, Frozen }

        [Header("Freeze")]
        [SerializeField] private float freezeThreshold = 25f;
        [SerializeField] private float frozenDamageMultiplier = 2f;
        [SerializeField] private Color frozenTint = new Color(0.6f, 0.85f, 1f);
        [SerializeField] private float freezeExposureDecayPerSecond = 10f;
        [SerializeField] private EnemyFreezeVisualEffect freezeVisual;
        [SerializeField] private float freezeKnockbackDistance = 0.4f;
        [SerializeField] private float freezeShakeDuration = 0.12f;
        [SerializeField] private float iceBreakHitStopDuration = 0.1f;

        [Header("Grab")]
        [SerializeField] private Vector3 grabLocalOffset = new Vector3(0f, 0.9f, 0f);

        [Header("Throw")]
        [SerializeField] private float throwDistance = 6f;
        [SerializeField] private float throwDuration = 0.25f;
        [SerializeField] private float throwImpactRadius = 2.5f;
        [SerializeField] private int throwSelfDamage = 3;
        [SerializeField] private int throwAoeDamage = 1;
        [SerializeField] private float throwAoeKnockbackForce = 8f;
        [SerializeField] private LayerMask throwAoeLayers;
        [SerializeField] private float throwImpactShakeDuration = 0.15f;
        [SerializeField] private float throwImpactShakeAmplitude = 1.75f;
        [SerializeField] private float throwImpactHitStopDuration = 0.08f;

        private Damageable damageable;
        private SpriteRenderer spriteRenderer;
        private Collider2D col;
        private HitSquashEffect squashEffect;
        private Transform player;

        private FreezeState state = FreezeState.Normal;
        private float freezeExposure;
        private Color baseColor;
        private bool isGrabbed;
        private Coroutine throwRoutine;

        public bool IsFrozen => state == FreezeState.Frozen;
        public bool IsGrabbed => isGrabbed;

        private void Awake()
        {
            damageable = GetComponent<Damageable>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            col = GetComponent<Collider2D>();
            squashEffect = GetComponent<HitSquashEffect>();

            if (spriteRenderer != null)
            {
                baseColor = spriteRenderer.color;
            }

            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
        }

        private void OnEnable()
        {
            damageable.OnDamaged += HandleDamaged;
            damageable.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            damageable.OnDamaged -= HandleDamaged;
            damageable.OnDeath -= HandleDeath;
        }

        private void Update()
        {
            if (state == FreezeState.Frozen)
            {
                return;
            }

            if (freezeExposure > 0f)
            {
                freezeExposure = Mathf.Max(0f, freezeExposure - freezeExposureDecayPerSecond * Time.deltaTime);
            }

            freezeVisual?.SetChillLevel(freezeExposure / freezeThreshold);
        }

        public void AddFreezeExposure(float amount, bool isNewActivation)
        {
            if (state == FreezeState.Frozen)
            {
                // Freeze Breath toggles an already-frozen target: a fresh
                // press frees it instead of re-freezing it, same convention
                // as EnemyRobot.
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
            state = FreezeState.Frozen;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = frozenTint;
            }
            freezeVisual?.PlayFreezeIn();
            CameraShake.GetOrCreate()?.Pulse(freezeShakeDuration);
            ApplyKnockbackAwayFromPlayer(freezeKnockbackDistance);
        }

        private void Thaw(bool shattered)
        {
            state = FreezeState.Normal;
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
        }

        /// <summary>
        /// DamageFlashAndDestroy keeps the GameObject alive (shrinking it)
        /// for a short delay after death before actually destroying it.
        /// Disabling the collider immediately on death closes a window
        /// where PlayerGrabAbility could grab an already-dying object —
        /// if it were then destroyed while still parented to the player,
        /// grab stance would never clear since that only happens via
        /// Throw(). Punching/AOE-ing a destructible to death while it's
        /// genuinely being carried can't happen (its collider is already
        /// disabled for the whole grab), so this only ever affects the
        /// not-yet-grabbed death window.
        /// </summary>
        private void HandleDeath()
        {
            if (col != null)
            {
                col.enabled = false;
            }
        }

        private void HandleDamaged(int amount)
        {
            if (state != FreezeState.Frozen)
            {
                return;
            }

            // Any damage breaks the ice and frees the object (the hit itself
            // already applied its frozen-bonus damage, since Damageable
            // computes/applies damage before firing OnDamaged) — same beat as
            // EnemyRobot.HandleDamaged.
            Thaw(shattered: true);
            Vector2 breakDirection = ApplyKnockbackAwayFromPlayer(freezeKnockbackDistance);
            CameraShake.GetOrCreate()?.Pulse(freezeShakeDuration);
            HitStop.GetOrCreate()?.Trigger(iceBreakHitStopDuration);
            squashEffect?.PlaySquash(breakDirection);
        }

        /// <summary>
        /// One-time, fixed-distance pop away from the player, applied
        /// directly via transform.position (no Rigidbody2D on this object).
        /// Returns the away-from-player direction applied (Vector2.zero if
        /// nothing was applied).
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
            transform.position += (Vector3)(direction * distance);
            return direction;
        }

        public float GetDamageMultiplier()
        {
            return state == FreezeState.Frozen ? frozenDamageMultiplier : 1f;
        }

        /// <summary>
        /// Mounts this object onto the player: disables its collider (so it
        /// doesn't block the player it's riding on top of) and parents it to
        /// the player's transform at a fixed carry offset.
        /// </summary>
        public void BeginGrab(Transform carrier)
        {
            isGrabbed = true;
            if (col != null)
            {
                col.enabled = false;
            }
            transform.SetParent(carrier, worldPositionStays: false);
            transform.localPosition = grabLocalOffset;
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// Releases this object from the player and sends it flying a fixed
        /// distance along direction, landing after throwDuration regardless
        /// of what's in the way.
        /// </summary>
        public void Throw(Vector2 direction, GameObject thrower)
        {
            isGrabbed = false;
            transform.SetParent(null, worldPositionStays: true);

            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            if (throwRoutine != null)
            {
                StopCoroutine(throwRoutine);
            }
            throwRoutine = StartCoroutine(ThrowRoutine(dir, thrower));
        }

        private IEnumerator ThrowRoutine(Vector2 direction, GameObject thrower)
        {
            Vector3 start = transform.position;
            Vector3 target = start + (Vector3)(direction * throwDistance);

            float t = 0f;
            while (t < throwDuration)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(start, target, Mathf.Clamp01(t / throwDuration));
                yield return null;
            }

            transform.position = target;
            throwRoutine = null;
            Land(direction, thrower);
        }

        private void Land(Vector2 direction, GameObject thrower)
        {
            if (col != null)
            {
                col.enabled = true;
            }

            if (IsFrozen)
            {
                // Shatter VFX/sound only here — the shake/hit-stop below
                // covers the whole landing impact so a frozen throw feels
                // exactly like an unfrozen one, plus the ice breaking.
                Thaw(shattered: true);
            }

            damageable.TakeDamage(throwSelfDamage, new DamageInfo(thrower, Vector2.zero, 0f));

            AttackUtility.OverlapCircleAndDamageRadial(
                transform.position,
                throwImpactRadius,
                throwAoeLayers,
                throwAoeDamage,
                thrower,
                throwAoeKnockbackForce,
                out _,
                (hit, dir) => hit.GetComponentInParent<HitSquashEffect>()?.PlaySquash(dir),
                exclude: col);

            CameraShake.GetOrCreate()?.Pulse(throwImpactShakeDuration, throwImpactShakeAmplitude);
            HitStop.GetOrCreate()?.Trigger(throwImpactHitStopDuration);
            squashEffect?.PlaySquash(direction);
        }
    }
}
