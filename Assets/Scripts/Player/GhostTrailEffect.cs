using System.Collections;
using UnityEngine;

namespace HeroFangame.Player
{
    /// <summary>
    /// Spawns short-lived, fading afterimage clones of the player's sprite
    /// while active — used both by Flight's sustained hover (a sparser
    /// trail for as long as Flight is held) and by Charge's fast
    /// uninterruptible dash (a denser trail, restarted at Charge's own
    /// interval so the two speeds read distinctly). No pooling: clones are
    /// cheap, short-lived, and flight is infrequent enough overall that a
    /// self-destructing GameObject per spawn is simpler than adding a
    /// pooling system the project doesn't otherwise have. Fades on unscaled
    /// time (matching HitSquashEffect/CameraShake) so trail ghosts still
    /// animate smoothly through a HitStop freeze-frame.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class GhostTrailEffect : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer sourceRenderer;
        [SerializeField] private Color ghostColor = new Color(0.5f, 0.85f, 1f, 0.5f);
        [Tooltip("Default spawn interval used when StartTrail is called without an explicit override (Charge uses this default). Doubled from the original 0.04s so Charge's ghosts sit twice as far apart along the dash, reading as a faster blur of sparse afterimages.")]
        [SerializeField] private float ghostTrailSpawnInterval = 0.08f;
        [SerializeField] private float ghostTrailFadeDuration = 0.15f;

        private Coroutine routine;

        private void Awake()
        {
            if (sourceRenderer == null)
            {
                sourceRenderer = GetComponent<SpriteRenderer>();
            }
        }

        /// <summary>
        /// Starts spawning ghosts at <paramref name="spawnInterval"/> (or the
        /// serialized default if null). If a trail is already running and
        /// <paramref name="restart"/> is false, this is a no-op — callers
        /// that need to switch an already-running trail to a different
        /// density (e.g. Flying hover switching to Charge's denser rate,
        /// or back) must pass restart: true.
        /// </summary>
        public void StartTrail(float? spawnInterval = null, bool restart = false)
        {
            if (routine != null)
            {
                if (!restart)
                {
                    return;
                }
                StopCoroutine(routine);
                routine = null;
            }
            routine = StartCoroutine(TrailRoutine(spawnInterval ?? ghostTrailSpawnInterval));
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

        private IEnumerator TrailRoutine(float interval)
        {
            var wait = new WaitForSecondsRealtime(interval);
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
