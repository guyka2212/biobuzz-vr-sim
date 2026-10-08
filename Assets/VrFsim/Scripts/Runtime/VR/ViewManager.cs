using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using VrFsim.Game;
using VrFsim.Input;
using VrFsim.Match;
using VrFsim.Settings;

namespace VrFsim.VR
{
    /// <summary>
    /// The player's head. Builds an XR Origin with a tracked camera, and moves it between views:
    /// the driver station (default, standing in the ALLIANCE AREA like a real DRIVER), overhead,
    /// chase, robot POV, and free-fly. Without a headset it falls back to a desktop camera with
    /// right-mouse look, so the sim can be tested on a monitor.
    /// </summary>
    public class ViewManager : MonoBehaviour
    {
        public static ViewManager Instance { get; private set; }

        public XROrigin Origin { get; private set; }
        public Camera Cam { get; private set; }
        public Transform Head => Cam.transform;
        public bool XrActive { get; private set; }
        public CameraView View { get; private set; }

        public Transform LeftController { get; private set; }
        public Transform RightController { get; private set; }

        Transform offset, recenter;
        readonly List<TrackedPoseDriver> trackers = new List<TrackedPoseDriver>(3);
        float eyeAdjust;
        Vector3 followVel;
        float desktopYaw, desktopPitch;
        const float DesktopEyeHeight = 1.65f;
        static readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();

        void Awake()
        {
            Instance = this;
            BuildRig();
        }

        void Start()
        {
            if (InputHub.Instance)
            {
                InputHub.Instance.CameraCyclePressed += CycleView;
                InputHub.Instance.RecenterPressed += Recenter;
            }
            if (MatchController.Instance) MatchController.Instance.PlayerSpawned += _ => ApplyView(SettingsStore.Current.camera.view, true);
            SettingsStore.Changed += OnSettingsChanged;
            ApplyView(SettingsStore.Current.camera.view, true);
        }

        void OnDestroy()
        {
            SettingsStore.Changed -= OnSettingsChanged;
            if (InputHub.Instance)
            {
                InputHub.Instance.CameraCyclePressed -= CycleView;
                InputHub.Instance.RecenterPressed -= Recenter;
            }
        }

        void OnSettingsChanged(SimSettings s)
        {
            if (s.camera.view != View) ApplyView(s.camera.view, true);
        }

        void BuildRig()
        {
            var root = new GameObject("XR Origin");
            root.SetActive(false);
            root.transform.SetParent(transform, false);
            offset = new GameObject("Camera Offset").transform;
            offset.SetParent(root.transform, false);
            // XROrigin owns the offset's height; recentering uses its own child transform.
            recenter = new GameObject("Recenter").transform;
            recenter.SetParent(offset, false);

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(recenter, false);
            Cam = camGo.GetComponent<Camera>();
            Cam.nearClipPlane = 0.03f;
            Cam.farClipPlane = 60f;
            var tpd = camGo.AddComponent<TrackedPoseDriver>();
            trackers.Add(tpd);
            tpd.positionInput = new InputActionProperty(new InputAction("HeadPos", binding: "<XRHMD>/centerEyePosition"));
            tpd.rotationInput = new InputActionProperty(new InputAction("HeadRot", binding: "<XRHMD>/centerEyeRotation"));
            tpd.trackingStateInput = new InputActionProperty(new InputAction("HeadState", binding: "<XRHMD>/trackingState"));
            tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            tpd.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;

            LeftController = Controller(recenter, "Left Controller", "{LeftHand}");
            RightController = Controller(recenter, "Right Controller", "{RightHand}");
            trackers.Add(LeftController.GetComponent<TrackedPoseDriver>());
            trackers.Add(RightController.GetComponent<TrackedPoseDriver>());

            Origin = root.AddComponent<XROrigin>();
            Origin.Origin = root;
            Origin.CameraFloorOffsetObject = offset.gameObject;
            Origin.Camera = Cam;
            Origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            Origin.CameraYOffset = DesktopEyeHeight;
            root.SetActive(true);
            gameObject.AddComponent<ComfortVignette>().Init(root.transform, Cam);
        }

