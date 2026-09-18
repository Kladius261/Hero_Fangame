namespace HeroFangame.Core
{
    /// <summary>
    /// Implemented by anything that can receive Freeze Breath exposure
    /// and eventually become fully frozen.
    /// </summary>
    public interface IFreezable
    {
        bool IsFrozen { get; }

        /// <summary>
        /// <paramref name="isNewActivation"/> is true only for the frame a
        /// fresh Freeze Breath press starts contact (a tap, or the first
        /// frame of a hold) — never for a hold's later, continuing frames.
        /// Implementations use this to distinguish a deliberate re-use of
        /// Freeze Breath on an already-frozen target (which should free it)
        /// from simply continuing to hold the breath on a target that
        /// happened to freeze mid-hold (which should not immediately
        /// un-freeze it again).
        /// </summary>
        void AddFreezeExposure(float amount, bool isNewActivation);
    }
}
