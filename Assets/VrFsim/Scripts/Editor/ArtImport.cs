using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VrFsim.EditorTools
{
    /// <summary>
    /// Import rules for Blender FBX models under Art/Models: metres, no animation, no imported
    /// materials (FBX material slots are remapped by name onto the shared library materials),
    /// mesh compression, no read/write copy. Keeps models small and draw calls batched.
    /// </summary>
    public class ArtImport : AssetPostprocessor
    {
        const string ModelsDir = "Assets/VrFsim/Art/Models";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelsDir)) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.bakeAxisConversion = true;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
            mi.importVisibility = false;
            mi.isReadable = false;
            mi.meshCompression = ModelImporterMeshCompression.Medium;
            mi.meshOptimizationFlags = MeshOptimizationFlags.Everything;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.None;
            mi.addCollider = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            mi.materialSearch = ModelImporterMaterialSearch.Everywhere;
            mi.materialName = ModelImporterMaterialName.BasedOnMaterialName;
        }

        void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(ModelsDir)) return;
            // Bind every renderer slot to the project material of the same name, if it exists.
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!mats[i]) continue;
                    var found = AssetDatabase.FindAssets($"t:Material {mats[i].name}", new[] { "Assets/VrFsim/Materials" })
                        .Select(AssetDatabase.GUIDToAssetPath)
                        .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == mats[i].name);
                    if (found != null) mats[i] = AssetDatabase.LoadAssetAtPath<Material>(found);
                }
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        /// <summary>Batch helper: print where each object of an FBX lands in Unity space.</summary>
        public static void ReportAxisTest()
        {
            AssetDatabase.Refresh();
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + "/_AxisTest.fbx");
            if (!go) { Debug.Log("[VrFsim] axis test model missing"); return; }
            var inst = Object.Instantiate(go);
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
                Debug.Log($"[VrFsim] AXIS {r.name}: center {r.bounds.center} size {r.bounds.size}");
            Object.DestroyImmediate(inst);
        }
    }
}
