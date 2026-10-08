using UnityEngine;

namespace HeroFangame.UI
{
    /// <summary>
    /// Pre-placed scene singleton driving the comic-book sound-effect text
    /// popup system (see Text Popup System Instructions). Holds the
    /// PopUpText prefab reference and every category's tunables, and
    /// exposes two spawn entry points:
    /// - SpawnBurst: fixed word, fixed duration, fire-and-forget
    ///   (RegularHit, FullFreeze, FlightTakeoff, FlightLanding).
    /// - BeginSustained: procedurally-growing "comic lettering" text tied
    ///   to an ability's hold duration (HeatVision, FreezeBreath,
    ///   FlightCharge) — returns a TextPopupHandle the caller drives via
    ///   Tick/UpdatePosition/Release.
    /// </summary>
    public class TextPopupManager : MonoBehaviour
    {
        public enum Category
        {
            RegularHit,
            HeatVision,
            FreezeBreath,
            FullFreeze,
            FlightTakeoff,
            FlightLanding,
            FlightCharge,
            FreezeBreak,
            Throw
        }

        [System.Serializable]
        public struct ProceduralWord
        {
            public string prefix;
            public char repeatChar;
            public string suffix;

            public ProceduralWord(string prefix, char repeatChar, string suffix)
            {
                this.prefix = prefix;
                this.repeatChar = repeatChar;
                this.suffix = suffix;
            }
        }

        [System.Serializable]
        public class FixedBurstConfig
        {
            public string[] words;
            public float fontSize;
            public Color gradientTop;
            public Color gradientBottom;
            public Color outlineColor;
            public float outlineThickness;
            public float zRotationRangeDegrees;
            public float duration;
        }

        [System.Serializable]
        public class SustainedConfig
        {
            public ProceduralWord[] words;
            public float fontSize;
            public Color gradientTop;
            public Color gradientBottom;
            public Color outlineColor;
            public float outlineThickness;
            public float zRotationRangeDegrees;
            [Tooltip("Added to the tracked worldPosition every frame (e.g. player position), so the popup floats above the subject instead of covering it.")]
            public Vector3 positionOffset;
        }

        public static TextPopupManager Instance { get; private set; }

        [SerializeField] private TextPopup popUpTextPrefab;

        [Tooltip("Curated palette for RegularHit's 2-random-color gradient. Deliberately excludes white/black/red/blue/yellow per spec. Two distinct entries are picked at random and sorted lighter-first/darker-last.")]
        [SerializeField]
        private Color[] regularHitPalette =
        {
            HexColor("FF8800"), // orange
            HexColor("9B30FF"), // purple
            HexColor("33CC33"), // green
            HexColor("FF66CC"), // pink
            HexColor("00CCCC"), // cyan
            HexColor("CC00CC"), // magenta
        };

        [Header("Regular Hit")]
        [SerializeField]
        private FixedBurstConfig regularHit = new FixedBurstConfig
        {
            words = new[] { "POW!", "WHAM!", "THWAK!", "SMACK!", "BASH!", "KRAK!", "K-POW!", "BLAM!", "BONK!", "KLANG!", "THOK!", "ZAH!" },
            fontSize = 1.5f,
            outlineColor = HexColor("000000"),
            outlineThickness = 0.35f,
            zRotationRangeDegrees = 25f,
            duration = 0.25f
        };

        [Header("Heat Vision (sustained)")]
        [SerializeField]
        private SustainedConfig heatVision = new SustainedConfig
        {
            words = new[]
            {
                new ProceduralWord("F", 'Z', "T"),
                new ProceduralWord("V", 'Z', "T"),
                new ProceduralWord("ZR", 'A', "P"),
                new ProceduralWord("SKR", 'Z', "T"),
            },
            fontSize = 2f,
            gradientTop = HexColor("FFBD00"),
            gradientBottom = HexColor("FF1400"),
            outlineColor = HexColor("FF2700"),
            outlineThickness = 0.4f,
            zRotationRangeDegrees = 15f,
            positionOffset = new Vector3(0f, 1.5f, 0f)
        };

