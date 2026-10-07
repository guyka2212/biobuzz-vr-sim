using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using VrFsim.Game;
using VrFsim.Robot;

namespace VrFsim.EditorTools
{
    /// <summary>
    /// Generates the Main scene from code: shared material assets, the static field (so it can be
    /// lightmapped and static-batched), light probes, lighting settings, and the bootstrap object.
    /// Re-run after changing FieldSpec or swapping in new Blender art.
    /// </summary>
    public static class SceneBuilder
    {
        const string Root = "Assets/VrFsim";
        const string MaterialsDir = Root + "/Materials";
        const string ResourcesDir = Root + "/Resources";
        const string LightingPath = Root + "/Scenes/MainLighting.lighting";

        [MenuItem("VrFsim/Setup/Build Scene")]
        public static void BuildScene()
        {
            var lib = EnsureMaterialLibrary();
            EnsureArtAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.color = new Color(1f, 0.97f, 0.92f);
            sun.shadows = LightShadows.Soft;
            sun.lightmapBakeType = LightmapBakeType.Mixed;
            sunGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            var field = FieldBuilder.Build(null, lib, FieldArt.Load());
            field.root.gameObject.AddComponent<FieldRoot>().Capture(field);
            MarkStatic(field.staticRoot.gameObject);
            // Clear polycarbonate panels do not cast shadows.
            foreach (var r in field.staticRoot.GetComponentsInChildren<MeshRenderer>())
                if (r.sharedMaterial == lib.wall) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            BuildLightProbes();
            new GameObject("VrFsim").AddComponent<GameBootstrap>();

            ConfigureLighting();
            Directory.CreateDirectory(Path.GetDirectoryName(ProjectSetup.MainScenePath));
            EditorSceneManager.SaveScene(scene, ProjectSetup.MainScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ProjectSetup.MainScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[VrFsim] Main scene built.");
        }

        [MenuItem("VrFsim/Setup/Bake Lighting")]
        public static void BakeLighting()
        {
            EditorSceneManager.OpenScene(ProjectSetup.MainScenePath, OpenSceneMode.Single);
            ConfigureLighting();
            if (!Lightmapping.Bake()) Debug.LogWarning("[VrFsim] Lightmap bake did not complete.");
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[VrFsim] Lighting baked.");
        }

        /// <summary>Batch entry: build the scene and bake its lighting (needs a GPU, so no -nographics).</summary>
        public static void BuildAndBake()
        {
            BuildScene();
            BakeLighting();
        }

        static void MarkStatic(GameObject go)
        {
            var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI |
                        StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
        }

        static void BuildLightProbes()
        {
            var go = new GameObject("LightProbes");
            var group = go.AddComponent<LightProbeGroup>();
            var pts = new System.Collections.Generic.List<Vector3>();
            for (int ix = -3; ix <= 3; ix++)
                for (int iy = -3; iy <= 3; iy++)
                    foreach (float h in new[] { 4f, 24f, 60f })
                        pts.Add(Units.Field(ix * 23.5f, iy * 23.5f, h));
            group.probePositions = pts.ToArray();
        }

        static void ConfigureLighting()
        {
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingPath);
            if (!settings)
            {
                settings = new LightingSettings { name = "MainLighting" };
                AssetDatabase.CreateAsset(settings, LightingPath);
            }
            settings.bakedGI = true;
            settings.realtimeGI = false;
            settings.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            // Subtractive: the static field gets baked direct light and shadows; only moving robots and
            // elements are lit and shadowed in realtime. Cheapest mode, and no shadow acne on the field.
            settings.mixedBakeMode = MixedLightingMode.Subtractive;
            settings.lightmapResolution = 6f;
            settings.lightmapPadding = 2;
            settings.lightmapMaxSize = 1024;
            settings.lightmapCompression = LightmapCompression.NormalQuality;
            settings.directionalityMode = LightmapsMode.NonDirectional;
            settings.ao = true;
            settings.aoMaxDistance = 0.4f;
            settings.indirectSampleCount = 128;
            settings.directSampleCount = 32;
            settings.environmentSampleCount = 64;
            EditorUtility.SetDirty(settings);
            Lightmapping.lightingSettings = settings;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.33f, 0.33f, 0.36f);
            RenderSettings.subtractiveShadowColor = new Color(0.42f, 0.45f, 0.52f);
            RenderSettings.skybox = null;
        }

        // ── Assets ──────────────────────────────────────────────────────────────────────────

        static MaterialLibrary EnsureMaterialLibrary()
        {
            Directory.CreateDirectory(MaterialsDir);
            Directory.CreateDirectory(ResourcesDir);
            string libPath = $"{ResourcesDir}/{MaterialLibrary.ResourceName}.asset";
            var lib = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(libPath);
            if (!lib)
            {
                lib = ScriptableObject.CreateInstance<MaterialLibrary>();
                AssetDatabase.CreateAsset(lib, libPath);
            }
            lib.FillDefaults();
            // Persist any material created in memory as its own asset, so builds include the shaders.
            var so = new SerializedObject(lib);
            var it = so.GetIterator();
            while (it.NextVisible(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference || !it.objectReferenceValue) continue;
                var obj = it.objectReferenceValue;
                if (AssetDatabase.Contains(obj)) continue;
                string ext = obj is Material ? "mat" : "physicMaterial";
                string path = $"{MaterialsDir}/{obj.name}.{ext}";
                var existing = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (existing) it.objectReferenceValue = existing;
                else AssetDatabase.CreateAsset(obj, path);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            return lib;
        }

        static void EnsureArtAssets()
        {
            string fieldArt = $"{ResourcesDir}/{FieldArt.ResourceName}.asset";
            if (!AssetDatabase.LoadAssetAtPath<FieldArt>(fieldArt))
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<FieldArt>(), fieldArt);
            string robotArt = $"{ResourcesDir}/{RobotArt.ResourceName}.asset";
            if (!AssetDatabase.LoadAssetAtPath<RobotArt>(robotArt))
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<RobotArt>(), robotArt);
        }
    }
}
