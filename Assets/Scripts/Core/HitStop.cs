using UnityEngine;

namespace HeroFangame.Core
{
    /// <summary>
    /// Brief, global freeze-frame effect: zeroes Time.timeScale for a short
    /// real-time duration so an impactful hit reads with weight (physics and
    /// scaled-time animation halt uniformly, then release together). Lazily
    /// creates its own persistent GameObject on first use, mirroring
    /// CameraShake.GetOrCreate(). Safe to call repeatedly; overlapping
    /// triggers extend the freeze rather than restarting or shortening it,
    /// same "extend, don't restart" rule as CameraShake.Pulse.
    /// </summary>
    public class HitStop : MonoBehaviour
    {
        public static HitStop Instance { get; private set; }

        private float remaining;
        private float previousTimeScale = 1f;

        public static HitStop GetOrCreate()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var go = new GameObject("HitStop");
            Object.DontDestroyOnLoad(go);
            Instance = go.AddComponent<HitStop>();
            return Instance;
        }

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// Freezes Time.timeScale to 0 for at least <paramref name="duration"/>
        /// real seconds. If a freeze is already in progress, extends it to
        /// whichever is longer rather than stacking durations.
        /// </summary>
        public void Trigger(float duration)
        {
            if (remaining <= 0f)
            {
                previousTimeScale = Time.timeScale;
            }
            remaining = Mathf.Max(remaining, duration);
            Time.timeScale = 0f;
        }

        private void Update()
        {
            if (remaining <= 0f)
            {
                return;
            }

            // Time.deltaTime is 0 while timeScale is 0, so this must count
            // down on unscaled time to ever actually end the freeze.
            remaining -= Time.unscaledDeltaTime;
            if (remaining <= 0f)
            {
                remaining = 0f;
                Time.timeScale = previousTimeScale;
            }
        }
    }
}
