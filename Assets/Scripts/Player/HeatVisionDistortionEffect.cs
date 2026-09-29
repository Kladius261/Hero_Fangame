using UnityEngine;

namespace HeroFangame.Player
{
    /// <summary>
    /// Thin wrapper around the HeatVisionDistortion shader graph sprite — a
    /// large looping heat-haze sprite that conveys the sense of heat while
    /// Heat Vision is actively firing. Mirrors the enable/disable lifecycle
    /// of <see cref="HeatVisionBeamRenderer"/>: shown for both the tap pulse
    /// and the sustained hold beam, hidden the instant the beam stops.
    /// </summary>
    public class HeatVisionDistortionEffect : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer distortionSprite;

        private void Awake()
        {
            SetActive(false);
        }

        public void SetActive(bool active)
        {
            if (distortionSprite != null)
            {
                distortionSprite.enabled = active;
            }
        }
    }
}
