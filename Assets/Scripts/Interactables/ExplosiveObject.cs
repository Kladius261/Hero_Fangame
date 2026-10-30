using System.Collections;
using UnityEngine;
using HeroFangame.Combat;
using HeroFangame.Core;
using HeroFangame.Camera;
using HeroFangame.Enemy;
using HeroFangame.UI;

namespace HeroFangame.Interactables
{
    /// <summary>
    /// Grabbable/throwable/freezable prop that detonates on a single hit
    /// (from a punch, Heat Vision, Freeze Breath, a throw landing, an AOE
    /// splash, or another explosion's chain reaction), unlike
    /// DestructibleObject which survives several hits. Can only ever be
    /// frozen once: Freeze Breath's chip damage on the hit(s) that build up
    /// to that freeze is vetoed via IFreezeDamageAbsorber (so the explosive
    /// survives long enough to actually freeze), but once frozen, there is
    /// no toggle-thaw — a further Freeze Breath activation detonates it
    /// outright, same as any other damage source. There is no "survive a
    /// hit while frozen" branch: every other damage source (punch, Heat
    /// Vision, AOE, another explosion) always detonates on contact via
    /// HandleDamaged; being frozen only adds a shatter flourish on top of
    /// the explosion.
    /// </summary>
    [RequireComponent(typeof(Damageable))]
    public class ExplosiveObject : MonoBehaviour, IFreezable, IDamageModifier, IGrabbable, IFreezeDamageAbsorber
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
        [SerializeField] private float freezeHapticDuration = 0.15f;
        [SerializeField] private float freezeHapticLowFrequency = 0.8f;
        [SerializeField] private float freezeHapticHighFrequency = 0.2f;

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
        [SerializeField] private float explosionHapticDuration = 0.2f;
        [SerializeField] private float explosionHapticLowFrequency = 1f;
        [SerializeField] private float explosionHapticHighFrequency = 0.8f;
        [SerializeField] private Color explosionFlashColor = Color.white;
        [SerializeField] private float explosionFlashDuration = 0.08f;
        [SerializeField] private float explosionVfxLifetime = 0.6f;
        [SerializeField] private GameObject cartoonBoomVfxPrefab;
        [SerializeField] private float cartoonBoomMinScale = 0.4f;
        [SerializeField] private float cartoonBoomMaxScale = 0.7f;
        [SerializeField] private float cartoonBoomVfxLifetime = 1.5f;
        [Tooltip("One randomly picked (no immediate repeat), layered with explosionClip below. Shared project-wide: this group plays for every ExplosiveObject detonation regardless of cause (regular hit, ice-break-while-frozen, grab-and-throw landing).")]
        [SerializeField] private AudioClip[] heavyHitClips;
        [SerializeField] private AudioClip explosionClip;

        private Damageable damageable;
        private SpriteRenderer spriteRenderer;
        private Collider2D col;
        private Transform player;

        private FreezeState state = FreezeState.Normal;
        private float freezeExposure;
        private Color baseColor;
        private bool isGrabbed;
        private bool hasExploded;
        private bool pendingDamageAbsorption;
        private bool suppressNextBoomVfx;
        private Coroutine throwRoutine;

