using UnityEngine;

namespace HeroFangame.Player
{
    /// <summary>
    /// Soft foggy mist cloud + flowing wind-streak lines + sparse cyan
    /// sparkle glints that visualize Freeze Breath's actual damage/exposure
    /// box every frame it is firing. Repositioned, rotated, and rescaled
    /// every frame to exactly match the box FreezeBreathAbility.ApplyCone()
    /// feeds it (so tap vs. hold size differences, and the player turning
    /// mid-breath, are always visibly accurate to the real hitbox — not a
    /// cosmetic approximation). Emission rate is scaled up for the tap's
    /// short pulse so the puff reads instantly instead of ramping in, and
    /// held at a steady rate for the sustained hold. Stopping lets
    /// already-alive mist/sparkle/streak particles fade out naturally
    /// instead of cutting off abruptly.
    /// </summary>
    public class FreezeBreathConeEffect : MonoBehaviour
    {
        [Header("Mist / Sparkle")]
        [SerializeField] private ParticleSystem mistParticles;
        [SerializeField] private ParticleSystem sparkleParticles;
        [SerializeField] private float tapEmissionRate = 220f;
        [SerializeField] private float holdEmissionRate = 150f;
        [SerializeField] private float sparkleEmissionRate = 26f;
        [SerializeField] private int tapBurstCount = 34;

        [Header("Wind Streaks")]
        [SerializeField] private ParticleSystem streakParticles;
        [SerializeField] private float streakEmissionRate = 65f;

        private bool isActive;

        /// <summary>
        /// Drives the cloud/streaks to exactly match the given box
        /// (same origin/size/angle values ApplyCone() computes) for this
        /// frame. isTap picks a punchier one-shot mist emission rate + an
        /// instant burst; calling this every frame while held keeps the
        /// sustained-hold look in sync with the player's position/facing.
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

            if (streakParticles != null)
            {
                var shape = streakParticles.shape;
                shape.scale = scale;

                var emission = streakParticles.emission;
                emission.rateOverTime = streakEmissionRate;
            }

            if (!isActive)
            {
                isActive = true;
                mistParticles?.Play();
                sparkleParticles?.Play();
                streakParticles?.Play();
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
            streakParticles?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
