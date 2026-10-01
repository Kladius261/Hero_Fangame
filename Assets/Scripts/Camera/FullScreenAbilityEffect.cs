using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HeroFangame.Camera
{
    /// <summary>
    /// Drives the full-screen post-process effect for whichever of Heat
    /// Vision / Freeze Breath / Flight is currently active, by swapping
    /// Renderer2D's FullScreenPassRendererFeature.passMaterial and lerping
    /// each material's _VoronoiIntensity/_VignetteIntensity in on
    /// activation and out on release. Relies entirely on the abilities'
    /// existing mutual exclusivity (AbilityLock + PlayerController's
    /// IsMovementLocked/IsFlightMode) -- this class just reacts to
    /// SetActive() calls from each ability's own visual on/off transition
    /// points and never needs to arbitrate between them itself.
    /// Also separately drives a second, independent full-screen pass
    /// (Renderer2D.asset's "FlightWindPass" feature) that layers
    /// FlightWindMAT on top of Flight's own shader for the duration of a
    /// Charge-Crash dash -- see SetFlightWindActive.
    /// </summary>
    public class FullScreenAbilityEffect : MonoBehaviour
    {
        public enum Kind { None, HeatVision, FreezeBreath, Flight }

        [SerializeField] private ScriptableRendererData rendererData;
        [SerializeField] private Material heatVisionSource;
        [SerializeField] private Material freezeBreathSource;
        [SerializeField] private Material flightSource;

        [Header("Flight Charge-Crash Wind Overlay (instant on/off, no fade)")]
        [Tooltip("Layered on top of the Flight shader above via a second FullScreenPassRendererFeature ('FlightWindPass' on Renderer2D.asset), active only for the duration of a Charge-Crash dash. Assigned directly as passMaterial -- no runtime instancing or value lerping, since this material's own shader handles its look entirely on its own (unlike heatVisionSource/freezeBreathSource/flightSource above, which get lerped _VoronoiIntensity/_VignetteIntensity instances).")]
        [SerializeField] private Material flightWindSource;

        [Header("Fade Durations")]
        [SerializeField] private float heatVisionFadeInDuration = 0.125f;
        [SerializeField] private float heatVisionFadeOutDuration = 0.75f;
        [SerializeField] private float freezeBreathFadeInDuration = 0.75f;
        [SerializeField] private float freezeBreathFadeOutDuration = 1.5f;
        [SerializeField] private float flightFadeInDuration = 0.25f;
        [SerializeField] private float flightFadeOutDuration = 0.35f;

        [Header("Freeze Breath Screen Snow Fade (independent of shader fade above)")]
        [SerializeField] private float freezeBreathSnowFadeInDuration = 0.5f;
        [SerializeField] private float freezeBreathSnowFadeOutDuration = 0.25f;
        [SerializeField] private float freezeBreathSnowSoftFadeOutDuration = 0.5f;

        public static FullScreenAbilityEffect Instance { get; private set; }

        private static readonly int VoronoiId = Shader.PropertyToID("_VoronoiIntensity");
        private static readonly int VignetteId = Shader.PropertyToID("_VignetteIntensity");

        private FullScreenPassRendererFeature feature;
        private FullScreenPassRendererFeature flightWindFeature;
        private Material heatVisionInstance, freezeBreathInstance, flightInstance;
        private Kind currentKind;
        private Coroutine fadeRoutine;

        private void Awake()
        {
            Instance = this;

            heatVisionInstance = new Material(heatVisionSource);
            freezeBreathInstance = new Material(freezeBreathSource);
            flightInstance = new Material(flightSource);

            if (rendererData == null || !rendererData.TryGetRendererFeature(out feature))
            {
                Debug.LogWarning("FullScreenAbilityEffect: renderer feature not found; screen effects disabled.", this);
                return;
            }
            feature.passMaterial = null;

            // TryGetRendererFeature<T> can't disambiguate between two
            // features of the same FullScreenPassRendererFeature type, so
            // the Charge-Crash wind overlay's own feature ("FlightWindPass")
            // has to be found by name instead.
            foreach (var candidate in rendererData.rendererFeatures)
            {
                if (candidate is FullScreenPassRendererFeature fullScreenFeature && candidate.name == "FlightWindPass")
                {
                    flightWindFeature = fullScreenFeature;
                    break;
                }
            }
            if (flightWindFeature == null)
            {
                Debug.LogWarning("FullScreenAbilityEffect: 'FlightWindPass' renderer feature not found; Charge-Crash wind overlay disabled.", this);
            }
            else
            {
                flightWindFeature.passMaterial = null;
            }
        }

        /// <summary>
        /// Single entry point ability scripts call from their existing
        /// visual on/off transition points. Safe to call every frame while
        /// an ability is held (idempotent) and safe to call once on
        /// single-fire transitions (e.g. Flight's EnterFlight/Land).
        /// </summary>
        public void SetActive(Kind kind, bool active)
        {
            if (kind == Kind.FreezeBreath)
            {
                // Forwarded unconditionally (not gated by currentKind like
                // the shader fade below) -- FreezeBreathAbility only ever
                // calls its own kind here, so there's no "stale release
                // from a non-owning kind" case to filter the way there is
                // for the shared full-screen material below. Uses its own
                // (faster) fade durations rather than the shader's -- the
                // snow reads better snapping in/out quicker than the
                // full-screen vignette it accompanies.
                FreezeBreathScreenSnow.Instance?.SetActive(active, freezeBreathSnowFadeInDuration, freezeBreathSnowFadeOutDuration, freezeBreathSnowSoftFadeOutDuration);
            }

            if (feature == null)
            {
                return;
            }

            if (active)
            {
                if (kind != currentKind)
                {
                    var instance = GetInstance(kind);
                    instance.SetFloat(VoronoiId, 0f);
                    instance.SetFloat(VignetteId, 0f);
                    feature.passMaterial = instance;
                    currentKind = kind;
                }
                RestartFade(targetOn: true);
            }
            else if (kind == currentKind)
            {
                RestartFade(targetOn: false);
            }
            // else: stale release for a kind that doesn't currently own the
            // screen (e.g. Heat Vision bailing out because Flight took
            // over) -- ignore, must not clear Flight's effect.
        }

        /// <summary>
        /// Called directly from FlightAbility's StartCharge/EndCharge --
        /// unlike SetActive above, this is a plain instant on/off with no
        /// fade: FlightWindMAT's own shader fully defines its look, so
        /// there's no exposed intensity value to lerp. Layers on top of
        /// whatever Flight's own shader (above) is currently showing via a
        /// second, independent FullScreenPassRendererFeature later in
        /// Renderer2D.asset's feature list.
        /// </summary>
        public void SetFlightWindActive(bool active)
        {
            if (flightWindFeature == null)
            {
                return;
            }
            flightWindFeature.passMaterial = active ? flightWindSource : null;
        }

        private void RestartFade(bool targetOn)
        {
            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
            }
            fadeRoutine = StartCoroutine(FadeRoutine(targetOn));
        }

        private IEnumerator FadeRoutine(bool targetOn)
        {
            var mat = GetInstance(currentKind);
            var source = GetSource(currentKind);
            float voronoiTarget = targetOn ? source.GetFloat(VoronoiId) : 0f;
            float vignetteTarget = targetOn ? source.GetFloat(VignetteId) : 0f;
            float duration = Mathf.Max(0.0001f, GetFadeDuration(currentKind, targetOn));

            float voronoi = mat.GetFloat(VoronoiId);
            float vignette = mat.GetFloat(VignetteId);
            float voronoiSpan = Mathf.Max(Mathf.Abs(source.GetFloat(VoronoiId)), 0.0001f);
            float vignetteSpan = Mathf.Max(Mathf.Abs(source.GetFloat(VignetteId)), 0.0001f);

            while (voronoi != voronoiTarget || vignette != vignetteTarget)
            {
                float rate = Time.unscaledDeltaTime / duration;
                voronoi = Mathf.MoveTowards(voronoi, voronoiTarget, voronoiSpan * rate);
                vignette = Mathf.MoveTowards(vignette, vignetteTarget, vignetteSpan * rate);
                mat.SetFloat(VoronoiId, voronoi);
                mat.SetFloat(VignetteId, vignette);
                yield return null;
            }

            if (!targetOn)
            {
                feature.passMaterial = null;
                currentKind = Kind.None;
            }
            fadeRoutine = null;
        }

        private float GetFadeDuration(Kind kind, bool fadeIn) => kind switch
        {
            Kind.HeatVision => fadeIn ? heatVisionFadeInDuration : heatVisionFadeOutDuration,
            Kind.FreezeBreath => fadeIn ? freezeBreathFadeInDuration : freezeBreathFadeOutDuration,
            Kind.Flight => fadeIn ? flightFadeInDuration : flightFadeOutDuration,
            _ => 0.25f,
        };

        private Material GetInstance(Kind kind) => kind switch
        {
            Kind.HeatVision => heatVisionInstance,
            Kind.FreezeBreath => freezeBreathInstance,
            Kind.Flight => flightInstance,
            _ => null,
        };

        private Material GetSource(Kind kind) => kind switch
        {
            Kind.HeatVision => heatVisionSource,
            Kind.FreezeBreath => freezeBreathSource,
            Kind.Flight => flightSource,
            _ => null,
        };
    }
}
