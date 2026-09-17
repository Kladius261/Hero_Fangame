namespace HeroFangame.Core
{
    /// <summary>
    /// Implemented by anything that can receive Freeze Breath exposure
    /// and eventually become fully frozen.
    /// </summary>
    public interface IFreezable
    {
        bool IsFrozen { get; }
        void AddFreezeExposure(float amount);
    }
}
