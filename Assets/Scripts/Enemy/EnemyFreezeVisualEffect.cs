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
        [SerializeField] private ParticleSystem iceShatterParticles;
        [SerializeField] private SpriteRenderer iceOverlayRenderer;
        [SerializeField] private Color iceOverlayColor = new Color(0.75f, 0.9f, 1f, 0.55f);
        [SerializeField] private float iceFadeInDuration = 0.25f;
        [SerializeField] private float iceFadeOutDuration = 0.4f;
        [SerializeField] private float iceShatterFadeOutDuration = 0.1f;
        [SerializeField] private float shatterFlashDuration = 0.06f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip freezeSound;
        [SerializeField] private AudioClip[] iceBreakSounds;

        private Coroutine fadeRoutine;
        private WaitForSeconds shatterFlashWait;
        private bool isLocked;
        private bool isChilling;
        private int lastIceBreakClipIndex = -1;

        private void Awake()
        {
            if (iceOverlayRenderer != null)
            {
                iceOverlayRenderer.color = new Color(iceOverlayColor.r, iceOverlayColor.g, iceOverlayColor.b, 0f);
                iceOverlayRenderer.gameObject.SetActive(false);
            }
            shatterFlashWait = new WaitForSeconds(shatterFlashDuration);
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
                StopFreezeExposureSound();
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
            PlayFreezeExposureSound();
            if (iceOverlayRenderer != null)
            {
                iceOverlayRenderer.gameObject.SetActive(true);
                iceOverlayRenderer.color = new Color(iceOverlayColor.r, iceOverlayColor.g, iceOverlayColor.b, iceOverlayColor.a);
            }
        }

        /// <summary>
        /// Starts the "freeze" sound the instant exposure begins (mirrors
        /// the overlay/ambient-particle snap-on above), rather than at full
        /// solid-freeze. Uses a regular Play() rather than PlayOneShot so
        /// StopFreezeExposureSound() can cut it off early if exposure is
        /// lost before the enemy finishes freezing.
        /// </summary>
        private void PlayFreezeExposureSound()
        {
            if (freezeSound == null || audioSource == null)
            {
                return;
            }
            audioSource.loop = false;
            audioSource.clip = freezeSound;
            audioSource.Play();
        }

        /// <summary>
        /// Cuts the "freeze" sound short the moment exposure drops back to
        /// zero (breath released or aimed elsewhere) instead of letting it
        /// play out to completion.
        /// </summary>
        private void StopFreezeExposureSound()
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
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

        /// <summary>
        /// Discrete transition for a violent break-free (the player's hit
        /// shattering the ice), as opposed to PlayThaw()'s smooth melt used
        /// when Freeze Breath toggles a frozen target back off. Stops the
        /// ambient frost loop immediately, fires a one-shot explosive
        /// shatter burst, snaps the ice overlay to a bright white flash for
        /// an instant, then fades it away almost instantly
        /// (iceShatterFadeOutDuration) rather than the slow melt-fade.
        /// </summary>
        public void PlayShatter()
        {
            isLocked = false;

            ambientFrostParticles?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            iceShatterParticles?.Play();
            PlayRandomIceBreakSound();

            if (iceOverlayRenderer != null)
            {
                StopFadeRoutineIfRunning();
                iceOverlayRenderer.gameObject.SetActive(true);
                fadeRoutine = StartCoroutine(ShatterFlashRoutine());
            }
        }

        /// <summary>
        /// Picks a random ice-break clip from the pool, avoiding an
        /// immediate repeat of the last one played, mirroring
        /// PunchHitSound's no-immediate-repeat convention.
        /// </summary>
        private void PlayRandomIceBreakSound()
        {
            if (iceBreakSounds == null || iceBreakSounds.Length == 0 || audioSource == null)
            {
                return;
            }

            int index;
            do
            {
                index = Random.Range(0, iceBreakSounds.Length);
            }
            while (iceBreakSounds.Length > 1 && index == lastIceBreakClipIndex);
            lastIceBreakClipIndex = index;

            var clip = iceBreakSounds[index];
            if (clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        /// <summary>
        /// Snaps the overlay to solid white for shatterFlashDuration before
        /// handing off to the normal fade-out, so the break-free moment
        /// reads as a punchy flash rather than quietly dissolving straight
        /// from the icy tint.
        /// </summary>
        private IEnumerator ShatterFlashRoutine()
        {
            iceOverlayRenderer.color = Color.white;
            yield return shatterFlashWait;
            fadeRoutine = StartCoroutine(FadeRoutine(0f, iceShatterFadeOutDuration, deactivateAtEnd: true));
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
