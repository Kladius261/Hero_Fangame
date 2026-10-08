using System.Collections;
using TMPro;
using UnityEngine;

namespace HeroFangame.UI
{
    /// <summary>
    /// Drives a single comic-style sound-effect text popup instance (see
    /// TextPopupManager). Two playback modes:
    /// - PlayBurst: fixed-duration pop-in/pop-out burst (mimics
    ///   PS_CartoonBoomText/PS_CartoonCrashText's Size-over-Lifetime curve),
    ///   self-destroys when the burst finishes.
    /// - PlayPopIn: pops in and then holds at a settled scale indefinitely,
    ///   for sustained/procedurally-growing text (see TextPopupHandle) —
    ///   caller must eventually call PopOutAndDestroy to end it.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class TextPopup : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;

        [Header("Burst Curve (matches PS_CartoonBoomText/CrashText Size-over-Lifetime)")]
        [SerializeField]
        private AnimationCurve burstCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.0692f, 1f),
            new Keyframe(0.2055f, 0.8859f),
            new Keyframe(0.8068f, 0.8979f),
            new Keyframe(0.9975f, 0f));

        [Header("Sustained Pop-In (settles & holds, no collapse)")]
        [SerializeField]
        private AnimationCurve popInCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.6f, 1.05f),
            new Keyframe(1f, 0.9f));
        [SerializeField] private float popInDuration = 0.18f;

        private Coroutine routine;

        private void Awake()
        {
            if (label == null)
            {
                label = GetComponentInChildren<TMP_Text>();
            }
        }

        /// <summary>
        /// Fixed-duration pop-in + pop-out burst. Destroys this GameObject
        /// once the burst finishes.
        /// </summary>
        public void PlayBurst(string text, float fontSize, Color topColor, Color bottomColor,
            Color outlineColor, float outlineThickness, float zRotationDegrees, Vector3 worldPosition, float duration)
        {
            ApplyStyle(text, fontSize, topColor, bottomColor, outlineColor, outlineThickness);
            transform.position = worldPosition;
            transform.eulerAngles = new Vector3(0f, 0f, zRotationDegrees);
            transform.localScale = Vector3.zero;
            Restart(BurstRoutine(duration));
        }

        /// <summary>
        /// Pops in and holds at a settled scale indefinitely — does not
        /// self-destroy. Used for sustained/procedurally-growing popups
        /// (see TextPopupHandle), which call SetText repeatedly and
        /// eventually PopOutAndDestroy to end it.
        /// </summary>
        public void PlayPopIn(string text, float fontSize, Color topColor, Color bottomColor,
            Color outlineColor, float outlineThickness, float zRotationDegrees, Vector3 worldPosition)
        {
            ApplyStyle(text, fontSize, topColor, bottomColor, outlineColor, outlineThickness);
            transform.position = worldPosition;
            transform.eulerAngles = new Vector3(0f, 0f, zRotationDegrees);
            transform.localScale = Vector3.zero;
            Restart(PopInRoutine());
        }

        public void SetText(string newText)
        {
            if (label != null)
            {
                label.text = newText;
                ResizeToFitText();
            }
        }

        public void PopOutAndDestroy(float duration)
        {
            Restart(PopOutRoutine(duration));
        }

        private void ApplyStyle(string text, float fontSize, Color topColor, Color bottomColor,
            Color outlineColor, float outlineThickness)
        {
            if (label == null)
            {
                return;
            }
            label.text = text;
            label.fontSize = fontSize;
            label.enableVertexGradient = true;
            label.colorGradient = new VertexGradient(topColor, topColor, bottomColor, bottomColor);
            label.outlineWidth = outlineThickness;

            // Not label.outlineColor = outlineColor — that setter no-ops
            // whenever the new value equals TMP_Text's hardcoded default
            // cache (Color.black) on a freshly instantiated component,
            // which silently drops every black outline (e.g. RegularHit).
            // Writing the shader property directly on the auto-instanced
            // material sidesteps that cache check entirely.
            label.fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);
            ResizeToFitText();
        }

        /// <summary>
        /// Grows/shrinks the label's RectTransform to exactly fit its
        /// current text as one line. Needed because the growing "comic
        /// lettering" text (see TextPopupHandle) is a single unbroken word —
        /// with the prefab's fixed-size RectTransform and word wrap on, it
        /// would force-wrap mid-word onto new (overlapping, centered-on-
        /// the-same-pivot) lines instead of growing sideways. The prefab
        /// disables wrapping (TextWrappingModes.NoWrap) so this resize is
        /// purely cosmetic bookkeeping, not what prevents the wrap — but
        /// keeping the rect's width in sync with the text still matters
        /// since RectTransform's anchors/pivot (both centered) are what
        /// keep the text growing outward symmetrically from its spawn point
        /// rather than drifting to one side.
        /// </summary>
        private void ResizeToFitText()
        {
            if (label == null)
            {
                return;
            }
            label.rectTransform.sizeDelta = label.GetPreferredValues();
        }

        private void Restart(IEnumerator next)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
            }
            routine = StartCoroutine(next);
        }

        private IEnumerator BurstRoutine(float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(t / duration);
                float scale = burstCurve.Evaluate(normalized);
                transform.localScale = Vector3.one * scale;
                yield return null;
            }
            Destroy(gameObject);
        }

        private IEnumerator PopInRoutine()
        {
            float t = 0f;
            while (t < popInDuration)
            {
                t += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(t / popInDuration);
                float scale = popInCurve.Evaluate(normalized);
                transform.localScale = Vector3.one * scale;
                yield return null;
            }
            transform.localScale = Vector3.one * popInCurve.Evaluate(1f);
            routine = null;
        }

        private IEnumerator PopOutRoutine(float duration)
        {
            Vector3 start = transform.localScale;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                transform.localScale = Vector3.Lerp(start, Vector3.zero, Mathf.Clamp01(t / duration));
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
