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
        [SerializeField] private float tapEmissionRate = 440f;
        [SerializeField] private float holdEmissionRate = 300f;
        [SerializeField] private float sparkleEmissionRate = 52f;
        [SerializeField] private int tapBurstCount = 68;

        [Header("Wind Streaks")]
        [SerializeField] private ParticleSystem streakParticles;
        [SerializeField] private float streakEmissionRate = 130f;

        [Header("Screen Boundary")]
        [SerializeField] private float boundaryBounce = 0.35f;
        [SerializeField] private float boundaryDampen = 0.25f;
        [SerializeField] private float boundaryLifetimeLoss = 0.15f;
        [SerializeField] private float boundaryPuffInterval = 0.1f;
        [SerializeField] private int boundaryPuffMistCount = 2;
        [SerializeField] private int boundaryPuffSparkleCount = 1;
        [SerializeField] private float boundaryPuffLifetime = 0.3f;
        [SerializeField] private float boundaryPuffRecoilSpeed = 1.5f;
        [SerializeField] private float boundaryPuffJitter = 0.4f;

        private bool isActive;
        private bool isBoundaryActive;
        private Transform boundaryPlane;
        private float boundaryPuffTimer;

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

        /// <summary>
        /// Called every frame the cone's reach is currently cut short by the
        /// camera's screen edge (see FreezeBreathAbility.ApplyCone /
        /// CameraViewBounds) rather than a physical obstacle. Enables
        /// particle collision against an invisible plane positioned right at
        /// that boundary so the mist/streaks visibly billow and curl at the
        /// edge of the screen instead of just having their emission shape
        /// cut short there. Uses the existing mist/sparkle/streak systems —
        /// no extra particle assets required.
        /// </summary>
        public void SetBoundaryContact(Vector2 point, Vector2 direction)
        {
            var plane = GetOrCreateBoundaryPlane();
            plane.position = point;
            // The Collision module treats a referenced Transform's local up
            // axis as the plane's normal. Orient it to face back toward the
            // player (opposing the breath's direction) so outbound
            // particles collide against it instead of passing through.
            plane.rotation = Quaternion.Euler(0f, 0f, direction.x > 0f ? 90f : -90f);

            if (!isBoundaryActive)
            {
                isBoundaryActive = true;
                boundaryPuffTimer = 0f;
                SetCollision(mistParticles, true, plane);
                SetCollision(sparkleParticles, true, plane);
                SetCollision(streakParticles, true, plane);
            }

            // The Collision module above only matters for particles that
            // are actually travelling — but this cloud's mist/sparkle/streak
            // systems all use startSpeed 0 (a static box fill jittered only
            // by Noise), so they never move far enough to reach the plane
            // and the collision bounce was invisible in practice. Emitting a
            // small trickle of extra particles pinned to the boundary point
            // gives a guaranteed, clearly visible "billowing against the
            // wall" cue — but a naive continuous spawn with no lifetime cap
            // (an earlier version of this) let particles pile up faster than
            // they could ever die out over a sustained hold, snowballing
            // into a runaway blob that grew for as long as the ability was
            // held and drifted well past the boundary itself. Explicitly
            // overriding each puff particle's lifetime to a short, fixed
            // duration and giving it a small recoil velocity kicking it back
            // away from the wall keeps the steady-state particle count (and
            // the effect's on-screen footprint) bounded regardless of how
            // long the boundary contact lasts.
            boundaryPuffTimer -= Time.deltaTime;
            if (boundaryPuffTimer <= 0f)
            {
                boundaryPuffTimer = boundaryPuffInterval;
                Vector2 jitter = Random.insideUnitCircle * boundaryPuffJitter;
                Vector2 recoilVelocity = -direction * boundaryPuffRecoilSpeed + jitter;
                EmitAt(mistParticles, point, recoilVelocity, boundaryPuffLifetime, boundaryPuffMistCount);
                EmitAt(sparkleParticles, point, recoilVelocity, boundaryPuffLifetime, boundaryPuffSparkleCount);
            }
        }

        public void StopBoundaryContact()
        {
            if (!isBoundaryActive)
            {
                return;
            }
            isBoundaryActive = false;

            SetCollision(mistParticles, false, null);
            SetCollision(sparkleParticles, false, null);
            SetCollision(streakParticles, false, null);
        }

        private static void EmitAt(ParticleSystem ps, Vector2 worldPoint, Vector2 worldVelocity, float lifetime, int count)
        {
            if (ps == null || count <= 0)
            {
                return;
            }

            // EmitParams.position/velocity are interpreted in whatever space
            // ps.main.simulationSpace is set to — for this cloud's mist and
            // sparkle systems that's Local, meaning a raw world-space point
            // passed straight through gets misinterpreted as a point
            // relative to this transform (which itself follows the player
            // and rotates 180 degrees when facing left), landing the puff
            // far from the intended boundary point (confirmed: it put
            // particles at screen-center when firing left instead of at the
            // left edge). Converting explicitly via the transform makes this
            // correct regardless of the emitter's current position/rotation
            // or which simulation space the system is configured for.
            Vector3 position;
            Vector3 velocity;
            if (ps.main.simulationSpace == ParticleSystemSimulationSpace.Local)
            {
                position = ps.transform.InverseTransformPoint(worldPoint);
                velocity = ps.transform.InverseTransformVector(worldVelocity);
            }
            else
            {
                position = worldPoint;
                velocity = worldVelocity;
            }

            var emitParams = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startLifetime = lifetime,
                applyShapeToPosition = false
            };
            ps.Emit(emitParams, count);
        }

        private Transform GetOrCreateBoundaryPlane()
        {
            if (boundaryPlane == null)
            {
                var go = new GameObject("FreezeBreathBoundaryPlane");
                go.transform.SetParent(transform, false);
                boundaryPlane = go.transform;
            }
            return boundaryPlane;
        }

        private void SetCollision(ParticleSystem ps, bool enabled, Transform plane)
        {
            if (ps == null)
            {
                return;
            }

            var collision = ps.collision;
            collision.enabled = enabled;
            if (enabled)
            {
                collision.type = ParticleSystemCollisionType.Planes;
                collision.SetPlane(0, plane);
                collision.bounce = boundaryBounce;
                collision.dampen = boundaryDampen;
                collision.lifetimeLoss = boundaryLifetimeLoss;
            }
        }
    }
}
