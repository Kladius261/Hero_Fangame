using UnityEngine;

namespace HeroFangame.Player
{
    /// <summary>
    /// Thin wrapper around the HeatVisionDistortion shader graph sprite — a
    /// large looping heat-haze sprite that conveys the sense of heat while
    /// Heat Vision is actively firing. Unlike the old disabled-child
    /// pattern, this is now dynamically Instantiate()'d by
    /// <see cref="HeatVisionAbility"/> at the player's position when the
    /// beam starts and Destroy()'d the instant it stops, mirroring
    /// ShockwavePulseEffect's "cheap, short-lived, no pooling" convention
    /// (just with an externally-controlled lifetime instead of a fixed
    /// one-shot duration).
    /// </summary>
    public class HeatVisionDistortionEffect : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer distortionSprite;

        private void Awake()
        {
            if (distortionSprite == null)
            {
                distortionSprite = GetComponent<SpriteRenderer>();
            }
        }
    }
}
