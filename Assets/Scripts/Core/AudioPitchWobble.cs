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
    }
}
