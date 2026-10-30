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
    /// together.
    ///
    /// Land() applies lethal self-damage to this object immediately after
    /// calling PlayThrow, which can destroy this GameObject (after a short
    /// death-shrink animation) before a PlayOneShot on its own AudioSource
    /// would finish playing -- audibly cutting the sound off. So, exactly
    /// like ExplosiveObject.PlayExplosionSound/PlayClip2D, each layer is
    /// played through its own disposable temp GameObject/AudioSource that
    /// outlives this one, rather than through a component on this object.
    ///
    /// The no-repeat tracking is static/shared across every instance of
    /// this component (every Destructible/EnemyRobot in the scene) rather
    /// than per-instance, mirroring ExplosiveObject.lastHeavyHitClipIndex --
    /// the player hears a sequence of throw-landing sounds regardless of
    /// which object produced them, so the "don't repeat" rule has to be
    /// tracked globally to actually read as non-repeating.
    /// </summary>
    public class ThrowImpactSound : MonoBehaviour
    {
        [SerializeField] private AudioClip[] layer1Clips;
        [SerializeField] private AudioClip[] layer2Clips;

        private static int lastLayer1Index = -1;
        private static int lastLayer2Index = -1;

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
                PlayClip2D(clip, transform.position);
            }
        }

        /// <summary>
        /// Non-positional equivalent of AudioSource.PlayClipAtPoint -- see
        /// ExplosiveObject.PlayClip2D for the full rationale (PlayClipAtPoint
        /// defaults to full 3D spatialBlend, which this 2D game's camera
        /// distance attenuates to near-silence). Spawning a dedicated temp
        /// GameObject/AudioSource per clip also means this sound keeps
        /// playing even after the object that triggered it is destroyed.
        /// </summary>
        private static void PlayClip2D(AudioClip clip, Vector3 position)
        {
            var go = new GameObject("OneShotThrowImpactAudio");
            go.transform.position = position;
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.clip = clip;
            source.spatialBlend = 0f;
            source.Play();
            Destroy(go, clip.length);
        }
    }
}
