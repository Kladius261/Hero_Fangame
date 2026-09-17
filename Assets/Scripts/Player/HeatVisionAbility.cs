using UnityEngine;
using HeroFangame.Combat;

namespace HeroFangame.Player
{
    /// <summary>
    /// Long-range narrow frontal attack. A tap fires one instant, piercing
    /// hit. Continuing to hold turns it into a sustained beam that ticks
    /// damage and continuously drains Power, auto-cutting off if Power
    /// reaches 0 mid-hold.
    /// </summary>
    [RequireComponent(typeof(PlayerInputHandler))]
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PowerGauge))]
    public class HeatVisionAbility : MonoBehaviour
    {
        [Header("Beam Shape")]
        [SerializeField] private Vector2 beamSize = new Vector2(6f, 0.35f);
        [SerializeField] private float beamOffset = 0.6f;
        [SerializeField] private LayerMask hittableLayers;

        [Header("Damage")]
        [SerializeField] private int tapDamage = 3;
        [SerializeField] private int beamTickDamage = 1;
        [SerializeField] private float beamTickInterval = 0.15f;

        [Header("Power Cost")]
        [SerializeField] private float tapPowerCost = 10f;
        [SerializeField] private float holdPowerCostPerSecond = 30f;

        private PlayerInputHandler input;
        private PlayerController controller;
        private PowerGauge power;

        private bool wasHeld;
        private float tickTimer;

        private void Awake()
        {
            input = GetComponent<PlayerInputHandler>();
            controller = GetComponent<PlayerController>();
            power = GetComponent<PowerGauge>();
        }

        private void Update()
        {
            bool isHeld = input.HeatVisionHeld;

            if (isHeld && !wasHeld)
            {
                // Tap: instant shot.
                if (power.TrySpend(tapPowerCost))
                {
                    Fire(tapDamage);
                }
                tickTimer = beamTickInterval;
            }
            else if (isHeld && wasHeld)
            {
                // Hold: sustained beam.
                float drained = power.DrainOverTime(holdPowerCostPerSecond);
                if (drained > 0f)
                {
                    tickTimer -= Time.deltaTime;
                    if (tickTimer <= 0f)
                    {
                        Fire(beamTickDamage);
                        tickTimer = beamTickInterval;
                    }
                }
            }

            wasHeld = isHeld && power.Current > 0f;
        }

        private void Fire(int damage)
        {
            Vector2 origin = (Vector2)transform.position + controller.Facing * (beamOffset + beamSize.x * 0.5f);
            float angle = Vector2.SignedAngle(Vector2.right, controller.Facing);

            AttackUtility.OverlapBoxAndDamage(
                origin,
                beamSize,
                angle,
                hittableLayers,
                damage,
                gameObject,
                controller.Facing,
                knockbackForce: 4f);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector2 facing = Application.isPlaying && controller != null ? controller.Facing : Vector2.right;
            Vector2 origin = (Vector2)transform.position + facing * (beamOffset + beamSize.x * 0.5f);
            Gizmos.color = Color.cyan;
            Gizmos.matrix = Matrix4x4.TRS(origin, Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector2.right, facing)), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, beamSize);
        }
#endif
    }
}
