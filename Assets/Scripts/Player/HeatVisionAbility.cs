using UnityEngine;
using HeroFangame.Combat;
using HeroFangame.Core;
using HeroFangame.Camera;
using BeamHitResult = HeroFangame.Combat.AttackUtility.BeamHitResult;

namespace HeroFangame.Player
{
    /// <summary>
    /// Long-range narrow attack, fired strictly horizontally (left or right,
    /// picked from the player's last horizontal facing — never up/down or
    /// diagonal) and far enough to reach across the screen from anywhere in
    /// the level. A tap fires one instant hit against only the first
    /// (closest) target the beam touches. Continuing to hold turns it into a
    /// sustained beam that keeps hitting only that same single-target rule,
    /// ticks damage, drains Power (auto-cutting off at 0), and knocks its
    /// current target back on first contact and then every second of
    /// continuous contact after that. Drives a beam visual, a smoke/fire
    /// impact effect, and a subtle continuous camera shake while firing.
    /// </summary>
    [RequireComponent(typeof(PlayerInputHandler))]
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PowerGauge))]
    [RequireComponent(typeof(AbilityLock))]
    public class HeatVisionAbility : MonoBehaviour
    {
        [Header("Beam Shape")]
        [SerializeField] private Vector2 beamSize = new Vector2(6f, 0.35f);
        [SerializeField] private float beamRange = 50f;
        [SerializeField] private float beamOffset = 0.6f;
        [SerializeField] private LayerMask hittableLayers;

        [Header("Damage")]
        [SerializeField] private int tapDamage = 3;
        [SerializeField] private int beamTickDamage = 1;
        [SerializeField] private float beamTickInterval = 0.15f;

        [Header("Knockback")]
        [SerializeField] private float knockbackForce = 6f;
        [SerializeField] private float knockbackRepeatInterval = 1f;

        [Header("Power Cost")]
        [SerializeField] private float tapPowerCost = 10f;
        [SerializeField] private float holdPowerCostPerSecond = 30f;

        [Header("Visuals")]
        [SerializeField] private float tapPulseDuration = 0.12f;
        [SerializeField] private HeatVisionBeamRenderer beamRenderer;
        [SerializeField] private HeatVisionImpactEffect impactEffect;

        [Header("Audio")]
        [SerializeField] private AudioSource beamAudioSource;

        [Header("Aim")]
        [SerializeField] private float aimSweepSpeedDegreesPerSecond = 180f;

        private PlayerInputHandler input;
        private PlayerController controller;
        private PowerGauge power;
        private AbilityLock abilityLock;
        private CameraShake cameraShake;
        private AbilityAimController aimController;

        private bool wasHeld;
        private float tickTimer;
        private float tapPulseTimeRemaining;
        private bool isBeamActive;

        private Collider2D currentContactTarget;
        private float contactKnockbackTimer;

        private void Awake()
        {
            input = GetComponent<PlayerInputHandler>();
            controller = GetComponent<PlayerController>();
            power = GetComponent<PowerGauge>();
            abilityLock = GetComponent<AbilityLock>();
            if (abilityLock == null)
            {
                abilityLock = gameObject.AddComponent<AbilityLock>();
            }
            aimController = new AbilityAimController(aimSweepSpeedDegreesPerSecond);
        }

        private void Update()
        {
            bool rawHeld = input.HeatVisionHeld;
            bool isHeld = abilityLock.CanActivate(this, rawHeld);

            if (!isHeld && tapPulseTimeRemaining > 0f)
            {
                // Tap already fired; keep its brief beam/shake pulse alive
                // for a few frames instead of cutting it off instantly.
                tapPulseTimeRemaining -= Time.deltaTime;
                if (tapPulseTimeRemaining <= 0f)
                {
                    StopBeamVisuals();
                }
                wasHeld = false;
                return;
            }

            bool activatedThisFrame = false;

            if (isHeld && !wasHeld)
            {
                // Tap: instant shot against the first target only.
                if (power.TrySpend(tapPowerCost))
                {
                    FireTap();
                    activatedThisFrame = true;
                }
                tickTimer = beamTickInterval;
            }
            else if (isHeld && wasHeld)
            {
                // Hold: sustained beam. Resolve aim/contact every frame so
                // the visual, impact VFX, and knockback cadence stay in
                // sync with the target, but only apply damage on the
                // existing tick cadence.
                float drained = power.DrainOverTime(holdPowerCostPerSecond);
                if (drained > 0f)
                {
                    tickTimer -= Time.deltaTime;
                    bool doDamageTick = tickTimer <= 0f;
                    if (doDamageTick)
                    {
                        tickTimer = beamTickInterval;
                    }
                    ResolveHoldFrame(doDamageTick);
                    activatedThisFrame = true;
                }
                else
                {
                    StopBeamVisuals();
                }
            }
            else
            {
                StopBeamVisuals();
            }

            // Deliberately NOT "isHeld && power.Current > 0f" — that let a
            // failed tap (insufficient power) silently flip back to the
            // cheaper hold-drain path the instant Current ticked up by even
            // a fraction from regen, letting a continuously-held press
            // stutter-fire on trickles of power instead of requiring the
            // full tap cost again. Gating on whether we actually activated
            // this frame means an empty gauge truly locks the ability out
            // (while still held) until enough power has regenerated to
            // afford a fresh full-cost tap.
            wasHeld = isHeld && activatedThisFrame;
        }

        private BeamHitResult Probe(Vector2 origin, Vector2 dir, out bool hitScreenEdge)
        {
            // Never let the beam reach past the edge of the camera's
            // current view — enemies further along the level haven't
            // scrolled into frame yet and shouldn't be hittable before the
            // player can even see them.
            float screenEdgeDistance = CameraViewBounds.GetDistanceToEdge(origin.x, dir.x);
            float maxDistance = Mathf.Min(beamRange, screenEdgeDistance);
            var result = AttackUtility.BoxCastSinglePeek(origin, new Vector2(beamSize.y, beamSize.y), dir, maxDistance, hittableLayers);

            // True only when nothing was actually hit AND the beam's reach
            // was cut short specifically by the screen edge (not merely by
            // its own beamRange running out) — lets callers show a scorch
            // impact right at the boundary instead of the beam just
            // vanishing into nothing.
            hitScreenEdge = !result.DidHit && screenEdgeDistance < beamRange;
            return result;
        }

        /// <summary>
        /// Heat Vision fires along the player's horizontal Facing, tilted
        /// up/down by the Up/Down arrow keys via <see cref="aimController"/>
        /// (see AbilityAimController for the exact aim rules).
        /// </summary>
        private Vector2 GetBeamDirection(bool isNewActivation)
        {
            return aimController.Resolve(isNewActivation, input.MoveInput, controller.Facing, Time.deltaTime);
        }

        private void FireTap()
        {
            Vector2 dir = GetBeamDirection(isNewActivation: true);
            Vector2 origin = (Vector2)transform.position + dir * beamOffset;
            var hit = Probe(origin, dir, out bool hitScreenEdge);

            if (hit.Target != null)
            {
                hit.Target.TakeDamage(tapDamage, new DamageInfo(gameObject, dir, knockbackForce));
                var rb = hit.Collider.attachedRigidbody;
                if (rb != null)
                {
                    rb.AddForce(dir * knockbackForce, ForceMode2D.Impulse);
                    hit.Collider.GetComponentInParent<HitSquashEffect>()?.PlaySquash(dir);
                }
            }

            tapPulseTimeRemaining = tapPulseDuration;
            ActivateBeamVisual(origin, dir, hit, hitScreenEdge);
            SetCameraShake(true);
        }

        private void ResolveHoldFrame(bool doDamageTick)
        {
            Vector2 dir = GetBeamDirection(isNewActivation: false);
            Vector2 origin = (Vector2)transform.position + dir * beamOffset;
            var hit = Probe(origin, dir, out bool hitScreenEdge);

            ActivateBeamVisual(origin, dir, hit, hitScreenEdge);
            SetCameraShake(true);

            Collider2D hitCollider = hit.DidHit ? hit.Collider : null;
            bool isNewContact = hitCollider != currentContactTarget;
            if (isNewContact)
            {
                currentContactTarget = hitCollider;
                contactKnockbackTimer = 0f;
            }

            bool applyKnockback = false;
            if (hitCollider != null)
            {
                contactKnockbackTimer += Time.deltaTime;
                if (isNewContact || contactKnockbackTimer >= knockbackRepeatInterval)
                {
                    applyKnockback = true;
                    contactKnockbackTimer = 0f;
                }
            }

            if (doDamageTick && hit.Target != null)
            {
                hit.Target.TakeDamage(beamTickDamage, new DamageInfo(gameObject, dir, applyKnockback ? knockbackForce : 0f));
            }

            if (applyKnockback && hit.Target != null)
            {
                var rb = hit.Collider.attachedRigidbody;
                if (rb != null)
                {
                    rb.AddForce(dir * knockbackForce, ForceMode2D.Impulse);
                    hit.Collider.GetComponentInParent<HitSquashEffect>()?.PlaySquash(dir);
                }
            }
        }

        private void ActivateBeamVisual(Vector2 origin, Vector2 dir, BeamHitResult hit, bool hitScreenEdge)
        {
            if (!isBeamActive)
            {
                beamAudioSource?.Play();
            }
            isBeamActive = true;
            controller.LockMovement(this);
            if (beamRenderer != null)
            {
                beamRenderer.SetActive(true);
                beamRenderer.UpdateBeam(origin, dir, hit.Distance);
            }

            if (impactEffect != null)
            {
                if (hit.DidHit || hitScreenEdge)
                {
                    // hit.Point already equals the clamped screen-edge point
                    // when nothing was actually hit (AttackUtility falls
                    // back to origin + direction * maxDistance), so the same
                    // fire/burn/sparkle impact effect used for real contact
                    // doubles as a "hit the edge of the screen" scorch mark.
                    impactEffect.UpdateContact(hit.Point, dir);
                }
                else
                {
                    impactEffect.StopContact();
                }
            }
        }

        private void StopBeamVisuals()
        {
            currentContactTarget = null;
            contactKnockbackTimer = 0f;

            if (!isBeamActive)
            {
                return;
            }

            isBeamActive = false;
            controller.UnlockMovement(this);
            beamRenderer?.SetActive(false);
            impactEffect?.StopContact();
            beamAudioSource?.Stop();
            SetCameraShake(false);
        }

        private void SetCameraShake(bool active)
        {
            if (cameraShake == null)
            {
                cameraShake = CameraShake.GetOrCreate();
            }
            cameraShake?.SetShaking(active);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector2 facing = Application.isPlaying && controller != null ? aimController.CurrentDirection(controller.Facing) : Vector2.right;
            Vector2 origin = (Vector2)transform.position + facing * beamOffset;
            Vector2 end = origin + facing * beamRange;
            float angle = Vector2.SignedAngle(Vector2.right, facing);

            Gizmos.color = Color.cyan;
            Gizmos.matrix = Matrix4x4.TRS((origin + end) * 0.5f, Quaternion.Euler(0, 0, angle), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(beamRange, beamSize.y, 0f));
        }
#endif
    }
}
