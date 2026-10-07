using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VrFsim.Game;
using VrFsim.Input;
using VrFsim.Match;
using VrFsim.Robot;
using VrFsim.Settings;

namespace VrFsim.Tests
{
    /// <summary>Boots the real Main scene and checks the simulation behaves like the game.</summary>
    public class SimulationTests
    {
        class ScriptSource : ICommandSource
        {
            public DriverCommand cmd;
            public DriverCommand Read(RobotController r)
            {
                var c = cmd;
                // Edge-triggered buttons fire once.
                cmd.placePollen = cmd.placeNectar = cmd.pass = false;
                return c;
            }
        }

        static IEnumerator Boot(SimSettings s)
        {
            Time.timeScale = 1f;
            SettingsStore.UseTransient(s);
            SimWorld.Field = null;
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
        }

        static SimSettings Free(RobotConfig robot = null)
        {
            var s = new SimSettings();
            s.match.mode = GameMode.FreeDrive;
            s.match.enforcePenalties = true;
            if (robot != null) s.robot = robot;
            s.assists.autoIntake = false;
            return s;
        }

        static IEnumerator Seconds(float t)
        {
            float end = Time.time + t;
            while (Time.time < end) yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Boot_StagesFieldPerManual()
        {
            var s = new SimSettings();
            yield return Boot(s);
            var m = MatchController.Instance;
            Assert.IsNotNull(m, "MatchController");
            Assert.IsNotNull(m.Player, "player robot");
            Assert.AreEqual(MatchPhase.PreMatch, m.Phase);
            Assert.AreEqual(56, GameElement.All.Count, "40 POLLEN + 16 NECTAR");
            Assert.AreEqual(4, m.Player.StoredCount, "4 preloads");
            Assert.AreEqual(5, m.HumanOf(Alliance.Red).Remaining);
            Assert.AreEqual(5, m.HumanOf(Alliance.Blue).Remaining);

            yield return Seconds(1.5f);
            var field = SimWorld.Field;
            Assert.AreEqual(3, field.redHive.UpNectar, "3 NECTAR in red up-CELL");
            Assert.AreEqual(3, field.blueHive.UpNectar, "3 NECTAR in blue up-CELL");
            foreach (var f in field.flowers)
            {
                int near = GameElement.All.Count(e => e.IsFree && (e.FieldPlaneIn - f.centerIn).magnitude < 2.5f);
                Assert.AreEqual(4, near, $"4 POLLEN in FLOWER F{f.index + 1}");
            }
            Assert.AreEqual(4, m.Red.gardenElements, "red GARDEN");
            Assert.AreEqual(4, m.Blue.gardenElements, "blue GARDEN");
            Assert.AreEqual(0, field.redHive.TipCount);
        }

        [UnityTest]
        public IEnumerator StoredElements_StayInsideTheRobotWhileDriving()
        {
            yield return Boot(Free());
            var r = MatchController.Instance.Player;
            r.PlaceAt(Units.Field(-45f, 0f, 0.05f), 90f);
            r.Source = new ScriptSource { cmd = new DriverCommand { robotCentric = true, translate = new Vector2(0.4f, 1f), turn = 0.5f } };
            yield return Seconds(1.2f);
            float reach = Units.In(Mathf.Max(r.Config.lengthIn, r.Config.widthIn)) * 0.75f;
            Assert.AreEqual(4, r.StoredCount);
            foreach (var e in r.Stored)
            {
                var d = e.transform.position - r.transform.position; d.y = 0f;
                Assert.Less(d.magnitude, reach, $"{e.name} is {Units.ToIn(d.magnitude):0.0} in from the robot centre");
            }
        }

        [UnityTest]
        public IEnumerator Hive_TipsAtThreePollenWithThreeNectar()
        {
            yield return Boot(Free());
            var m = MatchController.Instance;
            var hive = SimWorld.Field.redHive;
            var spare = GameElement.All.Where(e => e.IsFree && e.Kind == ElementKind.Pollen && e.FieldPlaneIn.y < -60f).Take(3).ToList();
            Assert.AreEqual(3, spare.Count);

            spare[0].PlaceAt(hive.UpCellAimPoint());
            spare[1].PlaceAt(hive.UpCellAimPoint() + Vector3.up * 0.08f);
            yield return Seconds(2f);
            Assert.AreEqual(0, hive.TipCount, "2 POLLEN + 3 NECTAR must not tip (EFG 12.3)");

            spare[2].PlaceAt(hive.UpCellAimPoint() + Vector3.up * 0.08f);
            for (int i = 0; i < 9; i++)
            {
                yield return Seconds(0.5f);
                string where = string.Join(" ", spare.Select(e =>
                {
                    var p = hive.transform.InverseTransformPoint(e.Position) / Units.MetersPerInch;
                    return $"(x{p.x:0.0} w{p.y:0.0} v{p.z:0.0} in={hive.InCell(e.Position, -1)})";
                }));
                Debug.Log($"t+{0.5f * (i + 1):0.0}s up {hive.UpPollen}P {hive.UpNectar}N swinging={hive.IsSwinging} tips={hive.TipCount} {where}");
            }
            Assert.AreEqual(1, hive.TipCount, "3 POLLEN + 3 NECTAR must tip");
            Assert.AreEqual(1, m.Red.teleopTips);
            Assert.AreEqual(1, m.HumanOf(Alliance.Red).PerTipAvailable);
            Assert.AreEqual(1, hive.UpSign, "red's rear cell is now up");
        }

        [UnityTest]
        public IEnumerator Drive_MecanumReachesRealisticSpeed()
        {
            yield return Boot(Free());
            var r = MatchController.Instance.Player;
            r.PlaceAt(Units.Field(-45f, 0f, 0.05f), 90f);
            var src = new ScriptSource { cmd = new DriverCommand { robotCentric = true, translate = new Vector2(0f, 1f) } };
            r.Source = src;
            var start = r.transform.position;
            yield return Seconds(0.8f);
            float dist = Units.ToIn(Vector3.Distance(start, r.transform.position));
            float speed = Units.ToIn(r.Velocity.magnitude);
            var p = DriveParams.For(r.Config, false);
            Debug.Log($"Drove {dist:0.0} in in 0.8 s, speed {speed:0.0} in/s (free {Units.ToIn(p.vFwd):0.0})");
            Assert.Greater(dist, 35f);
            Assert.That(speed, Is.InRange(Units.ToIn(p.vFwd) * 0.8f, Units.ToIn(p.vFwd) * 1.02f));
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(r.transform.eulerAngles.y, 90f)), 3f, "drives straight");
        }

