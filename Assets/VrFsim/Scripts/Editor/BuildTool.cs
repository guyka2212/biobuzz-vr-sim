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
    }
}
