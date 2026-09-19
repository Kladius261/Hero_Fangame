using System.Collections;
using UnityEngine;

namespace HeroFangame.Core
{
    /// <summary>
    /// Brief, smooth squash-and-recover on this transform's localScale,
    /// triggered on demand via PlaySquash() from whichever hit/knockback
    /// logic wants it (regular punch, Heat Vision push-back, Freeze Breath
    /// ice break). Animates on unscaled time so it plays out smoothly even
    /// during a HitStop freeze-frame, same convention as CameraShake.
    /// Restart-safe like DamageFlashAndDestroy's flash routine: a new
    /// PlaySquash() call while one is already running cancels and restarts
    /// cleanly from the base scale instead of compounding.
    /// </summary>
    public class HitSquashEffect : MonoBehaviour
    {
        [SerializeField] private float squashDuration = 0.14f;
        [SerializeField] private Vector2 squashScale = new Vector2(1.2f, 0.8f);

        private Vector3 baseScale;
        private Coroutine routine;

        private void Awake()
        {
            baseScale = transform.localScale;
        }

        public void PlaySquash()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
            }
            transform.localScale = baseScale;
            routine = StartCoroutine(SquashRoutine());
        }

        private IEnumerator SquashRoutine()
        {
            float t = 0f;
            while (t < squashDuration)
            {
                t += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(t / squashDuration);
                // 0 -> 1 -> 0 single arc: squash peaks mid-way, then eases
                // smoothly back to the base scale by the end.
                float curve = Mathf.Sin(normalized * Mathf.PI);
                transform.localScale = new Vector3(
                    Mathf.Lerp(baseScale.x, baseScale.x * squashScale.x, curve),
                    Mathf.Lerp(baseScale.y, baseScale.y * squashScale.y, curve),
                    baseScale.z);
                yield return null;
            }
            transform.localScale = baseScale;
            routine = null;
        }
    }
}