        static readonly Vector2[] TurretSpots =
        {
            new Vector2(FieldSpec.HivePivotXRed, -45f), new Vector2(FieldSpec.HivePivotXRed, -60f),
            new Vector2(-40f, -50f), new Vector2(5f, -40f),
        };

        [UnityTest]
        public IEnumerator Turret_LaunchesPreloadsIntoUpCell([ValueSource(nameof(TurretSpots))] Vector2 spot)
        {
            yield return Boot(Free());
            var r = MatchController.Instance.Player;
            var hive = SimWorld.Field.redHive;
            // The red up-CELL faces the audience (-y); stand on that side of the HIVE.
            r.PlaceAt(Units.Field(spot.x, spot.y, 0.05f), 0f);
            var src = new ScriptSource();
            r.Source = src;
            yield return Seconds(0.6f); // turret slews
            var shots = new System.Collections.Generic.List<GameElement>();
            r.Launched += (_, e) => shots.Add(e);
            src.cmd.fire = true;
            for (int i = 0; i < 25; i++)
            {
                yield return Seconds(0.1f);
                foreach (var e in shots)
                {
                    var p = e.FieldPlaneIn;
                    if (i % 2 == 1) Debug.Log($"  shot{shots.IndexOf(e)} t{0.1f * (i + 1):0.0} y{p.y:0.0} h{e.HeightIn:0.0} x{p.x:0.0}");
                }
            }
            src.cmd.fire = false;
            yield return Seconds(SettingsStore.Current.rules.hiveSwingSeconds + 1f);
            Debug.Log($"Turret at {spot}: tips {hive.TipCount}, up-cell {hive.UpPollen}P {hive.UpNectar}N, stored {r.StoredCount}");
            Assert.AreEqual(0, r.StoredCount, "all preloads launched");
            Assert.GreaterOrEqual(hive.TipCount, 1, "3 NECTAR staged + launched POLLEN should tip the HIVE");
        }

        [UnityTest]
        public IEnumerator Dumper_HeavesPreloadsIntoUpCell()
        {
            var cfg = RobotPresets.All()[1]; // Forager: dumper on the left edge
            cfg.Validate();
            yield return Boot(Free(cfg));
            var r = MatchController.Instance.Player;
            var hive = SimWorld.Field.redHive;
            // Heading +x puts the left edge (the dumper) toward +y, i.e. at the HIVE.
            r.PlaceAt(Units.Field(FieldSpec.HivePivotXRed, -60f, 0.05f), 90f);
            var src = new ScriptSource();
            r.Source = src;
            yield return Seconds(0.3f);
            src.cmd.fire = true;
            yield return Seconds(1f);
            src.cmd.fire = false;
            yield return Seconds(SettingsStore.Current.rules.hiveSwingSeconds + 1f);
            Debug.Log($"Dumper: tips {hive.TipCount}, up-cell {hive.UpPollen}P {hive.UpNectar}N");
            Assert.AreEqual(0, r.StoredCount);
            Assert.IsTrue(hive.TipCount >= 1 || hive.UpPollen >= 2, "most of the dump lands in the CELL");
        }

