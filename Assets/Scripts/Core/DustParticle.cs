using System.Collections;
using UnityEngine;

namespace HeroFangame.Core
{
    /// <summary>
    /// Single short-lived dust mote: drifts upward slightly, grows, and fades
    /// out on unscaled time (so it isn't frozen by HitStop), then destroys
    /// itself — same "cheap, short-lived, no pooling" convention as
    /// GhostTrailEffect/ShockwavePulseEffect. Meant to be spawned in clumps
    /// via <see cref="SpawnCluster"/> rather than one at a time.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class DustParticle : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private float fadeDuration = 0.45f;
        [SerializeField] private float driftDistance = 0.15f;
        [SerializeField] private float growthFactor = 1.15f;
        [SerializeField] private int sortingOrder = 30;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }
            spriteRenderer.sortingOrder = sortingOrder;
        }

        private void Start()
        {
            StartCoroutine(FadeDriftAndDestroy());
        }

        private IEnumerator FadeDriftAndDestroy()
        {
            Vector3 startPosition = transform.position;
            Vector3 endPosition = startPosition + Vector3.up * driftDistance;
            Vector3 startScale = transform.localScale;
            Vector3 endScale = startScale * growthFactor;
            Color startColor = spriteRenderer.color;

            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(t / fadeDuration);
                transform.position = Vector3.Lerp(startPosition, endPosition, normalized);
                transform.localScale = Vector3.Lerp(startScale, endScale, normalized);
                spriteRenderer.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(startColor.a, 0f, normalized));
                yield return null;
            }

            Destroy(gameObject);
        }

        /// <summary>
        /// Spawns several loose clumps of randomized dust motes around
        /// <paramref name="position"/> — randomized clump count, per-clump
        /// center offset, mote count, position offset, scale, and rotation,
        /// so the burst reads as a voluminous puff of kicked-up dust rather
        /// than a single repeated sprite. Mirrors
        /// ExplosiveObject.SpawnBurstVfxCluster's randomization shape.
        /// No-ops if <paramref name="dustPrefab"/> is unassigned.
        /// </summary>
        public static void SpawnCluster(
            GameObject dustPrefab,
            Vector3 position,
            int minClumps = 3,
            int maxClumps = 4,
            int minCount = 3,
            int maxCount = 5,
            float clumpSpread = 0.8f,
            float radius = 0.3f,
            float minScale = 0.6f,
            float maxScale = 1.3f,
            float lifetime = 0.6f,
            float verticalSpreadScale = 1f)
        {
            if (dustPrefab == null)
            {
                return;
            }

            int clumpCount = Random.Range(minClumps, maxClumps + 1);
            for (int c = 0; c < clumpCount; c++)
            {
                // verticalSpreadScale flattens the clump-center distribution
                // along Y — 1 (default) is a full circle around the anchor,
                // 0 confines clumps to a strictly horizontal band through it
                // (e.g. Flight takeoff/landing fanning out sideways from the
                // feet instead of ballooning upward).
                Vector2 clumpOffset = Random.insideUnitCircle * clumpSpread;
                clumpOffset.y *= verticalSpreadScale;
                Vector3 clumpCenter = position + (Vector3)clumpOffset;
                int count = Random.Range(minCount, maxCount + 1);
                for (int i = 0; i < count; i++)
                {
                    Vector2 offset = Random.insideUnitCircle * radius;
                    float zRotation = Random.Range(0f, 360f);
                    var clone = Instantiate(dustPrefab, clumpCenter + (Vector3)offset, Quaternion.Euler(0f, 0f, zRotation));
                    clone.transform.localScale = Vector3.one * Random.Range(minScale, maxScale);
                    Destroy(clone, lifetime);
                }
            }
        }
    }
}
