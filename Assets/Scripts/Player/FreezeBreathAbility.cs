using UnityEngine;
using HeroFangame.Combat;
using HeroFangame.Core;

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
    public class FreezeBreathAbility : MonoBehaviour
    {
        [Header("Cone Shape")]
        [SerializeField] private Vector2 tapConeSize = new Vector2(2.5f, 1.5f);
        [SerializeField] private Vector2 holdConeSize = new Vector2(3.5f, 2.2f);
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

        private PlayerInputHandler input;
        private PlayerController controller;
        private PowerGauge power;

        private bool wasHeld;
        private float tapPulseTimeRemaining;
        private bool isConeActive;

        private void Awake()
        {
            input = GetComponent<PlayerInputHandler>();
            controller = GetComponent<PlayerController>();
            power = GetComponent<PowerGauge>();
        }

        private void Update()
        {
            bool isHeld = input.FreezeBreathHeld;

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

            if (isHeld && !wasHeld)
            {
                if (power.TrySpend(tapPowerCost))
                {
                    ApplyCone(tapConeSize, tapDamage, tapExposure, isTap: true, isNewActivation: true);
                    tapPulseTimeRemaining = tapPulseDuration;
                }
            }
            else if (isHeld && wasHeld)
            {
                float drained = power.DrainOverTime(holdPowerCostPerSecond);
                if (drained > 0f)
                {
                    ApplyCone(holdConeSize, 0, holdExposurePerSecond * Time.deltaTime, isTap: false, isNewActivation: false);
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

            wasHeld = isHeld && power.Current > 0f;
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
            controller.LockMovement(this);

            Vector2 dir = GetBreathDirection();
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
                knockbackForce: 0f);

            foreach (var hit in hits)
            {
                var freezable = hit.GetComponentInParent<IFreezable>();
                freezable?.AddFreezeExposure(exposure, isNewActivation);
            }

            isConeActive = true;
            coneEffect?.SetCone(origin, size, angle, isTap);
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
