using UnityEngine;

namespace HeroFangame.Interactables
{
    /// <summary>
    /// Playable-area rectangle for Prototype.unity's walls (Wall_Top/Bottom
    /// at y=+-6.5 sized 22x1, Wall_Left/Right at x=+-10.5 sized 1x12), inset
    /// by half a standard 1x1 prop collider so a thrown object's collider
    /// rests fully inside the walls instead of clipping into/through them.
    /// Used to clamp fixed-distance throw landings (DestructibleObject,
    /// ExplosiveObject), which move via a direct transform lerp with no
    /// physics/collision of their own to stop them at a wall naturally.
    /// </summary>
    public static class LevelBounds
    {
        public const float MinX = -9.5f;
        public const float MaxX = 9.5f;
        public const float MinY = -5.5f;
        public const float MaxY = 5.5f;

        public static Vector3 Clamp(Vector3 position)
        {
            position.x = Mathf.Clamp(position.x, MinX, MaxX);
            position.y = Mathf.Clamp(position.y, MinY, MaxY);
            return position;
        }
    }
}
