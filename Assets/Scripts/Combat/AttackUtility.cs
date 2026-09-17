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
        /// Result of a single-target beam query: whether anything was hit, the
        /// resolved Damageable (if any), the exact surface point (for beam
        /// clipping / VFX placement), and the collider (for contact tracking).
        /// </summary>
        public struct BeamHitResult
        {
            public bool DidHit;
            public Collider2D Collider;
            public Damageable Target;
            public Vector2 Point;
            public float Distance;
        }

        /// <summary>
        /// Sweeps a thin box along <paramref name="direction"/> for
        /// <paramref name="maxDistance"/> and returns only the single closest
        /// hit, with no damage/knockback side effects, so callers can apply
        /// their own damage/knockback cadence (e.g. Heat Vision's single-target,
        /// per-second-knockback beam). Unlike OverlapBoxAndDamage below, this
        /// never hits more than one target.
        /// </summary>
        public static BeamHitResult BoxCastSinglePeek(
            Vector2 origin,
            Vector2 boxThickness,
            Vector2 direction,
            float maxDistance,
            LayerMask mask)
        {
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            float angle = Vector2.SignedAngle(Vector2.right, direction);
            RaycastHit2D[] hits = Physics2D.BoxCastAll(origin, boxThickness, angle, direction, maxDistance, mask);

            bool found = false;
            RaycastHit2D closest = default;
            float closestDist = float.MaxValue;
            foreach (var h in hits)
            {
                if (h.collider == null)
                {
                    continue;
                }
                if (h.distance < closestDist)
                {
                    closestDist = h.distance;
                    closest = h;
                    found = true;
                }
            }

            return new BeamHitResult
            {
                DidHit = found,
                Collider = found ? closest.collider : null,
                Target = found ? closest.collider.GetComponentInParent<Damageable>() : null,
                Point = found ? closest.point : origin + direction * maxDistance,
                Distance = found ? closest.distance : maxDistance,
            };
        }

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
