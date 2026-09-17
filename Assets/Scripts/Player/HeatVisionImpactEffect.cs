using UnityEngine;

namespace HeroFangame.Player
{
    /// <summary>
    /// Fire/ember + smoke burst that follows the Heat Vision beam's current
    /// contact point. Repositioned every frame while contact persists, but
    /// only (re)started on the frame contact begins so it doesn't restart
    /// (and flicker) every frame. When contact ends, emission is stopped
    /// gracefully so already-alive particles finish their own lifetime/fade
    /// instead of vanishing instantly.
    /// </summary>
    public class HeatVisionImpactEffect : MonoBehaviour
    {
        [SerializeField] private ParticleSystem fireParticles;
        [SerializeField] private ParticleSystem smokeParticles;

        private bool isContacting;

        public void UpdateContact(Vector2 point, Vector2 beamDirection)
        {
            transform.position = point;

            if (!isContacting)
            {
                isContacting = true;
                if (fireParticles != null && !fireParticles.isPlaying)
                {
                    fireParticles.Play();
                }
                if (smokeParticles != null && !smokeParticles.isPlaying)
                {
                    smokeParticles.Play();
                }
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
        }
    }
}
