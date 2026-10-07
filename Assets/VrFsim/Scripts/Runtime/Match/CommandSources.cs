using UnityEngine;
using VrFsim.Game;
using VrFsim.Input;
using VrFsim.Robot;
using VrFsim.Settings;

namespace VrFsim.Match
{
    /// <summary>The human driver, through <see cref="InputHub"/>.</summary>
    public class DriverSource : ICommandSource
    {
        public DriverCommand Read(RobotController robot) =>
            InputHub.Instance ? InputHub.Instance.ReadDriver() : DriverCommand.Idle;
    }

    /// <summary>
    /// Pre-programmed AUTO routines for the player's robot (drivers may not touch controls in AUTO,
    /// G401). Commands are robot-centric and computed from the robot's pose, like an OpMode using
    /// odometry.
    /// </summary>
    public class AutoSource : ICommandSource
    {
        readonly AutoRoutine routine;
        float startTime = -1f;
        int step;
        float stepStart;

        public AutoSource(AutoRoutine routine) { this.routine = routine; }

        public DriverCommand Read(RobotController r)
        {
            if (startTime < 0f) { startTime = Time.time; stepStart = Time.time; }
            float t = Time.time - stepStart;
            var cmd = new DriverCommand { robotCentric = true };
            switch (routine)
            {
                case AutoRoutine.DoNothing:
                    return DriverCommand.Idle;

                case AutoRoutine.Leave:
                    if (step == 0) { cmd.translate = new Vector2(0f, 0.5f); if (t > 0.7f) Next(); }
                    return cmd;

                default:
                    // 0: pull off the wall. 1: face the HIVE (dumper) / let the turret settle. 2: fire. 3: park or hold.
                    if (step == 0)
                    {
                        cmd.translate = new Vector2(0f, 0.5f);
                        if (t > 0.7f) Next();
                    }
                    else if (step == 1)
                    {
                        cmd.turn = FaceLauncherAtHive(r);
                        if (t > 1.2f || Mathf.Abs(cmd.turn) < 0.03f) Next();
                    }
                    else if (step == 2)
                    {
                        cmd.turn = FaceLauncherAtHive(r);
                        cmd.fire = true;
                        bool done = r.StoredCount == 0 || (r.Config.launcher == LauncherKind.Turret && !HasPollen(r));
                        if (done && t > 0.3f || t > 6f) Next();
                    }
                    else if (step == 3 && routine == AutoRoutine.LaunchPreloadsAndPark)
                    {
                        var lz = FieldSpec.LoadingZoneRed.For(r.Alliance);
                        var c = lz.Center;
                        // Aim a little into the field from the zone centre; PARK only needs partial overlap.
                        Vector2 goal = new Vector2(c.x + (r.Alliance == Alliance.Red ? 6f : -6f), c.y);
                        cmd = GoTo(r, goal);
                    }
                    return cmd;
            }
        }

        void Next() { step++; stepStart = Time.time; }

        static bool HasPollen(RobotController r)
        {
            foreach (var e in r.Stored) if (e.Kind == ElementKind.Pollen) return true;
            return false;
        }

        /// <summary>Turn command that points the launcher (dumper edge, or the front for a turret) at the HIVE.</summary>
        static float FaceLauncherAtHive(RobotController r)
        {
            var hive = SimWorld.Field?.HiveOf(r.Alliance);
            if (!hive) return 0f;
            Vector3 to = hive.UpCellAimPoint() - r.transform.position; to.y = 0f;
            Vector3 launcherDir = r.Config.launcher == LauncherKind.Dumper
                ? r.transform.TransformDirection(r.Rig.dumperDirLocal)
                : r.transform.forward;
            float err = Vector3.SignedAngle(launcherDir, to, Vector3.up);
            return Mathf.Clamp(err / 60f, -0.6f, 0.6f);
        }

        /// <summary>Simple proportional drive to a field point (inches), robot-centric.</summary>
        static DriverCommand GoTo(RobotController r, Vector2 goalIn)
        {
            var cmd = new DriverCommand { robotCentric = true };
            Vector3 goal = Units.Field(goalIn.x, goalIn.y);
            Vector3 err = goal - r.transform.position; err.y = 0f;
            float dist = Units.ToIn(err.magnitude);
            if (dist < 1.5f) return cmd;
            Vector3 local = r.transform.InverseTransformDirection(err.normalized);
            float speed = Mathf.Clamp(dist / 20f, 0.15f, 0.7f);
            bool holonomic = r.Config.drivetrain != DrivetrainType.Tank && !(r.Config.drivetrain == DrivetrainType.Butterfly && r.TractionMode);
            if (holonomic)
            {
                cmd.translate = new Vector2(local.x, local.z) * speed;
            }
            else
            {
                float yawErr = Vector3.SignedAngle(r.transform.forward, err, Vector3.up);
                cmd.turn = Mathf.Clamp(yawErr / 45f, -0.6f, 0.6f);
                cmd.translate = new Vector2(0f, Mathf.Abs(yawErr) < 20f ? speed : 0f);
            }
            return cmd;
        }
    }
}
