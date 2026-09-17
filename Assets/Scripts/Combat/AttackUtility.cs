using UnityEngine;
using HeroFangame.Core;

namespace HeroFangame.Combat
{
    /// <summary>
    /// Shared overlap-query + damage-application helper used by punches,
    /// Heat Vision, and Freeze Breath so all three attacks stay consistent.
    /// </summary>
    public static class AttackUtility
    {
        /// <summary>
        /// Runs an OverlapBox against the given mask, applies damage + knockback
        /// via Damageable.TakeDamage to every hit, and returns the hit colliders.
        /// </summary>
        public static Collider2D[] OverlapBoxAndDamage(
            Vector2 center,
            Vector2 size,
            float angle,
            LayerMask mask,
            int damage,
            GameObject source,
            Vector2 knockbackDirection,
            float knockbackForce)
        {
            Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, angle, mask);
            ApplyDamage(hits, damage, source, knockbackDirection, knockbackForce);
            return hits;
        }

        public static Collider2D[] OverlapCircleAndDamage(
            Vector2 center,
            float radius,
            LayerMask mask,
            int damage,
            GameObject source,
            Vector2 knockbackDirection,
            float knockbackForce)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius, mask);
            ApplyDamage(hits, damage, source, knockbackDirection, knockbackForce);
            return hits;
        }

        private static void ApplyDamage(
            Collider2D[] hits,
            int damage,
            GameObject source,
            Vector2 knockbackDirection,
            float knockbackForce)
        {
            var info = new DamageInfo(source, knockbackDirection, knockbackForce);
            foreach (var hit in hits)
            {
                if (hit == null)
                {
                    continue;
                }
                var damageable = hit.GetComponentInParent<Damageable>();
                if (damageable == null)
                {
                    continue;
                }
                damageable.TakeDamage(damage, info);

                var rb = hit.attachedRigidbody;
                if (rb != null && knockbackForce > 0f)
                {
                    rb.AddForce(knockbackDirection.normalized * knockbackForce, ForceMode2D.Impulse);
                }
            }
        }
    }
}
