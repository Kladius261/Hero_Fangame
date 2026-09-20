using System.Collections;
using UnityEngine;

namespace HeroFangame.Core
{
    /// <summary>
    /// Standalone brief color flash on this object's SpriteRenderer,
    /// triggered on demand via Flash(color, duration). Unlike
    /// DamageFlashAndDestroy, this isn't wired to a Damageable's
    /// OnDamaged/OnDeath events — it's for feedback on objects that don't
    /// take HP damage from the event that should flash (e.g. the player
    /// crashing into plain wall geometry during Flight's Charge). Animates
    /// on unscaled time and is restart-safe, same convention as
    /// HitSquashEffect.
    /// </summary>
    public class FlashEffect : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        private Color originalColor;
        private Coroutine routine;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
            if (spriteRenderer != null)
            {
                originalColor = spriteRenderer.color;
            }
        }

        public void Flash(Color color, float duration)
        {
            if (spriteRenderer == null)
            {
                return;
            }
            if (routine != null)
            {
                StopCoroutine(routine);
            }
            routine = StartCoroutine(FlashRoutine(color, duration));
        }

        private IEnumerator FlashRoutine(Color color, float duration)
        {
            spriteRenderer.color = color;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            spriteRenderer.color = originalColor;
            routine = null;
        }
    }
}
