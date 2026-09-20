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

        // Reusable buffers for the NonAlloc Physics2D query variants below —
        // these queries run every frame while Heat Vision / Freeze Breath is
        // held, so a fresh managed array per call would otherwise churn GC
        // continuously. Sized generously for expected on-screen hit counts;
        // results are trimmed to the actual hit count returned by Unity.
        private static readonly RaycastHit2D[] raycastBuffer = new RaycastHit2D[32];
        private static readonly Collider2D[] colliderBuffer = new Collider2D[32];

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
            int hitCount = Physics2D.BoxCastNonAlloc(origin, boxThickness, angle, direction, raycastBuffer, maxDistance, mask);

            bool found = false;
            RaycastHit2D closest = default;
            float closestDist = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                var h = raycastBuffer[i];
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
        /// via Damageable.TakeDamage to every hit, and returns the hit colliders
        /// via a shared internal buffer with <paramref name="hitCount"/> valid
        /// entries at the front — only indices [0, hitCount) are meaningful;
        /// callers must not hold onto the returned array past their own use,
        /// since it's reused by the next NonAlloc query.
        /// </summary>
        public static Collider2D[] OverlapBoxAndDamage(
            Vector2 center,
            Vector2 size,
            float angle,
            LayerMask mask,
            int damage,
            GameObject source,
            Vector2 knockbackDirection,
            float knockbackForce,
            out int hitCount)
        {
            hitCount = Physics2D.OverlapBoxNonAlloc(center, size, angle, colliderBuffer, mask);
            ApplyDamage(colliderBuffer, hitCount, damage, source, knockbackDirection, knockbackForce);
            return colliderBuffer;
        }

        public static Collider2D[] OverlapCircleAndDamage(
            Vector2 center,
            float radius,
            LayerMask mask,
            int damage,
            GameObject source,
            Vector2 knockbackDirection,
            float knockbackForce,
            out int hitCount)
        {
            hitCount = Physics2D.OverlapCircleNonAlloc(center, radius, colliderBuffer, mask);
            ApplyDamage(colliderBuffer, hitCount, damage, source, knockbackDirection, knockbackForce);
            return colliderBuffer;
        }

        /// <summary>
        /// Like OverlapCircleAndDamage, but knocks each hit away from
        /// <paramref name="center"/> individually instead of pushing every
        /// hit in one shared direction — used by Flight's liftoff/landing
        /// AOE, where surrounding enemies should scatter outward rather than
        /// all fly off the same way. <paramref name="exclude"/> optionally
        /// skips one collider entirely (damage, knockback, and callback) —
        /// used by Charge's crash splash so the primary target it already
        /// full-damaged via its own direct hit isn't also double-dipped by
        /// the secondary AOE pass.
        /// </summary>
        public static Collider2D[] OverlapCircleAndDamageRadial(
            Vector2 center,
            float radius,
            LayerMask mask,
            int damage,
            GameObject source,
            float knockbackForce,
            out int hitCount,
            System.Action<Collider2D, Vector2> onHit = null,
            Collider2D exclude = null)
        {
            hitCount = Physics2D.OverlapCircleNonAlloc(center, radius, colliderBuffer, mask);
            for (int i = 0; i < hitCount; i++)
            {
                var hit = colliderBuffer[i];
                if (hit == null || hit == exclude)
                {
                    continue;
                }
                var damageable = hit.GetComponentInParent<Damageable>();
                if (damageable == null)
                {
                    continue;
                }

                Vector2 direction = ((Vector2)hit.bounds.center - center);
                direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;

                damageable.TakeDamage(damage, new DamageInfo(source, direction, knockbackForce));

                var rb = hit.attachedRigidbody;
                if (rb != null && knockbackForce > 0f)
                {
                    rb.AddForce(direction * knockbackForce, ForceMode2D.Impulse);
                }

                onHit?.Invoke(hit, direction);
            }
            return colliderBuffer;
        }

        private static void ApplyDamage(
            Collider2D[] hits,
            int hitCount,
            int damage,
            GameObject source,
            Vector2 knockbackDirection,
            float knockbackForce)
        {
            var info = new DamageInfo(source, knockbackDirection, knockbackForce);
            for (int i = 0; i < hitCount; i++)
            {
                var hit = hits[i];
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
