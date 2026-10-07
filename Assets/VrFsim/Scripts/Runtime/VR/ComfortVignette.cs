using UnityEngine;
using VrFsim.Settings;

namespace VrFsim.VR
{
    /// <summary>
    /// Comfort vignette: when the view moves the player (chase, robot POV, free fly) the edges of the
    /// view darken in proportion to how fast the virtual camera moves or turns. Reduces motion
    /// sickness. Off in the driver station and overhead views, which never move on their own.
    /// </summary>
    public class ComfortVignette : MonoBehaviour
    {
        Transform origin;
        MeshRenderer quad;
        Material mat;
        Vector3 lastPos;
        float lastYaw, strength;

        public void Init(Transform xrOrigin, Camera cam)
        {
            origin = xrOrigin;
            var go = new GameObject("ComfortVignette", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, cam.nearClipPlane + 0.02f);
            go.transform.localScale = Vector3.one * 0.16f;
            go.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            quad = go.GetComponent<MeshRenderer>();
            quad.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mat = new Material(MaterialLibrary.Get().vignette);
            var tex = RadialTexture(128);
            mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
            quad.sharedMaterial = mat;
            lastPos = origin.position;
            lastYaw = origin.eulerAngles.y;
        }

        static Texture2D RadialTexture(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a = (byte)(Mathf.Clamp01(Mathf.InverseLerp(0.35f, 0.95f, r)) * 255f);
                    px[y * n + x] = new Color32(0, 0, 0, a);
                }
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        void LateUpdate()
        {
            if (!quad || !origin) return;
            var s = SettingsStore.Current;
            var view = ViewManager.Instance ? ViewManager.Instance.View : CameraView.DriverStation;
            bool moving = view == CameraView.Chase || view == CameraView.RobotPov || view == CameraView.Free;
            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
            float speed = (origin.position - lastPos).magnitude / dt;
            float turn = Mathf.Abs(Mathf.DeltaAngle(lastYaw, origin.eulerAngles.y)) / dt;
            lastPos = origin.position;
            lastYaw = origin.eulerAngles.y;
            float target = s.camera.comfortVignette && moving ? Mathf.Clamp01(speed / 1.5f + turn / 90f) * 0.85f : 0f;
            strength = Mathf.MoveTowards(strength, target, dt * 3f);
            quad.enabled = strength > 0.01f;
            var c = Color.black; c.a = strength;
            mat.SetColor("_BaseColor", c);
        }
    }
}