        [Header("Freeze Breath (sustained)")]
        [SerializeField]
        private SustainedConfig freezeBreath = new SustainedConfig
        {
            words = new[]
            {
                new ProceduralWord("FW", 'O', "SH"),
                new ProceduralWord("F", 'S', "HH"),
                new ProceduralWord("S", 'H', "H"),
                new ProceduralWord("H", 'S', "H"),
                new ProceduralWord("FRR", 'O', "SH"),
            },
            fontSize = 2f,
            gradientTop = HexColor("FFFFFF"),
            gradientBottom = HexColor("00B4FF"),
            outlineColor = HexColor("007DC0"),
            outlineThickness = 0.4f,
            zRotationRangeDegrees = 15f,
            positionOffset = new Vector3(0f, 1.5f, 0f)
        };

        [Header("Full Freeze (fixed burst)")]
        [SerializeField]
        private FixedBurstConfig fullFreeze = new FixedBurstConfig
        {
            words = new[] { "KRRRK!", "CRK!", "KRK-KRK!" },
            fontSize = 1.75f,
            gradientTop = HexColor("FFFFFF"),
            gradientBottom = HexColor("00B4FF"),
            outlineColor = HexColor("007DC0"),
            outlineThickness = 0.35f,
            zRotationRangeDegrees = 25f,
            duration = 0.5f
        };

        [Header("Flight Takeoff (fixed burst)")]
        [SerializeField]
        private FixedBurstConfig flightTakeoff = new FixedBurstConfig
        {
            words = new[] { "WHOOSH!", "FWOOM!", "VWOOM!", "SHOOM!", "FWUMP!", "ZWOOP!" },
            fontSize = 1.75f,
            gradientTop = HexColor("FFFFFF"),
            gradientBottom = HexColor("FFFB00"),
            outlineColor = HexColor("C8CA00"),
            outlineThickness = 0.4f,
            zRotationRangeDegrees = 25f,
            duration = 0.5f
        };

        [Header("Flight Landing (fixed burst)")]
        [SerializeField]
        private FixedBurstConfig flightLanding = new FixedBurstConfig
        {
            words = new[] { "THOOM!", "WHUMP!", "BOOM!", "THUD!", "K-THOOM!", "D-DOOM!", "BADOOM!" },
            fontSize = 1.75f,
            gradientTop = HexColor("FFFFFF"),
            gradientBottom = HexColor("FFFB00"),
            outlineColor = HexColor("C8CA00"),
            outlineThickness = 0.4f,
            zRotationRangeDegrees = 25f,
            duration = 0.5f
        };

        [Header("Flight Charge (sustained, fixed rotation/position)")]
        [SerializeField]
        private SustainedConfig flightCharge = new SustainedConfig
        {
            words = new[]
            {
                new ProceduralWord("FW", 'O', "SH"),
                new ProceduralWord("KSH", 'O', "M"),
                new ProceduralWord("VW", 'O', "SH"),
                new ProceduralWord("WH", 'O', "M"),
            },
            fontSize = 2f,
            gradientTop = HexColor("FFFFFF"),
            gradientBottom = HexColor("FFFB00"),
            outlineColor = HexColor("C8CA00"),
            outlineThickness = 0.4f,
            zRotationRangeDegrees = 0f,
            positionOffset = new Vector3(0f, 1.5f, 0f)
        };

        [Header("Freeze Break (fixed burst)")]
        [SerializeField]
        private FixedBurstConfig freezeBreak = new FixedBurstConfig
        {
            words = new[] { "KRAK!", "CRACK!", "KRSSSH!", "SHRAK!", "KRRK!", "KSHH!" },
            fontSize = 1.75f,
            gradientTop = HexColor("FFFFFF"),
            gradientBottom = HexColor("00B4FF"),
            outlineColor = HexColor("007DC0"),
            outlineThickness = 0.35f,
            zRotationRangeDegrees = 25f,
            duration = 0.5f
        };

