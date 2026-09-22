using System.Collections;
using UnityEngine;
using HeroFangame.Combat;
using HeroFangame.Core;
using HeroFangame.Camera;
using HeroFangame.Enemy;

namespace HeroFangame.Interactables
{
    /// <summary>
    /// Grabbable/throwable/freezable prop that detonates on a single hit
    /// (from a punch, Heat Vision, Freeze Breath tick, a throw landing, or
    /// another explosion's AOE), unlike DestructibleObject which survives
    /// several hits. Freeze behavior mirrors DestructibleObject/EnemyRobot
    /// exactly (reversible exposure/toggle-thaw), but there is no "survive
    /// a hit while frozen" branch here — any hit is lethal either way, so
    /// HandleDamaged always detonates; being frozen only adds a shatter
    /// flourish on top of the explosion.
    /// </summary>
    [RequireComponent(typeof(Damageable))]
    public class ExplosiveObject : MonoBehaviour, IFreezable, IDamageModifier, IGrabbable
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

        [Header("Grab")]
        [SerializeField] private Vector3 grabLocalOffset = new Vector3(0f, 0.9f, 0f);

        [Header("Throw")]
        [SerializeField] private float throwDistance = 6f;
        [SerializeField] private float throwDuration = 0.25f;

        [Header("Explosion")]
        [SerializeField] private float explosionRadius = 2.5f;
        [SerializeField] private int explosionDamage = 1;
        [SerializeField] private float explosionKnockbackForce = 8f;
        [SerializeField] private LayerMask explosionLayers;
        [SerializeField] private float explosionShakeDuration = 0.15f;
        [SerializeField] private float explosionShakeAmplitude = 1.75f;
        [SerializeField] private float explosionHitStopDuration = 0.08f;
        [SerializeField] private Color explosionFlashColor = Color.white;
        [SerializeField] private float explosionFlashDuration = 0.08f;
        [SerializeField] private float explosionVfxLifetime = 0.6f;
        [SerializeField] private ExplosionBurstVfx[] explosionBurstVfx;
        [SerializeField] private int minBurstVfxSpawnCount = 5;
        [SerializeField] private int maxBurstVfxSpawnCount = 6;
        [SerializeField] private float burstVfxClusterRadius = 0.6f;

        private Damageable damageable;
        private SpriteRenderer spriteRenderer;
        private Collider2D col;
        private Transform player;

        private FreezeState state = FreezeState.Normal;
        private float freezeExposure;
        private Color baseColor;
        private bool isGrabbed;
        private bool hasExploded;
        private Coroutine throwRoutine;

        public bool IsFrozen => state == FreezeState.Frozen;
        public bool IsGrabbed => isGrabbed;

        private void Awake()
        {
            damageable = GetComponent<Damageable>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            col = GetComponent<Collider2D>();

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
        }

        private void OnDisable()
        {
            damageable.OnDamaged -= HandleDamaged;
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
                // as EnemyRobot/DestructibleObject.
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
        /// Any hit is lethal for an explosive, frozen or not — unlike
        /// DestructibleObject, there is no "survive while frozen" branch
        /// here. The hit itself already applied its frozen-bonus damage
        /// (Damageable computes/applies damage before firing OnDamaged),
        /// so this simply detonates.
        /// </summary>
        private void HandleDamaged(int amount)
        {
            Explode(gameObject, Vector2.zero);
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
        /// distance along direction, detonating after throwDuration
        /// regardless of what's in the way.
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
            Vector3 target = LevelBounds.Clamp(start + (Vector3)(direction * throwDistance));

            float t = 0f;
            while (t < throwDuration)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(start, target, Mathf.Clamp01(t / throwDuration));
                yield return null;
            }

            transform.position = target;
            throwRoutine = null;
            Explode(thrower, direction);
        }

        /// <summary>
        /// Idempotent (guarded by hasExploded) so a direct call from
        /// ThrowRoutine landing on the same frame as a lingering OnDamaged
        /// can never double-fire. Disables the collider immediately (same
        /// fix as DestructibleObject.HandleDeath) so a mid-explosion re-grab
        /// can't happen, plays the full game-feel/VFX sequence, splashes
        /// outward AOE damage/knockback (which can chain into other nearby
        /// ExplosiveObjects via their own Damageable/OnDamaged pipeline),
        /// then destroys the GameObject once the burst VFX has had time to
        /// finish.
        /// </summary>
        private void Explode(GameObject source, Vector2 direction)
        {
            if (hasExploded)
            {
                return;
            }
            hasExploded = true;

            if (col != null)
            {
                col.enabled = false;
            }

            if (IsFrozen)
            {
                Thaw(shattered: true);
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.color = explosionFlashColor;
                StartCoroutine(HideSpriteAfterFlash());
            }

            CameraShake.GetOrCreate()?.Pulse(explosionShakeDuration, explosionShakeAmplitude);
            HitStop.GetOrCreate()?.Trigger(explosionHitStopDuration);

            SpawnBurstVfxCluster();

            AttackUtility.OverlapCircleAndDamageRadial(
                transform.position,
                explosionRadius,
                explosionLayers,
                explosionDamage,
                source,
                explosionKnockbackForce,
                out _,
                (hit, dir) => hit.GetComponentInParent<HitSquashEffect>()?.PlaySquash(dir),
                exclude: col);

            StartCoroutine(DestroyAfterDelay(explosionVfxLifetime));
        }

        /// <summary>
        /// Spawns 5-6 random picks (repeats allowed) from the 3 burst VFX
        /// templates as independent clones scattered around the explosion
        /// center, so a single explosion reads as a dense, layered blast
        /// instead of one lone particle burst. Clones are detached copies —
        /// unaffected by this object's own destruction below — and clean
        /// themselves up on the same timer as the explosion VFX lifetime.
        /// </summary>
        private void SpawnBurstVfxCluster()
        {
            if (explosionBurstVfx == null || explosionBurstVfx.Length == 0)
            {
                return;
            }

            int count = Random.Range(minBurstVfxSpawnCount, maxBurstVfxSpawnCount + 1);
            for (int i = 0; i < count; i++)
            {
                var template = explosionBurstVfx[Random.Range(0, explosionBurstVfx.Length)];
                if (template == null)
                {
                    continue;
                }

                Vector2 offset = Random.insideUnitCircle * burstVfxClusterRadius;
                var clone = Instantiate(template, transform.position + (Vector3)offset, template.transform.rotation);
                clone.Play();
                Destroy(clone.gameObject, explosionVfxLifetime);
            }
        }

        private IEnumerator HideSpriteAfterFlash()
        {
            yield return new WaitForSeconds(explosionFlashDuration);
            spriteRenderer.enabled = false;
        }

        private IEnumerator DestroyAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            Destroy(gameObject);
        }
    }
}
