using UnityEngine;

namespace HeroFangame.Player
{
    /// <summary>
    /// Two-layer beam visual for Heat Vision: a wide, dim HDR orange "glow"
    /// LineRenderer underneath a thin, bright HDR near-white "core"
    /// LineRenderer. Purely procedural (no textures) — both lines are
    /// two-point world-space segments redrawn every frame while firing,
    /// clipped exactly at the resolved hit point (or max range if nothing
    /// was hit). HDR colors are intentionally >1 so they trigger URP Bloom.
    /// </summary>
    public class HeatVisionBeamRenderer : MonoBehaviour
    {
        [SerializeField] private LineRenderer glowLine;
        [SerializeField] private LineRenderer coreLine;
        [SerializeField] private float glowWidth = 0.5f;
        [SerializeField] private float coreWidth = 0.15f;
        [SerializeField] private Color glowColor = new Color(3f, 0.6f, 0.05f, 1f);
        [SerializeField] private Color coreColor = new Color(4f, 3.5f, 3f, 1f);

        private void Awake()
        {
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
