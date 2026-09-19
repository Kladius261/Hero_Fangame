using UnityEngine;
using UnityEngine.UI;
using HeroFangame.Player;

namespace HeroFangame.UI
{
    /// <summary>
    /// Always-on power bar, top-left of the screen. Procedurally builds its
    /// own Canvas/Image hierarchy the first time it's needed — mirrors
    /// CameraShake.GetOrCreate()'s lazy self-bootstrapping pattern — so no
    /// Canvas/prefab setup is required anywhere in the scene. Reads
    /// PowerGauge.Current/Max every frame and drives a Filled/Horizontal
    /// Image's fillAmount, plus a bordered frame, a "POWER" label, a
    /// low/mid/high color ramp, and a pulse when running low so drain is
    /// easy to spot at a glance during play.
    /// </summary>
    public class PowerGaugeUI : MonoBehaviour
    {
        public static PowerGaugeUI Instance { get; private set; }

        [Header("Size & Position")]
        [SerializeField] private float barWidth = 340f;
        [SerializeField] private float barHeight = 34f;
        [SerializeField] private float margin = 24f;
        [SerializeField] private float borderThickness = 4f;

        [Header("Colors")]
        [SerializeField] private Color backgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.9f);
        [SerializeField] private Color borderColor = new Color(1f, 1f, 1f, 0.95f);
        [SerializeField] private Color fillColorHigh = new Color(0.15f, 1f, 0.85f, 1f);
        [SerializeField] private Color fillColorMid = new Color(1f, 0.85f, 0.1f, 1f);
        [SerializeField] private Color fillColorLow = new Color(1f, 0.15f, 0.15f, 1f);

        [Header("Low Power Pulse")]
        [SerializeField] private float lowPowerThreshold = 0.25f;
        [SerializeField] private float pulseSpeed = 6f;
        [SerializeField] private float pulseStrength = 0.35f;

        private PowerGauge target;
        private Image fillImage;
        private Image borderImage;
        private Text label;
        private static Sprite solidSprite;

        // Setting Image.fillAmount/.color marks the graphic dirty and forces a
        // canvas mesh rebuild — this HUD element is always on from the moment
        // the Player spawns, so writing every frame regardless of change costs
        // a rebuild forever, even fully idle. Track the last-applied fraction
        // and the border's rest/pulsing state so writes only happen when the
        // displayed value actually needs to change.
        private float lastFraction = -1f;
        private bool borderAtRestColor;

        /// <summary>Lazily builds a shared 1x1 opaque-white sprite used solely to make
        /// Image.Type.Filled actually clip its mesh (see fillImage setup in BuildUI).</summary>
        private static Sprite GetOrCreateSolidSprite()
        {
            if (solidSprite == null)
            {
                solidSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(0.5f, 0.5f), 100f);
            }
            return solidSprite;
        }

        /// <summary>Lazily creates the singleton UI once, (re)binds it to the given PowerGauge.</summary>
        public static PowerGaugeUI GetOrCreate(PowerGauge target)
        {
            if (Instance == null)
            {
                var go = new GameObject("PowerGaugeUI");
                Instance = go.AddComponent<PowerGaugeUI>();
                Instance.BuildUI();
            }

            Instance.target = target;
            return Instance;
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("PowerGaugeCanvas");
            canvasGO.transform.SetParent(transform, false);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // Border frame — slightly larger than the bar itself, drawn behind it,
            // so the whole bar reads as a bright-outlined HUD element instead of a
            // flat rectangle that blends into the background.
            var borderGO = new GameObject("PowerBarBorder", typeof(RectTransform));
            borderGO.transform.SetParent(canvasGO.transform, false);
            var borderRect = (RectTransform)borderGO.transform;
            borderRect.anchorMin = new Vector2(0f, 1f);
            borderRect.anchorMax = new Vector2(0f, 1f);
            borderRect.pivot = new Vector2(0f, 1f);
            borderRect.anchoredPosition = new Vector2(margin, -margin);
            borderRect.sizeDelta = new Vector2(barWidth + borderThickness * 2f, barHeight + borderThickness * 2f);
            borderImage = borderGO.AddComponent<Image>();
            borderImage.color = borderColor;

            var bgGO = new GameObject("PowerBarBackground", typeof(RectTransform));
            bgGO.transform.SetParent(borderGO.transform, false);
            var bgRect = (RectTransform)bgGO.transform;
            bgRect.anchorMin = new Vector2(0.5f, 0.5f);
            bgRect.anchorMax = new Vector2(0.5f, 0.5f);
            bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.anchoredPosition = Vector2.zero;
            bgRect.sizeDelta = new Vector2(barWidth, barHeight);
            bgGO.AddComponent<Image>().color = backgroundColor;

            var fillGO = new GameObject("PowerBarFill", typeof(RectTransform));
            fillGO.transform.SetParent(bgGO.transform, false);
            var fillRect = (RectTransform)fillGO.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            fillImage = fillGO.AddComponent<Image>();
            // Image.Type.Filled silently ignores fillAmount and always
            // renders the full rect unless a sprite is assigned — a plain
            // solid-color Image with sprite == null does NOT get clipped in
            // this Unity version. A 1x1 white sprite gives it something to
            // generate UV-clipped geometry from while still rendering as a
            // flat color (tinted by fillImage.color).
            fillImage.sprite = GetOrCreateSolidSprite();
            fillImage.color = fillColorHigh;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 1f;

            // "POWER" label sitting just above the bar so it's unmistakable
            // as a resource meter even at a glance.
            var labelGO = new GameObject("PowerBarLabel", typeof(RectTransform));
            labelGO.transform.SetParent(borderGO.transform, false);
            var labelRect = (RectTransform)labelGO.transform;
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 2f);
            labelRect.sizeDelta = new Vector2(barWidth + borderThickness * 2f, 20f);

            label = labelGO.AddComponent<Text>();
            label.text = "POWER";
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 16;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.LowerLeft;
            label.color = Color.white;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            var labelShadow = labelGO.AddComponent<Shadow>();
            labelShadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            labelShadow.effectDistance = new Vector2(1.5f, -1.5f);
        }

        private void Update()
        {
            if (target == null || fillImage == null)
            {
                return;
            }

            float fraction = target.Max > 0f ? target.Current / target.Max : 0f;

            if (!Mathf.Approximately(fraction, lastFraction))
            {
                lastFraction = fraction;
                fillImage.fillAmount = fraction;
                fillImage.color = fraction <= 0.5f
                    ? Color.Lerp(fillColorLow, fillColorMid, fraction / 0.5f)
                    : Color.Lerp(fillColorMid, fillColorHigh, (fraction - 0.5f) / 0.5f);
            }

            if (borderImage != null)
            {
                if (fraction <= lowPowerThreshold)
                {
                    // Continuously animated while low — must write every frame.
                    float pulse = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
                    borderImage.color = Color.Lerp(borderColor, fillColorLow, pulse * pulseStrength + (1f - pulseStrength));
                    borderAtRestColor = false;
                }
                else if (!borderAtRestColor)
                {
                    borderImage.color = borderColor;
                    borderAtRestColor = true;
                }
            }
        }
    }
}
