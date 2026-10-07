using System;
using UnityEngine;
using UnityEngine.InputSystem;
using VrFsim.Settings;

namespace VrFsim.Input
{
    /// <summary>
    /// Owns the control scheme. Works with any gamepad the Input System recognises as a
    /// <c>Gamepad</c> (Xbox / XInput, DualShock 4, DualSense, Switch Pro, and a Logitech F310 with
    /// its back switch set to X), plus keyboard as a fallback. Every action is rebindable; overrides
    /// are stored in <see cref="ControlSettings.bindingOverridesJson"/>.
    /// </summary>
    public class InputHub : MonoBehaviour
    {
        public const string AssetResource = "VrFsimControls";

        public static InputHub Instance { get; private set; }

        public InputActionAsset Asset { get; private set; }
        InputActionMap map;
        InputAction leftStick, rightStick, intake, fire, outtake, placePollen, placeNectar, humanNectar,
            rampToggle, pass, driveModeToggle, flipFront, slowMode, startMatch, restart, menu,
            cameraCycle, recenter, freeCamMove, freeCamLift;

        public event Action MenuPressed, RestartPressed, StartMatchPressed, CameraCyclePressed, RecenterPressed, HumanNectarPressed;

        /// <summary>When true (menu open), driving input is suppressed.</summary>
        public bool DrivingSuppressed { get; set; }

        /// <summary>Driving actions that the settings menu offers for rebinding, in display order.</summary>
        public static readonly string[] RebindableActions =
        {
            "Intake", "Fire", "Outtake", "PlacePollen", "PlaceNectar", "HumanNectar", "RampToggle", "Pass",
            "DriveModeToggle", "FlipFront", "SlowMode", "StartMatch", "Restart", "Menu", "CameraCycle", "Recenter",
        };

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            var source = Resources.Load<InputActionAsset>(AssetResource);
            if (source == null) { Debug.LogError("[VrFsim] Input actions asset missing from Resources."); return; }
            Asset = Instantiate(source);
            map = Asset.FindActionMap("Driver", true);

            leftStick = map.FindAction("LeftStick", true);
            rightStick = map.FindAction("RightStick", true);
            intake = map.FindAction("Intake", true);
            fire = map.FindAction("Fire", true);
            outtake = map.FindAction("Outtake", true);
            placePollen = map.FindAction("PlacePollen", true);
            placeNectar = map.FindAction("PlaceNectar", true);
            humanNectar = map.FindAction("HumanNectar", true);
            rampToggle = map.FindAction("RampToggle", true);
            pass = map.FindAction("Pass", true);
            driveModeToggle = map.FindAction("DriveModeToggle", true);
            flipFront = map.FindAction("FlipFront", true);
            slowMode = map.FindAction("SlowMode", true);
            startMatch = map.FindAction("StartMatch", true);
            restart = map.FindAction("Restart", true);
            menu = map.FindAction("Menu", true);
            cameraCycle = map.FindAction("CameraCycle", true);
            recenter = map.FindAction("Recenter", true);
            freeCamMove = map.FindAction("FreeCamMove", true);
            freeCamLift = map.FindAction("FreeCamLift", true);

            LoadOverrides(SettingsStore.Current.controls.bindingOverridesJson);
            map.Enable();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (Asset != null) Destroy(Asset);
        }

        void Update()
        {
            if (map == null) return;
            if (menu.WasPressedThisFrame()) MenuPressed?.Invoke();
            if (DrivingSuppressed) return;
            if (restart.WasPressedThisFrame()) RestartPressed?.Invoke();
            if (startMatch.WasPressedThisFrame()) StartMatchPressed?.Invoke();
            if (cameraCycle.WasPressedThisFrame()) CameraCyclePressed?.Invoke();
            if (recenter.WasPressedThisFrame()) RecenterPressed?.Invoke();
            if (humanNectar.WasPressedThisFrame()) HumanNectarPressed?.Invoke();
        }

