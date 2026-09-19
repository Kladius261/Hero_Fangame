using UnityEngine;
using HeroFangame.Combat;
using HeroFangame.Core;

namespace HeroFangame.Player
{
    /// <summary>
    /// Two-button (A/S) punch combo. Each press advances a 3-step combo chain
    /// that resets after a short window of no input. Each step spawns a brief
    /// overlap hitbox in front of the player on the Enemy/Destructible layers.
    /// </summary>
    [RequireComponent(typeof(PlayerInputHandler))]
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PunchHitEffect))]
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Combo")]
        [SerializeField] private int comboLength = 3;
        [SerializeField] private float comboResetTime = 0.8f;
        [SerializeField] private int[] comboDamage = { 1, 1, 2 };

        [Header("Hitbox")]
        [SerializeField] private Vector2 hitboxSize = new Vector2(1.2f, 1f);
        [SerializeField] private float hitboxDistance = 0.8f;
        [SerializeField] private LayerMask hittableLayers;
        [SerializeField] private float knockbackForce = 6f;

        private PlayerInputHandler input;
        private PlayerController controller;
        private PunchHitEffect hitEffect;

        private int comboStep;
        private float comboTimer;

        private void Awake()
        {
            input = GetComponent<PlayerInputHandler>();
            controller = GetComponent<PlayerController>();
            hitEffect = GetComponent<PunchHitEffect>();
        }

        private void OnEnable()
        {
            input.OnPunchA += HandlePunch;
            input.OnPunchS += HandlePunch;
        }

        private void OnDisable()
        {
            input.OnPunchA -= HandlePunch;
            input.OnPunchS -= HandlePunch;
        }

        private void Update()
        {
            if (comboStep > 0)
            {
                comboTimer -= Time.deltaTime;
                if (comboTimer <= 0f)
                {
                    comboStep = 0;
                }
            }
        }

        private void HandlePunch()
        {
            int damage = comboDamage[Mathf.Clamp(comboStep, 0, comboDamage.Length - 1)];

            Vector2 origin = (Vector2)transform.position + controller.Facing * hitboxDistance;
            float angle = Vector2.SignedAngle(Vector2.right, controller.Facing);

            Collider2D[] hits = AttackUtility.OverlapBoxAndDamage(
                origin,
                hitboxSize,
                angle,
                hittableLayers,
                damage,
                gameObject,
                controller.Facing,
                knockbackForce,
                out int hitCount);

            for (int i = 0; i < hitCount; i++)
            {
                var hit = hits[i];
                if (hit == null)
                {
                    continue;
                }
                var damageable = hit.GetComponentInParent<Damageable>();
                if (damageable != null)
                {
                    hitEffect.PlayRandomAt(hit.bounds.center);
                }
            }

            comboStep = (comboStep + 1) % Mathf.Max(1, comboLength);
            comboTimer = comboResetTime;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector2 facing = Application.isPlaying && controller != null ? controller.Facing : Vector2.right;
            Vector2 origin = (Vector2)transform.position + facing * hitboxDistance;
            Gizmos.color = Color.red;
            Gizmos.matrix = Matrix4x4.TRS(origin, Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector2.right, facing)), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, hitboxSize);
        }
#endif
    }
}
