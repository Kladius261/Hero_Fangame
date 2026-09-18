using UnityEngine;

namespace HeroFangame.Player
{
    /// <summary>
    /// Soft foggy mist cloud + sparse cyan sparkle glints that visualize
    /// Freeze Breath's actual damage/exposure box every frame it is firing.
    /// Repositioned, rotated, and rescaled every frame to exactly match the
    /// box FreezeBreathAbility.ApplyCone() feeds it (so tap vs. hold size
    /// differences, and the player turning mid-breath, are always visibly
    /// accurate to the real hitbox — not a cosmetic approximation). Emission
    /// rate is scaled up for the tap's short pulse so the puff reads
    /// instantly instead of ramping in, and held at a steady rate for the
    /// sustained hold. Stopping lets already-alive mist particles fade out
    /// naturally instead of vanishing.
    /// </summary>
    public class FreezeBreathConeEffect : MonoBehaviour
    {
        [SerializeField] private ParticleSystem mistParticles;
        [SerializeField] private ParticleSystem sparkleParticles;
        [SerializeField] private float tapEmissionRate = 60f;
        [SerializeField] private float holdEmissionRate = 30f;
        [SerializeField] private float sparkleEmissionRate = 5f;
        [SerializeField] private int tapBurstCount = 14;

        private bool isActive;

        /// <summary>
        /// Drives the cloud to exactly match the given box (same
        /// origin/size/angle values ApplyCone() computes) for this frame.
        /// isTap picks a punchier one-shot emission rate + an instant burst;
        /// calling this every frame while held keeps the sustained-hold
        /// look in sync with the player's position/facing.
        /// </summary>
        public void SetCone(Vector2 origin, Vector2 size, float angleDegrees, bool isTap)
        {
            transform.position = origin;
            transform.rotation = Quaternion.Euler(0f, 0f, angleDegrees);

            Vector3 scale = new Vector3(size.x, size.y, 0.3f);

            if (mistParticles != null)
            {
                var shape = mistParticles.shape;
                shape.scale = scale;

                var emission = mistParticles.emission;
                emission.rateOverTime = isTap ? tapEmissionRate : holdEmissionRate;
            }

            if (sparkleParticles != null)
            {
                var shape = sparkleParticles.shape;
                shape.scale = scale;

                var emission = sparkleParticles.emission;
                emission.rateOverTime = sparkleEmissionRate;
            }

            if (!isActive)
            {
                isActive = true;
                mistParticles?.Play();
                sparkleParticles?.Play();
                if (isTap)
                {
                    mistParticles?.Emit(tapBurstCount);
                }
            }
        }

        public void StopCone()
        {
            if (!isActive)
            {
                return;
            }
            isActive = false;

            mistParticles?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            sparkleParticles?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
