using UnityEngine;

namespace HeroFangame.Core
{
    /// <summary>
    /// Shared throw-landing impact sound for grabbable objects that aren't
    /// explosive (ExplosiveObject plays its own explosion sound instead) and
    /// weren't frozen at landing (that case plays the existing break-free
    /// sound via EnemyFreezeVisualEffect instead) -- see
    /// DestructibleObject.Land / EnemyRobot.Land, which only call PlayThrow
    /// from their non-frozen branch. One clip is independently picked (no
    /// immediate repeat) from each of two pools and both are played
    /// together via PlayOneShot.
    ///
    /// The no-repeat tracking is static/shared across every instance of
    /// this component (every Destructible/EnemyRobot in the scene) rather
    /// than per-instance, mirroring ExplosiveObject.lastHeavyHitClipIndex --
    /// the player hears a sequence of throw-landing sounds regardless of
    /// which object produced them, so the "don't repeat" rule has to be
    /// tracked globally to actually read as non-repeating.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class ThrowImpactSound : MonoBehaviour
    {
        [SerializeField] private AudioClip[] layer1Clips;
        [SerializeField] private AudioClip[] layer2Clips;

        private AudioSource audioSource;

        private static int lastLayer1Index = -1;
        private static int lastLayer2Index = -1;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
        }

        public void PlayThrow()
        {
            PlayLayer(layer1Clips, ref lastLayer1Index);
            PlayLayer(layer2Clips, ref lastLayer2Index);
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
