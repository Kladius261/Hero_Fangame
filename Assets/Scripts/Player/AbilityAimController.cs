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
    /// Once firing has begun, Left/Right are ignored entirely; only Up/Down
    /// matter: holding the same key keeps the angle pinned at its cap,
    /// holding the opposite key gradually sweeps it toward the other cap,
    /// and releasing both (or holding both) freezes the angle wherever it
    /// currently is. The resolved angle never carries over between separate
    /// activations — every new activation re-resolves from scratch via the
    /// isNewActivation branch below.
    /// </summary>
    public class AbilityAimController
    {
        public const float AngleCapDegrees = 60f;

        private readonly float sweepSpeedDegPerSecond;
        private float currentAngleDegrees;

        public AbilityAimController(float sweepSpeedDegPerSecond)
        {
            this.sweepSpeedDegPerSecond = sweepSpeedDegPerSecond;
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
                // carrying over any angle from a previous, separate
                // activation.
                bool horizontalHeld = Mathf.Abs(moveInput.x) > 0.01f;
                currentAngleDegrees = (!horizontalHeld && upHeld && !downHeld) ? AngleCapDegrees
                                     : (!horizontalHeld && downHeld && !upHeld) ? -AngleCapDegrees
                                     : 0f;
            }
            else
            {
                // Once firing has begun, Left/Right no longer have any
                // effect — only Up/Down can move the angle.
                if (upHeld && !downHeld)
                {
                    currentAngleDegrees = Mathf.Min(AngleCapDegrees, currentAngleDegrees + sweepSpeedDegPerSecond * deltaTime);
                }
                else if (downHeld && !upHeld)
                {
                    currentAngleDegrees = Mathf.Max(-AngleCapDegrees, currentAngleDegrees - sweepSpeedDegPerSecond * deltaTime);
                }
                // Neither, or both, held: freeze at the current angle.
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
