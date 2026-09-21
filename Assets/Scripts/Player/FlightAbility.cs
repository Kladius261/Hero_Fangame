using UnityEngine;
using HeroFangame.Combat;
using HeroFangame.Core;
using HeroFangame.Camera;

namespace HeroFangame.Player
{
    /// <summary>
    /// Reworks the old Shift burst-dash into a held flight stance. Holding
    /// Flight (Space) lifts the player, doubles move speed, disables all
    /// offensive abilities (see the IsFlightMode guards in PlayerCombat /
    /// HeatVisionAbility / FreezeBreathAbility), and knocks back/damages
    /// nearby enemies on liftoff. The guard also runs the other way: while
    /// grounded, Space is ignored for as long as Heat Vision or Freeze
    /// Breath has PlayerController's movement locked (see
    /// PlayerController.IsMovementLocked), so Flight can't be entered
    /// mid-beam/mid-cone — it only becomes available again once the player
    /// releases D/S or the ability force-cuts itself from running out of
    /// Power. While flying, double-tapping Left or Right
    /// fires an uninterruptible Charge dash with a ghost trail; once firing,
    /// holding Up/Down steers the dash diagonally (see AbilityAimController)
    /// without ever being cancelable. It still ends the instant it hits
    /// anything (impact feedback + a brief re-charge cooldown), returning to
    /// a normal hover afterward. Releasing Flight while hovering — whether
    /// idle or just after a Charge — is what actually triggers landing.
    /// </summary>
    [RequireComponent(typeof(PlayerInputHandler))]
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class FlightAbility : MonoBehaviour
    {
        private enum State { Grounded, Flying, Charging }

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

        [Header("Landing Impact")]
        [Tooltip("Mirrors the liftoff AOE pulse (damage/knockback/squash) at the landing spot.")]
        [SerializeField] private float landingRadius = 3f;
        [SerializeField] private int landingDamage = 1;
        [SerializeField] private float landingKnockbackForce = 8f;

        [Header("Hover")]
        [Tooltip("Small continuous up/down drift applied while hovering, purely to sell the sense of levitation.")]
        [SerializeField] private float hoverBobAmplitude = 0.12f;
        [SerializeField] private float hoverBobFrequency = 1.4f;
        [Tooltip("Ghost trail spawn interval while hovering/flying normally — deliberately denser (smaller) than Charge's own interval (set on GhostTrailEffect), so the sustained flight trail reads as fuller/richer while Charge's brief dash trail stays comparatively sparse.")]
        [SerializeField] private float flightHoverGhostTrailInterval = 0.03f;

        [Header("Charge")]
        [SerializeField] private float doubleTapWindow = 0.3f;
        [SerializeField] private float chargeSpeed = 30f;
        [Tooltip("How fast Up/Down steers the dash once it's firing, in degrees/second, mirroring Heat Vision/Freeze Breath's aim sweep (see AbilityAimController).")]
        [SerializeField] private float chargeAimSweepSpeedDegreesPerSecond = 180f;
        [SerializeField] private int chargeDamage = 3;
        [SerializeField] private float chargeKnockbackForce = 14f;
        [Tooltip("Secondary splash damage/knockback around the crash point, hitting anything near the primary target/wall a Charge slams into (which itself still takes the full chargeDamage/chargeKnockbackForce above via its own direct hit).")]
        [SerializeField] private float chargeAoeRadius = 2.5f;
        [SerializeField] private int chargeAoeDamage = 1;
        [SerializeField] private float chargeAoeKnockbackForce = 8f;
        [SerializeField] private float chargeCrashShakeDuration = 0.15f;
        [SerializeField] private float chargeCrashShakeAmplitude = 1.75f;
        [SerializeField] private float chargeCrashHitStopDuration = 0.08f;
        [SerializeField] private float playerFlashDuration = 0.1f;
        [Tooltip("After a Charge ends the player returns to hovering (landing is no longer forced). This briefly blocks a new double-tap from immediately chaining into another Charge.")]
        [SerializeField] private float chargeCooldown = 0.4f;

        [Header("Components")]
        [SerializeField] private GhostTrailEffect ghostTrail;
        [SerializeField] private FlashEffect playerFlash;
        [Tooltip("Reused from enemy hit feedback: PlaySquash(direction) stretches along direction and squishes perpendicular. Vector2.up on liftoff reads as a stretch (tall/thin); Vector2.right on landing reads as a squash (wide/flat).")]
        [SerializeField] private HitSquashEffect playerSquash;

        [Header("VFX")]
        [SerializeField] private ParticleSystem flightAuraVFX;
        [SerializeField] private ParticleSystem flightAuraRingsVFX;
        [SerializeField] private ParticleSystem flightPropulsionVFX;
        [SerializeField] private ParticleSystem flightLandingVFX;
        [SerializeField] private ParticleSystem flightCrashVFX;
        [SerializeField] private ParticleSystem flightChargeDebrisVFX;

        private PlayerInputHandler input;
        private PlayerController controller;
        private Rigidbody2D rb;

        private State state;
        private Vector2 chargeDirection;
        private Vector2 chargeHorizontalFacing;
        private AbilityAimController chargeAimController;

        private float hoverBobTimer;
        private float hoverBobOffset;

        private bool prevLeft, prevRight;
        private float lastTapTimeLeft = -100f, lastTapTimeRight = -100f;
        private float chargeCooldownUntil = -100f;

        private void Awake()
        {
            input = GetComponent<PlayerInputHandler>();
            controller = GetComponent<PlayerController>();
            rb = GetComponent<Rigidbody2D>();
            chargeAimController = new AbilityAimController(chargeAimSweepSpeedDegreesPerSecond);
        }

        private void Update()
        {
            switch (state)
            {
                case State.Grounded:
                    if (input.FlightHeld && !controller.IsMovementLocked)
                    {
                        EnterFlight();
                    }
                    break;

                case State.Flying:
                    if (!input.FlightHeld)
                    {
                        Land();
                        break;
                    }
                    CheckDoubleTap();
                    break;

                case State.Charging:
                    // Uninterruptible: FlightHeld is ignored entirely here.
                    // Only Up/Down can steer (see AbilityAimController); the
                    // dash itself only ends via OnCollisionEnter2D below.
                    chargeDirection = chargeAimController.Resolve(isNewActivation: false, input.MoveInput, chargeHorizontalFacing, Time.deltaTime);
                    controller.SetVelocityOverride(chargeDirection * chargeSpeed);
                    break;
            }
        }

        private void FixedUpdate()
        {
            // Only actively hovers while calmly airborne — once a Charge
            // starts the bob simply freezes at whatever small offset it had
            // (never reset mid-dash), so the high-speed charge reads as a
            // clean straight line with no added vertical wobble.
            if (state != State.Flying)
            {
                return;
            }

            hoverBobTimer += Time.fixedDeltaTime;
            float target = Mathf.Sin(hoverBobTimer * hoverBobFrequency * Mathf.PI * 2f) * hoverBobAmplitude;
            rb.position += Vector2.up * (target - hoverBobOffset);
            hoverBobOffset = target;
        }

        private void CheckDoubleTap()
        {
            Vector2 move = input.MoveInput;
            bool left = move.x < -0.5f;
            bool right = move.x > 0.5f;

            bool charged =
                CheckDirection(left, prevLeft, ref lastTapTimeLeft, Vector2.left) ||
                CheckDirection(right, prevRight, ref lastTapTimeRight, Vector2.right);

            prevLeft = left;
            prevRight = right;

            if (charged)
            {
                // A charge just started this frame — clear the other
                // direction's held-state tracking so it can't immediately
                // queue a second charge attempt once this one lands.
                prevLeft = prevRight = false;
            }
        }

        private bool CheckDirection(bool held, bool wasHeld, ref float lastTapTime, Vector2 direction)
        {
            if (!held || wasHeld)
            {
                return false;
            }

            if (Time.unscaledTime - lastTapTime <= doubleTapWindow)
            {
                lastTapTime = -100f;

                // Still consumes the double-tap even during the post-Charge
                // recovery window, so a buffered tap can't fire a delayed
                // Charge the instant the cooldown expires.
                if (Time.unscaledTime < chargeCooldownUntil)
                {
                    return false;
                }

                StartCharge(direction);
                return true;
            }

            lastTapTime = Time.unscaledTime;
            return false;
        }

        private void EnterFlight()
        {
            state = State.Flying;
            controller.SetFlightMode(true);

            rb.position += Vector2.up * liftoffHeightOffset;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            hoverBobTimer = 0f;
            hoverBobOffset = 0f;

            CameraShake.GetOrCreate()?.Pulse(liftoffShakeDuration, liftoffShakeAmplitude);
            playerSquash?.PlaySquash(Vector2.up);
            flightPropulsionVFX?.Play();
            flightAuraVFX?.Play();
            flightAuraRingsVFX?.Play();
            ghostTrail?.StartTrail(flightHoverGhostTrailInterval);

            AttackUtility.OverlapCircleAndDamageRadial(
                transform.position,
                liftoffRadius,
                enemyLayer,
                liftoffDamage,
                gameObject,
                liftoffKnockbackForce,
                out _,
                (hit, direction) => hit.GetComponentInParent<HitSquashEffect>()?.PlaySquash(direction));

            ResetDoubleTapState();
        }

        private void Land()
        {
            controller.SetFlightMode(false);
            controller.SetVelocityOverride(null);

            // Cancel any residual hover bob before removing the liftoff
            // height offset, so landing always settles back to exactly the
            // pre-flight ground position regardless of where in the bob
            // cycle it was interrupted.
            rb.position -= Vector2.up * hoverBobOffset;
            hoverBobOffset = 0f;
            rb.position -= Vector2.up * liftoffHeightOffset;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;

            flightAuraVFX?.Stop();
            flightAuraRingsVFX?.Stop();
            ghostTrail?.StopTrail();
            flightLandingVFX?.Play();
            playerSquash?.PlaySquash(Vector2.right);
            CameraShake.GetOrCreate()?.Pulse(landingShakeDuration, landingShakeAmplitude);

            // Same radial knockback/damage/squash treatment as liftoff, so
            // landing reads as an equally impactful shockwave on anything
            // standing nearby.
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

        private void StartCharge(Vector2 direction)
        {
            state = State.Charging;
            chargeHorizontalFacing = direction;
            chargeDirection = chargeAimController.Resolve(isNewActivation: true, input.MoveInput, chargeHorizontalFacing, Time.deltaTime);
            controller.SetVelocityOverride(chargeDirection * chargeSpeed);
            CameraShake.GetOrCreate()?.SetShaking(true);
            // Force-restart even though the hover trail is already running:
            // switches it from the denser hover interval to Charge's own
            // (sparser) default interval, so the sudden burst of speed reads
            // distinctly from the sustained hover trail.
            ghostTrail?.StartTrail(restart: true);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (state != State.Charging)
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

            PlayChargeDebris(collision.GetContact(0).point);
            EndCharge();
        }

        /// <summary>
        /// Secondary splash around the crash point, hitting anything nearby
        /// besides whatever the Charge directly collided with (that primary
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

        private void PlayChargeDebris(Vector2 point)
        {
            if (flightChargeDebrisVFX == null)
            {
                return;
            }
            flightChargeDebrisVFX.transform.position = point;
            flightChargeDebrisVFX.Play();
        }

        private void HandleChargeHitDamageable(Damageable damageable, Collision2D collision)
        {
            Vector2 awayDir = (Vector2)collision.collider.bounds.center - (Vector2)transform.position;
            awayDir = awayDir.sqrMagnitude > 0.0001f ? awayDir.normalized : chargeDirection;

            damageable.TakeDamage(chargeDamage, new DamageInfo(gameObject, awayDir, chargeKnockbackForce));

            var hitRb = collision.collider.attachedRigidbody;
            if (hitRb != null)
            {
                hitRb.AddForce(awayDir * chargeKnockbackForce, ForceMode2D.Impulse);
            }
            collision.collider.GetComponentInParent<HitSquashEffect>()?.PlaySquash(awayDir);

            if (flightCrashVFX != null)
            {
                flightCrashVFX.transform.position = collision.GetContact(0).point;
                flightCrashVFX.Play();
            }

            CameraShake.GetOrCreate()?.Pulse(chargeCrashShakeDuration, chargeCrashShakeAmplitude);
            HitStop.GetOrCreate()?.Trigger(chargeCrashHitStopDuration);

            PlayChargeAoe(collision.GetContact(0).point, collision.collider);
        }

        private void HandleChargeHitWall(Collision2D collision)
        {
            playerFlash?.Flash(Color.white, playerFlashDuration);
            CameraShake.GetOrCreate()?.Pulse(chargeCrashShakeDuration, chargeCrashShakeAmplitude);
            HitStop.GetOrCreate()?.Trigger(chargeCrashHitStopDuration);

            PlayChargeAoe(collision.GetContact(0).point, collision.collider);
        }

        private void EndCharge()
        {
            CameraShake.GetOrCreate()?.SetShaking(false);
            // Back to Flying (not Grounded) — resume the denser hover trail
            // rather than stopping it outright.
            ghostTrail?.StartTrail(flightHoverGhostTrailInterval, restart: true);
            controller.SetVelocityOverride(null);
            chargeCooldownUntil = Time.unscaledTime + chargeCooldown;

            // The player decides when to land now — a Charge only stops the
            // dash and returns to a normal hover, it no longer forces Land().
            state = State.Flying;
        }

        private void ResetDoubleTapState()
        {
            prevLeft = prevRight = false;
            lastTapTimeLeft = lastTapTimeRight = -100f;
        }
    }
}
