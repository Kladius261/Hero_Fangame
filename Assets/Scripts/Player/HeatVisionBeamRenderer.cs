using UnityEngine;

namespace HeroFangame.Player
{
    /// <summary>
    /// Three-layer beam visual for Heat Vision: a wide, very dim HDR red
    /// "outer glow" underneath a narrower, saturated HDR orange-red "mid
    /// glow", underneath a thin, hot HDR yellow-white "core" — approximating
    /// the soft-to-hot radial gradient of a real laser (LineRenderer can
    /// only gradient along its length, not across its width, so stacking
    /// three progressively narrower/brighter lines fakes that falloff).
    /// Purely procedural (no textures) — all three are two-point
    /// world-space segments redrawn every frame while firing, clipped
    /// exactly at the resolved hit point (or max range if nothing was hit).
    /// HDR colors are intentionally >1 so they trigger URP Bloom.
    /// </summary>
    public class HeatVisionBeamRenderer : MonoBehaviour
    {
        [SerializeField] private LineRenderer outerGlowLine;
        [SerializeField] private LineRenderer glowLine;
        [SerializeField] private LineRenderer coreLine;
        [SerializeField] private float outerGlowWidth = 1.4f;
        [SerializeField] private float glowWidth = 0.55f;
        [SerializeField] private float coreWidth = 0.12f;
        [SerializeField] private Color outerGlowColor = new Color(2.2f, 0.35f, 0.1f, 0.4f);
        [SerializeField] private Color glowColor = new Color(5f, 1f, 0.35f, 1f);
        [SerializeField] private Color coreColor = new Color(7f, 4f, 5f, 1f);

        private void Awake()
        {
            ConfigureLine(outerGlowLine, outerGlowWidth, outerGlowColor);
            ConfigureLine(glowLine, glowWidth, glowColor);
            ConfigureLine(coreLine, coreWidth, coreColor);
            SetActive(false);
        }

        private static void ConfigureLine(LineRenderer lr, float width, Color color)
        {
            if (lr == null)
            {
                return;
            }
            lr.positionCount = 2;
            lr.startWidth = width;
            lr.endWidth = width * 0.7f;
            lr.startColor = color;
            lr.endColor = color;
            lr.useWorldSpace = true;
            lr.numCapVertices = 4;
            lr.alignment = LineAlignment.View;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
        }

        public void SetActive(bool active)
        {
            if (outerGlowLine != null)
            {
                outerGlowLine.enabled = active;
            }
            if (glowLine != null)
            {
                glowLine.enabled = active;
            }
            if (coreLine != null)
            {
                coreLine.enabled = active;
            }
        }

        public void UpdateBeam(Vector2 origin, Vector2 direction, float length)
        {
            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            Vector2 end = origin + dir * length;

            if (outerGlowLine != null)
            {
                outerGlowLine.SetPosition(0, origin);
                outerGlowLine.SetPosition(1, end);
            }
            if (glowLine != null)
            {
                glowLine.SetPosition(0, origin);
                glowLine.SetPosition(1, end);
            }
            if (coreLine != null)
            {
                coreLine.SetPosition(0, origin);
                coreLine.SetPosition(1, end);
            }
        }
    }
}
