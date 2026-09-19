using UnityEngine;

namespace HeroFangame.Player
{
    /// <summary>
    /// Plays a random regular-hit impact sound from a pool each time a
    /// punch connects. Mirrors PunchHitEffect's "pool + random pick"
    /// convention, but for audio. Avoids repeating the same clip twice in
    /// a row so a rapid combo doesn't sound like it's looping one sample.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PunchHitSound : MonoBehaviour
    {
        [SerializeField] private AudioClip[] hitClips;

        private AudioSource audioSource;
        private int lastClipIndex = -1;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
        }

        public void PlayRandom()
        {
            if (hitClips == null || hitClips.Length == 0)
            {
                return;
            }

            int index;
            do
            {
                index = Random.Range(0, hitClips.Length);
            }
            while (hitClips.Length > 1 && index == lastClipIndex);
            lastClipIndex = index;

            var clip = hitClips[index];
            if (clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }
    }
}