        [Header("Throw (fixed burst, uses RegularHit's random palette)")]
        [SerializeField]
        private FixedBurstConfig throwPopup = new FixedBurstConfig
        {
            words = new[] { "THUD!", "WHUMP!", "THOOM!", "BAM!", "WHAM!", "KTHUD!" },
            fontSize = 1.75f,
            outlineColor = HexColor("000000"),
            outlineThickness = 0.35f,
            zRotationRangeDegrees = 25f,
            duration = 0.5f
        };

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// Fixed word, fixed duration, fire-and-forget. For RegularHit,
        /// FullFreeze, FlightTakeoff, FlightLanding only.
        /// </summary>
        public TextPopup SpawnBurst(Category category, Vector3 worldPosition)
        {
            FixedBurstConfig cfg = GetFixedConfig(category);
            if (cfg == null || popUpTextPrefab == null)
            {
                return null;
            }

            string word = cfg.words[Random.Range(0, cfg.words.Length)];
            float z = cfg.zRotationRangeDegrees > 0f
                ? Random.Range(-cfg.zRotationRangeDegrees, cfg.zRotationRangeDegrees)
                : 0f;

            Color top, bottom;
            if (category == Category.RegularHit || category == Category.Throw)
            {
                PickRegularHitGradient(out top, out bottom);
            }
            else
            {
                top = cfg.gradientTop;
                bottom = cfg.gradientBottom;
            }

            TextPopup instance = Instantiate(popUpTextPrefab, worldPosition, Quaternion.identity);
            instance.PlayBurst(word, cfg.fontSize, top, bottom, cfg.outlineColor, cfg.outlineThickness, z, worldPosition, cfg.duration);
            return instance;
        }

        /// <summary>
        /// Starts a procedurally-growing "comic lettering" popup tied to an
        /// ability's hold duration. For HeatVision, FreezeBreath,
        /// FlightCharge only. Caller must drive the returned handle via
        /// Tick/UpdatePosition and eventually call Release().
        /// </summary>
        public TextPopupHandle BeginSustained(Category category, Vector3 worldPosition)
        {
            SustainedConfig cfg = GetSustainedConfig(category);
            if (cfg == null || popUpTextPrefab == null || cfg.words == null || cfg.words.Length == 0)
            {
                return null;
            }

            ProceduralWord word = cfg.words[Random.Range(0, cfg.words.Length)];
            float z = cfg.zRotationRangeDegrees > 0f
                ? Random.Range(-cfg.zRotationRangeDegrees, cfg.zRotationRangeDegrees)
                : 0f;

            Vector3 spawnPosition = worldPosition + cfg.positionOffset;
            TextPopup instance = Instantiate(popUpTextPrefab, spawnPosition, Quaternion.identity);
            string initialText = word.prefix + "!";
            instance.PlayPopIn(initialText, cfg.fontSize, cfg.gradientTop, cfg.gradientBottom,
                cfg.outlineColor, cfg.outlineThickness, z, spawnPosition);

            return new TextPopupHandle(instance, word, fixedPosition: category == Category.FlightCharge, cfg.positionOffset);
        }

        private void PickRegularHitGradient(out Color top, out Color bottom)
        {
            if (regularHitPalette == null || regularHitPalette.Length < 2)
            {
                top = Color.white;
                bottom = Color.gray;
                return;
            }

            int i = Random.Range(0, regularHitPalette.Length);
            int j;
            do
            {
                j = Random.Range(0, regularHitPalette.Length);
            } while (j == i);

            Color a = regularHitPalette[i];
            Color b = regularHitPalette[j];
            float lumA = 0.299f * a.r + 0.587f * a.g + 0.114f * a.b;
            float lumB = 0.299f * b.r + 0.587f * b.g + 0.114f * b.b;
            top = lumA >= lumB ? a : b;
            bottom = lumA >= lumB ? b : a;
        }

        private FixedBurstConfig GetFixedConfig(Category category)
        {
            switch (category)
            {
                case Category.RegularHit: return regularHit;
                case Category.FullFreeze: return fullFreeze;
                case Category.FlightTakeoff: return flightTakeoff;
                case Category.FlightLanding: return flightLanding;
                case Category.FreezeBreak: return freezeBreak;
                case Category.Throw: return throwPopup;
                default:
                    Debug.LogError($"TextPopupManager.SpawnBurst called with sustained category {category} — use BeginSustained instead.");
                    return null;
            }
        }

        private SustainedConfig GetSustainedConfig(Category category)
        {
            switch (category)
            {
                case Category.HeatVision: return heatVision;
                case Category.FreezeBreath: return freezeBreath;
                case Category.FlightCharge: return flightCharge;
                default:
                    Debug.LogError($"TextPopupManager.BeginSustained called with fixed-burst category {category} — use SpawnBurst instead.");
                    return null;
            }
        }

        private static Color HexColor(string hex)
        {
            return ColorUtility.TryParseHtmlString("#" + hex, out Color color) ? color : Color.white;
        }
    }
}
