using UnityEngine;
using HeroFangame.Combat;
using HeroFangame.Core;

namespace HeroFangame.Player
{
    /// <summary>
    /// Shared vertical-aim state machine for Heat Vision and Freeze Breath.
    /// Both abilities only ever fire strictly along the player's horizontal
    /// Facing, but the Up/Down arrow keys tilt that direction up to
    /// +/-AngleCapDegrees while the ability is being held. Only the very
    /// instant an activation begins (a tap, or the first frame of a hold)
    /// determines the starting angle: the caller auto-aims at the closest
    /// Damageable target whose angle relative to Facing falls inside
    /// +/-AngleCapDegrees (see <see cref="FindAutoAimAngleDegrees"/>), or
    /// falls back to horizontal (0 degrees) if no such target exists. This
    /// initial snap is always instant, regardless of acceleration. Once
    /// firing has begun, Left/Right are ignored entirely; only Up/Down
    /// matter: holding the same key accelerates the sweep toward that
    /// key's cap (ramping up to, then holding, its max angular speed)
    /// rather than moving at a constant rate — this ramp is what gives the
    /// held sweep its "weight". Holding the opposite key decelerates the
    /// current sweep and re-accelerates it the other way instead of
    /// instantly reversing, so flipping between Up and Down mid-sweep
    /// feels inertial too. Releasing both (or holding both) zeroes the
    /// angular velocity immediately and freezes the angle exactly wherever
    /// it currently is — that freeze is deliberately instant (no
    /// coast/decay) so releasing the aim stays precise. The resolved angle
    /// and its angular velocity never carry over between separate
    /// activations — every new activation re-resolves from scratch,
    /// snapping instantly, via the isNewActivation branch below.
    /// </summary>
    public class AbilityAimController
    {
        public const float AngleCapDegrees = 60f;

        private readonly float sweepSpeedDegPerSecond;
        private readonly float sweepAccelerationDegPerSecondSquared;
        private float currentAngleDegrees;
        private float currentAngularVelocityDegPerSecond;

        /// <param name="sweepSpeedDegPerSecond">Max angular speed of the held sweep, in degrees/second.</param>
        /// <param name="sweepAccelerationDegPerSecondSquared">
        /// How fast the held sweep's angular velocity ramps toward
        /// +/-sweepSpeedDegPerSecond (or toward 0 on release), in
        /// degrees/second^2. Defaults to float.PositiveInfinity — velocity
        /// reaches its target instantly — reproducing the old, purely linear
        /// (no-inertia) sweep for any caller that doesn't pass this argument
        /// (e.g. FlightAbility's charge-aim, left intentionally unchanged).
        /// </param>
        public AbilityAimController(float sweepSpeedDegPerSecond, float sweepAccelerationDegPerSecondSquared = float.PositiveInfinity)
        {
            this.sweepSpeedDegPerSecond = sweepSpeedDegPerSecond;
            this.sweepAccelerationDegPerSecondSquared = sweepAccelerationDegPerSecondSquared;
        }

        /// <summary>
        /// Call once per frame the ability is actually firing (the
        /// activating tap/hold-start frame, and every subsequent held
        /// frame). Mutates the internal angle and returns the resolved fire
        /// direction, mirrored horizontally to match <paramref name="facing"/>.
        /// </summary>
        /// <param name="initialAngleDegrees">
        /// The starting angle to snap to when <paramref name="isNewActivation"/>
        /// is true (ignored otherwise) — typically the result of
        /// <see cref="FindAutoAimAngleDegrees"/>, clamped here regardless of
        /// what the caller passes in.
        /// </param>
        public Vector2 Resolve(bool isNewActivation, float initialAngleDegrees, Vector2 moveInput, Vector2 facing, float deltaTime)
        {
            bool upHeld = moveInput.y > 0.01f;
            bool downHeld = moveInput.y < -0.01f;

            if (isNewActivation)
            {
                // Fresh activation: snap straight to the auto-aimed angle,
                // never carrying over any angle or angular velocity from a
                // previous, separate activation.
                currentAngleDegrees = Mathf.Clamp(initialAngleDegrees, -AngleCapDegrees, AngleCapDegrees);
                currentAngularVelocityDegPerSecond = 0f;
            }
            else
            {
                // Once firing has begun, Left/Right no longer have any
                // effect — only Up/Down can move the angle. Ramp the
                // angular velocity toward the held direction's max speed
                // instead of jumping straight to it, so both catching up
                // to a cap and reversing mid-sweep have inertial "weight".
                bool exactlyOneHeld = upHeld != downHeld;
                if (exactlyOneHeld)
                {
                    float targetVelocityDegPerSecond = upHeld ? sweepSpeedDegPerSecond : -sweepSpeedDegPerSecond;

                    // Guard Infinity * 0 == NaN on a zero-length frame — the
                    // "no inertia" default must resolve to an unbounded
                    // step, not NaN, regardless of deltaTime.
                    float maxVelocityDelta = float.IsPositiveInfinity(sweepAccelerationDegPerSecondSquared)
                        ? float.PositiveInfinity
                        : sweepAccelerationDegPerSecondSquared * deltaTime;

                    currentAngularVelocityDegPerSecond = Mathf.MoveTowards(currentAngularVelocityDegPerSecond, targetVelocityDegPerSecond, maxVelocityDelta);
                }
                else
                {
                    // Neither, or both, held: a hard, instant freeze — zero
                    // the velocity outright (not decelerated toward 0) so
                    // letting go always stops the aim precisely on the
                    // frame it's released.
                    currentAngularVelocityDegPerSecond = 0f;
                }

                currentAngleDegrees = Mathf.Clamp(currentAngleDegrees + currentAngularVelocityDegPerSecond * deltaTime, -AngleCapDegrees, AngleCapDegrees);
            }

            return DirectionFromAngle(currentAngleDegrees, facing);
        }

