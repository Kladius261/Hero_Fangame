using System.Collections;
using UnityEngine;

namespace HeroFangame.Player
{
    /// <summary>
    /// Spawns short-lived, fading afterimage clones of the player's sprite
    /// while active — used by Flight's Charge to leave a ghost trail behind
    /// a fast uninterruptible dash. No pooling: clones are cheap, short-lived,
    /// and Charges are infrequent, so a self-destructing GameObject per spawn
    /// is simpler than adding a pooling system the project doesn't otherwise
    /// have. Fades on unscaled time (matching HitSquashEffect/CameraShake)
    /// so trail ghosts still animate smoothly through a HitStop freeze-frame.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class GhostTrailEffect : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer sourceRenderer;
        [SerializeField] private Color ghostColor = new Color(0.5f, 0.85f, 1f, 0.5f);
        [SerializeField] private float ghostTrailSpawnInterval = 0.04f;
        [SerializeField] private float ghostTrailFadeDuration = 0.15f;

        private Coroutine routine;

        private void Awake()
        {
            if (sourceRenderer == null)
            {
                sourceRenderer = GetComponent<SpriteRenderer>();
            }
        }

        public void StartTrail()
        {
            if (routine != null)
            {
                return;
            }
            routine = StartCoroutine(TrailRoutine());
        }

        public void StopTrail()
        {
            if (routine == null)
            {
                return;
            }
            StopCoroutine(routine);
            routine = null;
        }

        private IEnumerator TrailRoutine()
        {
            var wait = new WaitForSecondsRealtime(ghostTrailSpawnInterval);
            while (true)
            {
                SpawnGhost();
                yield return wait;
            }
        }

        private void SpawnGhost()
        {
            var ghostObject = new GameObject("FlightChargeGhost");
            ghostObject.transform.SetPositionAndRotation(transform.position, transform.rotation);
            ghostObject.transform.localScale = transform.localScale;

            var ghostRenderer = ghostObject.AddComponent<SpriteRenderer>();
            ghostRenderer.sprite = sourceRenderer.sprite;
            ghostRenderer.flipX = sourceRenderer.flipX;
            ghostRenderer.flipY = sourceRenderer.flipY;
            ghostRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            ghostRenderer.sortingOrder = sourceRenderer.sortingOrder - 1;
            ghostRenderer.color = ghostColor;

            StartCoroutine(FadeAndDestroy(ghostObject, ghostRenderer));
        }

        private IEnumerator FadeAndDestroy(GameObject ghostObject, SpriteRenderer ghostRenderer)
        {
            Color startColor = ghostRenderer.color;
            float t = 0f;
            while (t < ghostTrailFadeDuration)
            {
                t += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(t / ghostTrailFadeDuration);
                ghostRenderer.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(startColor.a, 0f, normalized));
                yield return null;
            }
            Destroy(ghostObject);
        }
    }
}
