using UnityEngine;

namespace HeroFangame.Camera
{
    /// <summary>
    /// Shared helper for keeping horizontal-only attacks (Heat Vision,
    /// Freeze Breath) from reaching enemies that haven't scrolled into view
    /// yet. The level is a single long horizontal strip revealed segment by
    /// segment as the camera advances, so "on screen" is defined purely by
    /// the main camera's current horizontal (X) extent in world space.
    /// </summary>
    public static class CameraViewBounds
    {
        /// <summary>
        /// Returns the main camera's current visible world-space X range.
        /// Assumes an orthographic camera (true for this project). Returns
        /// false if there's no orthographic main camera, so callers can
        /// degrade to "no restriction" rather than breaking.
        /// </summary>
        public static bool TryGetHorizontalBounds(out float minX, out float maxX)
        {
            var cam = UnityEngine.Camera.main;
            if (cam == null || !cam.orthographic)
            {
                minX = float.NegativeInfinity;
                maxX = float.PositiveInfinity;
                return false;
            }

            float halfWidth = cam.orthographicSize * cam.aspect;
            float camX = cam.transform.position.x;
            minX = camX - halfWidth;
            maxX = camX + halfWidth;
            return true;
        }

        /// <summary>
        /// Distance from <paramref name="originX"/> toward the given
        /// horizontal direction (positive = right, negative = left) to the
        /// current camera's visible edge, clamped to zero or more. Returns
        /// float.MaxValue if there's no camera to bound against, so callers
        /// naturally fall back to their own unclamped range.
        /// </summary>
        public static float GetDistanceToEdge(float originX, float dirX)
        {
            if (!TryGetHorizontalBounds(out float minX, out float maxX))
            {
                return float.MaxValue;
            }

            float edge = dirX < 0f ? minX : maxX;
            float distance = dirX < 0f ? (originX - edge) : (edge - originX);
            return Mathf.Max(0f, distance);
        }
    }
}
