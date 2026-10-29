using UnityEngine;

namespace HeroFangame.Enemy
{
    /// <summary>
    /// Scene-wide singleton owning the "singular" Freeze-target audio layers
    /// (Freezing, Fully Frozen, Maintain Frozen) that must play only once
    /// regardless of how many EnemyRobot/DestructibleObject/ExplosiveObject
    /// instances are simultaneously chilling or frozen -- those scripts have
    /// no visibility into each other's freeze state, so every
    /// EnemyFreezeVisualEffect reports into this manager instead. Mirrors the
    /// lazy GetOrCreate() singleton pattern used by CameraShake/HitStop, but
    /// unlike those, every AudioSource/AudioClip field here must be wired by
    /// hand on a dedicated FreezeTargetAudioManager GameObject placed once in
    /// the scene -- GetOrCreate()'s runtime-spawned fallback has no clip/
    /// source refs, so every call site stays null-guarded as a safety net.
    /// </summary>
    public class FreezeTargetAudioManager : MonoBehaviour
    {
        public static FreezeTargetAudioManager Instance { get; private set; }

        [Header("Freezing (chilling, not yet fully frozen) — scene-wide singular")]
        [SerializeField] private AudioSource freezingLayer1Source;
        [SerializeField] private AudioSource freezingLayer2Source;
        [SerializeField] private AudioClip freezingLayer1Clip;   // RPG3_IceMagic_FreezeLong02
        [SerializeField] private AudioClip freezingLayer2Clip;   // RPG3_IceMagic_Debuff01

        [Header("Fully Frozen (one-shot, no layering, frame-deduped)")]
        [SerializeField] private AudioSource fullyFrozenOneShotSource;
        [SerializeField] private AudioClip fullyFrozenClip;      // RPG3_IceMagic2_HeavyImpact01

        [Header("Maintain Frozen (looping, scene-wide singular, pause/unpause ducked)")]
        [SerializeField] private AudioSource maintainFrozenSource;
        [SerializeField] private AudioClip maintainFrozenClip;   // RPG3_IceMagic_DroneSubtle02_Loop

        private int chillingTargetCount;
        private int frozenTargetCount;
        private int lastFullyFrozenFrame = -1;

        public static FreezeTargetAudioManager GetOrCreate()
        {
            if (Instance != null)
            {
                return Instance;
            }
            var go = new GameObject("FreezeTargetAudioManager");
            Object.DontDestroyOnLoad(go);
            Instance = go.AddComponent<FreezeTargetAudioManager>();
            return Instance;
        }

        private void Awake()
        {
            Instance = this;
            if (freezingLayer1Source != null) freezingLayer1Source.loop = false;
            if (freezingLayer2Source != null) freezingLayer2Source.loop = false;
            if (maintainFrozenSource != null)
            {
                maintainFrozenSource.loop = true;
                maintainFrozenSource.clip = maintainFrozenClip;
            }
        }

        public void NotifyChillStarted()
        {
            chillingTargetCount++;
            if (chillingTargetCount == 1)
            {
                if (freezingLayer1Source != null) { freezingLayer1Source.clip = freezingLayer1Clip; freezingLayer1Source.Play(); }
                if (freezingLayer2Source != null) { freezingLayer2Source.clip = freezingLayer2Clip; freezingLayer2Source.Play(); }
            }
        }

        public void NotifyChillEnded()
        {
            chillingTargetCount = Mathf.Max(0, chillingTargetCount - 1);
            if (chillingTargetCount == 0)
            {
                if (freezingLayer1Source != null && freezingLayer1Source.isPlaying) freezingLayer1Source.Stop();
                if (freezingLayer2Source != null && freezingLayer2Source.isPlaying) freezingLayer2Source.Stop();
            }
        }

        public void NotifyTargetFrozen()
        {
            if (Time.frameCount != lastFullyFrozenFrame)
            {
                lastFullyFrozenFrame = Time.frameCount;
                fullyFrozenOneShotSource?.PlayOneShot(fullyFrozenClip);
            }

            frozenTargetCount++;
            if (frozenTargetCount == 1 && maintainFrozenSource != null)
            {
                maintainFrozenSource.clip = maintainFrozenClip;
                maintainFrozenSource.loop = true;
                maintainFrozenSource.Play();
            }
        }

        public void NotifyTargetUnfrozen()
        {
            frozenTargetCount = Mathf.Max(0, frozenTargetCount - 1);
            if (frozenTargetCount == 0)
            {
                maintainFrozenSource?.Stop();
            }
        }

        public void PauseMaintainFrozenLoop()
        {
            if (maintainFrozenSource != null && maintainFrozenSource.isPlaying)
            {
                maintainFrozenSource.Pause();
            }
        }

        public void ResumeMaintainFrozenLoop()
        {
            if (frozenTargetCount > 0 && maintainFrozenSource != null)
            {
                maintainFrozenSource.UnPause();
            }
        }
    }
}
