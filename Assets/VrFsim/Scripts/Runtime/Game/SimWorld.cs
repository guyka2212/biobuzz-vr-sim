using System.Collections.Generic;
using UnityEngine;

namespace VrFsim.Game
{
    /// <summary>Scene-wide references shared by the field, robots, and match logic.</summary>
    public static class SimWorld
    {
        public static FieldBuilder.Result Field;
        public static readonly List<Robot.RobotController> Robots = new List<Robot.RobotController>(4);

        /// <summary>Driver-station facing (yaw, degrees): red drivers look along +x, blue along −x.</summary>
        public static float DriverYaw(Alliance a) => a == Alliance.Red ? 90f : -90f;

        public static Vector3 DriverForward(Alliance a) => Quaternion.Euler(0f, DriverYaw(a), 0f) * Vector3.forward;
        public static Vector3 DriverRight(Alliance a) => Quaternion.Euler(0f, DriverYaw(a), 0f) * Vector3.right;

        /// <summary>Point given in red's frame (inches) → this alliance's world position.</summary>
        public static Vector3 AllianceToWorld(Alliance a, Vector2 redFrameIn, float zIn = 0f)
        {
            float s = a.Sign();
            return Units.Field(redFrameIn.x * s, redFrameIn.y * s, zIn);
        }

        /// <summary>Heading given in red's frame (degrees, 0 = +y) → world yaw for this alliance.</summary>
        public static float AllianceYaw(Alliance a, float redHeadingDeg) => a == Alliance.Red ? redHeadingDeg : redHeadingDeg + 180f;
    }
}
