using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VrFsim.UI;
using VrFsim.VR;

namespace VrFsim
{
    /// <summary>
    /// The first scene. Shows the VrFsim logo and a progress bar in front of the player (headset or
    /// monitor) while the Main scene loads in the background, then fades out and switches over.
    /// In a headset the panel stays level and lazily follows the player's gaze.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        public const string MainScene = "Main";

        // Brand colours (Docs/images/vrfsim-logo.png).
        public static readonly Color Background = new Color32(0x14, 0x16, 0x1A, 0xFF);
        public static readonly Color Accent = new Color32(0xFF, 0xB4, 0x00, 0xFF);
        static readonly Color Track = new Color32(0x2A, 0x2D, 0x33, 0xFF);
        static readonly Color Caption = new Color32(0xB9, 0xB5, 0xA8, 0xFF);

        const float MinShowSeconds = 1.6f;   // long enough to read the logo, short enough not to annoy
        const float FadeSeconds = 0.35f;
        const float DistanceM = 1.8f;
        const float FollowAngle = 30f;       // re-centre once the player looks this far away
        const float DesktopEyeHeight = 1.65f;

        static readonly string[] Tips =
        {
            "Press Start (Menu) to open the settings: robot, match, controls and graphics.",
            "D-pad right cycles camera views; D-pad left recentres your view.",
            "Press A to start the match. Back (View) restarts it.",
            "Build your robot in Settings > Robot and Mechanisms - just like dsim.",
            "A HIVE tips at 8 POLLEN - fewer when it also holds NECTAR.",
        };

        Camera cam;
        TrackedPoseDriver head;
        Transform panel;
        RectTransform fill;
        TextMeshProUGUI status;
        CanvasGroup group;
        Vector3 panelDir, targetDir;
        bool placed;
        float shown, target;

        void Awake()
        {
            BuildCamera();
            BuildPanel();
        }

        IEnumerator Start()
        {
            yield return null;   // let the first frame (the logo) reach the screen before loading starts
            float t0 = Time.realtimeSinceStartup;
            var op = SceneManager.LoadSceneAsync(MainScene);
            if (op == null) yield break;
            op.allowSceneActivation = false;

            // Unity reports 0..0.9 while loading and stops at 0.9 until activation is allowed.
            while (op.progress < 0.9f || Time.realtimeSinceStartup - t0 < MinShowSeconds)
            {
                target = Mathf.Min(op.progress / 0.9f, (Time.realtimeSinceStartup - t0) / MinShowSeconds) * 0.95f;
                yield return null;
            }
            target = 1f;
            status.text = "Starting the match";
            while (shown < 0.999f) yield return null;

            for (float t = 0f; t < FadeSeconds; t += Time.unscaledDeltaTime)
            {
                group.alpha = 1f - t / FadeSeconds;
                yield return null;
            }
            group.alpha = 0f;
            yield return null;
            op.allowSceneActivation = true;
        }

        void Update()
        {
            // XR can finish starting a frame or two after the first scene; until then drive the
            // camera as a desktop one (the pose driver would pin it to the floor).
            if (!head.enabled && ViewManager.HeadsetRunning()) head.enabled = true;

            shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * 1.2f);
            fill.anchorMax = new Vector2(shown, 1f);
            if (target < 1f) status.text = $"Loading the field   {Mathf.RoundToInt(shown * 100f)}%";
        }

        void LateUpdate()
        {
            var h = cam.transform;
            Vector3 fwd = Vector3.ProjectOnPlane(h.forward, Vector3.up);
            fwd = fwd.sqrMagnitude < 1e-4f ? Vector3.forward : fwd.normalized;
            if (!placed) { panelDir = targetDir = fwd; placed = true; }
            else if (Vector3.Angle(fwd, targetDir) > FollowAngle) targetDir = fwd;
            panelDir = Vector3.Slerp(panelDir, targetDir, 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime));
            panel.SetPositionAndRotation(h.position + panelDir * DistanceM, Quaternion.LookRotation(panelDir, Vector3.up));
        }

        void BuildCamera()
        {
            var go = new GameObject("Loading Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, DesktopEyeHeight, 0f);
            cam = go.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Background;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 20f;
            cam.cullingMask = 1 << 5;   // UI layer only

            head = go.AddComponent<TrackedPoseDriver>();
            head.positionInput = new InputActionProperty(new InputAction("HeadPos", binding: "<XRHMD>/centerEyePosition"));
            head.rotationInput = new InputActionProperty(new InputAction("HeadRot", binding: "<XRHMD>/centerEyeRotation"));
            head.trackingStateInput = new InputActionProperty(new InputAction("HeadState", binding: "<XRHMD>/trackingState"));
            head.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            head.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            head.enabled = ViewManager.HeadsetRunning();
        }

        void BuildPanel()
        {
            var canvas = UiKit.WorldCanvas("Loading", transform, new Vector2(1600, 900), 1.6f, false);
            canvas.worldCamera = cam;
            canvas.gameObject.layer = 5;
            panel = canvas.transform;
            group = canvas.gameObject.AddComponent<CanvasGroup>();

            var logo = Resources.Load<Sprite>("Branding/VrFsimLogo");
            var img = UiKit.Rect(panel, "Logo").gameObject.AddComponent<Image>();
            img.sprite = logo;
            img.preserveAspect = true;
            img.raycastTarget = false;
            var lrt = img.rectTransform;
            lrt.sizeDelta = new Vector2(1150, 430);
            lrt.anchoredPosition = new Vector2(0, 150);

            var track = UiKit.Box(panel, "Progress", Track);
            track.raycastTarget = false;
            var trt = track.rectTransform;
            trt.sizeDelta = new Vector2(900, 12);
            trt.anchoredPosition = new Vector2(0, -150);
            var bar = UiKit.Box(trt, "Fill", Accent);
            bar.raycastTarget = false;
            fill = bar.rectTransform;
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;

            status = UiKit.Text(panel, "Status", "Loading the field", 40, Caption, TextAlignmentOptions.Center);
            status.rectTransform.sizeDelta = new Vector2(1000, 56);
            status.rectTransform.anchoredPosition = new Vector2(0, -205);

            var tip = UiKit.Text(panel, "Tip", "Tip: " + Tips[Random.Range(0, Tips.Length)], 34, Caption * new Color(1, 1, 1, 0.7f), TextAlignmentOptions.Center);
            tip.rectTransform.sizeDelta = new Vector2(1400, 100);
            tip.rectTransform.anchoredPosition = new Vector2(0, -320);

            foreach (var t in panel.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 5;
        }
    }
}
