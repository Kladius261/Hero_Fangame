using UnityEngine;
using HeroFangame.Combat;
using HeroFangame.Core;
using HeroFangame.Camera;

namespace HeroFangame.Player
{
    /// <summary>
    /// Reworks the old Shift burst-dash into a held flight stance. Holding
    /// Flight (Shift) lifts the player, doubles move speed, disables all
    /// offensive abilities (see the IsFlightMode guards in PlayerCombat /
    /// HeatVisionAbility / FreezeBreathAbility), and knocks back/damages
    /// nearby enemies on liftoff. While flying, double-tapping an arrow key
    /// fires an uninterruptible Charge dash with a ghost trail that ends the
    /// instant it hits anything (impact feedback + a brief re-charge
    /// cooldown), returning to a normal hover afterward. Releasing Flight
    /// while hovering — whether idle or just after a Charge — is what
    /// actually triggers landing.
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

        [Header("Charge")]
        [SerializeField] private float doubleTapWindow = 0.3f;
        [SerializeField] private float chargeSpeed = 30f;
        [SerializeField] private int chargeDamage = 3;
        [SerializeField] private float chargeKnockbackForce = 14f;
        [SerializeField] private float chargeCrashShakeDuration = 0.15f;
        [SerializeField] private float chargeCrashShakeAmplitude = 1.75f;
        [SerializeField] private float chargeCrashHitStopDuration = 0.08f;
        [SerializeField] private float playerFlashDuration = 0.1f;
        [Tooltip("After a Charge ends the player returns to hovering (landing is no longer forced). This briefly blocks a new double-tap from immediately chaining into another Charge.")]
        [SerializeField] private float chargeCooldown = 0.4f;

        [Header("Components")]
        [SerializeField] private GhostTrailEffect ghostTrail;
        [SerializeField] private FlashEffect playerFlash;

        [Header("VFX")]
        [SerializeField] private ParticleSystem flightAuraVFX;
        [SerializeField] private ParticleSystem flightAuraRingsVFX;
        [SerializeField] private ParticleSystem flightPropulsionVFX;
        [SerializeField] private ParticleSystem flightLandingVFX;
        [SerializeField] private ParticleSystem flightLandingDebrisVFX;
        [SerializeField] private ParticleSystem flightCrashVFX;
        [SerializeField] private ParticleSystem flightChargeDebrisVFX;

        private PlayerInputHandler input;
        private PlayerController controller;
        private Rigidbody2D rb;

        private State state;
        private Vector2 chargeDirection;

        private float hoverBobTimer;
        private float hoverBobOffset;

        private bool prevUp, prevDown, prevLeft, prevRight;
        private float lastTapTimeUp = -100f, lastTapTimeDown = -100f, lastTapTimeLeft = -100f, lastTapTimeRight = -100f;
        private float chargeCooldownUntil = -100f;

        private void Awake()
        {
            input = GetComponent<PlayerInputHandler>();
            controller = GetComponent<PlayerController>();
            rb = GetComponent<Rigidbody2D>();
        }

        private void Update()
        {
            switch (state)
            {
                case State.Grounded:
                    if (input.FlightHeld)
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
                    // Resolution happens via OnCollisionEnter2D below.
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
            bool up = move.y > 0.5f;
            bool down = move.y < -0.5f;
            bool left = move.x < -0.5f;
            bool right = move.x > 0.5f;

            bool charged =
                CheckDirection(up, prevUp, ref lastTapTimeUp, Vector2.up) ||
                CheckDirection(down, prevDown, ref lastTapTimeDown, Vector2.down) ||
                CheckDirection(left, prevLeft, ref lastTapTimeLeft, Vector2.left) ||
                CheckDirection(right, prevRight, ref lastTapTimeRight, Vector2.right);

            prevUp = up;
            prevDown = down;
            prevLeft = left;
            prevRight = right;

            if (charged)
            {
                // A charge just started this frame — clear the other
                // directions' held-state tracking so a diagonal double-tap
                // held alongside the charged axis can't immediately queue a
                // second charge attempt once this one lands.
                prevUp = prevDown = prevLeft = prevRight = false;
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
            flightPropulsionVFX?.Play();
            flightAuraVFX?.Play();
            flightAuraRingsVFX?.Play();

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
            flightLandingVFX?.Play();
            flightLandingDebrisVFX?.Play();
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
            chargeDirection = direction;
            controller.SetVelocityOverride(direction * chargeSpeed);
            CameraShake.GetOrCreate()?.SetShaking(true);
            ghostTrail?.StartTrail();
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
                HandleChargeHitWall();
            }

            PlayChargeDebris(collision.GetContact(0).point);
            EndCharge();
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
        }

        private void HandleChargeHitWall()
        {
            playerFlash?.Flash(Color.white, playerFlashDuration);
            CameraShake.GetOrCreate()?.Pulse(chargeCrashShakeDuration, chargeCrashShakeAmplitude);
            HitStop.GetOrCreate()?.Trigger(chargeCrashHitStopDuration);
        }

        private void EndCharge()
        {
            CameraShake.GetOrCreate()?.SetShaking(false);
            ghostTrail?.StopTrail();
            controller.SetVelocityOverride(null);
            chargeCooldownUntil = Time.unscaledTime + chargeCooldown;

            // The player decides when to land now — a Charge only stops the
            // dash and returns to a normal hover, it no longer forces Land().
            state = State.Flying;
        }

        private void ResetDoubleTapState()
        {
            prevUp = prevDown = prevLeft = prevRight = false;
            lastTapTimeUp = lastTapTimeDown = lastTapTimeLeft = lastTapTimeRight = -100f;
        }
    }
}
