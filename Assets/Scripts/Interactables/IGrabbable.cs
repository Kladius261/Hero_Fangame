using UnityEngine;

namespace HeroFangame.Interactables
{
    /// <summary>
    /// Implemented by anything PlayerGrabAbility can pick up and throw
    /// (DestructibleObject, ExplosiveObject, ...).
    /// </summary>
    public interface IGrabbable
    {
        bool IsGrabbed { get; }
        void BeginGrab(Transform carrier);
        void Throw(Vector2 direction, GameObject thrower);
    }
}
