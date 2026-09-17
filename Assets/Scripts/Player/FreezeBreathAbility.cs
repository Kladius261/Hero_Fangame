using UnityEngine;
using HeroFangame.Combat;
using HeroFangame.Core;

namespace HeroFangame.Player
{
    /// <summary>
    /// Short-to-medium range frontal cone. A tap applies a small amount of
    /// freeze exposure plus minor direct damage. Holding widens the cone and
    /// builds sustained freeze exposure (via IFreezable) while continuously
    /// draining Power. Low damage / minimal knockback by design.
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

        private PlayerInputHandler input;
        private PlayerController controller;
        private PowerGauge power;

        private bool wasHeld;

        private void Awake()
        {
            input = GetComponent<PlayerInputHandler>();
            controller = GetComponent<PlayerController>();
            power = GetComponent<PowerGauge>();
        }

        private void Update()
        {
            bool isHeld = input.FreezeBreathHeld;

            if (isHeld && !wasHeld)
            {
                if (power.TrySpend(tapPowerCost))
                {
                    ApplyCone(tapConeSize, tapDamage, tapExposure);
                }
            }
            else if (isHeld && wasHeld)
            {
                float drained = power.DrainOverTime(holdPowerCostPerSecond);
                if (drained > 0f)
                {
                    ApplyCone(holdConeSize, 0, holdExposurePerSecond * Time.deltaTime);
                }
            }

            wasHeld = isHeld && power.Current > 0f;
        }

        private void ApplyCone(Vector2 size, int damage, float exposure)
        {
            Vector2 origin = (Vector2)transform.position + controller.Facing * (coneOffset + size.x * 0.5f);
            float angle = Vector2.SignedAngle(Vector2.right, controller.Facing);

            var hits = AttackUtility.OverlapBoxAndDamage(
                origin,
                size,
                angle,
                hittableLayers,
                damage,
                gameObject,
                controller.Facing,
                knockbackForce: 1f);

            foreach (var hit in hits)
            {
                var freezable = hit.GetComponentInParent<IFreezable>();
                freezable?.AddFreezeExposure(exposure);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector2 facing = Application.isPlaying && controller != null ? controller.Facing : Vector2.right;
            Vector2 origin = (Vector2)transform.position + facing * (coneOffset + holdConeSize.x * 0.5f);
            Gizmos.color = Color.blue;
            Gizmos.matrix = Matrix4x4.TRS(origin, Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector2.right, facing)), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, holdConeSize);
        }
#endif
    }
}
