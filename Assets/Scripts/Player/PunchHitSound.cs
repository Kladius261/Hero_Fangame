using UnityEngine;

namespace HeroFangame.Player
{
    /// <summary>
    /// Plays a layered regular-hit impact sound each time a punch connects:
    /// one clip is independently picked (no immediate repeat) from each of
    /// three pools and all three are played together via PlayOneShot, which
    /// natively mixes overlapping calls on a single AudioSource. Mirrors the
    /// same per-layer random-no-repeat convention as
    /// ExplosiveObject.PlayExplosionSound/EnemyFreezeVisualEffect.PlayBreakFreeSound.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PunchHitSound : MonoBehaviour
    {
        [SerializeField] private AudioClip[] layer1Clips;
        [SerializeField] private AudioClip[] layer2Clips;
        [SerializeField] private AudioClip[] layer3Clips;

        private AudioSource audioSource;
        private int lastLayer1Index = -1;
        private int lastLayer2Index = -1;
        private int lastLayer3Index = -1;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
        }

        public void PlayRandom()
        {
            PlayLayer(layer1Clips, ref lastLayer1Index);
            PlayLayer(layer2Clips, ref lastLayer2Index);
            PlayLayer(layer3Clips, ref lastLayer3Index);
        }

        private void PlayLayer(AudioClip[] clips, ref int lastIndex)
        {
            if (clips == null || clips.Length == 0)
            {
                return;
            }

            int index;
            do
            {
                index = Random.Range(0, clips.Length);
            }
            while (clips.Length > 1 && index == lastIndex);
            lastIndex = index;

            var clip = clips[index];
            if (clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }
    }
}
