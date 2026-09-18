using System.Collections;
using UnityEngine;

namespace HeroFangame.Enemy
{
    /// <summary>
    /// Visual layer for an EnemyRobot becoming frozen/thawing, added
    /// alongside (not replacing) EnemyRobot's existing instant sprite
    /// tint. On PlayFreezeIn(): plays a one-shot burst of frost puff
    /// particles at the enemy's position, starts a looping "ambient frost"
    /// sparkle system that drifts around the body for the freeze's
    /// duration, and fades in a translucent icy overlay sprite over
    /// iceFadeInDuration to fake the reference video's "spreads across
    /// body" look on a flat 2D sprite. On PlayThaw(): stops the ambient
    /// frost emission (existing sparkles finish naturally) and fades the
    /// overlay back out over iceFadeOutDuration. Purely additive — never
    /// touches EnemyRobot's own state machine, timers, or base sprite
    /// color.
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

        private void Awake()
        {
            if (iceOverlayRenderer != null)
            {
                iceOverlayRenderer.color = new Color(iceOverlayColor.r, iceOverlayColor.g, iceOverlayColor.b, 0f);
                iceOverlayRenderer.gameObject.SetActive(false);
            }
        }

        public void PlayFreezeIn()
        {
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
            ambientFrostParticles?.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            if (iceOverlayRenderer != null)
            {
                RestartFade(0f, iceFadeOutDuration, deactivateAtEnd: true);
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
