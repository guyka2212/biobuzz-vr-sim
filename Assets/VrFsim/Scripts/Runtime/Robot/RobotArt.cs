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

        [Tooltip("Unit cube chassis; submeshes: 0 plate (team colour), 1 metal, 2 electronics.")] public Mesh chassis;
        [Tooltip("Unit diameter, unit width, axle along X.")] public Mesh mecanumWheel;
        public Mesh tractionWheel;
        public Mesh omniWheel;
        [Tooltip("Swerve module housing, unit size.")] public Mesh swerveModule;
        [Tooltip("Turret: unit diameter ring + launcher, +Z is the shooting direction.")] public Mesh turret;
        [Tooltip("Dumper bucket, unit size, +Z outward.")] public Mesh dumper;
        [Tooltip("Box Tube, unit size, extends along +Y.")] public Mesh boxTube;
        [Tooltip("Intake roller, unit length along X.")] public Mesh intakeRoller;

        public static RobotArt Load() => Resources.Load<RobotArt>(ResourceName);
    }
}
