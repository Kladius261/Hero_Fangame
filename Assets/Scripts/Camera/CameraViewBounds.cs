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
        // Cached instead of re-resolving via Camera.main every call — Camera.main
        // internally does a FindGameObjectWithTag scan, and this is queried every
        // frame while Heat Vision or Freeze Breath is held. Re-fetched only when
        // null/destroyed (mirrors CameraShake.GetOrCreate()'s lazy-cache pattern).
        private static UnityEngine.Camera cachedCam;

        /// <summary>
        /// Returns the main camera's current visible world-space X range.
        /// Assumes an orthographic camera (true for this project). Returns
        /// false if there's no orthographic main camera, so callers can
        /// degrade to "no restriction" rather than breaking.
        /// </summary>
        public static bool TryGetHorizontalBounds(out float minX, out float maxX)
        {
            if (cachedCam == null)
            {
                cachedCam = UnityEngine.Camera.main;
            }
            var cam = cachedCam;
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
        /// Distance to travel from <paramref name="originX"/> along a
        /// direction whose horizontal component is <paramref name="dirX"/>
        /// (positive = rightward, negative = leftward) before its X
        /// coordinate reaches the current camera's visible edge, clamped to
        /// zero or more. Returns float.MaxValue if there's no camera to
        /// bound against, so callers naturally fall back to their own
        /// unclamped range. <paramref name="dirX"/> is expected to be the X
        /// component of a normalized direction vector (so |dirX| &lt;= 1) —
        /// Heat Vision / Freeze Breath can now fire at an angle rather than
        /// strictly horizontally, so the raw horizontal distance to the edge
        /// is divided by |dirX| to get the actual travel distance along that
        /// angled direction (a no-op for purely horizontal fire, where
        /// |dirX| == 1).
        /// </summary>
        public static float GetDistanceToEdge(float originX, float dirX)
        {
            if (!TryGetHorizontalBounds(out float minX, out float maxX))
            {
                return float.MaxValue;
            }

            float edge = dirX < 0f ? minX : maxX;
            float horizontalDistance = dirX < 0f ? (originX - edge) : (edge - originX);
            horizontalDistance = Mathf.Max(0f, horizontalDistance);

            float absDirX = Mathf.Abs(dirX);
            if (absDirX < 0.0001f)
            {
                // Direction is (near-)vertical — it never makes horizontal
                // progress toward either edge.
                return float.MaxValue;
            }

            return horizontalDistance / absDirX;
        }
    }
}
