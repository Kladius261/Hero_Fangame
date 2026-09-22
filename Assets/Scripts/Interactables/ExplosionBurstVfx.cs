using UnityEngine;

namespace HeroFangame.Interactables
{
    /// <summary>
    /// Thin wrapper around a Fire+Smoke ParticleSystem pair representing one
    /// explosion burst "flavor" (Cluster/Ring/Pillar) on an ExplosiveObject.
    /// Mirrors HeatVisionImpactEffect's own small-wrapper-component pattern.
    /// </summary>
    public class ExplosionBurstVfx : MonoBehaviour
    {
        [SerializeField] private ParticleSystem fire;
        [SerializeField] private ParticleSystem smoke;

        public void Play()
        {
            fire?.Play();
            smoke?.Play();
        }
    }
}
