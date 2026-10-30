using System.Collections;
using UnityEngine;
using HeroFangame.Combat;
using HeroFangame.Core;
using HeroFangame.Camera;
using HeroFangame.Enemy;
using HeroFangame.UI;

namespace HeroFangame.Player
{
    /// <summary>
    /// Short-to-medium range frontal cone, fired strictly horizontally
    /// (left or right, from the player's last horizontal facing — never
    /// up/down or diagonal), exactly like Heat Vision's beam. A tap applies
    /// a small amount of freeze exposure plus minor direct damage. Holding
    /// widens the cone and builds sustained freeze exposure (via
    /// IFreezable) while continuously draining Power. Low damage and
    /// deliberately zero knockback: targets are meant to be immobilized by
    /// the cold (see IFreezable/EnemyRobot), not physically pushed around
    /// like Heat Vision's beam. Like Heat Vision, locks player movement
    /// (and facing) for as long as the cone is active, so the player must
    /// stop breathing to reposition or flip the breath's direction.
    /// </summary>
    [RequireComponent(typeof(PlayerInputHandler))]
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PowerGauge))]
    [RequireComponent(typeof(AbilityLock))]
    public class FreezeBreathAbility : MonoBehaviour
    {
        [Header("Cone Shape")]
        [SerializeField] private Vector2 tapConeSize = new Vector2(4.375f, 1.25f);
        [SerializeField] private Vector2 holdConeSize = new Vector2(6.25f, 1.75f);
        [SerializeField] private float coneOffset = 0.5f;
        [SerializeField] private LayerMask hittableLayers;

        [Header("Damage & Exposure")]
        [SerializeField] private int tapDamage = 1;
        [SerializeField] private float tapExposure = 15f;
        [SerializeField] private float holdExposurePerSecond = 40f;

        [Header("Power Cost")]
        [SerializeField] private float tapPowerCost = 8f;
        [SerializeField] private float holdPowerCostPerSecond = 25f;

        [Header("Visuals")]
        [SerializeField] private float tapPulseDuration = 0.15f;
        [SerializeField] private FreezeBreathConeEffect coneEffect;
        [SerializeField] private float breathShakeDuration = 0.08f;

        [Header("Audio")]
        [SerializeField] private AudioSource breathAudioSource;
        [Tooltip("Second looping layer of the Loop/Main Group (RPG3_WindMagic_Drone01_DarkWindLoop), played and pitch-wobbled in lockstep with breathAudioSource.")]
        [SerializeField] private AudioSource breathAudioSourceLayer2;
        [Tooltip("Shared one-shot source for every non-looping layer (Introductory, maneuver Loop groups) -- PlayOneShot natively mixes overlapping calls on a single source, so one is enough.")]
        [SerializeField] private AudioSource oneShotAudioSource;
        [SerializeField] private float pitchWobbleSpeed = 6f;
        [SerializeField] private float pitchWobbleAmplitude = 0.1f;
        [Tooltip("Relative volume multiplier for Loop maneuver-group one-shots versus the Loop Main Group's own AudioSource volume (assumed 1.0) -- boosted so a maneuver burst cuts through the main loop's pitch-wobble instead of getting buried under it.")]
        [SerializeField] private float maneuverGroupVolumeScale = 2.25f;
        [Tooltip("Duration of the smooth volume fade-out applied to the Loop Main Group (both layers) when the breath is released -- replaces an instant cutoff with a quick decline.")]
        [SerializeField] private float releaseFadeOutDuration = 0.5f;
        [Tooltip("Relative volume multiplier for the Introductory one-shots (introClipA/introClipB).")]
        [SerializeField] private float introVolumeScale = 1.25f;

        [Header("Audio - Introductory (on press)")]
        [SerializeField] private AudioClip introClipA;
        [SerializeField] private AudioClip introClipB;

        [Header("Audio - Loop Maneuver Groups (one-shot, on aim sweep)")]
        [SerializeField] private AudioClip[] maneuverGroup1Clips;
        [SerializeField] private AudioClip[] maneuverGroup2Clips;
        [SerializeField] private AudioClip[] maneuverGroup3Clips;

        [Header("Aim")]
        [SerializeField] private float aimSweepSpeedDegreesPerSecond = 180f;
        [SerializeField] private float aimSweepAccelerationDegreesPerSecondSquared = 720f;

        [Header("Sound Effect Popup")]
        [Tooltip("How fast the Freeze Breath sound-effect popup's comic lettering grows while the cone is held (see TextPopupManager.Category.FreezeBreath).")]
        [SerializeField] private float freezeBreathLetterGrowthInterval = 0.12f;

        private PlayerInputHandler input;
        private PlayerController controller;
        private PowerGauge power;
        private AbilityLock abilityLock;
        private CameraShake cameraShake;
        private AbilityAimController aimController;

        private bool wasHeld;
        private float tapPulseTimeRemaining;
        private bool isConeActive;
        private AudioPitchWobble pitchWobble;
        private TextPopupHandle textPopupHandle;

        private int lastManeuverGroupIndex = -1;

        /// <summary>
        /// -1 = Down held, 0 = neither/both held, 1 = Up held. Tracked every
        /// frame unconditionally (not just while the cone is active), same
        /// rationale as HeatVisionAbility's lastManeuverDirState.
        /// </summary>
        private int lastManeuverDirState;

        /// <summary>
        /// Handle of the currently-running PlayIntroSequence coroutine (null
        /// once it has finished), so StopConeVisual can cancel it outright on
        /// release instead of letting it keep firing intro clips into the
        /// fade-out below.
        /// </summary>
        private Coroutine introCoroutine;

        /// <summary>
        /// Handle of the currently-running release fade-out (null once it has
        /// finished), so a fresh activation that starts mid-fade -- or a
        /// second release that re-enters StopConeVisual -- can cancel the old
        /// fade outright instead of letting two fades fight over the same
        /// AudioSources' volumes.
        /// </summary>
        private Coroutine releaseFadeOutCoroutine;

        /// <summary>
        /// Original inspector-configured volumes of the two faded
        /// AudioSources, captured once in Awake -- the release fade drives
        /// each source's volume down to 0 and leaves it there, so every
        /// fresh activation must restore this base value before playing
        /// again rather than starting back up silent.
        /// </summary>
        private float breathAudioSourceBaseVolume = 1f;
        private float breathAudioSourceLayer2BaseVolume = 1f;

        private void Awake()
        {
            input = GetComponent<PlayerInputHandler>();
            controller = GetComponent<PlayerController>();
            power = GetComponent<PowerGauge>();
            abilityLock = GetComponent<AbilityLock>();
            if (abilityLock == null)
            {
                abilityLock = gameObject.AddComponent<AbilityLock>();
            }
            aimController = new AbilityAimController(aimSweepSpeedDegreesPerSecond, aimSweepAccelerationDegreesPerSecondSquared);
            pitchWobble = new AudioPitchWobble(pitchWobbleSpeed, pitchWobbleAmplitude);

            if (breathAudioSource != null) breathAudioSourceBaseVolume = breathAudioSource.volume;
            if (breathAudioSourceLayer2 != null) breathAudioSourceLayer2BaseVolume = breathAudioSourceLayer2.volume;
        }

        private void Update()
        {
            UpdateManeuverEdgeDetection();

            if (controller.IsFlightMode || controller.IsGrabStance)
            {
                StopConeVisual();
                return;
            }

            bool rawHeld = input.FreezeBreathHeld;
            bool isHeld = abilityLock.CanActivate(this, rawHeld);

            if (!isHeld && tapPulseTimeRemaining > 0f)
            {
                // Tap already fired; keep its brief cloud pulse alive for a
                // few frames instead of cutting it off instantly.
                tapPulseTimeRemaining -= Time.deltaTime;
                if (tapPulseTimeRemaining <= 0f)
                {
                    StopConeVisual();
                }
                wasHeld = false;
                return;
            }

            bool activatedThisFrame = false;

            if (isHeld && !wasHeld)
            {
                if (power.TrySpend(tapPowerCost))
                {
                    ApplyCone(tapConeSize, tapDamage, tapExposure, isTap: true, isNewActivation: true);
                    tapPulseTimeRemaining = tapPulseDuration;

                    // Single, brief shake on the moment of firing only —
                    // unlike Heat Vision's continuous shake while its beam
                    // is held, Freeze Breath shakes once per activation and
                    // stays still for the rest of the hold.
                    if (cameraShake == null)
                    {
                        cameraShake = CameraShake.GetOrCreate();
                    }
                    cameraShake?.Pulse(breathShakeDuration);
                    activatedThisFrame = true;
                }
            }
            else if (isHeld && wasHeld)
            {
                float drained = power.DrainOverTime(holdPowerCostPerSecond);
                if (drained > 0f)
                {
                    ApplyCone(holdConeSize, 0, holdExposurePerSecond * Time.deltaTime, isTap: false, isNewActivation: false);
                    activatedThisFrame = true;
                }
                else
                {
                    StopConeVisual();
                }
            }
            else
            {
                StopConeVisual();
            }

            // Deliberately NOT "isHeld && power.Current > 0f" — that let a
            // failed tap (insufficient power) silently flip back to the
            // cheaper hold-drain path the instant Current ticked up by even
            // a fraction from regen, letting a continuously-held press
            // stutter-fire on trickles of power instead of requiring the
            // full tap cost again. Gating on whether we actually activated
            // this frame means an empty gauge truly locks the ability out
            // (while still held) until enough power has regenerated to
            // afford a fresh full-cost tap.
            wasHeld = isHeld && activatedThisFrame;
        }

        /// <summary>
        /// Freeze Breath fires along the player's horizontal Facing. On
        /// activation, it auto-aims at the closest on-screen target inside
        /// the +/-AngleCapDegrees cone (falling back to horizontal if none
        /// exists), using maxReach to limit the target search to what this
        /// particular call can actually hit; while held, Up/Down arrow
        /// keys tilt it further via <see cref="aimController"/> — mirrors
        /// HeatVisionAbility.GetBeamDirection().
        /// </summary>
        private Vector2 GetBreathDirection(bool isNewActivation, float maxReach)
        {
            float initialAngle = 0f;
            if (isNewActivation)
            {
                float horizontalSign = controller.Facing.x < 0f ? -1f : 1f;
                float maxDistance = Mathf.Min(maxReach, CameraViewBounds.GetDistanceToEdge(transform.position.x, horizontalSign));
                initialAngle = AbilityAimController.FindAutoAimAngleDegrees(transform.position, controller.Facing, maxDistance, hittableLayers);
            }
            return aimController.Resolve(isNewActivation, initialAngle, input.MoveInput, controller.Facing, Time.deltaTime);
        }

        private void ApplyCone(Vector2 size, int damage, float exposure, bool isTap, bool isNewActivation)
        {
            Vector2 dir = GetBreathDirection(isNewActivation, coneOffset + size.x);

            // Never let the cone reach past the edge of the camera's
            // current view — enemies further along the level haven't
            // scrolled into frame yet and shouldn't be hittable before the
            // player can even see them.
            float desiredFarDistance = coneOffset + size.x;
            float availableDistance = CameraViewBounds.GetDistanceToEdge(transform.position.x, dir.x);
            bool clampedByScreen = availableDistance < desiredFarDistance;
            float clampedFarDistance = Mathf.Min(desiredFarDistance, availableDistance);
            size.x = Mathf.Max(0f, clampedFarDistance - coneOffset);

            if (size.x <= 0f)
            {
                // Player is already right at (or past) the screen edge in
                // this direction — nothing on-screen left to reach, so
                // skip the attack and its visuals entirely rather than
                // firing a degenerate zero-width box.
                StopConeVisual();
                return;
            }

            controller.LockMovement(this);

            Vector2 origin = (Vector2)transform.position + dir * (coneOffset + size.x * 0.5f);
            float angle = Vector2.SignedAngle(Vector2.right, dir);

            // Freeze exposure is applied before damage (unlike Heat
            // Vision/Punch, which only ever deal damage) so an IFreezable
            // that also implements IFreezeDamageAbsorber (ExplosiveObject)
            // can veto this same hit's accompanying chip damage right after
            // its exposure lands — otherwise a one-hit-kill target would be
            // destroyed by the chip damage before it ever got a chance to
            // freeze. Absorbed hits are nulled out of the shared buffer so
            // ApplyDamage's null-check skips them entirely.
            var hits = AttackUtility.OverlapBox(origin, size, angle, hittableLayers, out int hitCount);

            for (int i = 0; i < hitCount; i++)
            {
                var hit = hits[i];
                if (hit == null)
                {
                    continue;
                }

                var freezable = hit.GetComponentInParent<IFreezable>();
                freezable?.AddFreezeExposure(exposure, isNewActivation);

                var absorber = hit.GetComponentInParent<IFreezeDamageAbsorber>();
                if (absorber != null && absorber.ConsumeDamageAbsorption())
                {
                    hits[i] = null;
                }
            }

            AttackUtility.ApplyDamage(hits, hitCount, damage, gameObject, dir, knockbackForce: 0f);

            if (!isConeActive)
            {
                // A fresh activation always wins over a still-running release
                // fade from a previous activation -- cancel it and restore
                // both faded sources back to their base volume before
                // playing again, so the new activation never starts out
                // partway faded/silent.
                if (releaseFadeOutCoroutine != null)
                {
                    StopCoroutine(releaseFadeOutCoroutine);
                    releaseFadeOutCoroutine = null;
                }
                if (breathAudioSource != null) breathAudioSource.volume = breathAudioSourceBaseVolume;
                if (breathAudioSourceLayer2 != null) breathAudioSourceLayer2.volume = breathAudioSourceLayer2BaseVolume;

                breathAudioSource?.Play();
                breathAudioSourceLayer2?.Play();
                pitchWobble.Restart();
                introCoroutine = StartCoroutine(PlayIntroSequence());
                textPopupHandle = TextPopupManager.Instance?.BeginSustained(TextPopupManager.Category.FreezeBreath, transform.position);

                FreezeTargetAudioManager.GetOrCreate()?.PauseMaintainFrozenLoop();
            }
            isConeActive = true;
            pitchWobble.Apply(breathAudioSource, breathAudioSourceLayer2, Time.deltaTime);
            textPopupHandle?.UpdatePosition(transform.position);
            textPopupHandle?.Tick(Time.deltaTime, freezeBreathLetterGrowthInterval);
            FullScreenAbilityEffect.Instance?.SetActive(FullScreenAbilityEffect.Kind.FreezeBreath, true);

            // Visually clip the cone at the nearest hittable surface so the
            // mist wraps around the target instead of visibly passing
            // through it — same convention as HeatVisionAbility's beam
            // clipping. Damage/exposure above is unaffected: it still uses
            // the full box, so every enemy inside it is still hit.
            Vector2 nearEdge = (Vector2)transform.position + dir * coneOffset;
            var probe = AttackUtility.BoxCastSinglePeek(nearEdge, new Vector2(size.y, size.y), dir, size.x, hittableLayers);
            float visualLength = probe.DidHit ? Mathf.Max(probe.Distance, 0.5f) : size.x;
            Vector2 visualSize = new Vector2(visualLength, size.y);
            Vector2 visualOrigin = nearEdge + dir * (visualLength * 0.5f);

            coneEffect?.SetCone(visualOrigin, visualSize, angle, isTap);

            if (clampedByScreen)
            {
                // The cone's reach was cut short by the camera edge rather
                // than a physical obstacle — billow the mist/streaks against
                // an invisible wall right at that boundary instead of just
                // letting their emission shape end abruptly.
                Vector2 boundaryPoint = (Vector2)transform.position + dir * clampedFarDistance;
                coneEffect?.SetBoundaryContact(boundaryPoint, dir);
            }
            else
            {
                coneEffect?.StopBoundaryContact();
            }
        }

        private void StopConeVisual()
        {
            controller.UnlockMovement(this);

            if (!isConeActive)
            {
                return;
            }
            isConeActive = false;
            textPopupHandle?.Release();
            textPopupHandle = null;
            coneEffect?.StopCone();
            coneEffect?.StopBoundaryContact();
            FullScreenAbilityEffect.Instance?.SetActive(FullScreenAbilityEffect.Kind.FreezeBreath, false);

            // Cancel the Introductory coroutine outright so no further intro
            // clips fire into the fade-out below, then fade the Loop Main
            // Group (both AudioSources) down to silence over
            // releaseFadeOutDuration instead of cutting it off instantly.
            if (introCoroutine != null)
            {
                StopCoroutine(introCoroutine);
                introCoroutine = null;
            }
            if (releaseFadeOutCoroutine != null)
            {
                StopCoroutine(releaseFadeOutCoroutine);
            }
            releaseFadeOutCoroutine = StartCoroutine(FadeOutOnRelease());

            FreezeTargetAudioManager.GetOrCreate()?.ResumeMaintainFrozenLoop();
        }

        /// <summary>
        /// Fades breathAudioSource and breathAudioSourceLayer2 (the Loop
        /// Main Group) from their current volume down to 0 over
        /// releaseFadeOutDuration, in lockstep, then stops both. Volumes are
        /// left at 0 afterward; ApplyCone restores each source's base volume
        /// before the next activation plays again.
        /// </summary>
        private IEnumerator FadeOutOnRelease()
        {
            float startVol1 = breathAudioSource != null ? breathAudioSource.volume : 0f;
            float startVol2 = breathAudioSourceLayer2 != null ? breathAudioSourceLayer2.volume : 0f;

            float elapsed = 0f;
            while (elapsed < releaseFadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / releaseFadeOutDuration);
                if (breathAudioSource != null) breathAudioSource.volume = Mathf.Lerp(startVol1, 0f, t);
                if (breathAudioSourceLayer2 != null) breathAudioSourceLayer2.volume = Mathf.Lerp(startVol2, 0f, t);
                yield return null;
            }

            breathAudioSource?.Stop();
            breathAudioSourceLayer2?.Stop();
            releaseFadeOutCoroutine = null;
        }

        /// <summary>
        /// Clip A immediately, clip B at clip A's natural half-length mark --
        /// both at normal (1x) speed, unlike Heat Vision's sped-up intro.
        /// </summary>
        private IEnumerator PlayIntroSequence()
        {
            if (introClipA != null)
            {
                oneShotAudioSource?.PlayOneShot(introClipA, introVolumeScale);
                yield return new WaitForSeconds(introClipA.length * 0.5f);
            }
            if (introClipB != null)
            {
                oneShotAudioSource?.PlayOneShot(introClipB, introVolumeScale);
            }
            introCoroutine = null;
        }

        /// <summary>
        /// Edge-detects a fresh Up/Down sweep input (idle-to-held, or a
        /// direction flip while still held) and fires a new maneuver layer
        /// group for it -- called every frame regardless of whether the cone
        /// is currently active, so the tracked direction never goes stale
        /// across frames the cone is off.
        /// </summary>
        private void UpdateManeuverEdgeDetection()
        {
            float y = input.MoveInput.y;
            int dirState = y > 0.5f ? 1 : (y < -0.5f ? -1 : 0);
            if (isConeActive && dirState != 0 && dirState != lastManeuverDirState)
            {
                PlayManeuverGroup();
            }
            lastManeuverDirState = dirState;
        }

        /// <summary>
        /// One-shot "the player just pivoted the breath" burst -- randomly
        /// picks one of the three maneuver layer groups (never repeating the
        /// immediately previous one) and plays every clip in it
        /// simultaneously, boosted by maneuverGroupVolumeScale so it isn't
        /// buried under the Main Group's pitch-wobbling loop.
        /// </summary>
        private void PlayManeuverGroup()
        {
            int index;
            do
            {
                index = Random.Range(0, 3);
            }
            while (index == lastManeuverGroupIndex);
            lastManeuverGroupIndex = index;
            PlayAllOneShot(index == 0 ? maneuverGroup1Clips : index == 1 ? maneuverGroup2Clips : maneuverGroup3Clips, maneuverGroupVolumeScale);
        }

        /// <summary>
        /// Plays every clip in the array back-to-back on oneShotAudioSource
        /// in the same frame -- PlayOneShot natively mixes overlapping calls
        /// on one source, so this layers them with zero relative delay.
        /// </summary>
        private void PlayAllOneShot(AudioClip[] clips, float volumeScale = 1f)
        {
            if (clips == null || oneShotAudioSource == null)
            {
                return;
            }
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null)
                {
                    oneShotAudioSource.pitch = 1f;
                    oneShotAudioSource.PlayOneShot(clips[i], volumeScale);
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector2 facing = Application.isPlaying && controller != null ? aimController.CurrentDirection(controller.Facing) : Vector2.right;
            Vector2 origin = (Vector2)transform.position + facing * (coneOffset + holdConeSize.x * 0.5f);
            Gizmos.color = Color.blue;
            Gizmos.matrix = Matrix4x4.TRS(origin, Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector2.right, facing)), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, holdConeSize);
        }
#endif
    }
}
