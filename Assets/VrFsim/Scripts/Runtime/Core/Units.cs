using UnityEngine;

namespace VrFsim
{
    /// <summary>
    /// Unit conversions and the field coordinate convention.
    ///
    /// Game data is written in inches in the FIRST field frame: origin at field centre, +x toward
    /// the audience's right, +y away from the audience, +z up. Unity is metres, Y-up, so field
    /// (x, y, z) maps to Unity (x, z, y).
    /// </summary>
    public static class Units
    {
        public const float MetersPerInch = 0.0254f;
        public const float KgPerLb = 0.45359237f;
        public const float Gravity = 9.81f;

        public static float In(float inches) => inches * MetersPerInch;
        public static float ToIn(float meters) => meters / MetersPerInch;
        public static float Lb(float pounds) => pounds * KgPerLb;

        /// <summary>Field-frame point in inches → Unity world position in metres.</summary>
        public static Vector3 Field(float xIn, float yIn, float zIn = 0f) =>
            new Vector3(xIn * MetersPerInch, zIn * MetersPerInch, yIn * MetersPerInch);

        /// <summary>Field-frame size in inches (x, y, z) → Unity size in metres.</summary>
        public static Vector3 Size(float xIn, float yIn, float zIn) =>
            new Vector3(xIn * MetersPerInch, zIn * MetersPerInch, yIn * MetersPerInch);

        /// <summary>Unity world position → field-plane point (x, y) in inches.</summary>
        public static Vector2 ToFieldPlane(Vector3 world) =>
            new Vector2(world.x / MetersPerInch, world.z / MetersPerInch);
    }
}
