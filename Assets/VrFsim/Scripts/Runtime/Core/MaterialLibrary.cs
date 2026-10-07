using UnityEngine;

namespace VrFsim
{
    /// <summary>
    /// Every shared material and physics material in the game, in one asset
    /// (Resources/VrFsimMaterials). Sharing keeps draw calls batched under the SRP batcher and
    /// keeps the build small. The editor creates the asset (VrFsim ▸ Setup ▸ Build Scene).
    /// </summary>
    [CreateAssetMenu(menuName = "VrFsim/Material Library")]
    public class MaterialLibrary : ScriptableObject
    {
        public const string ResourceName = "VrFsimMaterials";

        [Header("Field")]
        public Material tile;
        public Material wall;
        public Material wallFrame;
        public Material tapeRed;
        public Material tapeBlue;
        public Material hiveFrame;
        public Material hiveRed;
        public Material hiveBlue;
        public Material flower;
        public Material flowerRing;
        public Material venueFloor;
        public Material venueWall;

        [Header("Elements")]
        public Material pollen;
        public Material nectarRed;
        public Material nectarBlue;

        [Header("Robots")]
        public Material robotMetal;
        public Material robotDark;
        public Material robotWheel;
        public Material bumperRed;
        public Material bumperBlue;

        [Header("UI / effects")]
        public Material blobShadow;

        [Header("Physics")]
        public PhysicsMaterial tilePhysics;
        public PhysicsMaterial wallPhysics;
        public PhysicsMaterial ballPhysics;
        public PhysicsMaterial robotPhysics;
        public PhysicsMaterial structurePhysics;

        static MaterialLibrary cached;

        public static MaterialLibrary Get()
        {
            if (cached == null) cached = Resources.Load<MaterialLibrary>(ResourceName);
            if (cached == null)
            {
                Debug.LogWarning("[VrFsim] Material library missing; using runtime fallback materials.");
                cached = CreateFallback();
            }
            return cached;
        }

        public static Shader LitShader => Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Standard");

        public static Material NewMat(string name, Color c, float smooth = 0.2f, bool emissive = false)
        {
            var m = new Material(LitShader) { name = name, color = c, enableInstancing = true };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (emissive && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * 0.6f);
            }
            return m;
        }

        public static PhysicsMaterial NewPhys(string name, float dyn, float stat, float bounce,
            PhysicsMaterialCombine friction = PhysicsMaterialCombine.Average,
            PhysicsMaterialCombine bounceCombine = PhysicsMaterialCombine.Average)
        {
            return new PhysicsMaterial(name)
            {
                dynamicFriction = dyn, staticFriction = stat, bounciness = bounce,
                frictionCombine = friction, bounceCombine = bounceCombine,
            };
        }

        /// <summary>Fill any empty slot with a default. Used by the editor and by the fallback.</summary>
        public void FillDefaults()
        {
            if (!tile) tile = NewMat("Tile", new Color(0.32f, 0.33f, 0.35f), 0.05f);
            if (!wall) wall = NewMat("WallPanel", new Color(0.85f, 0.9f, 0.95f), 0.6f);
            if (!wallFrame) wallFrame = NewMat("WallFrame", new Color(0.62f, 0.64f, 0.67f), 0.5f);
            if (!tapeRed) tapeRed = NewMat("TapeRed", new Color(0.85f, 0.08f, 0.08f), 0.3f);
            if (!tapeBlue) tapeBlue = NewMat("TapeBlue", new Color(0.05f, 0.35f, 0.95f), 0.3f);
            if (!hiveFrame) hiveFrame = NewMat("HiveFrame", new Color(0.18f, 0.18f, 0.2f), 0.4f);
            if (!hiveRed) hiveRed = NewMat("HiveRed", new Color(0.8f, 0.1f, 0.12f), 0.35f);
            if (!hiveBlue) hiveBlue = NewMat("HiveBlue", new Color(0.1f, 0.25f, 0.85f), 0.35f);
            if (!flower) flower = NewMat("FlowerPipe", new Color(0.93f, 0.93f, 0.9f), 0.4f);
            if (!flowerRing) flowerRing = NewMat("FlowerRing", new Color(0.2f, 0.62f, 0.24f), 0.3f);
            if (!venueFloor) venueFloor = NewMat("VenueFloor", new Color(0.16f, 0.17f, 0.2f), 0.15f);
            if (!venueWall) venueWall = NewMat("VenueWall", new Color(0.1f, 0.11f, 0.14f), 0.05f);
            if (!pollen) pollen = NewMat("Pollen", new Color(1f, 0.85f, 0.05f), 0.35f);
            if (!nectarRed) nectarRed = NewMat("NectarRed", new Color(0.9f, 0.08f, 0.1f), 0.35f);
            if (!nectarBlue) nectarBlue = NewMat("NectarBlue", new Color(0.08f, 0.3f, 0.95f), 0.35f);
            if (!robotMetal) robotMetal = NewMat("RobotMetal", new Color(0.7f, 0.72f, 0.75f), 0.55f);
            if (!robotDark) robotDark = NewMat("RobotDark", new Color(0.12f, 0.12f, 0.13f), 0.3f);
            if (!robotWheel) robotWheel = NewMat("RobotWheel", new Color(0.08f, 0.08f, 0.08f), 0.1f);
            if (!bumperRed) bumperRed = NewMat("BumperRed", new Color(0.78f, 0.06f, 0.08f), 0.15f);
            if (!bumperBlue) bumperBlue = NewMat("BumperBlue", new Color(0.06f, 0.22f, 0.8f), 0.15f);

            if (!tilePhysics) tilePhysics = NewPhys("Tile", 0.6f, 0.7f, 0.25f);
            if (!wallPhysics) wallPhysics = NewPhys("Wall", 0.25f, 0.3f, 0.45f);
            if (!ballPhysics) ballPhysics = NewPhys("Ball", 0.45f, 0.5f, 0.5f);
            // Wheel traction is modelled by the drive code, so the chassis itself must not grip.
            if (!robotPhysics) robotPhysics = NewPhys("Robot", 0f, 0f, 0.05f, PhysicsMaterialCombine.Minimum, PhysicsMaterialCombine.Minimum);
            if (!structurePhysics) structurePhysics = NewPhys("Structure", 0.3f, 0.35f, 0.3f);
        }

        static MaterialLibrary CreateFallback()
        {
            var lib = CreateInstance<MaterialLibrary>();
            lib.FillDefaults();
            return lib;
        }
    }
}
