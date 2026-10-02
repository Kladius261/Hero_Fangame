using System.Collections;
using UnityEngine;

namespace HeroFangame.Core
{
    /// <summary>
    /// One-shot full-screen shockwave pulse for a dynamically instantiated
    /// prefab (e.g. Flight takeoff/landing): drives the ShockWaveScreen
    /// material's _WaveDistanceFromCenter from waveDistanceStart to
    /// waveDistanceEnd over duration via a MaterialPropertyBlock (no material
    /// instance leak), then destroys this GameObject. Unlike ShockwaveEffect
    /// (PlayAt/reused), this is meant to be Instantiate()'d fresh each time
    /// and cleans itself up — no pooling.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class ShockwavePulseEffect : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private float waveDistanceStart = -0.1f;
        [SerializeField] private float waveDistanceEnd = 0.5f;
        [SerializeField] private float duration = 0.5f;

        private static readonly int WaveDistanceFromCenterId = Shader.PropertyToID("_WaveDistanceFromCenter");

        private MaterialPropertyBlock propertyBlock;

        private void Awake()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<SpriteRenderer>();
            }
            propertyBlock = new MaterialPropertyBlock();
        }

        private void Start()
        {
            StartCoroutine(PulseRoutine());
        }

        private IEnumerator PulseRoutine()
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float value = Mathf.Lerp(waveDistanceStart, waveDistanceEnd, Mathf.Clamp01(t / duration));
                propertyBlock.SetFloat(WaveDistanceFromCenterId, value);
                targetRenderer.SetPropertyBlock(propertyBlock);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