        /// <summary>Sample the driver's command for this frame.</summary>
        public DriverCommand ReadDriver()
        {
            var s = SettingsStore.Current;
            if (map == null || DrivingSuppressed) return DriverCommand.Idle;

            var c = s.controls;
            Vector2 l = Shape(leftStick.ReadValue<Vector2>(), c);
            Vector2 r = Shape(rightStick.ReadValue<Vector2>(), c);
            Vector2 drive = c.driveStick == StickSide.Left ? l : r;
            Vector2 aux = c.driveStick == StickSide.Left ? r : l;

            var cmd = new DriverCommand
            {
                translate = drive,
                turn = aux.x,
                useTank = s.assists.controlMode == DriveControlMode.Tank,
                tankLeft = l.y,
                tankRight = r.y,
                intake = intake.ReadValue<float>() >= c.triggerThreshold,
                fire = fire.ReadValue<float>() >= c.triggerThreshold,
                outtake = outtake.IsPressed(),
                slow = slowMode.IsPressed(),
                placePollen = placePollen.WasPressedThisFrame(),
                placeNectar = placeNectar.WasPressedThisFrame(),
                humanNectar = humanNectar.WasPressedThisFrame(),
                rampToggle = rampToggle.WasPressedThisFrame(),
                pass = pass.WasPressedThisFrame(),
                driveModeToggle = driveModeToggle.WasPressedThisFrame(),
                flipFront = flipFront.WasPressedThisFrame(),
            };
            return cmd;
        }

        public Vector2 FreeCamMove => freeCamMove?.ReadValue<Vector2>() ?? Vector2.zero;
        public float FreeCamLift => freeCamLift?.ReadValue<float>() ?? 0f;

        /// <summary>Radial deadzone rescaled to keep full range, then a power response curve.</summary>
        public static Vector2 Shape(Vector2 v, ControlSettings c)
        {
            float mag = Mathf.Min(v.magnitude, 1f);
            if (mag <= c.deadzone || mag < 1e-5f) return Vector2.zero;
            float scaled = (mag - c.deadzone) / (1f - c.deadzone);
            scaled = Mathf.Pow(scaled, c.curve);
            return v / v.magnitude * scaled;
        }

        // ── Rebinding ───────────────────────────────────────────────────────────────────────

        public InputAction Action(string name) => map?.FindAction(name);

        /// <summary>Human-readable current binding for an action, e.g. "LT / Left Shift".</summary>
        public string BindingLabel(string actionName)
        {
            var a = Action(actionName);
            if (a == null) return "";
            string pad = a.GetBindingDisplayString(InputBinding.MaskByGroup("Gamepad"));
            string kb = a.GetBindingDisplayString(InputBinding.MaskByGroup("Keyboard"));
            if (string.IsNullOrEmpty(pad)) return kb;
            if (string.IsNullOrEmpty(kb)) return pad;
            return pad + "  /  " + kb;
        }

        InputActionRebindingExtensions.RebindingOperation rebinding;
        public bool IsRebinding => rebinding != null;

        /// <summary>
        /// Wait for the next gamepad button (or key, when <paramref name="keyboard"/> is set) and bind
        /// it to the action. Pressing Escape cancels.
        /// </summary>
        public void StartRebind(string actionName, bool keyboard, Action<bool> done)
        {
            var a = Action(actionName);
            if (a == null || rebinding != null) { done?.Invoke(false); return; }
            string group = keyboard ? "Keyboard" : "Gamepad";
            int index = a.GetBindingIndex(InputBinding.MaskByGroup(group));
            if (index < 0) { done?.Invoke(false); return; }

            map.Disable();
            rebinding = a.PerformInteractiveRebinding(index)
                .WithControlsHavingToMatchPath(keyboard ? "<Keyboard>" : "<Gamepad>")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(op => FinishRebind(true, done))
                .OnCancel(op => FinishRebind(false, done))
                .Start();
        }

        void FinishRebind(bool ok, Action<bool> done)
        {
            rebinding?.Dispose();
            rebinding = null;
            map.Enable();
            if (ok) SaveOverrides();
            done?.Invoke(ok);
        }

        public void ResetBindings()
        {
            Asset.RemoveAllBindingOverrides();
            SaveOverrides();
        }

        void SaveOverrides()
        {
            SettingsStore.Current.controls.bindingOverridesJson = Asset.SaveBindingOverridesAsJson();
            SettingsStore.Commit();
        }

        void LoadOverrides(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try { Asset.LoadBindingOverridesFromJson(json); }
            catch (Exception e) { Debug.LogWarning($"[VrFsim] Ignoring bad binding overrides: {e.Message}"); }
        }
    }
}
