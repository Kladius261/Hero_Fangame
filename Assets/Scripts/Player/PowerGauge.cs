using UnityEngine;

namespace HeroFangame.Player
{
    /// <summary>
    /// Shared resource drained by Heat Vision / Freeze Breath. Regenerates
    /// quickly after a short delay once nothing has spent it. Movement,
    /// flight, and punches never touch this.
    /// </summary>
    public class PowerGauge : MonoBehaviour
    {
        [SerializeField] private float max = 100f;
        [SerializeField] private float regenPerSecond = 40f;
        [SerializeField] private float regenDelay = 0.5f;

        public float Max => max;
        public float Current { get; private set; }

        private float regenDelayRemaining;

        private void Awake()
        {
            Current = max;
        }

        private void Update()
        {
            if (regenDelayRemaining > 0f)
            {
                regenDelayRemaining -= Time.deltaTime;
                return;
            }

            if (Current < max)
            {
                Current = Mathf.Min(max, Current + regenPerSecond * Time.deltaTime);
            }
        }

        /// <summary>Spends a flat amount instantly (e.g. a tap). Returns false if insufficient.</summary>
        public bool TrySpend(float amount)
        {
            if (Current < amount)
            {
                return false;
            }
            Current -= amount;
            regenDelayRemaining = regenDelay;
            return true;
        }

        /// <summary>Drains continuously (e.g. while held). Returns the amount actually drained.</summary>
        public float DrainOverTime(float ratePerSecond)
        {
            float amount = Mathf.Min(Current, ratePerSecond * Time.deltaTime);
            Current -= amount;
            regenDelayRemaining = regenDelay;
            return amount;
        }
    }
}