        static Transform Controller(Transform parent, string name, string hand)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tpd = go.AddComponent<TrackedPoseDriver>();
            tpd.positionInput = new InputActionProperty(new InputAction(name + "Pos", binding: $"<XRController>{hand}/devicePosition"));
            tpd.rotationInput = new InputActionProperty(new InputAction(name + "Rot", binding: $"<XRController>{hand}/deviceRotation"));
            tpd.trackingStateInput = new InputActionProperty(new InputAction(name + "State", binding: $"<XRController>{hand}/trackingState"));
            return go.transform;
        }

        /// <summary>
        /// A headset is in use only if an XR display is running AND the runtime reports an active
        /// device. A PC VR runtime can be installed and selected with no headset connected; then
        /// the game must behave as a desktop game, not wait for tracking that never comes.
        /// </summary>
        public static bool HeadsetRunning()
        {
            if (!XRSettings.isDeviceActive) return false;
            SubsystemManager.GetSubsystems(displays);
            foreach (var d in displays) if (d.running) return true;
            return false;
        }

        // ── Views ───────────────────────────────────────────────────────────────────────────

        public void CycleView()
        {
            var next = (CameraView)(((int)View + 1) % System.Enum.GetValues(typeof(CameraView)).Length);
            SettingsStore.Current.camera.view = next;
            SettingsStore.Commit();
        }

        public void ApplyView(CameraView v, bool snap)
        {
            View = v;
            UpdateView(snap);
            if (v == CameraView.DriverStation || v == CameraView.Overhead || v == CameraView.Free) Recenter();
        }

        Alliance PlayerAlliance => SettingsStore.Current.match.alliance;

        /// <summary>Where the DRIVER stands: 12 in behind the wall, a quarter of the area's width either side of centre.</summary>
        public static Vector3 StationPosition(Alliance a, StationSlot slot)
        {
            float x = -FieldSpec.WallInner - FieldSpec.WallThickness - 14f;
            float y = slot == StationSlot.NearAudience ? -24f : 24f;
            var p = Units.Field(a == Alliance.Red ? x : -x, y, -FieldSpec.TileThickness);
            return p;
        }

        void LateUpdate()
        {
            XrActive = HeadsetRunning();
            // Without a headset the tracked-pose drivers would reset the camera to the tracking
            // origin (the floor) every frame, so they only run in VR.
            foreach (var t in trackers) if (t && t.enabled != XrActive) t.enabled = XrActive;
            if (InputHub.Instance) InputHub.Instance.FreeCameraActive = View == CameraView.Free;
            UpdateView(false);
            if (!XrActive) DesktopLook();
            else
            {
                float h = SettingsStore.Current.camera.eyeHeightOverrideIn;
                // Eye-height override raises or lowers the floor so the tracked head sits at the requested height.
                float target = h > 0f && View == CameraView.DriverStation ? Units.In(h) - Mathf.Max(0.5f, Head.localPosition.y) : 0f;
                eyeAdjust = Mathf.MoveTowards(eyeAdjust, target, Time.deltaTime * 0.5f);
                var lp = recenter.localPosition;
                recenter.localPosition = new Vector3(lp.x, eyeAdjust, lp.z);
            }
        }

