using UnityEngine;

namespace HeroFangame.Player
{
    /// <summary>
    /// Plays a single, fixed pick-up "whoosh" sound the instant the player
    /// grabs a grabbable object. No layering or randomness -- unlike the
    /// punch/throw impact sounds, this is a one-off cue with only one clip.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PlayerGrabThrowSound : MonoBehaviour
    {
        [SerializeField] private AudioClip grabClip;

        private AudioSource audioSource;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
        }

        public void PlayGrab()
        {
            if (grabClip != null)
            {
                audioSource.PlayOneShot(grabClip);
            }
        }
    }
}
