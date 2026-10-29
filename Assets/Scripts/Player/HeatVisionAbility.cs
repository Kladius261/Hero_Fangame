using System.Collections;
using UnityEngine;
using HeroFangame.Combat;
using HeroFangame.Core;
using HeroFangame.Camera;
using HeroFangame.Interactables;
using HeroFangame.UI;
using BeamHitResult = HeroFangame.Combat.AttackUtility.BeamHitResult;

namespace HeroFangame.Player
{
    /// <summary>
    /// Long-range narrow attack, fired strictly horizontally (left or right,
    /// picked from the player's last horizontal facing — never up/down or
    /// diagonal) and far enough to reach across the screen from anywhere in
    /// the level. A tap fires one instant, piercing hit that damages and
    /// knocks back every enemy currently inside the beam's box, not just the
    /// closest. Continuing to hold turns it into a sustained beam that
    /// re-applies that same piercing damage + knockback to everyone
    /// currently inside the beam on every tick, drains Power (auto-cutting
    /// off at 0). Drives a beam visual, a smoke/fire impact effect (clipped
    /// to the single closest surface for visual accuracy), and a subtle
    /// continuous camera shake while firing.
    /// </summary>
    [RequireComponent(typeof(PlayerInputHandler))]
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PowerGauge))]
    [RequireComponent(typeof(AbilityLock))]
    public class HeatVisionAbility : MonoBehaviour
    {
        [Header("Beam Shape")]
        [SerializeField] private Vector2 beamSize = new Vector2(6f, 0.35f);
        [SerializeField] private float beamRange = 50f;
        [SerializeField] private float beamOffset = 0.6f;
        [SerializeField] private LayerMask hittableLayers;

        [Header("Damage")]
        [SerializeField] private int tapDamage = 3;
        [SerializeField] private int beamTickDamage = 1;
        [SerializeField] private float beamTickInterval = 0.1f;

        [Header("Knockback")]
        [SerializeField] private float knockbackForce = 6f;

        [Header("Haptics")]
        [Tooltip("Continuous rumble while the beam is firing (tap pulse or held), mirroring the continuous camera shake toggle.")]
        [SerializeField] private float beamHapticLowFrequency = 0.3f;
        [SerializeField] private float beamHapticHighFrequency = 0.5f;

        [Header("Power Cost")]
        [SerializeField] private float tapPowerCost = 10f;
        [SerializeField] private float holdPowerCostPerSecond = 30f;

        [Header("Visuals")]
        [SerializeField] private float tapPulseDuration = 0.12f;
        [SerializeField] private HeatVisionBeamRenderer beamRenderer;
        [SerializeField] private HeatVisionImpactEffect impactEffect;
        [Tooltip("Heat-haze distortion sprite, dynamically instantiated at the player's position when the beam starts and destroyed the instant it stops (lighter than keeping a disabled instance parented under the player).")]
        [SerializeField] private GameObject distortionEffectPrefab;

        [Header("Audio")]
        [SerializeField] private AudioSource beamAudioSource;
        [Tooltip("Second looping layer of the Loop/Main Group (ASF_WEAPON_Lightsaber_Idle02_Loop), played and pitch-wobbled in lockstep with beamAudioSource.")]
        [SerializeField] private AudioSource beamAudioSourceLayer2;
        [Tooltip("Shared one-shot source for every non-looping layer (Introductory, maneuver Loop groups, Ending, Feedback) — PlayOneShot natively mixes overlapping calls on a single source, so one is enough.")]
        [SerializeField] private AudioSource oneShotAudioSource;
        [SerializeField] private float pitchWobbleSpeed = 6f;
        [SerializeField] private float pitchWobbleAmplitude = 0.1f;
        [Tooltip("Playback speed multiplier for the Introductory one-shot group -- doubled so the opening bass-drop/whoosh lands faster.")]
        [SerializeField] private float introEndingPlaybackSpeed = 2f;
        [Tooltip("Playback speed multiplier for the Ending (release) one-shot group's 3 stages -- its natural ~4.1s total timeline (stage 1's length, plus half of stage 2's, plus stage 3's own length) divided by this gives its actual playtime; 2.74x compresses it to ~1.5s.")]
        [SerializeField] private float endingPlaybackSpeed = 2.74f;
        [Tooltip("Relative volume multiplier for Loop maneuver-group one-shots versus the Loop Main Group's own AudioSource volume (assumed 1.0) -- boosted so a maneuver burst cuts through the main loop's pitch-wobble instead of getting buried under it.")]
        [SerializeField] private float maneuverGroupVolumeScale = 1.5f;
        [Tooltip("Relative volume multiplier for Feedback (Enemies/Destructibles) group one-shots versus the Loop Main Group's own AudioSource volume (assumed 1.0). Left at 1.0 (original) -- an earlier 1.25x boost was tried and then reverted.")]
        [SerializeField] private float feedbackGroupVolumeScale = 1f;
        [Tooltip("Duration of the smooth volume fade-out applied to the Introductory one-shot, Loop Main Group (both layers), and any still-sounding Loop maneuver one-shot when the beam is released -- replaces an instant cutoff with a quick decline so release doesn't sound like an abrupt audio pop. Independent of the Ending sequence's own pacing (introEndingPlaybackSpeed) -- the two just run concurrently.")]
        [SerializeField] private float releaseFadeOutDuration = 0.5f;

        [Header("Audio - Introductory (on press)")]
        [SerializeField] private AudioClip introClipA;
        [SerializeField] private AudioClip introClipB;

        [Header("Audio - Loop Maneuver Groups (one-shot, on aim sweep)")]
        [SerializeField] private AudioClip[] maneuverGroup1Clips;
        [SerializeField] private AudioClip[] maneuverGroup2Clips;
        [SerializeField] private AudioClip[] maneuverGroup3Clips;

        [Header("Audio - Ending (on release)")]
        [SerializeField] private AudioClip endingStage1ClipA;
        [SerializeField] private AudioClip endingStage1ClipB;
        [SerializeField] private AudioClip endingStage2ClipA;
        [SerializeField] private AudioClip endingStage2ClipB;
        [SerializeField] private AudioClip endingStage2ClipC;
        [SerializeField] private AudioClip endingStage3Clip;

        [Header("Audio - Feedback (Enemies/Destructibles, on hit)")]
        [SerializeField] private AudioClip[] feedbackGroup1Clips;
        [SerializeField] private AudioClip[] feedbackGroup2Clips;
        [SerializeField] private AudioClip[] feedbackGroup3Clips;

        [Header("Aim")]
        [SerializeField] private float aimSweepSpeedDegreesPerSecond = 180f;
        [SerializeField] private float aimSweepAccelerationDegreesPerSecondSquared = 720f;

        private PlayerInputHandler input;
        private PlayerController controller;
        private PowerGauge power;
        private AbilityLock abilityLock;
        private CameraShake cameraShake;
        private AbilityAimController aimController;

        private bool wasHeld;
        private float tickTimer;
        private float tapPulseTimeRemaining;
        private bool isBeamActive;
        private GameObject distortionEffectInstance;
        private TextPopupHandle textPopupHandle;

        private AudioPitchWobble pitchWobble;
        private int lastManeuverGroupIndex = -1;
        private int lastFeedbackGroupIndex = -1;

        /// <summary>
        /// True once the current activation's PlayIntroSequence has fully
        /// finished playing (including introClipB). Currently unused for
        /// gating any Stop logic (release now always fades out rather than
        /// branching on this), but kept set so a future re-enable of
        /// PlayEndingSequence can again gate on "did the intro finish" the
        /// same way it originally did.
        /// </summary>
        private bool introSequenceFinished;

        /// <summary>
        /// Handle of the currently-running PlayIntroSequence coroutine (null
        /// once it has finished), so StopBeamVisuals can cancel it outright
        /// on release instead of letting it keep firing intro clips into the
        /// fade-out below.
        /// </summary>
        private Coroutine introCoroutine;

        /// <summary>
        /// Handle of the currently-running release fade-out (null once it
        /// has finished), so a fresh activation that starts mid-fade -- or a
        /// second release that re-enters StopBeamVisuals -- can cancel the
        /// old fade outright instead of letting two fades fight over the
        /// same AudioSources' volumes.
        /// </summary>
        private Coroutine releaseFadeOutCoroutine;

        /// <summary>
        /// Original inspector-configured volumes of the two faded
        /// AudioSources, captured once in Awake -- the release fade drives
        /// each source's volume down to 0 and leaves it there, so every
        /// fresh activation must restore this base value before playing
        /// again rather than starting back up silent. oneShotAudioSource is
        /// NOT included -- see FadeOutOnRelease.
        /// </summary>
        private float beamAudioSourceBaseVolume = 1f;
        private float beamAudioSourceLayer2BaseVolume = 1f;

        /// <summary>
        /// -1 = Down held, 0 = neither/both held, 1 = Up held. Tracked every
        /// frame unconditionally (not just while the beam is active) so that
        /// if the player is already holding a direction the instant Heat
        /// Vision activates, that pre-existing hold doesn't register as a
        /// fresh "new sweep" edge on the activation frame.
        /// </summary>
        private int lastManeuverDirState;

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

            if (beamAudioSource != null) beamAudioSourceBaseVolume = beamAudioSource.volume;
            if (beamAudioSourceLayer2 != null) beamAudioSourceLayer2BaseVolume = beamAudioSourceLayer2.volume;
        }

        private void Update()
        {
            UpdateManeuverEdgeDetection();

            if (controller.IsFlightMode || controller.IsGrabStance)
            {
                StopBeamVisuals();
                return;
            }

            bool rawHeld = input.HeatVisionHeld;
            bool isHeld = abilityLock.CanActivate(this, rawHeld);

            if (!isHeld && tapPulseTimeRemaining > 0f)
            {
                // Tap already fired; keep its brief beam/shake pulse alive
                // for a few frames instead of cutting it off instantly.
                tapPulseTimeRemaining -= Time.deltaTime;
                if (tapPulseTimeRemaining <= 0f)
                {
                    StopBeamVisuals();
                }
                wasHeld = false;
                return;
            }

            bool activatedThisFrame = false;

            if (isHeld && !wasHeld)
            {
                // Tap: instant piercing shot against every enemy in the beam.
                if (power.TrySpend(tapPowerCost))
                {
                    FireTap();
                    activatedThisFrame = true;
                }
                tickTimer = beamTickInterval;
            }
            else if (isHeld && wasHeld)
            {
                // Hold: sustained beam. Resolve aim/visuals every frame so
                // they stay in sync with player input, but only apply
                // damage + knockback to everyone in the beam on the tick
                // cadence.
                float drained = power.DrainOverTime(holdPowerCostPerSecond);
                if (drained > 0f)
                {
                    tickTimer -= Time.deltaTime;
                    bool doDamageTick = tickTimer <= 0f;
                    if (doDamageTick)
                    {
                        tickTimer = beamTickInterval;
                    }
                    ResolveHoldFrame(doDamageTick);
                    activatedThisFrame = true;
                }
                else
                {
                    StopBeamVisuals();
                }
            }
            else
            {
                StopBeamVisuals();
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

        private BeamHitResult Probe(Vector2 origin, Vector2 dir, out bool hitScreenEdge, out float maxDistance)
        {
            // Never let the beam reach past the edge of the camera's
            // current view — enemies further along the level haven't
            // scrolled into frame yet and shouldn't be hittable before the
            // player can even see them.
            float screenEdgeDistance = CameraViewBounds.GetDistanceToEdge(origin.x, dir.x);
            maxDistance = Mathf.Min(beamRange, screenEdgeDistance);
            var result = AttackUtility.BoxCastSinglePeek(origin, new Vector2(beamSize.y, beamSize.y), dir, maxDistance, hittableLayers);

            // True only when nothing was actually hit AND the beam's reach
            // was cut short specifically by the screen edge (not merely by
            // its own beamRange running out) — lets callers show a scorch
            // impact right at the boundary instead of the beam just
            // vanishing into nothing.
            hitScreenEdge = !result.DidHit && screenEdgeDistance < beamRange;
            return result;
        }

        /// <summary>
        /// Damages and knocks back every enemy currently inside the beam's
        /// full box (origin to maxDistance along dir) in one pass, instead
        /// of only the single closest target — lets the beam pierce through
        /// and hit multiple enemies lined up along its path, and lets a
        /// sweeping beam damage/push each enemy it currently touches every
        /// tick rather than just the first one it ever made contact with.
        /// </summary>
        private void ApplyBeamDamageAndKnockback(Vector2 origin, Vector2 dir, float maxDistance, int damage, bool applyKnockback)
        {
            Vector2 center = origin + dir * (maxDistance * 0.5f);
            Vector2 size = new Vector2(maxDistance, beamSize.y);
            float angle = Vector2.SignedAngle(Vector2.right, dir);
            var hits = AttackUtility.OverlapBoxAndDamage(center, size, angle, hittableLayers, damage, gameObject, dir, applyKnockback ? knockbackForce : 0f, out int hitCount);

            // Explosives get their own dedicated sound (shared project-wide,
            // played from ExplosiveObject.Explode itself) instead of the
            // generic Enemies/Destructible feedback group below -- only
            // trigger that group if at least one struck target isn't an
            // ExplosiveObject, so a beam pass that hits only explosive(s)
            // doesn't double up both sounds at once.
            bool hasNonExplosiveHit = false;
            for (int i = 0; i < hitCount; i++)
            {
                hits[i].GetComponentInParent<HitSquashEffect>()?.PlaySquash(dir);
                if (hits[i].GetComponentInParent<ExplosiveObject>() == null)
                {
                    hasNonExplosiveHit = true;
                }
            }

            if (hitCount > 0 && hasNonExplosiveHit)
            {
                PlayFeedbackGroup();
            }
        }

        /// <summary>
        /// Heat Vision fires along the player's horizontal Facing. On
        /// activation, it auto-aims at the closest on-screen target inside
        /// the +/-AngleCapDegrees cone (falling back to horizontal if none
        /// exists); while held, Up/Down arrow keys tilt it further via
        /// <see cref="aimController"/> (see AbilityAimController for the
        /// exact aim rules).
        /// </summary>
        private Vector2 GetBeamDirection(bool isNewActivation)
        {
            float initialAngle = 0f;
            if (isNewActivation)
            {
                float horizontalSign = controller.Facing.x < 0f ? -1f : 1f;
                float maxDistance = Mathf.Min(beamRange, CameraViewBounds.GetDistanceToEdge(transform.position.x, horizontalSign));
                initialAngle = AbilityAimController.FindAutoAimAngleDegrees(transform.position, controller.Facing, maxDistance, hittableLayers);
            }
            return aimController.Resolve(isNewActivation, initialAngle, input.MoveInput, controller.Facing, Time.deltaTime);
        }

        private void FireTap()
        {
            Vector2 dir = GetBeamDirection(isNewActivation: true);
            Vector2 origin = (Vector2)transform.position + dir * beamOffset;
            var hit = Probe(origin, dir, out bool hitScreenEdge, out float maxDistance);

            ApplyBeamDamageAndKnockback(origin, dir, maxDistance, tapDamage, applyKnockback: true);

            tapPulseTimeRemaining = tapPulseDuration;
            ActivateBeamVisual(origin, dir, hit, hitScreenEdge);
            SetCameraShake(true);
        }

        private void ResolveHoldFrame(bool doDamageTick)
        {
            Vector2 dir = GetBeamDirection(isNewActivation: false);
            Vector2 origin = (Vector2)transform.position + dir * beamOffset;
            var hit = Probe(origin, dir, out bool hitScreenEdge, out float maxDistance);

            ActivateBeamVisual(origin, dir, hit, hitScreenEdge);
            SetCameraShake(true);

            if (doDamageTick)
            {
                ApplyBeamDamageAndKnockback(origin, dir, maxDistance, beamTickDamage, applyKnockback: true);
            }
        }

        private void ActivateBeamVisual(Vector2 origin, Vector2 dir, BeamHitResult hit, bool hitScreenEdge)
        {
            if (!isBeamActive)
            {
                // A fresh activation always wins over a still-running
                // release fade from a previous activation (e.g. a quick
                // retap) -- cancel it and restore every faded source back
                // to its base volume before playing again, so the new
                // activation never starts out partway faded/silent.
                if (releaseFadeOutCoroutine != null)
                {
                    StopCoroutine(releaseFadeOutCoroutine);
                    releaseFadeOutCoroutine = null;
                }
                if (beamAudioSource != null) beamAudioSource.volume = beamAudioSourceBaseVolume;
                if (beamAudioSourceLayer2 != null) beamAudioSourceLayer2.volume = beamAudioSourceLayer2BaseVolume;

                beamAudioSource?.Play();
                beamAudioSourceLayer2?.Play();
                pitchWobble.Restart();
                introCoroutine = StartCoroutine(PlayIntroSequence());
                if (distortionEffectPrefab != null && distortionEffectInstance == null)
                {
                    distortionEffectInstance = Instantiate(distortionEffectPrefab, transform.position, Quaternion.identity);
                }
                textPopupHandle = TextPopupManager.Instance?.BeginSustained(TextPopupManager.Category.HeatVision, transform.position);
            }
            isBeamActive = true;
            pitchWobble.Apply(beamAudioSource, beamAudioSourceLayer2, Time.deltaTime);
            textPopupHandle?.UpdatePosition(transform.position);
            textPopupHandle?.Tick(Time.deltaTime, beamTickInterval);
            controller.LockMovement(this);
            if (beamRenderer != null)
            {
                beamRenderer.SetActive(true);
                beamRenderer.UpdateBeam(origin, dir, hit.Distance);
            }
            FullScreenAbilityEffect.Instance?.SetActive(FullScreenAbilityEffect.Kind.HeatVision, true);

            if (impactEffect != null)
            {
                if (hit.DidHit || hitScreenEdge)
                {
                    // hit.Point already equals the clamped screen-edge point
                    // when nothing was actually hit (AttackUtility falls
                    // back to origin + direction * maxDistance), so the same
                    // fire/burn/sparkle impact effect used for real contact
                    // doubles as a "hit the edge of the screen" scorch mark.
                    impactEffect.UpdateContact(hit.Point, dir);
                }
                else
                {
                    impactEffect.StopContact();
                }
            }
        }

        private void StopBeamVisuals()
        {
            if (!isBeamActive)
            {
                return;
            }

            isBeamActive = false;
            controller.UnlockMovement(this);
            textPopupHandle?.Release();
            textPopupHandle = null;
            beamRenderer?.SetActive(false);
            impactEffect?.StopContact();
            if (distortionEffectInstance != null)
            {
                Destroy(distortionEffectInstance);
                distortionEffectInstance = null;
            }
            FullScreenAbilityEffect.Instance?.SetActive(FullScreenAbilityEffect.Kind.HeatVision, false);

            // Cancel the Introductory coroutine outright so no further
            // intro clips fire into the fade-out below, then fade the Loop
            // Main Group (both AudioSources) down to silence over
            // releaseFadeOutDuration instead of cutting it off instantly.
            // oneShotAudioSource is deliberately NOT touched here -- it now
            // also carries the Ending sequence below, which is meant to
            // keep sounding well past releaseFadeOutDuration, so it's left
            // to finish each one-shot naturally instead of being faded/
            // stopped by a timer sized for the two looping beam sources.
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

            // The Ending group starts immediately on release, on the same
            // oneShotAudioSource -- which the fade above no longer touches,
            // so its ~2s sequence plays out at full, unfaded volume instead
            // of being silenced/stopped partway through by a 0.5s timer.
            // Not tracked in a Coroutine field (same as PlayIntroSequence's
            // rationale) since it only ever calls PlayOneShot
            // (fire-and-forget); a fresh activation or a second release
            // overlapping it can't corrupt any shared state.
            StartCoroutine(PlayEndingSequence());

            SetCameraShake(false);
        }

        /// <summary>
        /// Fades beamAudioSource and beamAudioSourceLayer2 (the Loop Main
        /// Group) from their current volume down to 0 over
        /// releaseFadeOutDuration, in lockstep, then stops both -- these are
        /// the only sources that actually need an explicit fade+stop, since
        /// as loops they'd otherwise keep sounding forever. oneShotAudioSource
        /// is intentionally excluded: every sound on it (Intro, maneuver
        /// bursts, Ending) is a one-shot that stops itself once its clip
        /// ends, so there's nothing to fade or stop there -- doing so would
        /// cut the ~2s Ending sequence off after just this fade's 0.5s.
        /// Volumes are left at 0 afterward; ActivateBeamVisual restores each
        /// source's base volume before the next activation plays again.
        /// </summary>
        private IEnumerator FadeOutOnRelease()
        {
            float beamStartVolume = beamAudioSource != null ? beamAudioSource.volume : 0f;
            float beamLayer2StartVolume = beamAudioSourceLayer2 != null ? beamAudioSourceLayer2.volume : 0f;

            float elapsed = 0f;
            while (elapsed < releaseFadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / releaseFadeOutDuration);
                if (beamAudioSource != null) beamAudioSource.volume = Mathf.Lerp(beamStartVolume, 0f, t);
                if (beamAudioSourceLayer2 != null) beamAudioSourceLayer2.volume = Mathf.Lerp(beamLayer2StartVolume, 0f, t);
                yield return null;
            }

            beamAudioSource?.Stop();
            beamAudioSourceLayer2?.Stop();
            releaseFadeOutCoroutine = null;
        }

        private void SetCameraShake(bool active)
        {
            if (cameraShake == null)
            {
                cameraShake = CameraShake.GetOrCreate();
            }
            cameraShake?.SetShaking(active);
            Haptics.GetOrCreate()?.SetContinuous(active, beamHapticLowFrequency, beamHapticHighFrequency);
        }

        /// <summary>
        /// Edge-detects a fresh Up/Down sweep input (idle-to-held, or a
        /// direction flip while still held) and fires a new maneuver layer
        /// group for it -- called every frame regardless of whether the beam
        /// is currently active, so the tracked direction never goes stale
        /// across frames the beam is off.
        /// </summary>
        private void UpdateManeuverEdgeDetection()
        {
            float y = input.MoveInput.y;
            int dirState = y > 0.5f ? 1 : (y < -0.5f ? -1 : 0);
            if (isBeamActive && dirState != 0 && dirState != lastManeuverDirState)
            {
                PlayManeuverGroup();
            }
            lastManeuverDirState = dirState;
        }

        /// <summary>
        /// One-shot "the player just pivoted the beam" burst -- randomly
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
        /// One-shot "the beam just hit something" burst -- randomly picks
        /// one of the three Enemies/Destructible feedback groups (never
        /// repeating the immediately previous one) and plays every clip in
        /// it simultaneously.
        /// </summary>
        private void PlayFeedbackGroup()
        {
            int index;
            do
            {
                index = Random.Range(0, 3);
            }
            while (index == lastFeedbackGroupIndex);
            lastFeedbackGroupIndex = index;
            PlayAllOneShot(index == 0 ? feedbackGroup1Clips : index == 1 ? feedbackGroup2Clips : feedbackGroup3Clips, feedbackGroupVolumeScale);
        }

        /// <summary>
        /// Plays every clip in the array back-to-back on oneShotAudioSource
        /// in the same frame -- PlayOneShot natively mixes overlapping calls
        /// on one source, so this layers them with zero relative delay.
        /// Always played at normal (1x) pitch/speed -- only the Introductory
        /// and Ending groups get sped up, via PlayOneShotSped below.
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

        /// <summary>
        /// Plays a single clip on oneShotAudioSource at introEndingPlaybackSpeed
        /// -- used only by the Introductory sequence. Unity snapshots an
        /// AudioSource's pitch at the instant PlayOneShot is called, so
        /// stamping the sped-up pitch here (rather than leaving it set
        /// between calls) can't bleed into a Loop maneuver/Feedback one-shot
        /// that happens to fire the same frame.
        /// </summary>
        private void PlayOneShotSped(AudioClip clip)
        {
            PlayOneShotAtSpeed(clip, introEndingPlaybackSpeed);
        }

        /// <summary>
        /// Plays a single clip on oneShotAudioSource at an arbitrary pitch
        /// multiplier -- the general form PlayOneShotSped delegates to for
        /// the Introductory sequence's speed, and PlayEndingSequence
        /// delegates to for its own, separate endingPlaybackSpeed.
        /// </summary>
        private void PlayOneShotAtSpeed(AudioClip clip, float speed)
        {
            if (clip == null || oneShotAudioSource == null)
            {
                return;
            }
            oneShotAudioSource.pitch = speed;
            oneShotAudioSource.PlayOneShot(clip);
        }

        /// <summary>
        /// Bass drop immediately, whoosh at its midpoint, both played at
        /// introEndingPlaybackSpeed so the whole intro lands faster. Sets
        /// introSequenceFinished once introClipB has been triggered, so
        /// StopBeamVisuals can tell a quick release ("short zap") apart from
        /// a release that comes after the intro has fully played out. Left
        /// to finish on its own timeline even if a new activation or release
        /// happens mid-sequence -- it only ever calls PlayOneShot
        /// (fire-and-forget), so overlapping with a later sequence can't
        /// corrupt shared state.
        /// </summary>
        private IEnumerator PlayIntroSequence()
        {
            introSequenceFinished = false;
            if (introClipA != null)
            {
                PlayOneShotSped(introClipA);
                yield return new WaitForSeconds(introClipA.length / introEndingPlaybackSpeed * 0.5f);
            }
            if (introClipB != null)
            {
                PlayOneShotSped(introClipB);
            }
            introSequenceFinished = true;
            introCoroutine = null;
        }

        /// <summary>
        /// Three-stage release sequence: stage 1 (metallic reverse + reverse
        /// sweep) immediately, stage 2 (bass drop + casting impact + whoosh
        /// hit) right after stage 1 finishes, stage 3 (magic curse fire) at
        /// stage 2's midpoint, all at endingPlaybackSpeed -- its own fixed
        /// multiplier, independent of the Introductory sequence's speed, so
        /// its natural ~4.1s total timeline compresses to ~1.5s instead.
        /// Started immediately on release, independent of
        /// releaseFadeOutDuration -- the two just happen to run
        /// concurrently, not in lockstep.
        /// </summary>
        private IEnumerator PlayEndingSequence()
        {
            PlayOneShotAtSpeed(endingStage1ClipA, endingPlaybackSpeed);
            PlayOneShotAtSpeed(endingStage1ClipB, endingPlaybackSpeed);
            yield return new WaitForSeconds(Mathf.Max(SafeLength(endingStage1ClipA), SafeLength(endingStage1ClipB)) / endingPlaybackSpeed);

            PlayOneShotAtSpeed(endingStage2ClipA, endingPlaybackSpeed);
            PlayOneShotAtSpeed(endingStage2ClipB, endingPlaybackSpeed);
            PlayOneShotAtSpeed(endingStage2ClipC, endingPlaybackSpeed);
            float stage2Duration = Mathf.Max(SafeLength(endingStage2ClipA), SafeLength(endingStage2ClipB), SafeLength(endingStage2ClipC)) / endingPlaybackSpeed;
            yield return new WaitForSeconds(stage2Duration * 0.5f);

            PlayOneShotAtSpeed(endingStage3Clip, endingPlaybackSpeed);
        }

        private static float SafeLength(AudioClip clip)
        {
            return clip != null ? clip.length : 0f;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector2 facing = Application.isPlaying && controller != null ? aimController.CurrentDirection(controller.Facing) : Vector2.right;
            Vector2 origin = (Vector2)transform.position + facing * beamOffset;
            Vector2 end = origin + facing * beamRange;
            float angle = Vector2.SignedAngle(Vector2.right, facing);

            Gizmos.color = Color.cyan;
            Gizmos.matrix = Matrix4x4.TRS((origin + end) * 0.5f, Quaternion.Euler(0, 0, angle), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(beamRange, beamSize.y, 0f));
        }
#endif
    }
}
