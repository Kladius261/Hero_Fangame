using UnityEngine;

namespace HeroFangame.Core
{
    /// <summary>
    /// Continuously oscillates an AudioSource's pitch along a sine wave
    /// while a looping sound is playing, giving it a subtle "breathing"
    /// register shift instead of a static, flat pitch. Shared by
    /// HeatVisionAbility's beam loop and FreezeBreathAbility's wind loop.
    /// </summary>
    public class AudioPitchWobble
    {
        private readonly float speed;
        private readonly float amplitude;
        private float elapsed;

        public AudioPitchWobble(float speed, float amplitude)
        {
            this.speed = speed;
            this.amplitude = amplitude;
        }

        /// <summary>
        /// Resets the sine phase so every fresh activation starts back at
        /// the sound's natural pitch rather than wherever the wave
        /// happened to be left off.
        /// </summary>
        public void Restart()
        {
            elapsed = 0f;
        }

        public void Apply(AudioSource source, float deltaTime)
        {
            if (source == null)
            {
                return;
            }
            elapsed += deltaTime;
            source.pitch = 1f + Mathf.Sin(elapsed * speed) * amplitude;
        }

        /// <summary>
        /// Same wobble as <see cref="Apply(AudioSource, float)"/>, but stamps
        /// the identical pitch onto two simultaneous AudioSources off a
        /// single advance of <see cref="elapsed"/> — calling the single-source
        /// overload twice in one frame would double-advance the phase and
        /// desync the two sources from each other.
        /// </summary>
        public void Apply(AudioSource source1, AudioSource source2, float deltaTime)
        {
            elapsed += deltaTime;
            float pitch = 1f + Mathf.Sin(elapsed * speed) * amplitude;
            if (source1 != null)
            {
                source1.pitch = pitch;
            }
            if (source2 != null)
            {
                source2.pitch = pitch;
            }
        }
    }
}
