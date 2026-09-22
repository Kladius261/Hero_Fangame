using UnityEngine;
using HeroFangame.Interactables;

namespace HeroFangame.Player
{
    /// <summary>
    /// Double-tap-F grab-and-throw for nearby DestructibleObjects. Hooks into
    /// PlayerInputHandler.PunchInterceptor so it gets first, single-point,
    /// order-independent refusal authority over every F-press: while
    /// grabbing nothing, a double-tap near a grabbable object mounts it on
    /// the player (entering "grab stance") and swallows that press; while
    /// already grabbing, the very next single press throws it. Every other
    /// press is left alone and falls through to the normal punch.
    /// </summary>
    [RequireComponent(typeof(PlayerInputHandler))]
    [RequireComponent(typeof(PlayerController))]
    public class PlayerGrabAbility : MonoBehaviour
    {
        [Header("Grab")]
        [SerializeField] private float grabRange = 1.5f;
        [SerializeField] private LayerMask grabbableLayers;
        [SerializeField] private float doubleTapWindow = 0.3f;

        private PlayerInputHandler input;
        private PlayerController controller;

        private static readonly Collider2D[] overlapBuffer = new Collider2D[16];

        private float lastTapTime = -100f;
        private DestructibleObject grabbedTarget;

        public bool IsGrabbing => grabbedTarget != null;

        private void Awake()
        {
            input = GetComponent<PlayerInputHandler>();
            controller = GetComponent<PlayerController>();
        }

        private void OnEnable()
        {
            input.PunchInterceptor = EvaluatePunchForGrab;
        }

        private void OnDisable()
        {
            if (input.PunchInterceptor == (System.Func<bool>)EvaluatePunchForGrab)
            {
                input.PunchInterceptor = null;
            }
        }

        private void Update()
        {
            // Defensive: if the carried object was destroyed out from under
            // us (Unity's overridden null-check makes grabbedTarget appear
            // null the instant that happens), IsGrabbing silently flips to
            // false without ever going through Throw() — which is the only
            // place that normally clears grab stance. Without this, Punch/
            // HeatVision/FreezeBreath would stay locked out forever.
            if (controller.IsGrabStance && grabbedTarget == null)
            {
                controller.SetGrabStance(false);
            }
        }

        private bool EvaluatePunchForGrab()
        {
            if (IsGrabbing)
            {
                Throw(controller.Facing);
                return true;
            }

            float now = Time.unscaledTime;
            bool isDoubleTap = (now - lastTapTime) <= doubleTapWindow;
            lastTapTime = now;

            if (isDoubleTap)
            {
                // Consume the tap regardless of outcome so a triple-tap can't
                // chain straight into another grab attempt off the same pair
                // of presses.
                lastTapTime = -100f;

                if (!controller.IsFlightMode && !controller.IsMovementLocked && !controller.IsGrabStance)
                {
                    var target = FindNearestGrabbable();
                    if (target != null)
                    {
                        BeginGrab(target);
                        return true;
                    }
                }
            }

            return false;
        }

        private DestructibleObject FindNearestGrabbable()
        {
            int count = Physics2D.OverlapCircleNonAlloc(transform.position, grabRange, overlapBuffer, grabbableLayers);

            DestructibleObject nearest = null;
            float nearestSqrDist = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var hit = overlapBuffer[i];
                if (hit == null)
                {
                    continue;
                }
                var candidate = hit.GetComponentInParent<DestructibleObject>();
                if (candidate == null || candidate.IsGrabbed)
                {
                    continue;
                }
                float sqrDist = ((Vector2)candidate.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (sqrDist < nearestSqrDist)
                {
                    nearestSqrDist = sqrDist;
                    nearest = candidate;
                }
            }
            return nearest;
        }

        private void BeginGrab(DestructibleObject target)
        {
            grabbedTarget = target;
            controller.SetGrabStance(true);
            target.BeginGrab(transform);
        }

        private void Throw(Vector2 direction)
        {
            var target = grabbedTarget;
            grabbedTarget = null;
            controller.SetGrabStance(false);
            target.Throw(direction, gameObject);
        }
    }
}