        void UpdateView(bool snap)
        {
            var cam = SettingsStore.Current.camera;
            var player = MatchController.Instance ? MatchController.Instance.Player : null;
            Transform o = Origin.transform;
            float yaw = SimWorld.DriverYaw(PlayerAlliance);
            switch (View)
            {
                case CameraView.DriverStation:
                    o.SetPositionAndRotation(StationPosition(PlayerAlliance, cam.station), Quaternion.Euler(0f, yaw, 0f));
                    break;
                case CameraView.Overhead:
                {
                    // High above our own alliance's side of the field; look down to see the whole field.
                    float side = PlayerAlliance == Alliance.Red ? -1f : 1f;
                    var p = Units.Field(side * 40f, 0f, cam.overheadHeightIn) - Vector3.up * DesktopEyeHeight;
                    o.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
                    break;
                }
                case CameraView.Chase:
                    if (!player) break;
                    {
                        var rt = player.transform;
                        float followYaw = SettingsStore.Current.graphics.reducedMotion ? yaw : rt.eulerAngles.y;
                        var back = Quaternion.Euler(0f, followYaw, 0f) * Vector3.back * Units.In(cam.chaseDistanceIn);
                        var target = rt.position + back + Vector3.up * (Units.In(cam.chaseHeightIn) - DesktopEyeHeight);
                        o.position = snap ? target : Vector3.SmoothDamp(o.position, target, ref followVel, 0.35f);
                        float curYaw = o.eulerAngles.y;
                        o.rotation = Quaternion.Euler(0f, snap ? followYaw : Mathf.LerpAngle(curYaw, followYaw, Time.deltaTime * 2f), 0f);
                    }
                    break;
                case CameraView.RobotPov:
                    if (!player) break;
                    {
                        var rt = player.transform;
                        o.SetPositionAndRotation(rt.position + Vector3.up * (Units.In(player.Config.heightIn + 2f) - DesktopEyeHeight),
                            Quaternion.Euler(0f, rt.eulerAngles.y, 0f));
                    }
                    break;
                case CameraView.Free:
                    if (snap) o.SetPositionAndRotation(StationPosition(PlayerAlliance, cam.station), Quaternion.Euler(0f, yaw, 0f));
                    var hub = InputHub.Instance;
                    if (hub)
                    {
                        var look = hub.FreeCamLook;
                        if (look.sqrMagnitude > 0f)
                        {
                            // Turn the whole view (comfortable in VR); on a monitor also pitch.
                            o.rotation = Quaternion.Euler(0f, o.eulerAngles.y + look.x * 90f * Time.deltaTime, 0f);
                            if (!XrActive) desktopPitch = Mathf.Clamp(desktopPitch - look.y * 70f * Time.deltaTime, -80f, 85f);
                        }
                        var mv = hub.FreeCamMove;
                        Vector3 fwd = Head.forward; fwd.y = 0f; fwd.Normalize();
                        Vector3 right = Vector3.Cross(Vector3.up, fwd);
                        o.position += (fwd * mv.y + right * mv.x) * 1.5f * Time.deltaTime + Vector3.up * hub.FreeCamLift * 1.0f * Time.deltaTime;
                    }
                    break;
            }
        }

        /// <summary>Turn the tracking space so the player faces the field from wherever they physically stand.</summary>
        public void Recenter()
        {
            if (!XrActive) { desktopYaw = 0f; desktopPitch = View == CameraView.Overhead ? 50f : 12f; return; }
            var subsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            foreach (var s in subsystems) s.TryRecenter();
            // Rotate the recenter transform so the head's current yaw maps onto the view's forward,
            // and shift it so the head stands at the view's origin.
            Vector3 headFwd = Head.localRotation * Vector3.forward; headFwd.y = 0f;
            if (headFwd.sqrMagnitude > 1e-4f)
            {
                float headYaw = Mathf.Atan2(headFwd.x, headFwd.z) * Mathf.Rad2Deg;
                recenter.localRotation = Quaternion.Euler(0f, -headYaw, 0f);
                Vector3 headPos = Head.localPosition; headPos.y = 0f;
                Vector3 shift = -(recenter.localRotation * headPos);
                recenter.localPosition = new Vector3(shift.x, eyeAdjust, shift.z);
            }
        }

        void DesktopLook()
        {
            recenter.localPosition = Vector3.zero;
            recenter.localRotation = Quaternion.identity;
            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                var d = mouse.delta.ReadValue() * 0.15f;
                desktopYaw += d.x; desktopPitch = Mathf.Clamp(desktopPitch - d.y, -80f, 85f);
            }
            float h = SettingsStore.Current.camera.eyeHeightOverrideIn;
            Head.localPosition = new Vector3(0f, h > 0f && View == CameraView.DriverStation ? Units.In(h) : DesktopEyeHeight, 0f);
            Head.localRotation = Quaternion.Euler(desktopPitch, desktopYaw, 0f);
        }
    }
}
