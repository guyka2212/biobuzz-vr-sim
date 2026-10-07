using System.Linq;
using UnityEditor;
using UnityEngine;
using VrFsim.Game;
using VrFsim.Robot;

namespace VrFsim.EditorTools
{
    /// <summary>
    /// Hooks the Blender exports (Art/Models/*.fbx, Art/Textures/*.png) into the game: fills the
    /// FieldArt / RobotArt slots, sets texture import settings, and gives the tile and element
    /// materials their textures. Run after re-exporting from Blender, then rebuild the scene.
    /// </summary>
    public static class ArtAssigner
    {
        const string Models = "Assets/VrFsim/Art/Models/";
        const string Textures = "Assets/VrFsim/Art/Textures/";

        [MenuItem("VrFsim/Setup/Assign Art")]
        public static void AssignArt()
        {
            AssetDatabase.Refresh();
            ConfigureTexture("FieldTiles.png", 1024, 8);
            ConfigureTexture("ElementHoles.png", 512, 2);

            var lib = Resources.Load<MaterialLibrary>(MaterialLibrary.ResourceName);
            if (lib)
            {
                SetBaseMap(lib.tile, "FieldTiles.png", Color.white);
                SetBaseMap(lib.pollen, "ElementHoles.png", null);
                SetBaseMap(lib.nectarRed, "ElementHoles.png", null);
                SetBaseMap(lib.nectarBlue, "ElementHoles.png", null);
            }

            var field = Resources.Load<FieldArt>(FieldArt.ResourceName);
            if (field)
            {
                field.tiles = Model("Tiles");
                field.perimeter = Model("Perimeter");
                field.hiveFrame = Model("HiveFrame");
                field.hiveTrayRed = Model("HiveTrayRed");
                field.hiveTrayBlue = Model("HiveTrayBlue");
                field.flower = Model("Flower");
                field.pollenMesh = Mesh("Element");
                field.nectarMesh = Mesh("Element");
                EditorUtility.SetDirty(field);
            }

            var robot = Resources.Load<RobotArt>(RobotArt.ResourceName);
            if (robot)
            {
                robot.mecanumWheel = Mesh("RobotMecanumWheel");
                robot.tractionWheel = Mesh("RobotTractionWheel");
                robot.omniWheel = Mesh("RobotOmniWheel");
                robot.swerveModule = Mesh("RobotSwerveModule");
                robot.turret = Mesh("RobotTurret");
                robot.dumper = Mesh("RobotDumper");
                robot.boxTube = Mesh("RobotBoxTube");
                robot.intakeRoller = Mesh("RobotIntakeRoller");
                EditorUtility.SetDirty(robot);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[VrFsim] Art assigned.");
        }

        /// <summary>Batch entry: assign art, then regenerate the Main scene with it.</summary>
        public static void AssignArtAndBuildScene()
        {
            AssignArt();
            SceneBuilder.BuildScene();
        }

        static GameObject Model(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(Models + name + ".fbx");

        static Mesh Mesh(string name) =>
            AssetDatabase.LoadAllAssetsAtPath(Models + name + ".fbx").OfType<Mesh>().FirstOrDefault();

        static void ConfigureTexture(string file, int maxSize, int aniso)
        {
            var ti = AssetImporter.GetAtPath(Textures + file) as TextureImporter;
            if (!ti) return;
            ti.maxTextureSize = maxSize;
            ti.mipmapEnabled = true;
            ti.anisoLevel = aniso;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.sRGBTexture = true;
            ti.SaveAndReimport();
        }

        static void SetBaseMap(Material m, string file, Color? tint)
        {
            if (!m) return;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + file);
            if (!tex) return;
            m.SetTexture("_BaseMap", tex);
            m.mainTexture = tex;
            if (tint.HasValue) { m.SetColor("_BaseColor", tint.Value); m.color = tint.Value; }
            EditorUtility.SetDirty(m);
        }
    }
}
