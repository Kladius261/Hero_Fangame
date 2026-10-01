using System.Collections;
using UnityEngine;

namespace HeroFangame.Camera
{
    /// <summary>
    /// Large drifting snow particles banked along the four screen edges,
    /// shown alongside Freeze Breath's full-screen shader vignette
    /// (<see cref="FullScreenAbilityEffect"/>). Lives as a child of
    /// Camera.main so it always frames the current viewport regardless of
    /// camera position; all eight edge particle systems (four hard
    /// diamond shards + four soft glow layered on top) are re-sized every
    /// frame from the camera's live orthographic size/aspect so the band
    /// keeps hugging the screen edges even if those ever change at
    /// runtime (e.g. a future zoom). The two layers share one fade-in
    /// duration but fade out independently (see <see cref="SetActive"/>),
    /// since the soft glow is meant to read as snapping away faster than
    /// the hard diamonds.
    /// Reuses Freeze Breath's own mist material/look (see
    /// FreezeBreathConeEffect's MistParticles) rather than introducing a
    /// new art asset -- each edge particle system is pre-authored in the
    /// Editor (same convention as FreezeBreathConeEffect's serialized
    /// particle references) and this script only ever touches their
    /// shape/emission at runtime, never other modules.
    /// </summary>
    public class FreezeBreathScreenSnow : MonoBehaviour
    {
        [Header("Edge Particle Systems (hard diamond shards)")]
        [SerializeField] private ParticleSystem topEdge;
        [SerializeField] private ParticleSystem bottomEdge;
        [SerializeField] private ParticleSystem leftEdge;
        [SerializeField] private ParticleSystem rightEdge;

        [Header("Soft Edge Particle Systems (round glow, layered on top)")]
        [SerializeField] private ParticleSystem topEdgeSoft;
        [SerializeField] private ParticleSystem bottomEdgeSoft;
        [SerializeField] private ParticleSystem leftEdgeSoft;
        [SerializeField] private ParticleSystem rightEdgeSoft;

        [Header("Framing")]
        [SerializeField] private float edgeMargin = 0.5f;
        [SerializeField] private float edgeBandThickness = 3f;

        public static FreezeBreathScreenSnow Instance { get; private set; }

        /// <summary>
        /// Fades independently from <see cref="softGroup"/> so the hard
        /// diamond shards and the soft glow layered on top can each use
        /// their own fade-out duration (e.g. the soft layer snapping away
        /// quicker than the diamonds) despite both being driven by the
        /// same single <see cref="SetActive"/> call.
        /// </summary>
        private class EdgeGroup
        {
            public ParticleSystem[] systems;
            public float[] baseRates;
            public float emissionMultiplier;
            public Coroutine fadeRoutine;
        }

        private EdgeGroup hardGroup;
        private EdgeGroup softGroup;
        private UnityEngine.Camera cam;

        private void Awake()
        {
            Instance = this;
            cam = UnityEngine.Camera.main;
            hardGroup = BuildGroup(new[] { topEdge, bottomEdge, leftEdge, rightEdge });
            softGroup = BuildGroup(new[] { topEdgeSoft, bottomEdgeSoft, leftEdgeSoft, rightEdgeSoft });
        }

        private EdgeGroup BuildGroup(ParticleSystem[] systems)
        {
            var group = new EdgeGroup { systems = systems, baseRates = new float[systems.Length] };
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] == null)
                {
                    continue;
                }
                group.baseRates[i] = systems[i].emission.rateOverTime.constant;
                var emission = systems[i].emission;
                emission.rateOverTimeMultiplier = 0f;
                systems[i].Play();
            }
            return group;
        }

        private void LateUpdate()
        {
            if (cam == null)
            {
                return;
            }
            ResizeEdges();
        }

        /// <summary>
        /// Single entry point called from FullScreenAbilityEffect whenever
        /// Freeze Breath's own screen effect activates/releases. Fade-in
        /// is shared by both layers; fade-out is independent per layer so
        /// the soft glow can snap away faster than the hard diamonds.
        /// </summary>
        public void SetActive(bool active, float fadeInDuration, float hardFadeOutDuration, float softFadeOutDuration)
        {
            RestartFade(hardGroup, active, fadeInDuration, hardFadeOutDuration);
            RestartFade(softGroup, active, fadeInDuration, softFadeOutDuration);
        }

        private void RestartFade(EdgeGroup group, bool targetOn, float fadeInDuration, float fadeOutDuration)
        {
            if (group.fadeRoutine != null)
            {
                StopCoroutine(group.fadeRoutine);
            }
            float duration = Mathf.Max(0.0001f, targetOn ? fadeInDuration : fadeOutDuration);
            group.fadeRoutine = StartCoroutine(FadeRoutine(group, targetOn, duration));
        }

        private IEnumerator FadeRoutine(EdgeGroup group, bool targetOn, float duration)
        {
            float target = targetOn ? 1f : 0f;

            while (!Mathf.Approximately(group.emissionMultiplier, target))
            {
                group.emissionMultiplier = Mathf.MoveTowards(group.emissionMultiplier, target, Time.unscaledDeltaTime / duration);
                ApplyEmissionMultiplier(group);
                yield return null;
            }

            group.emissionMultiplier = target;
            ApplyEmissionMultiplier(group);
            group.fadeRoutine = null;
        }

        private void ApplyEmissionMultiplier(EdgeGroup group)
        {
            for (int i = 0; i < group.systems.Length; i++)
            {
                if (group.systems[i] == null)
                {
                    continue;
                }
                // rateOverTimeMultiplier IS the curve's constant value (not
                // an independent 0-1 scale layered on top of a preserved
                // base rate), so the base rate captured in Awake has to be
                // reapplied here every time, scaled by the fade fraction.
                var emission = group.systems[i].emission;
                emission.rateOverTimeMultiplier = group.baseRates[i] * group.emissionMultiplier;
            }
        }

        private void ResizeEdges()
        {
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            float fullWidthWithBand = halfWidth * 2f + edgeBandThickness;
            float fullHeightWithBand = halfHeight * 2f + edgeBandThickness;

            var topPos = new Vector3(0f, halfHeight + edgeMargin, 0f);
            var bottomPos = new Vector3(0f, -(halfHeight + edgeMargin), 0f);
            var leftPos = new Vector3(-(halfWidth + edgeMargin), 0f, 0f);
            var rightPos = new Vector3(halfWidth + edgeMargin, 0f, 0f);
            var horizontalSize = new Vector2(fullWidthWithBand, edgeBandThickness);
            var verticalSize = new Vector2(edgeBandThickness, fullHeightWithBand);

            PositionEdge(topEdge, topPos, horizontalSize);
            PositionEdge(bottomEdge, bottomPos, horizontalSize);
            PositionEdge(leftEdge, leftPos, verticalSize);
            PositionEdge(rightEdge, rightPos, verticalSize);

            PositionEdge(topEdgeSoft, topPos, horizontalSize);
            PositionEdge(bottomEdgeSoft, bottomPos, horizontalSize);
            PositionEdge(leftEdgeSoft, leftPos, verticalSize);
            PositionEdge(rightEdgeSoft, rightPos, verticalSize);
        }

        private static void PositionEdge(ParticleSystem ps, Vector3 localPosition, Vector2 boxSize)
        {
            if (ps == null)
            {
                return;
            }
            ps.transform.localPosition = localPosition;
            var shape = ps.shape;
            shape.scale = new Vector3(boxSize.x, boxSize.y, 0.3f);
        }
    }
}
