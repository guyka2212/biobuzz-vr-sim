using UnityEngine;

namespace VrFsim.Robot
{
    /// <summary>
    /// Optional Blender meshes for robot parts. Every mesh is modelled at unit size around its
    /// origin (1 m cube / unit-diameter wheel) so the builder can scale it to any legal build.
    /// An empty slot falls back to a primitive.
    /// </summary>
    [CreateAssetMenu(menuName = "VrFsim/Robot Art")]
    public class RobotArt : ScriptableObject
    {
        public const string ResourceName = "VrFsimRobotArt";

        [Tooltip("Enclosed body, unit cube; slots: 0 team colour, 1 aluminium, 2 black.")] public Mesh body;
        [Tooltip("Unit diameter, unit width, axle along X.")] public Mesh mecanumWheel;
        public Mesh tractionWheel;
        public Mesh omniWheel;
        [Tooltip("Swerve module housing, unit size.")] public Mesh swerveModule;
        [Tooltip("Turret on a geared turntable, unit diameter, base at y = 0, shoots toward +Z.")] public Mesh turret;
        [Tooltip("Dumper bucket, unit size, +Z outward.")] public Mesh dumper;
        [Tooltip("Box Tube, unit size, extends along +Y.")] public Mesh boxTube;
        [Tooltip("Intake roller, unit length along X.")] public Mesh intakeRoller;
        [Tooltip("Intake side plate, unit cube, rounded end toward +Z.")] public Mesh intakePlate;
        [Tooltip("Box Tube placer cup, unit cube.")] public Mesh placerCup;

        public static RobotArt Load() => Resources.Load<RobotArt>(ResourceName);
    }
}
