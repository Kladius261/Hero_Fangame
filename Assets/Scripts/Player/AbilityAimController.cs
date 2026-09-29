using UnityEngine;

namespace HeroFangame.Player
{
    /// <summary>
    /// Shared vertical-aim state machine for Heat Vision and Freeze Breath.
    /// Both abilities only ever fire strictly along the player's horizontal
    /// Facing, but the Up/Down arrow keys tilt that direction up to
    /// +/-AngleCapDegrees while the ability is being held. Only the very
    /// instant an activation begins (a tap, or the first frame of a hold)
    /// determines the starting angle: Up (and not Down) snaps straight to
    /// the +cap, Down (and not Up) snaps to the -cap, and anything else —
    /// neither held, both held, or Left/Right held — starts horizontal.
    /// This initial snap is always instant, regardless of acceleration.
    /// Once firing has begun, Left/Right are ignored entirely; only Up/Down
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
        public Vector2 Resolve(bool isNewActivation, Vector2 moveInput, Vector2 facing, float deltaTime)
        {
            bool upHeld = moveInput.y > 0.01f;
            bool downHeld = moveInput.y < -0.01f;

            if (isNewActivation)
            {
                // Fresh activation: snap straight to the cap in whichever
                // single vertical direction is held (Left/Right — or
                // neither/both vertical keys — starts horizontal), never
                // carrying over any angle or angular velocity from a
                // previous, separate activation.
                bool horizontalHeld = Mathf.Abs(moveInput.x) > 0.01f;
                currentAngleDegrees = (!horizontalHeld && upHeld && !downHeld) ? AngleCapDegrees
                                     : (!horizontalHeld && downHeld && !upHeld) ? -AngleCapDegrees
                                     : 0f;
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
