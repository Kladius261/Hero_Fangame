using UnityEngine;
using HeroFangame.Interactables;

namespace HeroFangame.Player
{
    /// <summary>
    /// G-to-grab and G-to-throw for nearby IGrabbable objects (Destructibles,
    /// Explosives, ...). G is a dedicated grab input — it never deals damage,
    /// so approaching (and even picking up a frozen) object never risks an
    /// accidental hit the way double-tapping the punch key used to. While not
    /// carrying anything, G grabs the nearest target in range; while already
    /// carrying, the next G-press throws instead. F is left as a plain punch
    /// at all times.
    /// </summary>
    [RequireComponent(typeof(PlayerInputHandler))]
    [RequireComponent(typeof(PlayerController))]
    public class PlayerGrabAbility : MonoBehaviour
    {
        [Header("Grab")]
        [SerializeField] private float grabRange = 1.5f;
        [SerializeField] private LayerMask grabbableLayers;

        private PlayerInputHandler input;
        private PlayerController controller;

        private static readonly Collider2D[] overlapBuffer = new Collider2D[16];

        private IGrabbable grabbedTarget;

        public bool IsGrabbing => grabbedTarget != null;

        private void Awake()
        {
            input = GetComponent<PlayerInputHandler>();
            controller = GetComponent<PlayerController>();
        }

        private void OnEnable()
        {
            input.OnGrab += HandleGrabPressed;
        }

        private void OnDisable()
        {
            input.OnGrab -= HandleGrabPressed;
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

        private void HandleGrabPressed()
        {
            if (IsGrabbing)
            {
                Throw(controller.Facing);
                return;
            }

            if (controller.IsFlightMode || controller.IsMovementLocked || controller.IsGrabStance)
            {
                return;
            }

            var target = FindNearestGrabbable();
            if (target != null)
            {
                BeginGrab(target);
            }
        }

        private IGrabbable FindNearestGrabbable()
        {
            int count = Physics2D.OverlapCircleNonAlloc(transform.position, grabRange, overlapBuffer, grabbableLayers);

            IGrabbable nearest = null;
            float nearestSqrDist = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var hit = overlapBuffer[i];
                if (hit == null)
                {
                    continue;
                }
                var candidate = hit.GetComponentInParent<IGrabbable>();
                if (candidate == null || candidate.IsGrabbed)
                {
                    continue;
                }
                var candidateTransform = (candidate as Component).transform;
                float sqrDist = ((Vector2)candidateTransform.position - (Vector2)transform.position).sqrMagnitude;
                if (sqrDist < nearestSqrDist)
                {
                    nearestSqrDist = sqrDist;
                    nearest = candidate;
                }
            }
            return nearest;
        }

        private void BeginGrab(IGrabbable target)
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
