using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR.OpenXR.Features;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
#endif

namespace VrFsim.VR
{
    /// <summary>
    /// Lets the standalone Quest build pick its refresh rate (72 / 90 / 120 Hz) through the OpenXR
    /// extension XR_FB_display_refresh_rate. Without it a Quest app runs at the system default.
    /// Calls the extension function directly, so no extra package is needed. If the runtime does not
    /// offer the extension, requests are simply ignored.
    /// </summary>
#if UNITY_EDITOR
    [OpenXRFeature(UiName = "VrFsim Display Refresh Rate",
        BuildTargetGroups = new[] { BuildTargetGroup.Android },
        Company = "VrFsim",
        Desc = "Requests the headset refresh rate chosen in the VrFsim settings (XR_FB_display_refresh_rate).",
        OpenxrExtensionStrings = Extension,
        Version = "1.0.0",
        FeatureId = Id)]
#endif
    public class RefreshRateFeature : OpenXRFeature
    {
        public const string Id = "com.vrfsim.openxr.refreshrate";
        const string Extension = "XR_FB_display_refresh_rate";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int GetInstanceProcAddr(ulong instance, [MarshalAs(UnmanagedType.LPStr)] string name, out IntPtr function);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int RequestDisplayRefreshRate(ulong session, float displayRefreshRate);

        static ulong instance, session;
        static RequestDisplayRefreshRate request;
        static float pending;

        /// <summary>Ask for a refresh rate. Applied now if a session runs, else when one starts.</summary>
        public static void Request(float hz)
        {
            pending = hz;
            Apply();
        }

        protected override bool OnInstanceCreate(ulong xrInstance)
        {
            instance = xrInstance;
            return true;
        }

        protected override void OnSessionCreate(ulong xrSession)
        {
            session = xrSession;
            request = null;
            try
            {
                if (instance == 0 || xrGetInstanceProcAddr == IntPtr.Zero) return;
                var getProc = Marshal.GetDelegateForFunctionPointer<GetInstanceProcAddr>(xrGetInstanceProcAddr);
                if (getProc(instance, "xrRequestDisplayRefreshRateFB", out var fn) == 0 && fn != IntPtr.Zero)
                    request = Marshal.GetDelegateForFunctionPointer<RequestDisplayRefreshRate>(fn);
            }
            catch (Exception e) { Debug.LogWarning("[VrFsim] Refresh rate extension unavailable: " + e.Message); }
        }

        protected override void OnSessionBegin(ulong xrSession) => Apply();

        protected override void OnSessionDestroy(ulong xrSession)
        {
            session = 0;
            request = null;
        }

        protected override void OnInstanceDestroy(ulong xrInstance) => instance = 0;

        static void Apply()
        {
            if (request == null || session == 0 || pending <= 0f) return;
            int result = request(session, pending);
            if (result != 0) Debug.Log($"[VrFsim] Headset refused {pending:0} Hz (XrResult {result}).");
        }
    }
}
