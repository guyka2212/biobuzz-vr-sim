using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using VrFsim.Game;
using VrFsim.Input;
using VrFsim.Match;
using VrFsim.Robot;
using VrFsim.Settings;

namespace VrFsim
{
    /// <summary>
    /// Performance check: run the player with <c>-benchmark</c>. Starts free drive with three
    /// practice robots, drives the player robot in a busy pattern (intake, launching), records
    /// frame times for 30 s after a warm-up, writes benchmark.txt next to the executable, quits.
    /// </summary>
    public class Benchmark : MonoBehaviour, ICommandSource
    {
        public static bool Requested => Environment.GetCommandLineArgs().Any(a => a.Equals("-benchmark", StringComparison.OrdinalIgnoreCase));

        const float WarmUp = 4f, Duration = 30f;
        readonly List<float> frames = new List<float>(4096);
        float start;

        public static SimSettings Settings()
        {
            var s = new SimSettings();
            s.match.mode = GameMode.FreeDrive;
            s.match.partner = s.match.opponent1 = s.match.opponent2 = PracticeSeat.Dummy;
            s.assists.autoIntake = true;
            s.graphics.perfOverlay = PerfOverlay.Full;
            s.camera.view = CameraView.DriverStation;
            return s;
        }

        void Start()
        {
            start = Time.realtimeSinceStartup;
            if (!UnityEngine.XR.XRSettings.isDeviceActive) Screen.SetResolution(2560, 1440, FullScreenMode.Windowed);
        }

        public DriverCommand Read(RobotController r)
        {
            float t = Time.time;
            return new DriverCommand
            {
                translate = new Vector2(Mathf.Sin(t * 0.7f) * 0.8f, Mathf.Cos(t * 0.5f) * 0.8f),
                turn = Mathf.Sin(t * 0.9f) * 0.5f,
                intake = true,
                fire = (t % 3f) < 0.6f,
            };
        }

        void Update()
        {
            // Uncapped, so the numbers show headroom rather than the frame limiter.
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
            var m = MatchController.Instance;
            if (m && m.Player && m.Player.Source != (ICommandSource)this) m.Player.Source = this;

            float elapsed = Time.realtimeSinceStartup - start;
            if (elapsed < WarmUp) return;
            frames.Add(Time.unscaledDeltaTime * 1000f);
            if (elapsed < WarmUp + Duration) return;

            frames.Sort();
            float avg = frames.Average();
            float p50 = frames[frames.Count / 2], p95 = frames[(int)(frames.Count * 0.95f)], p99 = frames[(int)(frames.Count * 0.99f)];
            int tris = 0;
            foreach (var mf in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
                if (mf.sharedMesh && mf.TryGetComponent<MeshRenderer>(out var mr) && mr.enabled) tris += (int)mf.sharedMesh.GetIndexCount(0) / 3;
            string report =
                $"VrFsim benchmark {DateTime.Now:u}\n" +
                $"Device: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), {SystemInfo.processorType}\n" +
                $"Mode: {(UnityEngine.XR.XRSettings.isDeviceActive ? "VR " + UnityEngine.XR.XRSettings.loadedDeviceName : $"desktop {Screen.width}x{Screen.height}")}\n" +
                $"Frames: {frames.Count} over {Duration}s\n" +
                $"Average: {avg:0.00} ms ({1000f / avg:0} fps)\n" +
                $"Median: {p50:0.00} ms   95th: {p95:0.00} ms   99th: {p99:0.00} ms\n" +
                $"Frames slower than 11.1 ms (90 fps budget): {frames.Count(f => f > 11.1f) * 100f / frames.Count:0.0}%\n" +
                $"Robots: {SimWorld.Robots.Count}, elements: {GameElement.All.Count}, rendered triangles (approx): {tris}\n";
            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "benchmark.txt");
            File.WriteAllText(path, report);
            Debug.Log(report);
            Application.Quit();
            enabled = false;
        }
    }
}
