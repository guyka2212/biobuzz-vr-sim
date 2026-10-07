using UnityEngine;

namespace VrFsim
{
    public enum Alliance
    {
        Red = 0,
        Blue = 1,
    }

    public static class AllianceExt
    {
        public static Alliance Opponent(this Alliance a) => a == Alliance.Red ? Alliance.Blue : Alliance.Red;

        /// <summary>
        /// The layout is point-symmetric about the field centre (not mirrored), so blue's version of
        /// any red feature is the red one rotated 180°: (x, y) → (−x, −y).
        /// </summary>
        public static float Sign(this Alliance a) => a == Alliance.Red ? 1f : -1f;

        public static Color Tint(this Alliance a) =>
            a == Alliance.Red ? new Color(0.86f, 0.12f, 0.12f) : new Color(0.10f, 0.32f, 0.92f);
    }
}
