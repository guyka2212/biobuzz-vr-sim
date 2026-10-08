using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VrFsim.EditorTools
{
    /// <summary>
    /// Windows 64-bit player build. Menu: VrFsim ▸ Build ▸ Windows, or batch:
    /// <c>Unity.exe -batchmode -quit -projectPath . -executeMethod VrFsim.EditorTools.BuildTool.BuildWindows</c>.
    /// Output: Builds/VrFsim/VrFsim.exe.
    /// </summary>
    public static class BuildTool
    {
        public const string OutputDir = "Builds/VrFsim";

        [MenuItem("VrFsim/Build/Windows")]
        public static void BuildWindows()
        {
            ProjectSetup.ConfigureProject();
            var opts = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = Path.Combine(OutputDir, "VrFsim.exe"),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.CompressWithLz4HC,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var sum = report.summary;
            Debug.Log($"[VrFsim] Build {sum.result}: {sum.totalSize / (1024f * 1024f):0.0} MB, {sum.totalErrors} errors, {sum.totalTime.TotalSeconds:0}s -> {sum.outputPath}");
            if (sum.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
        }

        public const string QuestApk = "Builds/VrFsim-Quest.apk";

        /// <summary>
        /// Standalone Meta Quest build (runs on the headset, no PC). Needs Unity's Android Build
        /// Support module. Output: Builds/VrFsim-Quest.apk; install with Tools/quest-install.sh.
        /// The editor is switched back to Windows afterwards.
        /// </summary>
        [MenuItem("VrFsim/Build/Meta Quest (standalone)")]
        public static void BuildQuest()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.LogError("[VrFsim] Android Build Support is not installed (Unity Hub: Installs, Add modules).");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            ProjectSetup.ConfigureProject();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            var opts = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = QuestApk,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.CompressWithLz4HC,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var sum = report.summary;
            Debug.Log($"[VrFsim] Quest build {sum.result}: {sum.totalSize / (1024f * 1024f):0.0} MB, {sum.totalErrors} errors, {sum.totalTime.TotalSeconds:0}s -> {sum.outputPath}");
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            if (sum.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
