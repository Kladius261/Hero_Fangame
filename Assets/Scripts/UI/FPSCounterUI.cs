using UnityEngine;
using UnityEngine.UI;

namespace HeroFangame.UI
{
    /// <summary>
    /// Top-right on-screen FPS counter for playtesting. Procedurally builds
    /// its own Canvas/Text the first time it's needed - mirrors
    /// CameraShake.GetOrCreate()/PowerGaugeUI's lazy self-bootstrapping
    /// pattern, so no manual Canvas/prefab setup is required in the scene.
    /// Unlike PowerGaugeUI it isn't driven by another gameplay script's
    /// Awake(), since nothing naturally "owns" an FPS readout - instead it
    /// self-bootstraps via RuntimeInitializeOnLoadMethod, and only inside
    /// the Editor, since it exists purely to gauge performance while
    /// playtesting (e.g. with 6 enemies now roaming a scene) rather than
    /// something players should see in a real build. Samples
    /// Time.unscaledDeltaTime every frame and reports an average over a
    /// short rolling window (rather than a raw, jittery 1/deltaTime) both
    /// on-screen and to the Console at the same fixed interval.
    /// </summary>
    public class FPSCounterUI : MonoBehaviour
    {
        public static FPSCounterUI Instance { get; private set; }

        [Header("Position")]
        [SerializeField] private float margin = 24f;

        [Header("Sampling")]
        [SerializeField] private float updateInterval = 0.5f;

        [Header("Color Thresholds")]
        [SerializeField] private float goodFpsThreshold = 50f;
        [SerializeField] private float okFpsThreshold = 30f;
        [SerializeField] private Color goodColor = new Color(0.15f, 1f, 0.4f, 1f);
        [SerializeField] private Color okColor = new Color(1f, 0.85f, 0.1f, 1f);
        [SerializeField] private Color badColor = new Color(1f, 0.2f, 0.2f, 1f);

        private Text label;
        private float accumulatedTime;
        private int accumulatedFrames;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            GetOrCreate();
        }
#endif

        /// <summary>Lazily creates the singleton overlay the first time it's needed.</summary>
        public static FPSCounterUI GetOrCreate()
        {
            if (Instance == null)
            {
                var go = new GameObject("FPSCounterUI");
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<FPSCounterUI>();
                Instance.BuildUI();
            }

            return Instance;
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("FPSCounterCanvas");
            canvasGO.transform.SetParent(transform, false);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // Top-right anchored label, mirroring PowerGaugeUI's top-left bar.
            var labelGO = new GameObject("FPSLabel", typeof(RectTransform));
            labelGO.transform.SetParent(canvasGO.transform, false);
            var labelRect = (RectTransform)labelGO.transform;
            labelRect.anchorMin = new Vector2(1f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(1f, 1f);
            labelRect.anchoredPosition = new Vector2(-margin, -margin);
            labelRect.sizeDelta = new Vector2(220f, 30f);

            label = labelGO.AddComponent<Text>();
            label.text = "FPS: --";
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 20;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.UpperRight;
            label.color = goodColor;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;

            var labelShadow = labelGO.AddComponent<Shadow>();
            labelShadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            labelShadow.effectDistance = new Vector2(1.5f, -1.5f);
        }

        private void Update()
        {
            accumulatedTime += Time.unscaledDeltaTime;
            accumulatedFrames++;

            if (accumulatedTime < updateInterval)
            {
                return;
            }

            float fps = accumulatedFrames / accumulatedTime;
            accumulatedTime = 0f;
            accumulatedFrames = 0;

            if (label != null)
            {
                label.text = $"FPS: {fps:F0}";
                label.color = fps >= goodFpsThreshold
                    ? goodColor
                    : fps >= okFpsThreshold ? okColor : badColor;
            }

            Debug.Log($"[FPSCounterUI] {fps:F1} FPS ({1000f / Mathf.Max(fps, 0.001f):F1} ms/frame)");
        }
    }
}
