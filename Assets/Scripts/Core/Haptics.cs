using UnityEngine;
using UnityEngine.InputSystem;

namespace HeroFangame.Core
{
    /// <summary>
    /// Lazily-created singleton driving the active Gamepad's rumble motors,
    /// mirroring CameraShake/HitStop's GetOrCreate() pattern. Pulse() fires a
    /// brief, self-clearing rumble for a one-off impact (extends rather than
    /// restarts if a stronger/longer pulse is already running — same
    /// "extend, don't restart" rule as CameraShake.Pulse). SetContinuous()
    /// toggles a sustained rumble tied to an ability being held (e.g. Heat
    /// Vision's beam), exactly like CameraShake.SetShaking(). A pulse always
    /// wins over the continuous rumble while it's running. No-ops safely
    /// when no gamepad is connected (keyboard-only play).
    /// </summary>
    public class Haptics : MonoBehaviour
    {
        public static Haptics Instance { get; private set; }

        private float pulseTimeRemaining;
        private float pulseLow;
        private float pulseHigh;

        private bool continuousActive;
        private float continuousLow;
        private float continuousHigh;

        public static Haptics GetOrCreate()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var go = new GameObject("Haptics");
            Object.DontDestroyOnLoad(go);
            Instance = go.AddComponent<Haptics>();
            return Instance;
        }

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// Fires a brief rumble pulse of the given duration that clears
        /// itself. Safe to call repeatedly; a new pulse only extends the
        /// rumble if it would last longer than what's already remaining,
        /// taking over its low/high frequency in that case.
        /// </summary>
        public void Pulse(float duration, float lowFrequency, float highFrequency)
        {
            if (duration >= pulseTimeRemaining)
            {
                pulseLow = lowFrequency;
                pulseHigh = highFrequency;
            }
            pulseTimeRemaining = Mathf.Max(pulseTimeRemaining, duration);
        }

        /// <summary>
        /// Toggles a sustained rumble tied to an ability being held (e.g.
        /// Heat Vision's beam). Any active Pulse still takes priority while
        /// it's running.
        /// </summary>
        public void SetContinuous(bool active, float lowFrequency = 0f, float highFrequency = 0f)
        {
            continuousActive = active;
            continuousLow = lowFrequency;
            continuousHigh = highFrequency;
        }

        private void Update()
        {
            // Unscaled: several call sites (Punch, Charge-Crash) pair this
            // with HitStop.Trigger, which zeroes Time.timeScale for a beat —
            // a scaled countdown would freeze mid-pulse right along with it.
            if (pulseTimeRemaining > 0f)
            {
                pulseTimeRemaining -= Time.unscaledDeltaTime;
            }

            var pad = Gamepad.current;
            if (pad == null)
            {
                return;
            }

            bool pulsing = pulseTimeRemaining > 0f;
            float low = pulsing ? pulseLow : (continuousActive ? continuousLow : 0f);
            float high = pulsing ? pulseHigh : (continuousActive ? continuousHigh : 0f);
            pad.SetMotorSpeeds(low, high);
        }

        private void OnDisable()
        {
            Gamepad.current?.SetMotorSpeeds(0f, 0f);
        }

        private void OnApplicationQuit()
        {
            Gamepad.current?.SetMotorSpeeds(0f, 0f);
        }
    }
}
