using System.Collections;
using UnityEngine;

namespace HeroFangame.Enemy
{
    /// <summary>
    /// Visual layer for an EnemyRobot's freeze exposure, added alongside
    /// (not replacing) EnemyRobot's existing instant sprite tint. Has two
    /// complementary modes:
    /// - SetChillLevel(t): called continuously (every Chase-state frame)
    ///   with exposure/threshold in [0,1]. Any exposure above zero snaps
    ///   the icy overlay straight to full opacity and starts the ambient
    ///   frost sparkle loop immediately (no fade-in ramp), so the enemy
    ///   visibly starts frosting over on the very first hit rather than
    ///   only at full freeze. Dropping back to zero exposure clears the
    ///   overlay and stops the sparkles just as instantly. No-ops while
    ///   solid-frozen (isLocked) so it never fights the discrete
    ///   transition below.
    /// - PlayFreezeIn()/PlayThaw(): discrete transition into/out of the
    ///   fully solid-frozen state. PlayFreezeIn() plays a one-shot burst of
    ///   frost puff particles and fades the overlay to full opacity over
    ///   iceFadeInDuration. PlayThaw() stops the ambient frost emission
    ///   (existing sparkles finish naturally) and fades the overlay back
    ///   out over iceFadeOutDuration.
    /// Purely additive — never touches EnemyRobot's own state machine or
    /// base sprite color.
    /// </summary>
    public class EnemyFreezeVisualEffect : MonoBehaviour
    {
        [SerializeField] private ParticleSystem freezeBurstParticles;
        [SerializeField] private ParticleSystem ambientFrostParticles;
        [SerializeField] private SpriteRenderer iceOverlayRenderer;
        [SerializeField] private Color iceOverlayColor = new Color(0.75f, 0.9f, 1f, 0.55f);
        [SerializeField] private float iceFadeInDuration = 0.25f;
        [SerializeField] private float iceFadeOutDuration = 0.4f;

        private Coroutine fadeRoutine;
        private bool isLocked;
        private bool isChilling;

        private void Awake()
        {
            if (iceOverlayRenderer != null)
            {
                iceOverlayRenderer.color = new Color(iceOverlayColor.r, iceOverlayColor.g, iceOverlayColor.b, 0f);
                iceOverlayRenderer.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Syncs the frost visual to whether there is any exposure at all
        /// (t &gt; 0), snapping fully on/off rather than fading — the enemy
        /// should look instantly frosted the moment it's touched by Freeze
        /// Breath. Ignored while solid-frozen (between PlayFreezeIn() and
        /// PlayThaw()).
        /// </summary>
        public void SetChillLevel(float t)
        {
            if (isLocked)
            {
                return;
            }

            if (t <= 0f)
            {
                if (!isChilling)
                {
                    return;
                }
                isChilling = false;
                StopFadeRoutineIfRunning();
                ambientFrostParticles?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (iceOverlayRenderer != null)
                {
                    iceOverlayRenderer.color = new Color(iceOverlayColor.r, iceOverlayColor.g, iceOverlayColor.b, 0f);
                    iceOverlayRenderer.gameObject.SetActive(false);
                }
                return;
            }

            if (isChilling)
            {
                return;
            }

            isChilling = true;
            StopFadeRoutineIfRunning();
            ambientFrostParticles?.Play();
            if (iceOverlayRenderer != null)
            {
                iceOverlayRenderer.gameObject.SetActive(true);
                iceOverlayRenderer.color = new Color(iceOverlayColor.r, iceOverlayColor.g, iceOverlayColor.b, iceOverlayColor.a);
            }
        }

        public void PlayFreezeIn()
        {
            isLocked = true;
            isChilling = false;

            freezeBurstParticles?.Play();
            ambientFrostParticles?.Play();

            if (iceOverlayRenderer != null)
            {
                iceOverlayRenderer.gameObject.SetActive(true);
                RestartFade(iceOverlayColor.a, iceFadeInDuration, deactivateAtEnd: false);
            }
        }

        public void PlayThaw()
        {
            isLocked = false;

            ambientFrostParticles?.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            if (iceOverlayRenderer != null)
            {
                RestartFade(0f, iceFadeOutDuration, deactivateAtEnd: true);
            }
        }

        private void StopFadeRoutineIfRunning()
        {
            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }
        }

        private void RestartFade(float targetAlpha, float duration, bool deactivateAtEnd)
        {
            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
            }
            fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha, duration, deactivateAtEnd));
        }

        private IEnumerator FadeRoutine(float targetAlpha, float duration, bool deactivateAtEnd)
        {
            float startAlpha = iceOverlayRenderer.color.a;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float a = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(t / duration));
                iceOverlayRenderer.color = new Color(iceOverlayColor.r, iceOverlayColor.g, iceOverlayColor.b, a);
                yield return null;
            }
            iceOverlayRenderer.color = new Color(iceOverlayColor.r, iceOverlayColor.g, iceOverlayColor.b, targetAlpha);
            if (deactivateAtEnd)
            {
                iceOverlayRenderer.gameObject.SetActive(false);
            }
            fadeRoutine = null;
        }
    }
}
