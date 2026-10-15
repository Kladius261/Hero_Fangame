using UnityEngine;
using HeroFangame.Combat;
using HeroFangame.Core;
using HeroFangame.Camera;
using HeroFangame.Enemy;
using HeroFangame.Interactables;
using HeroFangame.UI;

namespace HeroFangame.Player
{
    /// <summary>
    /// A short, one-shot horizontal Dash. Pressing Flight (Space) fires it
    /// immediately in whichever direction the player is currently moving
    /// (or, if standing still, whichever way they're facing) — there is no
    /// held "flight mode" stance anymore; the press itself is edge-triggered
    /// (see PlayerInputHandler.FlightPressed), so holding the key down
    /// through the end of one Dash will never auto-chain into another. Like
    /// the old Flight Mode, this still disables all offensive abilities
    /// (see the IsFlightMode guards in PlayerCombat / HeatVisionAbility /
    /// FreezeBreathAbility / PlayerGrabAbility) and can't be entered
    /// mid-beam/mid-cone (see PlayerController.IsMovementLocked). The Dash
    /// continuously drains Power the same way the old held Flight Mode did;
    /// running Power dry, or releasing Flight early, cancels the Dash
    /// mid-flight into a landing. Holding Up/Down steers the Dash
    /// diagonally (see AbilityAimController) the whole time it's active,
    /// mirroring the old Charge. It ends the instant it hits anything
    /// (impact feedback + returning straight to grounded) — a Crash is the
    /// Dash's natural end, not just a redirect back into hovering, since
    /// there's no hovering left to redirect into.
    /// </summary>
    [RequireComponent(typeof(PlayerInputHandler))]
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PowerGauge))]
    public class FlightAbility : MonoBehaviour
    {
        private enum State { Grounded, Dashing }

        [Header("Power")]
        [Tooltip("Power drained per second for as long as the Dash is active (matches Freeze Breath's hold-drain rate).")]
        [SerializeField] private float holdPowerCostPerSecond = 25f;
        [Tooltip("Minimum Power that must be available/regenerated before the Dash can be (re-)triggered — matches Heat Vision's tap cost.")]
        [SerializeField] private float minPowerToTakeOff = 10f;

        [Header("Liftoff / Landing")]
        [SerializeField] private float liftoffHeightOffset = 0.3f;
        [SerializeField] private float liftoffRadius = 3f;
        [SerializeField] private int liftoffDamage = 1;
        [SerializeField] private float liftoffKnockbackForce = 8f;
        [SerializeField] private LayerMask enemyLayer;
        [SerializeField] private float liftoffShakeDuration = 0.15f;
        [SerializeField] private float liftoffShakeAmplitude = 1.5f;
        [SerializeField] private float landingShakeDuration = 0.12f;
        [SerializeField] private float landingShakeAmplitude = 1.25f;
        [SerializeField] private float liftoffHapticDuration = 0.15f;
        [SerializeField] private float liftoffHapticLowFrequency = 0.8f;
        [SerializeField] private float liftoffHapticHighFrequency = 0.4f;
        [SerializeField] private float landingHapticDuration = 0.15f;
        [SerializeField] private float landingHapticLowFrequency = 1f;
        [SerializeField] private float landingHapticHighFrequency = 0.4f;

        [Header("Landing Impact")]
        [Tooltip("Mirrors the liftoff AOE pulse (damage/knockback/squash) at the landing spot. Only plays when the Dash is cancelled (Flight released, or Power ran out) before a Crash — a Crash already delivers its own impact and skips this.")]
        [SerializeField] private float landingRadius = 3f;
        [SerializeField] private int landingDamage = 1;
        [SerializeField] private float landingKnockbackForce = 8f;

        [Header("Charge")]
        [SerializeField] private float chargeSpeed = 25.5f;
        [Tooltip("How fast Up/Down steers the Dash while it's in flight, in degrees/second, mirroring Heat Vision/Freeze Breath's aim sweep (see AbilityAimController).")]
        [SerializeField] private float chargeAimSweepSpeedDegreesPerSecond = 180f;
        [Tooltip("How fast the Charge sound-effect popup's comic lettering grows while the Dash is in flight (see TextPopupManager.Category.FlightCharge).")]
        [SerializeField] private float flightChargeLetterGrowthInterval = 0.12f;
        [SerializeField] private int chargeDamage = 3;
        [SerializeField] private float chargeKnockbackForce = 14f;
        [Tooltip("Secondary splash damage/knockback around the crash point, hitting anything near the primary target/wall a Dash slams into (which itself still takes the full chargeDamage/chargeKnockbackForce above via its own direct hit).")]
        [SerializeField] private float chargeAoeRadius = 2.5f;
        [SerializeField] private int chargeAoeDamage = 1;
        [SerializeField] private float chargeAoeKnockbackForce = 8f;
        [SerializeField] private float chargeCrashShakeDuration = 0.15f;
        [SerializeField] private float chargeCrashShakeAmplitude = 1.75f;
        [SerializeField] private float chargeCrashHitStopDuration = 0.08f;
        [SerializeField] private float chargeCrashHapticDuration = 0.2f;
        [SerializeField] private float chargeCrashHapticLowFrequency = 1f;
        [SerializeField] private float chargeCrashHapticHighFrequency = 1f;
        [SerializeField] private float playerFlashDuration = 0.1f;

        [Header("Components")]
        [SerializeField] private GhostTrailEffect ghostTrail;
        [SerializeField] private FlashEffect playerFlash;
        [Tooltip("Reused from enemy hit feedback: PlaySquash(direction) stretches along direction and squishes perpendicular. Vector2.up on liftoff reads as a stretch (tall/thin); Vector2.right on landing reads as a squash (wide/flat).")]
        [SerializeField] private HitSquashEffect playerSquash;

        [Header("VFX")]
        [SerializeField] private ShockwaveEffect flightChargeShockwaveVFX;
        [Tooltip("Full-screen shockwave pulse, dynamically instantiated at the player's position on both liftoff and landing and destroyed once its pulse finishes (unlike the pre-placed VFX above).")]
        [SerializeField] private GameObject flightShockwavePrefab;
        [SerializeField] private GameObject dustParticlePrefab;
        [Tooltip("Vertical offset applied to the dust spawn point so it appears at the player's feet rather than its center pivot.")]
        [SerializeField] private float dustSpawnHeightOffset = -0.75f;

        [Header("Charge-Crash VFX")]
        [Tooltip("Cartoon crash burst, instantiated at the Dash's impact point on every Crash collision (enemy or wall). Mirrors ExplosiveObject's cartoon boom cluster: random Z rotation + random scale, destroyed after chargeCrashVfxLifetime.")]
        [SerializeField] private GameObject chargeCrashVfxPrefab;
        [SerializeField] private float chargeCrashMinScale = 0.4f;
        [SerializeField] private float chargeCrashMaxScale = 0.7f;
        [SerializeField] private float chargeCrashVfxLifetime = 1.5f;

        private PlayerInputHandler input;
        private PlayerController controller;
        private Rigidbody2D rb;
        private PowerGauge power;

        private State state;
        private Vector2 chargeDirection;
        private Vector2 chargeHorizontalFacing;
        private AbilityAimController chargeAimController;
        private TextPopupHandle chargeTextPopupHandle;

        private void Awake()
        {
            input = GetComponent<PlayerInputHandler>();
            controller = GetComponent<PlayerController>();
            rb = GetComponent<Rigidbody2D>();
            power = GetComponent<PowerGauge>();
            chargeAimController = new AbilityAimController(chargeAimSweepSpeedDegreesPerSecond);
        }

        private void Update()
        {
            switch (state)
            {
                case State.Grounded:
                    if (input.FlightPressed && !controller.IsMovementLocked && !controller.IsGrabStance && power.Current >= minPowerToTakeOff)
                    {
                        EnterDash();
                    }
                    break;

                case State.Dashing:
                    // Check the gauge's actual remaining value rather than
                    // DrainOverTime's return value — that return value is
                    // also 0 whenever Time.deltaTime is 0 (e.g. during the
                    // brief Time.timeScale=0 HitStop freeze right after a
                    // Crash impact), which would otherwise read as "Power
                    // ran out" every frame of the freeze.
                    power.DrainOverTime(holdPowerCostPerSecond);
                    if (power.Current <= 0f)
                    {
                        // Power ran dry mid-dash — force-cancel right here,
                        // right now, same forced-cutoff idea as Heat
                        // Vision/Freeze Breath running out of Power.
                        CancelDash();
                        break;
                    }

                    if (!input.FlightHeld)
                    {
                        // Unlike the old uninterruptible Charge, releasing
                        // Flight mid-Dash cancels it early.
                        CancelDash();
                        break;
                    }

                    chargeDirection = chargeAimController.Resolve(isNewActivation: false, 0f, input.MoveInput, chargeHorizontalFacing, Time.deltaTime);
                    controller.SetVelocityOverride(chargeDirection * chargeSpeed);
                    chargeTextPopupHandle?.Tick(Time.deltaTime, flightChargeLetterGrowthInterval);
                    break;
            }
        }

        /// <summary>
        /// The Dash's initial direction is always horizontal: whichever way
        /// the player is currently moving Left/Right, or — if standing
        /// still — whichever way they're currently facing.
        /// </summary>
        private Vector2 ResolveDashDirection()
        {
            Vector2 move = input.MoveInput;
            if (move.x > 0.5f)
            {
                return Vector2.right;
            }
            if (move.x < -0.5f)
            {
                return Vector2.left;
            }
            return controller.Facing.x < 0f ? Vector2.left : Vector2.right;
        }

        private void EnterDash()
        {
            state = State.Dashing;
            controller.SetFlightMode(true);

            rb.position += Vector2.up * liftoffHeightOffset;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CameraShake.GetOrCreate()?.Pulse(liftoffShakeDuration, liftoffShakeAmplitude);
            Haptics.GetOrCreate()?.Pulse(liftoffHapticDuration, liftoffHapticLowFrequency, liftoffHapticHighFrequency);
            playerSquash?.PlaySquash(Vector2.up);
            if (flightShockwavePrefab != null)
            {
                Instantiate(flightShockwavePrefab, transform.position, Quaternion.identity);
            }
            DustParticle.SpawnCluster(
                dustParticlePrefab,
                transform.position + Vector3.up * dustSpawnHeightOffset,
                minClumps: 7,
                maxClumps: 8,
                clumpSpread: 3f,
                minScale: 1.2f,
                maxScale: 2.6f,
                verticalSpreadScale: 0f);

            AttackUtility.OverlapCircleAndDamageRadial(
                transform.position,
                liftoffRadius,
                enemyLayer,
                liftoffDamage,
                gameObject,
                liftoffKnockbackForce,
                out _,
                (hit, direction) => hit.GetComponentInParent<HitSquashEffect>()?.PlaySquash(direction));

            chargeHorizontalFacing = ResolveDashDirection();
            chargeDirection = chargeAimController.Resolve(isNewActivation: true, AbilityAimController.AngleFromVerticalHeld(input.MoveInput), input.MoveInput, chargeHorizontalFacing, Time.deltaTime);
            controller.SetVelocityOverride(chargeDirection * chargeSpeed);
            CameraShake.GetOrCreate()?.SetShaking(true);
            ghostTrail?.StartTrail();
            FullScreenAbilityEffect.Instance?.SetFlightWindActive(true);
            chargeTextPopupHandle = TextPopupManager.Instance?.BeginSustained(TextPopupManager.Category.FlightCharge, transform.position);
        }

        /// <summary>
        /// Releasing Flight mid-Dash, or Power running dry mid-Dash, cancels
        /// it early — same landing impact (dust/shockwave/squash/shake/
        /// haptics/AOE) as the old held Flight Mode's Land(), just without
        /// its text popup (removed per the Flight System Rework).
        /// </summary>
        private void CancelDash()
        {
            EndDashCommon();

            rb.position -= Vector2.up * liftoffHeightOffset;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;

            if (flightShockwavePrefab != null)
            {
                Instantiate(flightShockwavePrefab, transform.position, Quaternion.identity);
            }
            DustParticle.SpawnCluster(
                dustParticlePrefab,
                transform.position + Vector3.up * dustSpawnHeightOffset,
                minClumps: 7,
                maxClumps: 8,
                clumpSpread: 3f,
                minScale: 1.2f,
                maxScale: 2.6f,
                verticalSpreadScale: 0f);
            playerSquash?.PlaySquash(Vector2.right);
            CameraShake.GetOrCreate()?.Pulse(landingShakeDuration, landingShakeAmplitude);
            Haptics.GetOrCreate()?.Pulse(landingHapticDuration, landingHapticLowFrequency, landingHapticHighFrequency);

            AttackUtility.OverlapCircleAndDamageRadial(
                transform.position,
                landingRadius,
                enemyLayer,
                landingDamage,
                gameObject,
                landingKnockbackForce,
                out _,
                (hit, direction) => hit.GetComponentInParent<HitSquashEffect>()?.PlaySquash(direction));

            state = State.Grounded;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            HandleChargeCollision(collision);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            // Catches the case where the player's Dash starts already
            // touching a wall/enemy — Unity never fires OnCollisionEnter2D
            // for a contact that already existed, so without this the Dash
            // would keep shoving into the solid object (and its continuous
            // shake would never turn off) until steered away with Up/Down.
            // This resolves the hit on the very next physics step instead.
            HandleChargeCollision(collision);
        }

        private void HandleChargeCollision(Collision2D collision)
        {
            if (state != State.Dashing)
            {
                return;
            }

            var damageable = collision.collider.GetComponentInParent<Damageable>();
            if (damageable != null)
            {
                HandleChargeHitDamageable(damageable, collision);
            }
            else
            {
                HandleChargeHitWall(collision);
            }

            flightChargeShockwaveVFX?.PlayAt(collision.GetContact(0).point);
            SpawnChargeCrashCluster(collision.GetContact(0).point);
            chargeTextPopupHandle?.Release();
            chargeTextPopupHandle = null;
            FinishDashAfterCrash();
        }

        /// <summary>
        /// Secondary splash around the crash point, hitting anything nearby
        /// besides whatever the Dash directly collided with (that primary
        /// target/wall already got its own full-strength hit, so it's
        /// excluded here to avoid double-dipping it).
        /// </summary>
        private void PlayChargeAoe(Vector2 center, Collider2D primaryCollider)
        {
            AttackUtility.OverlapCircleAndDamageRadial(
                center,
                chargeAoeRadius,
                enemyLayer,
                chargeAoeDamage,
                gameObject,
                chargeAoeKnockbackForce,
                out _,
                (hit, direction) => hit.GetComponentInParent<HitSquashEffect>()?.PlaySquash(direction),
                primaryCollider);
        }

        /// <summary>
        /// Spawns a single PS_CartoonCrash clone centered on the Dash's
        /// impact point, with randomized rotation and scale for some
        /// per-crash variety. The comic-book crash text is handled by the
        /// TextPopupManager's ChargeCrash category instead of a particle
        /// system, so it stays visually consistent (color/lighting) with
        /// every other popup in the game. Mirrors
        /// ExplosiveObject.SpawnCartoonBoomCluster. Fires on every
        /// Dash-ending collision (enemy or wall) via HandleChargeCollision.
        /// </summary>
        private void SpawnChargeCrashCluster(Vector2 point)
        {
            if (chargeCrashVfxPrefab != null)
            {
                float zRotation = Random.Range(0f, 360f);
                var clone = Instantiate(chargeCrashVfxPrefab, point, Quaternion.Euler(0f, 0f, zRotation));
                clone.transform.localScale = Vector3.one * Random.Range(chargeCrashMinScale, chargeCrashMaxScale);
                Destroy(clone, chargeCrashVfxLifetime);
            }

            TextPopupManager.Instance?.SpawnBurst(TextPopupManager.Category.ChargeCrash, point);
        }

        private void HandleChargeHitDamageable(Damageable damageable, Collision2D collision)
        {
            Vector2 awayDir = (Vector2)collision.collider.bounds.center - (Vector2)transform.position;
            awayDir = awayDir.sqrMagnitude > 0.0001f ? awayDir.normalized : chargeDirection;

            // Crashing straight into an explosive would otherwise detonate
            // it on the same frame as SpawnChargeCrashCluster below, visibly
            // overlapping two cartoon bursts at the same point -- let the
            // Charge-Crash VFX take priority instead.
            collision.collider.GetComponentInParent<ExplosiveObject>()?.SuppressNextExplosionVfx();

            // Same idea for a frozen enemy/destructible: the Charge-Crash
            // VFX already covers this impact, so veto the FreezeBreak text
            // popup that HandleDamaged would otherwise spawn on the ice
            // breaking.
            collision.collider.GetComponentInParent<EnemyRobot>()?.SuppressNextFreezeBreakPopup();
            collision.collider.GetComponentInParent<DestructibleObject>()?.SuppressNextFreezeBreakPopup();

            damageable.TakeDamage(chargeDamage, new DamageInfo(gameObject, awayDir, chargeKnockbackForce));

            var hitRb = collision.collider.attachedRigidbody;
            if (hitRb != null)
            {
                hitRb.AddForce(awayDir * chargeKnockbackForce, ForceMode2D.Impulse);
            }
            collision.collider.GetComponentInParent<HitSquashEffect>()?.PlaySquash(awayDir);

            CameraShake.GetOrCreate()?.Pulse(chargeCrashShakeDuration, chargeCrashShakeAmplitude);
            HitStop.GetOrCreate()?.Trigger(chargeCrashHitStopDuration);
            Haptics.GetOrCreate()?.Pulse(chargeCrashHapticDuration, chargeCrashHapticLowFrequency, chargeCrashHapticHighFrequency);

            PlayChargeAoe(collision.GetContact(0).point, collision.collider);
        }

        private void HandleChargeHitWall(Collision2D collision)
        {
            playerFlash?.Flash(Color.white, playerFlashDuration);
            CameraShake.GetOrCreate()?.Pulse(chargeCrashShakeDuration, chargeCrashShakeAmplitude);
            HitStop.GetOrCreate()?.Trigger(chargeCrashHitStopDuration);
            Haptics.GetOrCreate()?.Pulse(chargeCrashHapticDuration, chargeCrashHapticLowFrequency, chargeCrashHapticHighFrequency);

            PlayChargeAoe(collision.GetContact(0).point, collision.collider);
        }

        /// <summary>
        /// Ends the Dash right at its Crash — no separate landing impact
        /// plays here, since the Crash (HandleChargeHitDamageable/
        /// HandleChargeHitWall above) already delivered equivalent impact
        /// feedback at the same point.
        /// </summary>
        private void FinishDashAfterCrash()
        {
            EndDashCommon();

            rb.position -= Vector2.up * liftoffHeightOffset;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;

            state = State.Grounded;
        }

        /// <summary>
        /// Cleanup shared by both ways the Dash can end (Crash or Cancel):
        /// stop driving movement, release the Charge text popup if it's
        /// still growing, and turn off the Dash's ambient feedback loops.
        /// </summary>
        private void EndDashCommon()
        {
            controller.SetVelocityOverride(null);
            chargeTextPopupHandle?.Release();
            chargeTextPopupHandle = null;
            ghostTrail?.StopTrail();
            CameraShake.GetOrCreate()?.SetShaking(false);
            FullScreenAbilityEffect.Instance?.SetFlightWindActive(false);
            controller.SetFlightMode(false);
        }
    }
}
