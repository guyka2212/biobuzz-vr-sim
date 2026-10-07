using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;
using VrFsim.Settings;

namespace VrFsim
{
    /// <summary>Applies <see cref="VisualSettings"/> to URP, XR and the scene lighting whenever settings change.</summary>
    public class GraphicsApplier : MonoBehaviour
    {
        Light sun;

        void Start()
        {
#if UNITY_EDITOR
            // Work on a copy in the editor so play sessions never modify the project's URP asset.
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset src)
                QualitySettings.renderPipeline = Instantiate(src);
#endif
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
            SettingsStore.Changed += Apply;
            Apply(SettingsStore.Current);
        }

        void OnDestroy() => SettingsStore.Changed -= Apply;

        void Apply(SimSettings s)
        {
            var g = s.graphics;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.msaaSampleCount = (int)g.msaa;
                urp.shadowDistance = g.shadows == ShadowLevel.Off ? 0f : g.shadows == ShadowLevel.Low ? 8f : 16f;
                urp.renderScale = XRSettings.enabled ? 1f : Mathf.Clamp(g.renderScale, 0.5f, 1.5f);
            }
            if (XRSettings.enabled) XRSettings.eyeTextureResolutionScale = g.renderScale;
            Application.targetFrameRate = XRSettings.isDeviceActive ? -1 : Mathf.RoundToInt(g.targetRefreshRate);

            if (sun)
            {
                sun.shadows = g.shadows == ShadowLevel.Off ? LightShadows.None : g.shadows == ShadowLevel.Low ? LightShadows.Hard : LightShadows.Soft;
                switch (g.venue)
                {
                    case Venue.Arena: sun.intensity = 1.1f; sun.color = new Color(1f, 0.97f, 0.92f); break;
                    case Venue.Gym: sun.intensity = 1.25f; sun.color = new Color(0.95f, 0.98f, 1f); break;
                    case Venue.Night: sun.intensity = 0.55f; sun.color = new Color(0.8f, 0.85f, 1f); break;
                }
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = g.venue == Venue.Night ? new Color(0.12f, 0.13f, 0.18f)
                : g.venue == Venue.Gym ? new Color(0.42f, 0.43f, 0.45f) : new Color(0.33f, 0.33f, 0.36f);
            var cam = Camera.main;
            if (cam)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = g.venue == Venue.Night ? new Color(0.02f, 0.02f, 0.04f)
                    : g.venue == Venue.Gym ? new Color(0.55f, 0.58f, 0.62f) : new Color(0.08f, 0.09f, 0.12f);
            }
        }
    }
}
