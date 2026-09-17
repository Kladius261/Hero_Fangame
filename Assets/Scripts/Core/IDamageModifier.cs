namespace HeroFangame.Core
{
    /// <summary>
    /// Implemented by anything whose incoming damage should be modified
    /// (e.g. an enemy that takes bonus damage while frozen).
    /// </summary>
    public interface IDamageModifier
    {
        float GetDamageMultiplier();
    }
}
