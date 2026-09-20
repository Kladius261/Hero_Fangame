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
        private InputAction punchAAction;
        private InputAction punchSAction;
        private InputAction flightAction;
        private InputAction heatVisionAction;
        private InputAction freezeBreathAction;

        public Vector2 MoveInput { get; private set; }
        public bool HeatVisionHeld { get; private set; }
        public bool FreezeBreathHeld { get; private set; }
        public bool FlightHeld { get; private set; }

        public event System.Action OnPunchA;
        public event System.Action OnPunchS;

        private void Awake()
        {
            if (inputActions == null)
            {
                Debug.LogError("PlayerInputHandler: no InputActionAsset assigned.", this);
                return;
            }

            map = inputActions.FindActionMap(actionMapName, throwIfNotFound: true);

            moveAction = map.FindAction("Move");
            punchAAction = map.FindAction("PunchA");
            punchSAction = map.FindAction("PunchS");
            flightAction = map.FindAction("Flight");
            heatVisionAction = map.FindAction("HeatVision");
            freezeBreathAction = map.FindAction("FreezeBreath");
        }

        private void OnEnable()
        {
            map?.Enable();

            if (punchAAction != null) punchAAction.performed += HandlePunchA;
            if (punchSAction != null) punchSAction.performed += HandlePunchS;
        }

        private void OnDisable()
        {
            if (punchAAction != null) punchAAction.performed -= HandlePunchA;
            if (punchSAction != null) punchSAction.performed -= HandlePunchS;

            map?.Disable();
        }

        private void Update()
        {
            MoveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
            HeatVisionHeld = heatVisionAction != null && heatVisionAction.IsPressed();
            FreezeBreathHeld = freezeBreathAction != null && freezeBreathAction.IsPressed();
            FlightHeld = flightAction != null && flightAction.IsPressed();
        }

        private void HandlePunchA(InputAction.CallbackContext ctx) => OnPunchA?.Invoke();
        private void HandlePunchS(InputAction.CallbackContext ctx) => OnPunchS?.Invoke();
    }
}
