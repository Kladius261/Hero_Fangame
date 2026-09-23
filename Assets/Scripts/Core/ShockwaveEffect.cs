using System.Collections;
using UnityEngine;

namespace HeroFangame.Core
{
    /// <summary>
    /// One-shot expanding circular shockwave rendered by a Shader Graph
    /// material (ChargeShockwave), triggered on demand via PlayAt(point) —
    /// currently used for Flight's Charge attack crashing into a wall or
    /// enemy. Repositions this pre-placed object to the impact point and
    /// drives the material's _Progress property from 0 to 1 over duration,
    /// same pre-placed/repositioned convention as flightCrashVFX and
    /// flightChargeDebrisVFX (no Instantiate/Destroy, no pooling). Animates
    /// on unscaled time and is restart-safe, same convention as
    /// HitSquashEffect/FlashEffect. Uses a MaterialPropertyBlock instead of
    /// material.SetFloat so no material instance is leaked.
    /// </summary>
    public class ShockwaveEffect : MonoBehaviour
    {
        [SerializeField] private MeshRenderer targetRenderer;
        [SerializeField] private float duration = 0.4f;
        [SerializeField] private float worldDiameter = 22f;

        private static readonly int ProgressId = Shader.PropertyToID("_Progress");

        private MaterialPropertyBlock propertyBlock;
        private Coroutine routine;

        private void Awake()
        {
            transform.localScale = new Vector3(worldDiameter, worldDiameter, 1f);
            propertyBlock = new MaterialPropertyBlock();
            if (targetRenderer != null)
            {
                targetRenderer.enabled = false;
            }
        }

        public void PlayAt(Vector2 point)
        {
            if (targetRenderer == null)
            {
                return;
            }
            transform.position = point;
            if (routine != null)
            {
                StopCoroutine(routine);
            }
            targetRenderer.enabled = true;
            routine = StartCoroutine(ShockwaveRoutine());
        }

        private IEnumerator ShockwaveRoutine()
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(t / duration);
                propertyBlock.SetFloat(ProgressId, progress);
                targetRenderer.SetPropertyBlock(propertyBlock);
                yield return null;
            }
            targetRenderer.enabled = false;
            routine = null;
        }
    }
}
