using UnityEngine;

namespace HeroFangame.Player
{
    /// <summary>
    /// Plays the "swing" sound for every punch attempt, regardless of
    /// whether it actually connects -- the sound of the character's
    /// fists/legs moving through the air. First layer is a random pool
    /// (no immediate repeat); second layer is a single fixed clip that
    /// always plays alongside it. Called before hit detection in
    /// PlayerCombat.HandlePunch so it plays slightly before
    /// PunchHitSound.PlayRandom on frames where the punch lands.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PunchSwingSound : MonoBehaviour
    {
        [SerializeField] private AudioClip[] firstLayerClips;
        [SerializeField] private AudioClip secondLayerClip;

        private AudioSource audioSource;
        private int lastFirstLayerIndex = -1;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
        }

        public void PlaySwing()
        {
            if (firstLayerClips != null && firstLayerClips.Length > 0)
            {
                int index;
                do
                {
                    index = Random.Range(0, firstLayerClips.Length);
                }
                while (firstLayerClips.Length > 1 && index == lastFirstLayerIndex);
                lastFirstLayerIndex = index;

                var clip = firstLayerClips[index];
                if (clip != null)
                {
                    audioSource.PlayOneShot(clip);
                }
            }

            if (secondLayerClip != null)
            {
                audioSource.PlayOneShot(secondLayerClip);
            }
        }
    }
}
