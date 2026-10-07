using System;
using System.Linq;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine;

namespace VrFsim.EditorTools
{
    /// <summary>
    /// Headless test runs (-batchmode -runTests) have no headset and no window to render to. If a
    /// PC VR runtime is installed, OpenXR would still try to start and log a failure every frame
    /// (gigabytes of log), so XR start-up is switched off in memory for those runs only, and
    /// switched back on — and saved — when the editor quits, so it never sticks.
    /// </summary>
    [InitializeOnLoad]
    static class BatchXrGuard
    {
        static BatchXrGuard()
        {
            if (!Application.isBatchMode) return;
            if (!Environment.GetCommandLineArgs().Any(a => a.Equals("-runTests", StringComparison.OrdinalIgnoreCase))) return;
            var s = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (s == null || !s.InitManagerOnStart) return;
            s.InitManagerOnStart = false;
            EditorApplication.quitting += () =>
            {
                s.InitManagerOnStart = true;
                EditorUtility.SetDirty(s);
                AssetDatabase.SaveAssets();
            };
        }
    }
}
