using UnityEngine;

namespace HeroFangame.Player
{
    /// <summary>
    /// Plays one of three randomized hit-spark variants at a punch's
    /// contact point. Follows the same "pre-placed, repositioned
    /// ParticleSystem" convention as HeatVisionImpactEffect rather than
    /// Instantiate/Destroy: each variant is a persistent one-shot
    /// ParticleSystem that gets moved to the contact point and
    /// re-triggered via Play().
    /// </summary>
    public class PunchHitEffect : MonoBehaviour
    {
        [SerializeField] private ParticleSystem starburstHit;
        [SerializeField] private ParticleSystem circularHit;
        [SerializeField] private ParticleSystem slashHit;

        private ParticleSystem[] variants;

        private void Awake()
        {
            variants = new[] { starburstHit, circularHit, slashHit };
        }

        public void PlayRandomAt(Vector2 worldPosition)
        {
            var variant = variants[Random.Range(0, variants.Length)];
            if (variant == null)
            {
                return;
            }
            variant.transform.position = worldPosition;
            variant.Play(true); // withChildren: true, needed for SlashHit's two diagonal sub-emitters
        }
    }
}
