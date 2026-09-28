namespace HeroFangame.Core
{
    /// <summary>
    /// Optional companion to IFreezable for a target that dies from a
    /// single point of damage (see ExplosiveObject). Freeze Breath deals a
    /// small amount of "chip" damage alongside its freeze exposure on every
    /// hit; for a one-hit-kill target that damage would otherwise deplete
    /// its HP and permanently mark it dead in Damageable before it ever got
    /// a chance to actually freeze (and Damageable.TakeDamage silently
    /// no-ops forever once IsDead is set, so nothing could ever damage it
    /// again afterward either). Implementing this lets such a target veto
    /// that same hit's accompanying damage right after AddFreezeExposure
    /// runs, so the freeze attempt costs it nothing.
    /// </summary>
    public interface IFreezeDamageAbsorber
    {
        /// <summary>
        /// Called immediately after AddFreezeExposure for the same hit.
        /// Must be a one-shot check (reset any internal flag before
        /// returning) so it only ever suppresses the exact hit it was
        /// raised for, never a later, unrelated attack.
        /// </summary>
        bool ConsumeDamageAbsorption();
    }
}
