using UnityEngine;
using HeroFangame.Combat;
using HeroFangame.Core;
using HeroFangame.Camera;

namespace HeroFangame.Player
{
    /// <summary>
    /// Short-to-medium range frontal cone, fired strictly horizontally
    /// (left or right, from the player's last horizontal facing — never
    /// up/down or diagonal), exactly like Heat Vision's beam. A tap applies
    /// a small amount of freeze exposure plus minor direct damage. Holding
    /// widens the cone and builds sustained freeze exposure (via
    /// IFreezable) while continuously draining Power. Low damage and
    /// deliberately zero knockback: targets are meant to be immobilized by
    /// the cold (see IFreezable/EnemyRobot), not physically pushed around
    /// like Heat Vision's beam. Like Heat Vision, locks player movement
    /// (and facing) for as long as the cone is active, so the player must
    /// stop breathing to reposition or flip the breath's direction.
    /// </summary>
    [RequireComponent(typeof(PlayerInputHandler))]
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PowerGauge))]
    [RequireComponent(typeof(AbilityLock))]
    public class FreezeBreathAbility : MonoBehaviour
    {
        [Header("Cone Shape")]
        [SerializeField] private Vector2 tapConeSize = new Vector2(3.5f, 1.0f);
        [SerializeField] private Vector2 holdConeSize = new Vector2(5f, 1.4f);
        [SerializeField] private float coneOffset = 0.5f;
        [SerializeField] private LayerMask hittableLayers;

        [Header("Damage & Exposure")]
        [SerializeField] private int tapDamage = 1;
        [SerializeField] private float tapExposure = 15f;
        [SerializeField] private float holdExposurePerSecond = 40f;

        [Header("Power Cost")]
        [SerializeField] private float tapPowerCost = 8f;
        [SerializeField] private float holdPowerCostPerSecond = 25f;

        [Header("Visuals")]
        [SerializeField] private float tapPulseDuration = 0.15f;
        [SerializeField] private FreezeBreathConeEffect coneEffect;
        [SerializeField] private float breathShakeDuration = 0.08f;

        private PlayerInputHandler input;
        private PlayerController controller;
        private PowerGauge power;
        private AbilityLock abilityLock;
        private CameraShake cameraShake;

        private bool wasHeld;
        private float tapPulseTimeRemaining;
        private bool isConeActive;

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
        }

        private void Update()
        {
            bool rawHeld = input.FreezeBreathHeld;
            bool isHeld = abilityLock.CanActivate(this, rawHeld);

            if (!isHeld && tapPulseTimeRemaining > 0f)
            {
                // Tap already fired; keep its brief cloud pulse alive for a
                // few frames instead of cutting it off instantly.
                tapPulseTimeRemaining -= Time.deltaTime;
                if (tapPulseTimeRemaining <= 0f)
                {
                    StopConeVisual();
                }
                wasHeld = false;
                return;
            }

            bool activatedThisFrame = false;

            if (isHeld && !wasHeld)
            {
                if (power.TrySpend(tapPowerCost))
                {
                    ApplyCone(tapConeSize, tapDamage, tapExposure, isTap: true, isNewActivation: true);
                    tapPulseTimeRemaining = tapPulseDuration;

                    // Single, brief shake on the moment of firing only —
                    // unlike Heat Vision's continuous shake while its beam
                    // is held, Freeze Breath shakes once per activation and
                    // stays still for the rest of the hold.
                    if (cameraShake == null)
                    {
                        cameraShake = CameraShake.GetOrCreate();
                    }
                    cameraShake?.Pulse(breathShakeDuration);
                    activatedThisFrame = true;
                }
            }
            else if (isHeld && wasHeld)
            {
                float drained = power.DrainOverTime(holdPowerCostPerSecond);
                if (drained > 0f)
                {
                    ApplyCone(holdConeSize, 0, holdExposurePerSecond * Time.deltaTime, isTap: false, isNewActivation: false);
                    activatedThisFrame = true;
                }
                else
                {
                    StopConeVisual();
                }
            }
            else
            {
                StopConeVisual();
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

        /// <summary>
        /// Freeze Breath only ever fires straight left or right (never
        /// up/down or diagonal), regardless of the player's full analog
        /// Facing — mirrors HeatVisionAbility.GetBeamDirection().
        /// </summary>
        private Vector2 GetBreathDirection()
        {
            return controller.Facing.x < 0f ? Vector2.left : Vector2.right;
        }

        private void ApplyCone(Vector2 size, int damage, float exposure, bool isTap, bool isNewActivation)
        {
            Vector2 dir = GetBreathDirection();

            // Never let the cone reach past the edge of the camera's
            // current view — enemies further along the level haven't
            // scrolled into frame yet and shouldn't be hittable before the
            // player can even see them.
            float desiredFarDistance = coneOffset + size.x;
            float availableDistance = CameraViewBounds.GetDistanceToEdge(transform.position.x, dir.x);
            bool clampedByScreen = availableDistance < desiredFarDistance;
            float clampedFarDistance = Mathf.Min(desiredFarDistance, availableDistance);
            size.x = Mathf.Max(0f, clampedFarDistance - coneOffset);

            if (size.x <= 0f)
            {
                // Player is already right at (or past) the screen edge in
                // this direction — nothing on-screen left to reach, so
                // skip the attack and its visuals entirely rather than
                // firing a degenerate zero-width box.
                StopConeVisual();
                return;
            }

            controller.LockMovement(this);

            Vector2 origin = (Vector2)transform.position + dir * (coneOffset + size.x * 0.5f);
            float angle = Vector2.SignedAngle(Vector2.right, dir);

            var hits = AttackUtility.OverlapBoxAndDamage(
                origin,
                size,
                angle,
                hittableLayers,
                damage,
                gameObject,
                dir,
                knockbackForce: 0f,
                out int hitCount);

            for (int i = 0; i < hitCount; i++)
            {
                var freezable = hits[i].GetComponentInParent<IFreezable>();
                freezable?.AddFreezeExposure(exposure, isNewActivation);
            }

            isConeActive = true;

            // Visually clip the cone at the nearest hittable surface so the
            // mist wraps around the target instead of visibly passing
            // through it — same convention as HeatVisionAbility's beam
            // clipping. Damage/exposure above is unaffected: it still uses
            // the full box, so every enemy inside it is still hit.
            Vector2 nearEdge = (Vector2)transform.position + dir * coneOffset;
            var probe = AttackUtility.BoxCastSinglePeek(nearEdge, new Vector2(size.y, size.y), dir, size.x, hittableLayers);
            float visualLength = probe.DidHit ? Mathf.Max(probe.Distance, 0.5f) : size.x;
            Vector2 visualSize = new Vector2(visualLength, size.y);
            Vector2 visualOrigin = nearEdge + dir * (visualLength * 0.5f);

            coneEffect?.SetCone(visualOrigin, visualSize, angle, isTap);

            if (clampedByScreen)
            {
                // The cone's reach was cut short by the camera edge rather
                // than a physical obstacle — billow the mist/streaks against
                // an invisible wall right at that boundary instead of just
                // letting their emission shape end abruptly.
                Vector2 boundaryPoint = (Vector2)transform.position + dir * clampedFarDistance;
                coneEffect?.SetBoundaryContact(boundaryPoint, dir);
            }
            else
            {
                coneEffect?.StopBoundaryContact();
            }
        }

        private void StopConeVisual()
        {
            controller.UnlockMovement(this);

            if (!isConeActive)
            {
                return;
            }
            isConeActive = false;
            coneEffect?.StopCone();
            coneEffect?.StopBoundaryContact();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector2 facing = Application.isPlaying && controller != null ? GetBreathDirection() : Vector2.right;
            Vector2 origin = (Vector2)transform.position + facing * (coneOffset + holdConeSize.x * 0.5f);
            Gizmos.color = Color.blue;
            Gizmos.matrix = Matrix4x4.TRS(origin, Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector2.right, facing)), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, holdConeSize);
        }
#endif
    }
}
