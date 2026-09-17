using System;
using UnityEngine;

namespace HeroFangame.Core
{
    /// <summary>
    /// Info describing an incoming hit, passed through TakeDamage so
    /// attacks can carry knockback/source data without extra components.
    /// </summary>
    public struct DamageInfo
    {
        public GameObject Source;
        public Vector2 KnockbackDirection;
        public float KnockbackForce;

        public DamageInfo(GameObject source, Vector2 knockbackDirection, float knockbackForce)
        {
            Source = source;
            KnockbackDirection = knockbackDirection;
            KnockbackForce = knockbackForce;
        }
    }

    /// <summary>
    /// Shared HP/damage pipeline reused by EnemyRobot and destructible objects.
    /// Applies any IDamageModifier present on the same GameObject (e.g. bonus
    /// damage while frozen) before subtracting HP.
    /// </summary>
    [DisallowMultipleComponent]
    public class Damageable : MonoBehaviour
    {
        [SerializeField] private int maxHP = 3;

        public int MaxHP => maxHP;
        public int CurrentHP { get; private set; }
        public bool IsDead { get; private set; }

        public event Action<int> OnDamaged;
        public event Action OnDeath;

        private IDamageModifier damageModifier;

        private void Awake()
        {
            CurrentHP = maxHP;
            damageModifier = GetComponent<IDamageModifier>();
        }

        public void TakeDamage(int amount, DamageInfo info)
        {
            if (IsDead || amount <= 0)
            {
                return;
            }

            float multiplier = damageModifier != null ? damageModifier.GetDamageMultiplier() : 1f;
            int finalAmount = Mathf.Max(1, Mathf.RoundToInt(amount * multiplier));

            CurrentHP = Mathf.Max(0, CurrentHP - finalAmount);
            OnDamaged?.Invoke(finalAmount);

            if (CurrentHP == 0)
            {
                IsDead = true;
                OnDeath?.Invoke();
            }
        }
    }
}
