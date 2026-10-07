using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VrFsim.Game;
using VrFsim.Match;
using VrFsim.Settings;

namespace VrFsim.Tests
{
    /// <summary>
    /// Renders the running scene from fixed viewpoints to Logs/Captures/*.png for visual review.
    /// Needs a GPU: run with Tools/unity-capture.sh (not -nographics). Excluded from normal runs.
    /// </summary>
    [Category("Visual")]
    public class VisualCaptureTests
    {
        [UnityTest, Explicit("Renders screenshots; run with Tools/unity-capture.sh")]
        public IEnumerator CaptureViews()
        {
            var s = new SimSettings();
            s.match.partner = PracticeSeat.Dummy;
            s.match.opponent1 = PracticeSeat.Dummy;
            s.match.opponent2 = PracticeSeat.Dummy;
            SettingsStore.UseTransient(s);
            SimWorld.Field = null;
            SceneManager.LoadScene("Main");
            for (int i = 0; i < 90; i++) yield return null;

            string dir = Path.Combine(Application.dataPath, "../Logs/Captures");
            Directory.CreateDirectory(dir);
            var cam = new GameObject("CaptureCam").AddComponent<Camera>();
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.03f;
            var main = Camera.main;
            cam.clearFlags = main ? main.clearFlags : CameraClearFlags.SolidColor;
            cam.backgroundColor = main ? main.backgroundColor : Color.black;

            var station = VR.ViewManager.StationPosition(Alliance.Red, StationSlot.NearAudience) + Vector3.up * 1.65f;
            Shot(cam, dir, "01_driver_station", station, Quaternion.Euler(18f, 90f, 0f));
            Shot(cam, dir, "02_overhead", Units.Field(0, -150f, 200f), Quaternion.Euler(50f, 0f, 0f));
            Shot(cam, dir, "03_hive", Units.Field(-12f, -70f, 40f), Quaternion.Euler(12f, 0f, 0f));
            Shot(cam, dir, "04_flower", Units.Field(-45f, -23.4f, 22f), Quaternion.Euler(20f, -90f, 0f));
            var p = MatchController.Instance.Player.transform;
            Shot(cam, dir, "05_robot", p.position + new Vector3(0.6f, 0.45f, -0.5f), Quaternion.LookRotation(p.position - (p.position + new Vector3(0.6f, 0.45f, -0.5f)) + Vector3.up * 0.1f));
            Assert.Pass("Captured to " + dir);
        }

        [UnityTest, Explicit("Renders screenshots; run with Tools/unity-capture.sh")]
        public IEnumerator CaptureResults()
        {
            var s = new SimSettings();
            s.match.auto = AutoRoutine.LaunchPreloadsAndPark;
            SettingsStore.UseTransient(s);
            SimWorld.Field = null;
            SceneManager.LoadScene("Main");
            for (int i = 0; i < 30; i++) yield return null;
            var m = MatchController.Instance;
            m.StartMatch();
            Time.timeScale = 8f;
            while (m.Phase != MatchPhase.Ended) yield return null;
            Time.timeScale = 1f;
            for (int i = 0; i < 10; i++) yield return null;
            string dir = Path.Combine(Application.dataPath, "../Logs/Captures");
            Directory.CreateDirectory(dir);
            var cam = new GameObject("CaptureCam").AddComponent<Camera>();
            cam.fieldOfView = 70f;
            cam.backgroundColor = Color.black;
            cam.clearFlags = CameraClearFlags.SolidColor;
            var head = VR.ViewManager.Instance.Head;
            Debug.Log($"[capture] head {head.position} fwd {head.forward}");
            Shot(cam, dir, "06_results", head.position, head.rotation);
            var panel = GameObject.Find("MatchResults");
            Debug.Log($"[capture] panel {panel.transform.position}");
            Shot(cam, dir, "06b_results_wide", head.position - head.forward * 1.2f + Vector3.up * 0.4f, Quaternion.LookRotation(panel.transform.position - (head.position - head.forward * 1.2f + Vector3.up * 0.4f)));
            panel.SetActive(false);
            Shot(cam, dir, "06c_results_hidden", head.position, head.rotation);
        }

        static void Shot(Camera cam, string dir, string name, Vector3 pos, Quaternion rot)
        {
            cam.transform.SetPositionAndRotation(pos, rot);
            var rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.Destroy(rt);
            Object.Destroy(tex);
        }
    }
}