        /// <summary>
        /// Every instance is destroyed after its own single explosion (see
        /// hasExploded/DestroyAfterDelay below), so an instance field could
        /// never actually prevent two separate explosions from repeating the
        /// same heavy-hit clip back to back -- this has to be static, shared
        /// across every ExplosiveObject for the life of the play session.
        /// </summary>
        private static int lastHeavyHitClipIndex = -1;

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
                // Explosives can only ever be frozen once: unlike
                // EnemyRobot/DestructibleObject's toggle-thaw convention, a
                // fresh Freeze Breath activation on an already-frozen
                // explosive detonates it instead of quietly freeing it.
                if (isNewActivation)
                {
                    Explode(gameObject, Vector2.zero);
                }
                return;
            }

            // FreezeBreathAbility checks IFreezeDamageAbsorber right after
            // calling this, on every hit, and vetoes that same hit's chip
            // damage before it ever reaches Damageable.TakeDamage (see
            // ConsumeDamageAbsorption below and FreezeBreathAbility.ApplyCone).
            // Without this, the very first freeze-attempt tap would zero this
            // object's HP and permanently mark it dead in Damageable before
            // it ever got a chance to actually freeze.
            pendingDamageAbsorption = true;
            freezeExposure += amount;
            freezeVisual?.SetChillLevel(freezeExposure / freezeThreshold);
            if (freezeExposure >= freezeThreshold)
            {
                Freeze();
            }
        }

        /// <summary>
        /// One-shot veto for Freeze Breath's chip damage on the exact hit
        /// that just called AddFreezeExposure this frame — see
        /// FreezeBreathAbility.ApplyCone, which calls this immediately after
        /// AddFreezeExposure for each hit and nulls the hit out of its damage
        /// pass if this returns true. Never left dangling: only ever set
        /// true inside AddFreezeExposure's own building-up branch, and
        /// always consumed (reset to false) here in the same synchronous
        /// call chain, so it can never carry over to absorb some later,
        /// unrelated attack.
        /// </summary>
        public bool ConsumeDamageAbsorption()
        {
            bool result = pendingDamageAbsorption;
            pendingDamageAbsorption = false;
            return result;
        }

        /// <summary>
        /// Called by an attacker that's about to deal the killing hit and
        /// already plays its own crash/impact VFX on top of this explosion
        /// (currently just Flight's Charge-Crash) -- lets that VFX take
        /// priority instead of the two bursts overlapping. One-shot, same
        /// consume-on-use convention as ConsumeDamageAbsorption: set right
        /// before the attack's own TakeDamage call, consumed inside Explode
        /// below the instant it fires, so it can never leak into some later,
        /// unrelated explosion.
        /// </summary>
        public void SuppressNextExplosionVfx()
        {
            suppressNextBoomVfx = true;
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
            Haptics.GetOrCreate()?.Pulse(freezeHapticDuration, freezeHapticLowFrequency, freezeHapticHighFrequency);
            ApplyKnockbackAwayFromPlayer(freezeKnockbackDistance);
            TextPopupManager.Instance?.SpawnBurst(TextPopupManager.Category.FullFreeze, transform.position);
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
                // Explosive's own detonation audio already covers this
                // moment, so suppress the normal Break Free sound to avoid
                // doubling up — the freeze state still clears normally.
                freezeVisual?.PlayShatter(playBreakFreeSound: false);
            }
            else
            {
                freezeVisual?.PlayThaw();
            }
        }

        /// <summary>
        /// Any hit that actually reaches Damageable.TakeDamage is lethal for
        /// an explosive, frozen or not — unlike DestructibleObject, there is
        /// no "survive while frozen" branch here, so this always detonates
        /// unconditionally. Freeze Breath's own chip damage never reaches
        /// this point in the first place while a freeze is building up: it's
        /// vetoed upstream via IFreezeDamageAbsorber (see
        /// ConsumeDamageAbsorption and FreezeBreathAbility.ApplyCone), so
        /// there's nothing left to exempt here.
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
            Haptics.GetOrCreate()?.Pulse(explosionHapticDuration, explosionHapticLowFrequency, explosionHapticHighFrequency);
            PlayExplosionSound();

            if (suppressNextBoomVfx)
            {
                suppressNextBoomVfx = false;
            }
            else
            {
                SpawnCartoonBoomCluster();
            }

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
        /// Spawns a single PS_CartoonBoom clone centered on the explosion,
        /// with randomized rotation and scale for some per-explosion
        /// variety. The comic-book boom text is handled by the
        /// TextPopupManager's Explosion category instead of a particle
        /// system, so it stays visually consistent (color/lighting) with
        /// every other popup in the game. Clone is a detached copy —
        /// unaffected by this object's own destruction below — and cleans
        /// itself up on cartoonBoomVfxLifetime, decoupled from the exploded
        /// object's own destroy timer (explosionVfxLifetime).
        /// </summary>
        private void SpawnCartoonBoomCluster()
        {
            if (cartoonBoomVfxPrefab != null)
            {
                float zRotation = Random.Range(0f, 360f);
                var clone = Instantiate(cartoonBoomVfxPrefab, transform.position, Quaternion.Euler(0f, 0f, zRotation));
                clone.transform.localScale = Vector3.one * Random.Range(cartoonBoomMinScale, cartoonBoomMaxScale);
                Destroy(clone, cartoonBoomVfxLifetime);
            }

            TextPopupManager.Instance?.SpawnBurst(TextPopupManager.Category.Explosion, transform.position);
        }

        /// <summary>
        /// Shared explosion sound, played for every detonation regardless of
        /// cause. Uses PlayClip2D (below) instead of a component on this
        /// object because this GameObject self-destructs shortly after (see
        /// DestroyAfterDelay/explosionVfxLifetime) -- PlayClip2D spawns its
        /// own disposable temp GameObject that outlives this one, so a clip
        /// longer than explosionVfxLifetime still plays out fully.
        /// </summary>
        private void PlayExplosionSound()
        {
            if (heavyHitClips != null && heavyHitClips.Length > 0)
            {
                int index;
                do
                {
                    index = Random.Range(0, heavyHitClips.Length);
                }
                while (heavyHitClips.Length > 1 && index == lastHeavyHitClipIndex);
                lastHeavyHitClipIndex = index;

                var clip = heavyHitClips[index];
                if (clip != null)
                {
                    PlayClip2D(clip, transform.position);
                }
            }

            if (explosionClip != null)
            {
                PlayClip2D(explosionClip, transform.position);
            }
        }

        /// <summary>
        /// Non-positional equivalent of AudioSource.PlayClipAtPoint: every
        /// other SFX in the game plays through a component-based AudioSource
        /// (spatialBlend 0, set in the Inspector), which stays at full volume
        /// regardless of distance from the camera. PlayClipAtPoint instead
        /// defaults its temp AudioSource to spatialBlend 1 (full 3D) with
        /// Unity's default logarithmic rolloff -- combined with this game's
        /// camera sitting ~10 units back on Z for its 2D view, that alone
        /// was enough distance to attenuate every explosion down to
        /// near-silence (worse the farther a thrown explosive lands from
        /// where it started). Spawning our own temp GameObject/AudioSource
        /// with spatialBlend forced to 0 keeps the explosion as audible as
        /// every other hit sound, while still outliving this object's own
        /// self-destruct timer the same way PlayClipAtPoint would have.
        /// </summary>
        private static void PlayClip2D(AudioClip clip, Vector3 position)
        {
            var go = new GameObject("OneShotExplosionAudio");
            go.transform.position = position;
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.clip = clip;
            source.spatialBlend = 0f;
            source.Play();
            Destroy(go, clip.length);
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
