using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VrFsim.Game;
using VrFsim.Settings;

namespace VrFsim.Tests
{
    public class RulesTests
    {
        static readonly RuleAssumptions Defaults = new RuleAssumptions();

        [Test]
        public void TipModel_MatchesOfficialCalibrationPoints()
        {
            // Event Field Setup Guide §12.3: 7 POLLEN must not tip, 8 must.
            Assert.IsFalse(TipModel.Tips(7, 0, Defaults));
            Assert.IsTrue(TipModel.Tips(8, 0, Defaults));
            // With 3 NECTAR: 2 POLLEN must not tip, 3 must.
            Assert.IsFalse(TipModel.Tips(2, 3, Defaults));
            Assert.IsTrue(TipModel.Tips(3, 3, Defaults));
        }

        [Test]
        public void TipModel_PollenNeededIsMonotone()
        {
            Assert.AreEqual(8, TipModel.PollenNeeded(0, Defaults));
            Assert.AreEqual(3, TipModel.PollenNeeded(3, Defaults));
            int prev = int.MaxValue;
            for (int n = 0; n <= 6; n++)
            {
                int need = TipModel.PollenNeeded(n, Defaults);
                Assert.LessOrEqual(need, prev);
                prev = need;
            }
            Assert.AreEqual(0, TipModel.PollenNeeded(5, Defaults));
        }

        [Test]
        public void Flower_TopNectarOwns_BottomNectarGetsBonus()
        {
            var entries = new List<FlowerEntry>
            {
                new FlowerEntry(ElementKind.Pollen, 6f),
                new FlowerEntry(ElementKind.NectarBlue, 9f),
                new FlowerEntry(ElementKind.Pollen, 12f),
                new FlowerEntry(ElementKind.NectarRed, 15f),
            };
            var r = FlowerRules.Evaluate(entries);
            Assert.AreEqual(Alliance.Red, r.owner);
            Assert.AreEqual(Alliance.Blue, r.bottomBonus);
            Assert.AreEqual(4, r.scoringElements);
        }

        [Test]
        public void Flower_NoNectar_NoOwner()
        {
            var r = FlowerRules.Evaluate(new List<FlowerEntry> { new FlowerEntry(ElementKind.Pollen, 6f) });
            Assert.IsNull(r.owner);
            Assert.IsNull(r.bottomBonus);
        }

        [Test]
        public void Score_TotalsAndRankingPoints()
        {
            var red = new AllianceScore
            {
                leaveCount = 2, autoParkCount = 1, teleopParkCount = 2, autoTips = 1, teleopTips = 6,
                cellElements = 3, ownedFlowerElements = 5, bottomBonuses = 1, gardenElements = 4,
                majorFoulsAgainstOpponent = 1,
            };
            var blue = new AllianceScore();
            // AUTO: 6 + 5 + 20 = 31. TELEOP: 10 + 120 + 6 + 10 + 5 + 4 = 155. Fouls: 20.
            Assert.AreEqual(31, red.AutoPoints);
            Assert.AreEqual(155, red.TeleopPoints);
            Assert.AreEqual(206, red.Total);
            Assert.IsTrue(red.SwarmRp);        // 6 + 15 = 21 ≥ 16
            Assert.IsTrue(red.Pollinator1Rp);  // 7 tips
            Assert.IsTrue(red.Pollinator2Rp);
            Assert.AreEqual(6, red.RankingPoints(blue));
            Assert.AreEqual(0, blue.RankingPoints(red));
        }

        [Test]
        public void Zones_ArePointSymmetric()
        {
            var blue = FieldSpec.LoadingZoneRed.For(Alliance.Blue);
            Assert.AreEqual(-FieldSpec.LoadingZoneRed.xMax, blue.xMin, 1e-4f);
            Assert.Greater(blue.xMin, 0f);
            Assert.Less(blue.yMax, 0f);
            Assert.IsTrue(FieldSpec.GardenRed.OverlapsCircle(new Vector2(-60f, -67f), 1.4f));
            Assert.IsFalse(FieldSpec.GardenRed.OverlapsCircle(new Vector2(-60f, -65f), 1.4f));
        }

        [Test]
        public void RobotConfig_ValidateClampsToLegalBuild()
        {
            var c = new RobotConfig { lengthIn = 30f, widthIn = 5f, drivetrain = DrivetrainType.Swerve, storage = 9, hoodDeg = 10f, massLb = 1f };
            c.Validate();
            Assert.AreEqual(18f, c.lengthIn);
            Assert.AreEqual(RobotConfig.SwerveMinWidth, c.widthIn);
            Assert.AreEqual(4, c.storage);
            Assert.AreEqual(RobotConfig.HoodMin, c.hoodDeg);
            Assert.GreaterOrEqual(c.massLb, RobotConfig.MassRange(DrivetrainType.Swerve).x);

            var twin = new RobotConfig { launcher = LauncherKind.DoubleTurret, launcherMount = MountPos.Front, launcherMount2 = MountPos.Center };
            twin.Validate();
            Assert.IsFalse(RobotConfig.Adjacent(twin.launcherMount, twin.launcherMount2));
        }

        [Test]
        public void Settings_RoundTripThroughJson()
        {
            var s = new SimSettings();
            s.robot.name = "Test Bot";
            s.assists.slowModePercent = 45f;
            s.match.alliance = Alliance.Blue;
            var back = JsonUtility.FromJson<SimSettings>(JsonUtility.ToJson(s));
            back.Validate();
            Assert.AreEqual("Test Bot", back.robot.name);
            Assert.AreEqual(45f, back.assists.slowModePercent);
            Assert.AreEqual(Alliance.Blue, back.match.alliance);
        }
    }
}
