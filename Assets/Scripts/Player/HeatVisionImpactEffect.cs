using UnityEngine;

namespace HeroFangame.Player
{
    /// <summary>
    /// Fire/ember + smoke burst that follows the Heat Vision beam's current
    /// contact point, plus a bright star-shaped "sparkle" glint sitting on
    /// top of it (the hot white flare a laser makes at its point of
    /// contact). Repositioned every frame while contact persists, but only
    /// (re)started on the frame contact begins so it doesn't restart (and
    /// flicker) every frame. While contact holds, the sparkle idles with a
    /// small pulsing scale so it reads as "alive" instead of a static
    /// decal. When contact ends, fire/smoke emission is stopped gracefully
    /// so already-alive particles finish their own lifetime/fade instead of
    /// vanishing instantly, and the sparkle is hidden immediately (it has
    /// no lingering particles of its own).
    /// </summary>
    public class HeatVisionImpactEffect : MonoBehaviour
    {
        [SerializeField] private ParticleSystem fireParticles;
        [SerializeField] private ParticleSystem smokeParticles;
        [SerializeField] private Transform sparkle;
        [SerializeField] private float sparkleBaseScale = 2.2f;
        [SerializeField] private float sparklePulseAmplitude = 0.15f;
        [SerializeField] private float sparklePulseSpeed = 14f;

        private bool isContacting;
        private float sparkleTime;

        public void UpdateContact(Vector2 point, Vector2 beamDirection)
        {
            transform.position = point;

            if (!isContacting)
            {
                isContacting = true;
                sparkleTime = 0f;
                if (fireParticles != null && !fireParticles.isPlaying)
                {
                    fireParticles.Play();
                }
                if (smokeParticles != null && !smokeParticles.isPlaying)
                {
                    smokeParticles.Play();
                }
                if (sparkle != null)
                {
                    sparkle.gameObject.SetActive(true);
                }
            }

            if (sparkle != null)
            {
                sparkleTime += Time.deltaTime;
                float pulse = 1f + Mathf.Sin(sparkleTime * sparklePulseSpeed) * sparklePulseAmplitude;
                sparkle.localScale = Vector3.one * (sparkleBaseScale * pulse);
            }
        }

        public void StopContact()
        {
            if (!isContacting)
            {
                return;
            }
            isContacting = false;

            fireParticles?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            smokeParticles?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (sparkle != null)
            {
                sparkle.gameObject.SetActive(false);
            }
        }
    }
}
