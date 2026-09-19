using System.Collections;
using UnityEngine;

namespace HeroFangame.Core
{
    /// <summary>
    /// Brief, smooth squash-and-recover on this transform's localScale,
    /// triggered on demand via PlaySquash(direction) from whichever
    /// hit/knockback logic wants it (regular punch, Heat Vision push-back,
    /// Freeze Breath ice break). The squash is oriented by the incoming hit
    /// direction: the object stretches along the axis it's being hit/pushed
    /// along and squishes along the perpendicular axis (e.g. a purely
    /// horizontal hit stretches horizontally and squishes vertically; a
    /// purely vertical hit does the opposite; a diagonal hit blends between
    /// the two per-axis rather than rotating the transform, since this
    /// GameObject's Collider2D/Rigidbody2D live on the same transform and
    /// rotating it would skew the physics shape). Animates on unscaled time
    /// so it plays out smoothly even during a HitStop freeze-frame, same
    /// convention as CameraShake. Restart-safe like DamageFlashAndDestroy's
    /// flash routine: a new PlaySquash() call while one is already running
    /// cancels and restarts cleanly from the base scale instead of
    /// compounding.
    /// </summary>
    public class HitSquashEffect : MonoBehaviour
    {
        [SerializeField] private float squashDuration = 0.14f;
        [SerializeField] private float stretchFactor = 1.2f;
        [SerializeField] private float squishFactor = 0.8f;

        private Vector3 baseScale;
        private Coroutine routine;

        private void Awake()
        {
            baseScale = transform.localScale;
        }

        /// <summary>
        /// Plays the squash, stretching along <paramref name="direction"/>
        /// and squishing perpendicular to it. Pass Vector2.zero (or leave
        /// default) for the original axis-agnostic horizontal squash.
        /// </summary>
        public void PlaySquash(Vector2 direction = default)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
            }
            transform.localScale = baseScale;
            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            routine = StartCoroutine(SquashRoutine(dir));
        }

        private IEnumerator SquashRoutine(Vector2 direction)
        {
            // Blend each axis between the squish and stretch factor based on
            // how much of the hit direction lies along it, instead of
            // rotating the transform to line up with an arbitrary angle.
            float targetScaleX = Mathf.Lerp(squishFactor, stretchFactor, Mathf.Abs(direction.x));
            float targetScaleY = Mathf.Lerp(squishFactor, stretchFactor, Mathf.Abs(direction.y));

            float t = 0f;
            while (t < squashDuration)
            {
                t += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(t / squashDuration);
                // 0 -> 1 -> 0 single arc: squash peaks mid-way, then eases
                // smoothly back to the base scale by the end.
                float curve = Mathf.Sin(normalized * Mathf.PI);
                transform.localScale = new Vector3(
                    Mathf.Lerp(baseScale.x, baseScale.x * targetScaleX, curve),
                    Mathf.Lerp(baseScale.y, baseScale.y * targetScaleY, curve),
                    baseScale.z);
                yield return null;
            }
            transform.localScale = baseScale;
            routine = null;
        }
    }
}
