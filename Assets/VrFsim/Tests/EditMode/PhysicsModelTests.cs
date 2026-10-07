using NUnit.Framework;
using UnityEngine;
using VrFsim.Game;
using VrFsim.Robot;
using VrFsim.Settings;

namespace VrFsim.Tests
{
    public class PhysicsModelTests
    {
        static RobotConfig Bot(DrivetrainType dt, float rpm = 435f, float mass = 28f)
        {
            var c = new RobotConfig { drivetrain = dt, driveRpm = rpm, massLb = mass, lengthIn = 16f, widthIn = 16f };
            c.Validate();
            return c;
        }

        [Test]
        public void Mecanum435_HasRealisticTopSpeed()
        {
            var p = DriveParams.For(Bot(DrivetrainType.Mecanum), false);
            float inPerSec = Units.ToIn(p.vFwd);
            Assert.That(inPerSec, Is.InRange(70f, 95f), "435 rpm on 104 mm wheels is roughly 6–8 ft/s");
            Assert.Less(p.vStrafe, p.vFwd);
        }

        [Test]
        public void Tank_CannotStrafe_AndPushesHarderThanMecanum()
        {
            var tank = DriveParams.For(Bot(DrivetrainType.Tank, 340f), false);
            var mec = DriveParams.For(Bot(DrivetrainType.Mecanum, 340f), false);
            Assert.AreEqual(0f, DriveKinematics.Target(tank, 1f, 0f, 0f).x);
            Assert.Greater(tank.PushForceN, mec.PushForceN);
        }

        [Test]
        public void XDrive_IsFasterInStraightLines()
        {
            var x = DriveParams.For(Bot(DrivetrainType.XDrive), false);
            var m = DriveParams.For(Bot(DrivetrainType.Mecanum), false);
            Assert.Greater(x.vFwd, m.vFwd);
            Assert.Less(x.PushForceN, m.PushForceN);
        }

        [Test]
        public void Butterfly_TractionModeBehavesLikeTank()
        {
            var c = Bot(DrivetrainType.Butterfly, 420f, 31f);
            Assert.IsTrue(DriveParams.For(c, false).holonomic);
            Assert.IsFalse(DriveParams.For(c, true).holonomic);
        }

        [Test]
        public void Mixing_SaturatesLikeWheelPowers()
        {
            var p = DriveParams.For(Bot(DrivetrainType.Mecanum), false);
            var t = DriveKinematics.Target(p, 1f, 1f, 0f);
            Assert.AreEqual(p.vFwd * 0.5f, t.y, 1e-4f);
            Assert.AreEqual(p.vStrafe * 0.5f, t.x, 1e-4f);
        }

        [Test]
        public void AxisAccel_IsTractionLimitedFromRest_AndFallsWithSpeed()
        {
            float a0 = DriveKinematics.AxisAccel(0f, 2f, 2f, 20f, 8f, 0.01f);
            float aHalf = DriveKinematics.AxisAccel(1.5f, 2f, 2f, 20f, 8f, 0.01f);
            Assert.AreEqual(8f, a0, 1e-4f);
            Assert.AreEqual(20f * 0.25f, aHalf, 1e-3f);
            // Braking uses full traction.
            Assert.AreEqual(-8f, DriveKinematics.AxisAccel(1.5f, 0f, 2f, 20f, 8f, 0.01f), 1e-4f);
        }

        [TestCase(IntakeMount.Front, 1)]
        [TestCase(IntakeMount.Left, 1)]
        [TestCase(IntakeMount.Right, 1)]
        [TestCase(IntakeMount.Side, 2)]
        [TestCase(IntakeMount.FrontAndBack, 2)]
        public void IntakeMounts_BuildTheRightMouths(IntakeMount mount, int mouths)
        {
            var c = new RobotConfig { intakeMount = mount };
            c.Validate();
            var rig = RobotBuilder.Build(c, Alliance.Red, MaterialLibrary.Get(), null, "Test");
            try
            {
                Assert.AreEqual(mouths, rig.mouths.Count);
                if (mount == IntakeMount.Side)
                {
                    Assert.AreEqual(1f, rig.mouths[0].outward.x, 1e-4f);
                    Assert.AreEqual(-1f, rig.mouths[1].outward.x, 1e-4f, "double side intake covers both sides");
                }
                if (mount == IntakeMount.Left) Assert.AreEqual(-1f, rig.mouths[0].outward.x, 1e-4f);
                Assert.AreEqual(mouths == 2, c.IsDoubleIntake);
            }
            finally { Object.DestroyImmediate(rig.root); }
        }

        [Test]
        public void ApexShot_PassesThroughTarget()
        {
            var from = new Vector3(0f, 0.4f, 0f);
            var to = new Vector3(2f, 1.45f, 1f);
            Assert.IsTrue(Ballistics.ApexShot(from, to, 1.75f, out var v));
            // Time to reach the target's horizontal distance, then the height there.
            float flat = new Vector2(to.x, to.z).magnitude;
            float t = flat / new Vector2(v.x, v.z).magnitude;
            float y = from.y + v.y * t - 0.5f * Units.Gravity * t * t;
            Assert.AreEqual(to.y, y, 0.01f);
            Assert.Less(v.y - Units.Gravity * t, 0f, "must be descending at the target");
        }

        [Test]
        public void FixedAngleSpeed_LandsAtDistance()
        {
            float elev = 75f * Mathf.Deg2Rad;
            Assert.IsTrue(Ballistics.FixedAngleSpeed(1.5f, 1f, elev, out float s));
            float vx = s * Mathf.Cos(elev), vy = s * Mathf.Sin(elev);
            float t = 1.5f / vx;
            Assert.AreEqual(1f, vy * t - 0.5f * Units.Gravity * t * t, 0.01f);
        }

        [Test]
        public void Obb_ParkAndWallTests()
        {
            var obb = new FieldObb { center = new Vector2(-62.6f, 35f), axisX = Vector2.right, axisY = Vector2.up, halfX = 8f, halfY = 8f };
            Assert.IsTrue(obb.Overlaps(FieldSpec.LoadingZoneRed));
            Assert.IsTrue(obb.TouchesWall());
            Assert.IsFalse(obb.Overlaps(FieldSpec.LoadingZoneRed.For(Alliance.Blue)));
            var mid = new FieldObb { center = Vector2.zero, axisX = Vector2.right, axisY = Vector2.up, halfX = 8f, halfY = 8f };
            Assert.IsFalse(mid.TouchesWall());
            Assert.IsFalse(mid.FullyOnSide(Alliance.Red));
        }
    }
}
