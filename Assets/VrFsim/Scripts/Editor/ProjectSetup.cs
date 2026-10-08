using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

namespace VrFsim.EditorTools
{
    /// <summary>
    /// One-shot, idempotent project configuration for PC VR. Safe to re-run from the menu or from
    /// batch mode (<c>-executeMethod VrFsim.EditorTools.ProjectSetup.ConfigureProject</c>).
    /// </summary>
    public static class ProjectSetup
    {
        public const string MainScenePath = "Assets/VrFsim/Scenes/Main.unity";
        public const string LoadingScenePath = "Assets/VrFsim/Scenes/Loading.unity";
        /// <summary>Game version: the build, the installer and the GitHub release tag (v + this).</summary>
        public const string Version = "0.2.0";
        /// <summary>Android package name of the standalone Meta Quest build.</summary>
        public const string AndroidPackage = "com.vrfsim.biobuzz";
        const string QuestSplashPath = "Assets/VrFsim/Art/Branding/VrFsimQuestSplash.png";

        // The controllers a Meta Quest headset has (Quest 2: Touch; Quest 3 / 3S: Touch Plus; Pro).
        static readonly HashSet<string> QuestProfiles = new HashSet<string>
        {
            "OculusTouchControllerProfile",
            "MetaQuestTouchPlusControllerProfile",
            "MetaQuestTouchProControllerProfile",
        };

        // Controller profiles for the PC headsets OpenXR runtimes commonly expose. Matched by type
        // name so a profile missing from a future OpenXR package version is skipped, not a compile error.
        static readonly HashSet<string> InteractionProfiles = new HashSet<string>
        {
            "OculusTouchControllerProfile",
            "MetaQuestTouchPlusControllerProfile",
            "MetaQuestTouchProControllerProfile",
            "ValveIndexControllerProfile",
            "HTCViveControllerProfile",
            "MicrosoftMotionControllerProfile",
            "HPReverbG2ControllerProfile",
            "KHRSimpleControllerProfile",
        };

        // Config objects left behind by template packages this project no longer uses.
        static readonly string[] StaleConfigObjects =
        {
            "Unity.XR.Oculus.Settings",
            "Unity.XR.WindowsMR.Settings",
            "UnityEditor.XR.ARCore.ARCoreSettings",
            "UnityEditor.XR.ARKit.ARKitSettings",
            "com.unity.dt.app-ui",
            "com.unity.xr.arfoundation.simulation_settings",
            "com.unity.input.settings.actions",
        };

        [MenuItem("VrFsim/Setup/Configure Project")]
        public static void ConfigureProject()
        {
            ConfigurePlayer();
            ConfigureXR();
            if (BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android)) ConfigureQuest();
            ConfigureRendering();
            CleanConfigObjects();
            EnsureMainScene();
            SetBuildScenes();
            EditorSceneManager.playModeStartScene = null;   // Play uses the open scene; tests use their own
            AssetDatabase.SaveAssets();
            Debug.Log("[VrFsim] Project configured for PC VR (OpenXR, Standalone).");
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "VrFsim";
            PlayerSettings.productName = "VrFsim";
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            // Mono: IL2CPP needs the separate "Windows Build Support (IL2CPP)" editor module.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Medium);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SplashScreen.show = false;
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtImport.IconPath);
            if (icon) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;      // the spectator window (VR mirror) can be resized
            PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;