        [UnityTest]
        public IEnumerator BoxTube_PlacesPollenIntoFlower()
        {
            var cfg = RobotPresets.All()[0]; // Pollinator: Box Tube on the back
            cfg.Validate();
            yield return Boot(Free(cfg));
            var r = MatchController.Instance.Player;
            var f = SimWorld.Field.flowers[0]; // left wall, inward +x
            float x = f.centerIn.x + (FieldSpec.FlowerPlateDeep - FieldSpec.FlowerBoreFromWall) + cfg.lengthIn * 0.5f + 0.3f;
            r.PlaceAt(Units.Field(x, f.centerIn.y, 0.05f), 90f); // back faces the FLOWER
            var src = new ScriptSource();
            r.Source = src;
            yield return Seconds(1.5f);
            int before = f.Scoring.Count;
            src.cmd.placePollen = true;
            yield return Seconds(2.5f);
            Debug.Log($"FLOWER F1 scoring {before} -> {f.Scoring.Count}");
            Assert.AreEqual(before + 1, f.Scoring.Count);
        }

        [UnityTest]
        public IEnumerator StartPoses_AreLegalUnderG304()
        {
            yield return Boot(new SimSettings());
            foreach (var cfg in RobotPresets.All())
            {
                cfg.Validate();
                foreach (Alliance a in new[] { Alliance.Red, Alliance.Blue })
                    foreach (StartAnchor anchor in new[] { StartAnchor.WallNearAudience, StartAnchor.WallCenter, StartAnchor.WallBehindLoadingZone, StartAnchor.AudienceWall })
                    {
                        var (pos, yaw) = MatchController.StartPose(cfg, a, anchor, new CustomPose());
                        var f = new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad));
                        var rgt = new Vector3(f.z, 0f, -f.x);
                        var fp = new FieldObb
                        {
                            center = Units.ToFieldPlane(pos), axisX = new Vector2(rgt.x, rgt.z), axisY = new Vector2(f.x, f.z),
                            halfX = cfg.widthIn * 0.5f, halfY = cfg.lengthIn * 0.5f,
                        };
                        string where = $"{cfg.name} {a} {anchor}";
                        Assert.IsTrue(fp.FullyOnSide(a), where + ": G304.A own side");
                        Assert.IsTrue(fp.TouchesWall(0.6f), where + ": G304.C touching the wall");
                        for (int i = 0; i < 4; i++)
                            Assert.LessOrEqual(Mathf.Abs(fp.Corner(i).x), FieldSpec.WallInner + 0.01f, where + ": inside the field");
                        Assert.IsFalse(fp.Overlaps(FieldSpec.LoadingZoneRed.For(a)), where + ": G304.E not in LOADING ZONE");
                        foreach (var fl in SimWorld.Field.flowers)
                            Assert.Greater((fp.center - fl.centerIn).magnitude, Mathf.Max(fp.halfX, fp.halfY) + 2.5f, where + ": G304.D clear of FLOWER");
                    }
            }
        }

        [UnityTest]
        public IEnumerator Match_RunsFullTimelineAndScoresLeave()
        {
            var s = new SimSettings();
            s.match.auto = AutoRoutine.LaunchPreloadsAndLeave;
            yield return Boot(s);
            var m = MatchController.Instance;
            m.StartMatch();
            Assert.AreEqual(MatchPhase.Auto, m.Phase);
            Time.timeScale = 6f;
            while (m.Phase == MatchPhase.Auto) yield return null;
            Assert.AreEqual(MatchPhase.Transition, m.Phase);
            Assert.AreEqual(1, m.Red.leaveCount, "AUTO routine leaves the wall");
            Assert.IsFalse(m.Player.Enabled, "no powered movement in transition (G403)");
            Debug.Log($"After AUTO: red {m.Red.AutoPoints} pts, tips {m.Red.autoTips}");
            while (m.Phase == MatchPhase.Transition) yield return null;
            Assert.AreEqual(MatchPhase.Teleop, m.Phase);
            while (m.Phase != MatchPhase.Ended) yield return null;
            Time.timeScale = 1f;
            Assert.IsTrue(m.FinalScored);
            Debug.Log(string.Join("\n", m.EventLog));
            Assert.GreaterOrEqual(m.Red.Total, Points.Leave);
        }
    }
}
