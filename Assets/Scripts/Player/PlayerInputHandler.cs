using UnityEngine;
using UnityEngine.InputSystem;

namespace HeroFangame.Player
{
    /// <summary>
    /// Wraps the "HeroCombat" action map from the project's InputActionAsset
    /// and exposes simple properties/events for the other player scripts.
    /// Looks the map up by name so it works regardless of whether a generated
    /// C# wrapper class exists for the asset.
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "HeroCombat";

        private InputActionMap map;
        private InputAction moveAction;
        private InputAction punchAction;
        private InputAction flightAction;
        private InputAction heatVisionAction;
        private InputAction freezeBreathAction;

        public Vector2 MoveInput { get; private set; }
        public bool HeatVisionHeld { get; private set; }
        public bool FreezeBreathHeld { get; private set; }
        public bool FlightHeld { get; private set; }

        public event System.Action OnPunch;

        /// <summary>
        /// Optional single-subscriber hook that gets first refusal on every
        /// F-press before it becomes a punch. Returning true means the press
        /// was consumed (e.g. by PlayerGrabAbility grabbing/throwing) and
        /// OnPunch should NOT fire. Using a synchronous delegate here (rather
        /// than letting both PlayerCombat and PlayerGrabAbility subscribe to
        /// OnPunch independently) avoids any Update()-order dependency
        /// between them for a single physical key press.
        /// </summary>
        public System.Func<bool> PunchInterceptor;

        private void Awake()
        {
            if (inputActions == null)
            {
                Debug.LogError("PlayerInputHandler: no InputActionAsset assigned.", this);
                return;
            }

            map = inputActions.FindActionMap(actionMapName, throwIfNotFound: true);

            moveAction = map.FindAction("Move");
            punchAction = map.FindAction("Punch");
            flightAction = map.FindAction("Flight");
            heatVisionAction = map.FindAction("HeatVision");
            freezeBreathAction = map.FindAction("FreezeBreath");
        }

        private void OnEnable()
        {
            map?.Enable();

            if (punchAction != null) punchAction.performed += HandlePunch;
        }

        private void OnDisable()
        {
            if (punchAction != null) punchAction.performed -= HandlePunch;

            map?.Disable();
        }

        private void Update()
        {
            MoveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
            HeatVisionHeld = heatVisionAction != null && heatVisionAction.IsPressed();
            FreezeBreathHeld = freezeBreathAction != null && freezeBreathAction.IsPressed();
            FlightHeld = flightAction != null && flightAction.IsPressed();
        }

        private void HandlePunch(InputAction.CallbackContext ctx)
        {
            if (PunchInterceptor != null && PunchInterceptor())
            {
                return;
            }
            OnPunch?.Invoke();
        }
    }
}