            // Input System only (0 = old, 1 = new, 2 = both). No public API, so set the serialized field.
            var playerSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (playerSettings.Length > 0)
            {
                var so = new SerializedObject(playerSettings[0]);
                var handler = so.FindProperty("activeInputHandler");
                if (handler != null && handler.intValue != 1)
                {
                    handler.intValue = 1;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        static void ConfigureXR()
        {
            var perTarget = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (perTarget == null)
            {
                Debug.LogWarning("[VrFsim] XR General Settings for Standalone not found; open Project Settings > XR Plug-in Management once.");
                return;
            }

            perTarget.InitManagerOnStart = true;
            var manager = perTarget.AssignedSettings;
            if (manager != null && !manager.activeLoaders.Any(l => l != null && l.GetType().Name == "OpenXRLoader"))
                XRPackageMetadataStore.AssignLoader(manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Standalone);

            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Standalone);
            var openXR = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
            if (openXR == null)
            {
                Debug.LogWarning("[VrFsim] OpenXR settings for Standalone are unavailable.");
                return;
            }

            openXR.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            openXR.depthSubmissionMode = OpenXRSettings.DepthSubmissionMode.Depth16Bit;

            foreach (var feature in openXR.GetFeatures<OpenXRInteractionFeature>())
            {
                bool want = InteractionProfiles.Contains(feature.GetType().Name);
                if (feature.enabled != want) feature.enabled = want;
                EditorUtility.SetDirty(feature);
            }
            EditorUtility.SetDirty(openXR);
        }

        /// <summary>
        /// Standalone Meta Quest (Android) build: ARM64 + IL2CPP (Quest requires it), Vulkan, ASTC
        /// textures, OpenXR with Meta Quest support, the Touch controller profiles, fixed foveated
        /// rendering, and the logo as the system splash while the app starts. Needs Unity's
        /// "Android Build Support" module. Does not change the PC build.
        /// </summary>
        public static void ConfigureQuest()
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, AndroidPackage);
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Medium);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.bundleVersionCode = VersionCode();
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
            PlayerSettings.Android.forceInternetPermission = true;   // loopback socket (RemoteGamepad)
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget))
            {
                Debug.LogWarning("[VrFsim] XR General Settings not found; open Project Settings > XR Plug-in Management once.");
                return;
            }
            if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
                perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            var general = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            general.InitManagerOnStart = true;
            var manager = general.Manager;
            if (!manager.activeLoaders.Any(l => l != null && l.GetType().Name == "OpenXRLoader"))
                XRPackageMetadataStore.AssignLoader(manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android);
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(perTarget);

            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            var openXR = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (openXR == null) { Debug.LogWarning("[VrFsim] OpenXR settings for Android are unavailable."); return; }
            openXR.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;   // multiview on Quest
            openXR.depthSubmissionMode = OpenXRSettings.DepthSubmissionMode.Depth16Bit;
            foreach (var feature in openXR.GetFeatures<OpenXRInteractionFeature>())
            {
                feature.enabled = QuestProfiles.Contains(feature.GetType().Name);
                EditorUtility.SetDirty(feature);
            }
            var quest = openXR.GetFeature<MetaQuestFeature>();
            if (quest != null)
            {
                quest.enabled = true;
                // The game never goes online, but Android needs the INTERNET permission for any
                // socket, including the loopback port VrFsim Controller Connect's controller arrives on.
                quest.ForceRemoveInternetPermission = false;
                var splash = AssetDatabase.LoadAssetAtPath<Texture2D>(QuestSplashPath);
                if (splash) quest.systemSplashScreen = splash;
                EditorUtility.SetDirty(quest);
            }
            var foveation = openXR.GetFeature<FoveatedRenderingFeature>();
            if (foveation != null) { foveation.enabled = true; EditorUtility.SetDirty(foveation); }
            var refresh = openXR.GetFeature<VrFsim.VR.RefreshRateFeature>();
            if (refresh != null) { refresh.enabled = true; EditorUtility.SetDirty(refresh); }
            else Debug.LogWarning("[VrFsim] Refresh rate feature not registered yet; run Configure Project again.");
            EditorUtility.SetDirty(openXR);
        }

        /// <summary>Android version code from <see cref="Version"/>: 1.2.3 becomes 10203.</summary>
        static int VersionCode()
        {
            var p = Version.Split('.').Select(int.Parse).ToArray();
            return p[0] * 10000 + (p.Length > 1 ? p[1] : 0) * 100 + (p.Length > 2 ? p[2] : 0);
        }

        const string VrPipelinePath = "Assets/Settings/Rendering/URP_Pipeline_VR.asset";

        /// <summary>
        /// One URP asset for every quality level, tuned for a stable 90 fps in a headset: no HDR, no
        /// depth/opaque copies, no additional lights, one 1024 px shadow cascade, SRP batcher on.
        /// Static lighting is baked; only robots and elements cast realtime shadows.
        /// </summary>
        static void ConfigureRendering()
        {
            var urp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(VrPipelinePath);
            if (!urp) { Debug.LogWarning("[VrFsim] VR URP asset missing"); return; }
            var so = new SerializedObject(urp);
            void Set(string prop, int v) { var p = so.FindProperty(prop); if (p != null) p.intValue = v; }
            void SetF(string prop, float v) { var p = so.FindProperty(prop); if (p != null) p.floatValue = v; }
            void SetB(string prop, bool v) { var p = so.FindProperty(prop); if (p != null) p.boolValue = v; }
            SetB("m_SupportsHDR", false);
            SetB("m_RequireDepthTexture", false);
            SetB("m_RequireOpaqueTexture", false);
            Set("m_MSAA", 4);
            SetF("m_RenderScale", 1f);
            Set("m_AdditionalLightsRenderingMode", 0);
            Set("m_MainLightShadowmapResolution", 1024);
            SetF("m_ShadowDistance", 6f);
            Set("m_ShadowCascadeCount", 1);
            SetB("m_SoftShadowsSupported", false);
            SetB("m_UseSRPBatcher", true);
            SetB("m_UseAdaptivePerformance", false);
            so.ApplyModifiedPropertiesWithoutUndo();

            GraphicsSettings.defaultRenderPipeline = urp;
            var names = QualitySettings.names;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
                QualitySettings.vSyncCount = 0;
                QualitySettings.antiAliasing = 0;          // MSAA comes from the URP asset
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
                QualitySettings.pixelLightCount = 1;
                QualitySettings.lodBias = 1f;
            }
            QualitySettings.SetQualityLevel(current, false);
            EditorUtility.SetDirty(urp);
        }

        static void CleanConfigObjects()
        {
            foreach (var key in StaleConfigObjects)
                EditorBuildSettings.RemoveConfigObject(key);
        }

        static void EnsureMainScene()
        {
            if (!File.Exists(MainScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, MainScenePath);
            }
        }

        /// <summary>The build starts on the Loading scene (logo + progress bar), which loads Main.</summary>
        public static void SetBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            if (File.Exists(LoadingScenePath)) scenes.Add(new EditorBuildSettingsScene(LoadingScenePath, true));
            scenes.Add(new EditorBuildSettingsScene(MainScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
