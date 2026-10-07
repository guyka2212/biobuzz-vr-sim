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

namespace VrFsim.EditorTools
{
    /// <summary>
    /// One-shot, idempotent project configuration for PC VR. Safe to re-run from the menu or from
    /// batch mode (<c>-executeMethod VrFsim.EditorTools.ProjectSetup.ConfigureProject</c>).
    /// </summary>
    public static class ProjectSetup
    {
        public const string MainScenePath = "Assets/VrFsim/Scenes/Main.unity";

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
            CleanConfigObjects();
            EnsureMainScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[VrFsim] Project configured for PC VR (OpenXR, Standalone).");
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "VrFsim";
            PlayerSettings.productName = "VrFsim";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Medium);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
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
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScenePath, true) };
        }
    }
}
