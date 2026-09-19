using System.Collections;
using UnityEngine;

namespace HeroFangame.Core
{
    /// <summary>
    /// Generic hit/death feedback: brief color flash when damaged,
    /// scale-down + destroy when killed. Attach alongside Damageable.
    /// </summary>
    [RequireComponent(typeof(Damageable))]
    public class DamageFlashAndDestroy : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField] private float flashDuration = 0.08f;
        [SerializeField] private float deathShrinkDuration = 0.2f;

        private Damageable damageable;
        private Color originalColor;
        private Coroutine flashRoutine;
        private WaitForSeconds flashWait;

        private void Awake()
        {
            damageable = GetComponent<Damageable>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
            if (spriteRenderer != null)
            {
                originalColor = spriteRenderer.color;
            }
            flashWait = new WaitForSeconds(flashDuration);
        }

        private void OnEnable()
        {
            damageable.OnDamaged += HandleDamaged;
            damageable.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            damageable.OnDamaged -= HandleDamaged;
            damageable.OnDeath -= HandleDeath;
        }

        private void HandleDamaged(int amount)
        {
            if (spriteRenderer == null)
            {
                return;
            }
            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
            }
            flashRoutine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            spriteRenderer.color = flashColor;
            yield return flashWait;
            spriteRenderer.color = originalColor;
            flashRoutine = null;
        }

        private void HandleDeath()
        {
            StopAllCoroutines();
            StartCoroutine(DeathRoutine());
        }

        private IEnumerator DeathRoutine()
        {
            Vector3 startScale = transform.localScale;
            float t = 0f;
            while (t < deathShrinkDuration)
            {
                t += Time.deltaTime;
                float lerp = Mathf.Clamp01(t / deathShrinkDuration);
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, lerp);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
