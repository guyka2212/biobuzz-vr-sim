using UnityEngine;
using VrFsim.Settings;

namespace VrFsim.Robot
{
    /// <summary>
    /// Physical drive parameters for one build, derived from real FTC hardware:
    /// goBILDA 5203-series motor (6000 rpm free, 0.144 N·m stall at the motor shaft), 104 mm
    /// wheels, four drive motors, and a wheel-on-foam-tile traction coefficient per wheel type.
    /// A brushed DC motor's torque falls linearly with speed, so available acceleration is
    /// <c>min(traction, motorStall × (1 − v / vFree))</c>.
    /// </summary>
    public readonly struct DriveParams
    {
        public readonly float massKg;
        public readonly float yawInertia;    // kg·m²
        public readonly float vFwd, vStrafe; // m/s free speed per axis
        public readonly float omegaMax;      // rad/s
        public readonly float fFwd, fStrafe; // N, stall force per axis (all motors)
        public readonly float muFwd, muLat;  // traction coefficient per axis
        public readonly float turnRadius;    // m, effective wheel lever arm for yaw
        public readonly bool holonomic, swerve;

        public const float WheelDiameter = 0.104f;
        public const float MotorFreeRpm = 6000f;
        public const float MotorStallTorque = 0.144f;
        public const float GearEfficiency = 0.9f;
        public const float SpeedEfficiency = 0.92f;   // loaded speed vs. free speed
        public const int DriveMotors = 4;
        public const float MaxOmega = 12f;

        DriveParams(float massKg, float yawInertia, float vFwd, float vStrafe, float omegaMax, float fFwd, float fStrafe,
            float muFwd, float muLat, float turnRadius, bool holonomic, bool swerve)
        {
            this.massKg = massKg; this.yawInertia = yawInertia; this.vFwd = vFwd; this.vStrafe = vStrafe;
            this.omegaMax = omegaMax; this.fFwd = fFwd; this.fStrafe = fStrafe; this.muFwd = muFwd; this.muLat = muLat;
            this.turnRadius = turnRadius; this.holonomic = holonomic; this.swerve = swerve;
        }

        /// <param name="tractionMode">Butterfly only: true when the traction wheels are down.</param>
        public static DriveParams For(RobotConfig c, bool tractionMode)
        {
            float m = Units.Lb(c.massLb);
            float L = Units.In(c.lengthIn), W = Units.In(c.widthIn);
            float inset = Units.In(2.6f);
            float halfTrack = W * 0.5f - inset, halfBase = L * 0.5f - inset;
            float yawI = m * (L * L + W * W) / 12f;

            var dt = c.drivetrain;
            float rpm = c.driveRpm;
            if (dt == DrivetrainType.Butterfly && tractionMode) { dt = DrivetrainType.Tank; rpm = c.butterflyTractionRpm; }
            else if (dt == DrivetrainType.Butterfly) dt = DrivetrainType.Mecanum;

            float vWheel = Mathf.PI * WheelDiameter * rpm / 60f * SpeedEfficiency;
            float gear = MotorFreeRpm / Mathf.Max(rpm, 1f);
            float wheelForce = MotorStallTorque * gear * GearEfficiency / (WheelDiameter * 0.5f);
            float fAll = wheelForce * DriveMotors;

            switch (dt)
            {
                case DrivetrainType.Tank:
                {
                    // Skid steer: wheels scrub sideways when turning, so turn rate is below the ideal 2v/track.
                    float omega = Mathf.Min(MaxOmega, 0.75f * vWheel / halfTrack);
                    return new DriveParams(m, yawI, vWheel, 0f, omega, fAll, 0f, 1.0f, 1.1f, halfTrack, false, false);
                }
                case DrivetrainType.XDrive:
                {
                    // Omni wheels at 45°: √2 faster in any straight line, but each wheel pushes at cos 45°.
                    float v = vWheel * 1.41421f * 0.9f;
                    float r = Mathf.Sqrt(halfTrack * halfTrack + halfBase * halfBase);
                    float omega = Mathf.Min(MaxOmega, vWheel / r);
                    return new DriveParams(m, yawI, v, v, omega, fAll * 0.7071f, fAll * 0.7071f, 0.6f, 0.6f, r, true, false);
                }
                case DrivetrainType.Swerve:
                {
                    float r = Mathf.Sqrt(halfTrack * halfTrack + halfBase * halfBase);
                    float omega = Mathf.Min(MaxOmega, vWheel / r);
                    return new DriveParams(m, yawI, vWheel, vWheel, omega, fAll, fAll, 0.95f, 0.95f, r, true, true);
                }
                default: // Mecanum
                {
                    // Rollers slip: some forward loss, more when strafing.
                    float omega = Mathf.Min(MaxOmega, vWheel * 0.95f / (halfTrack + halfBase));
                    return new DriveParams(m, yawI, vWheel * 0.95f, vWheel * 0.8f, omega, fAll * 0.9f, fAll * 0.72f, 0.8f, 0.65f,
                        Mathf.Sqrt(halfTrack * halfTrack + halfBase * halfBase), true, false);
                }
            }
        }

        public float PushForceN => Mathf.Min(muFwd * massKg * Units.Gravity, fFwd);
    }

    /// <summary>Body-frame velocity target: x = right, y = forward (m/s), w = yaw rate (rad/s, + = clockwise).</summary>
    public struct BodyTarget
    {
        public float x, y, w;
    }

    public static class DriveKinematics
    {
        /// <summary>
        /// Map normalised commands (−1…1, robot frame) to a body velocity target, saturating the way
        /// the real wheel mixing does: if any wheel would exceed full power, all are scaled down.
        /// </summary>
        public static BodyTarget Target(in DriveParams p, float strafe, float forward, float turn)
        {
            if (!p.holonomic) strafe = 0f;
            float scale;
            if (p.swerve) scale = Mathf.Max(1f, new Vector2(strafe, forward).magnitude + Mathf.Abs(turn));
            else if (p.holonomic) scale = Mathf.Max(1f, Mathf.Abs(forward) + Mathf.Abs(strafe) + Mathf.Abs(turn));
            else scale = Mathf.Max(1f, Mathf.Abs(forward) + Mathf.Abs(turn));
            return new BodyTarget { x = strafe / scale * p.vStrafe, y = forward / scale * p.vFwd, w = turn / scale * p.omegaMax };
        }

        /// <summary>Tank-style control: independent left and right side powers.</summary>
        public static BodyTarget TankTarget(in DriveParams p, float left, float right)
        {
            left = Mathf.Clamp(left, -1f, 1f); right = Mathf.Clamp(right, -1f, 1f);
            return new BodyTarget { x = 0f, y = (left + right) * 0.5f * p.vFwd, w = (left - right) * 0.5f * p.omegaMax };
        }

        /// <summary>
        /// Acceleration toward a target along one axis, limited by traction and by the motor's
        /// falling torque curve. Braking (target opposes motion) is traction-limited only.
        /// </summary>
        public static float AxisAccel(float v, float target, float vFree, float motorAccel, float tractionAccel, float dt)
        {
            float want = (target - v) / dt;
            bool braking = Mathf.Abs(target) < Mathf.Abs(v) || Mathf.Sign(target) != Mathf.Sign(v);
            float limit = braking || vFree <= 0f
                ? tractionAccel
                : Mathf.Min(tractionAccel, motorAccel * Mathf.Max(0.06f, 1f - Mathf.Abs(v) / vFree));
            return Mathf.Clamp(want, -limit, limit);
        }
    }
}
