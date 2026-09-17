using UnityEngine;

namespace HeroFangame.Camera
{
    /// <summary>
    /// Subtle, continuous Perlin-noise camera jitter, toggled on/off by
    /// abilities (e.g. Heat Vision) rather than a one-shot burst shake.
    /// Lazily attaches itself to Camera.main on first use. Always computes
    /// its offset from a fixed base position captured once in Awake, so
    /// repeated enable/disable never compounds drift, and the camera
    /// returns exactly to its resting position once shaking stops.
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        [SerializeField] private float amplitude = 0.06f;
        [SerializeField] private float frequency = 18f;
        [SerializeField] private float fadeTime = 0.05f;

        private Transform cam;
        private Vector3 basePosition;
        private bool shaking;
        private float weight;
        private float seedX;
        private float seedY;

        public static CameraShake GetOrCreate()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var mainCam = UnityEngine.Camera.main;
            if (mainCam == null)
            {
                return null;
            }

            var existing = mainCam.GetComponent<CameraShake>();
            Instance = existing != null ? existing : mainCam.gameObject.AddComponent<CameraShake>();
            return Instance;
        }

        private void Awake()
        {
            Instance = this;
            cam = transform;
            basePosition = cam.localPosition;
            seedX = Random.Range(0f, 1000f);
            seedY = Random.Range(0f, 1000f);
        }

        public void SetShaking(bool active)
        {
            shaking = active;
        }

        private void LateUpdate()
        {
            float targetWeight = shaking ? 1f : 0f;
            float rate = fadeTime > 0f ? Time.deltaTime / fadeTime : 1f;
            weight = Mathf.MoveTowards(weight, targetWeight, rate);

            if (weight <= 0f)
            {
                cam.localPosition = basePosition;
                return;
            }

            float t = Time.unscaledTime * frequency;
            float nx = (Mathf.PerlinNoise(seedX, t) - 0.5f) * 2f;
            float ny = (Mathf.PerlinNoise(seedY, t) - 0.5f) * 2f;

            Vector3 offset = new Vector3(nx, ny, 0f) * (amplitude * weight);
            cam.localPosition = basePosition + offset;
        }
    }
}