        /// <summary>
        /// Side-effect-free read of the current resolved direction — used by
        /// editor Gizmos so drawing them never advances/mutates aim state.
        /// </summary>
        public Vector2 CurrentDirection(Vector2 facing)
        {
            return DirectionFromAngle(currentAngleDegrees, facing);
        }

        /// <summary>
        /// Scans for the closest Damageable within maxDistance of origin
        /// whose angle relative to facing falls inside +/-AngleCapDegrees,
        /// and returns the signed angle (in this controller's
        /// mirrored-angle space, i.e. the inverse of
        /// <see cref="DirectionFromAngle"/>) needed to aim directly at it
        /// on activation. Targets outside the cone — including anything
        /// behind the player — are not eligible at all, never clamped to
        /// the cap edge. Returns 0 (horizontal) if no valid target is
        /// found, the documented edge-case fallback.
        /// </summary>
        public static float FindAutoAimAngleDegrees(Vector2 origin, Vector2 facing, float maxDistance, LayerMask mask)
        {
            var hits = AttackUtility.OverlapCircle(origin, maxDistance, mask, out int hitCount);
            float horizontalSign = facing.x < 0f ? -1f : 1f;

            bool found = false;
            float bestAngle = 0f;
            float bestSqrDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                var hit = hits[i];
                if (hit == null || hit.GetComponentInParent<Damageable>() == null)
                {
                    continue;
                }

                Vector2 toTarget = (Vector2)hit.bounds.center - origin;
                float sqrDistance = toTarget.sqrMagnitude;
                if (sqrDistance < 0.0001f || sqrDistance >= bestSqrDistance)
                {
                    continue;
                }

                float angle = Mathf.Atan2(toTarget.y, horizontalSign * toTarget.x) * Mathf.Rad2Deg;
                if (angle < -AngleCapDegrees || angle > AngleCapDegrees)
                {
                    // Outside the reachable cone entirely -- not eligible.
                    continue;
                }

                bestSqrDistance = sqrDistance;
                bestAngle = angle;
                found = true;
            }

            return found ? bestAngle : 0f;
        }

        /// <summary>
        /// Reproduces the original Up/Down activation-snap rule (Up-only
        /// snaps to +cap, Down-only to -cap, anything else to horizontal).
        /// Superseded by <see cref="FindAutoAimAngleDegrees"/> for Heat
        /// Vision/Freeze Breath, but still used verbatim by FlightAbility's
        /// charge-aim, which deliberately keeps the old, input-driven snap
        /// instead of auto-aiming at a target.
        /// </summary>
        public static float AngleFromVerticalHeld(Vector2 moveInput)
        {
            bool upHeld = moveInput.y > 0.01f;
            bool downHeld = moveInput.y < -0.01f;
            bool horizontalHeld = Mathf.Abs(moveInput.x) > 0.01f;
            return (!horizontalHeld && upHeld && !downHeld) ? AngleCapDegrees
                 : (!horizontalHeld && downHeld && !upHeld) ? -AngleCapDegrees
                 : 0f;
        }

        private static Vector2 DirectionFromAngle(float angleDegrees, Vector2 facing)
        {
            // Mirroring the facing sign onto cos (rather than picking
            // between Vector2.left/right) keeps "Up" always meaning
            // visually-upward regardless of which way the character faces.
            float horizontalSign = facing.x < 0f ? -1f : 1f;
            float rad = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(horizontalSign * Mathf.Cos(rad), Mathf.Sin(rad));
        }
    }
}
