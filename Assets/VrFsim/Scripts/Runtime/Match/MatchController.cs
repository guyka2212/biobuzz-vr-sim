using System;
using System.Collections.Generic;
using UnityEngine;
using VrFsim.Game;
using VrFsim.Input;
using VrFsim.Robot;
using VrFsim.Settings;

namespace VrFsim.Match
{
    public enum MatchPhase { PreMatch, Auto, Transition, Teleop, Settling, Ended, FreeDrive }

    public enum MatchCue { Countdown, AutoStart, AutoEnd, TransitionCountdown, TeleopStart, FlowersOpen, Final20, MatchEnd, Tip, Foul, HumanNectar }

    /// <summary>
    /// Runs a BIOBUZZ match: staging (§10.3.1), robot placement (G304), the 0:30 / 0:08 / 2:00
    /// clock (Table 9-1), live and final scoring (Table 10-2), human-player NECTAR (G426/G427),
    /// and automatic fouls. Also runs free drive (no clock, everything live).
    /// </summary>
    public class MatchController : MonoBehaviour
    {
        public const float AutoSeconds = 30f, TransitionSeconds = 8f, TeleopSeconds = 120f;
        public const float FlowerUnlockRemaining = 60f, Final20Remaining = 20f;

        public static MatchController Instance { get; private set; }

        public MatchPhase Phase { get; private set; } = MatchPhase.PreMatch;
        public float PhaseTime { get; private set; }
        public RobotController Player { get; private set; }
        public readonly AllianceScore Red = new AllianceScore(), Blue = new AllianceScore();
        public readonly List<string> EventLog = new List<string>();
        public bool FinalScored { get; private set; }

        public event Action<MatchCue> Cue;
        public event Action<string> Logged;
        public event Action<RobotController> PlayerSpawned;

        public AllianceScore ScoreOf(Alliance a) => a == Alliance.Red ? Red : Blue;

        readonly List<GameElement> pollen = new List<GameElement>(FieldSpec.PollenTotal);
        readonly List<GameElement> nectarRed = new List<GameElement>(8), nectarBlue = new List<GameElement>(8);
        readonly List<RobotController> robots = new List<RobotController>(4);
        readonly PenaltyTracker penalties = new PenaltyTracker();
        readonly HumanPlayer[] humans = { new HumanPlayer(Alliance.Red), new HumanPlayer(Alliance.Blue) };
        Transform elementRoot, robotRoot;
        bool flowersOpenCued, final20Cued;
        int lastCountdown = -1;
        float settleStart;
        MaterialLibrary lib;
        Mesh elementMesh;

        public HumanPlayer HumanOf(Alliance a) => humans[(int)a];

        void Awake()
        {
            Instance = this;
            lib = MaterialLibrary.Get();
            var art = FieldArt.Load();
            elementMesh = art && art.pollenMesh ? art.pollenMesh : MeshUtil.Sphere();
            elementRoot = new GameObject("Elements").transform;
            robotRoot = new GameObject("Robots").transform;
            penalties.Foul += OnFoul;
        }

        void Start()
        {
            if (SimWorld.Field == null)
            {
                var existing = FindAnyObjectByType<FieldRoot>();
                SimWorld.Field = existing ? existing.Rebind() : FieldBuilder.Build(null, lib, FieldArt.Load());
            }
            SimWorld.Field.redHive.Tipped += OnTipped;
            SimWorld.Field.blueHive.Tipped += OnTipped;
            foreach (var f in SimWorld.Field.flowers) f.ElementEntered += OnFlowerEntered;
            CreateElements();
            if (InputHub.Instance)
            {
                InputHub.Instance.StartMatchPressed += OnStartPressed;
                InputHub.Instance.RestartPressed += ResetMatch;
                InputHub.Instance.HumanNectarPressed += OnHumanNectarPressed;
            }
            ResetMatch();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (InputHub.Instance)
            {
                InputHub.Instance.StartMatchPressed -= OnStartPressed;
                InputHub.Instance.RestartPressed -= ResetMatch;
                InputHub.Instance.HumanNectarPressed -= OnHumanNectarPressed;
            }
        }

        void OnHumanNectarPressed()
        {
            if (Player) TryHumanNectar(Player.Alliance);
        }

        // ── Clock ───────────────────────────────────────────────────────────────────────────

        /// <summary>The FIELD timer: 2:30 → 2:00 in AUTO, 0:08 → 0:01 in transition, 2:00 → 0:00 in TELEOP.</summary>
        public float DisplaySeconds
        {
            get
            {
                switch (Phase)
                {
                    case MatchPhase.PreMatch: return AutoSeconds + TeleopSeconds;
                    case MatchPhase.Auto: return AutoSeconds + TeleopSeconds - PhaseTime;
                    case MatchPhase.Transition: return Mathf.Max(1f, TransitionSeconds - PhaseTime);
                    case MatchPhase.Teleop: return Mathf.Max(0f, TeleopSeconds - PhaseTime);
                    default: return 0f;
                }
            }
        }

        /// <summary>Seconds left in the MATCH (AUTO counts the whole remaining match).</summary>
        public float SecondsRemaining =>
            Phase == MatchPhase.Teleop ? TeleopSeconds - PhaseTime :
            Phase == MatchPhase.Transition || Phase == MatchPhase.PreMatch ? TeleopSeconds :
            Phase == MatchPhase.Auto ? TeleopSeconds + AutoSeconds - PhaseTime : 0f;

        public bool FlowersOpen => Phase == MatchPhase.FreeDrive || (Phase == MatchPhase.Teleop && SecondsRemaining <= FlowerUnlockRemaining);

        bool IsLive => Phase == MatchPhase.Auto || Phase == MatchPhase.Transition || Phase == MatchPhase.Teleop || Phase == MatchPhase.FreeDrive;

        // ── Setup ───────────────────────────────────────────────────────────────────────────

        void CreateElements()
        {
            var rules = SettingsStore.Current.rules;
            for (int i = 0; i < FieldSpec.PollenTotal; i++) pollen.Add(MakeElement(ElementKind.Pollen, rules.pollenMassGrams, lib.pollen, i));
            for (int i = 0; i < FieldSpec.NectarPerAlliance; i++)
            {
                nectarRed.Add(MakeElement(ElementKind.NectarRed, rules.nectarMassGrams, lib.nectarRed, i));
                nectarBlue.Add(MakeElement(ElementKind.NectarBlue, rules.nectarMassGrams, lib.nectarBlue, i));
            }
        }

        GameElement MakeElement(ElementKind kind, float grams, Material mat, int i)
        {
            var go = new GameObject($"{kind}_{i}", typeof(MeshFilter), typeof(MeshRenderer), typeof(Rigidbody), typeof(SphereCollider));
            go.transform.SetParent(elementRoot, false);
            go.GetComponent<MeshFilter>().sharedMesh = kind == ElementKind.Pollen || !FieldArt.Load() || !FieldArt.Load().nectarMesh
                ? elementMesh : FieldArt.Load().nectarMesh;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var e = go.AddComponent<GameElement>();
            e.Init(kind, grams / 1000f, lib.ballPhysics);
            return e;
        }

        /// <summary>Restage the field and robots for a new match (or free drive).</summary>
        public void ResetMatch()
        {
            var s = SettingsStore.Current;
            FinalScored = false;
            Red.ResetLive(); Blue.ResetLive();
            ResetScore(Red); ResetScore(Blue);
            EventLog.Clear();
            penalties.Reset();
            flowersOpenCued = final20Cued = false;
            lastCountdown = -1;
            foreach (var h in humans) h.Reset();

            SpawnRobots(s);
            var field = SimWorld.Field;
            field.redHive.ResetTo(FieldSpec.StagedUpCellSign(Alliance.Red));
            field.blueHive.ResetTo(FieldSpec.StagedUpCellSign(Alliance.Blue));
            foreach (var f in field.flowers) f.ResetState();
            StageElements();
            Physics.SyncTransforms();

            if (s.match.mode == GameMode.FreeDrive) BeginPhase(MatchPhase.FreeDrive);
            else BeginPhase(MatchPhase.PreMatch);
            Log(s.match.mode == GameMode.FreeDrive ? "Free drive" : "Match ready. Press Start Match (A) to begin.");
        }

        static void ResetScore(AllianceScore sc)
        {
            sc.leaveCount = sc.autoParkCount = sc.teleopParkCount = sc.autoTips = sc.teleopTips = 0;
            sc.minorFoulsAgainstOpponent = sc.majorFoulsAgainstOpponent = 0;
        }

        void StageElements()
        {
            int pi = 0;
            // 4 POLLEN stacked in each FLOWER.
            foreach (var f in SimWorld.Field.flowers)
                for (int k = 0; k < FieldSpec.PollenPerFlower; k++)
                    pollen[pi++].PlaceAt(Units.Field(f.centerIn.x, f.centerIn.y, 1.45f + k * 2.85f));
            // 4 POLLEN in each GARDEN, in a line from the corner nearest that alliance's area, against the wall.
            foreach (Alliance a in new[] { Alliance.Red, Alliance.Blue })
                for (int k = 0; k < FieldSpec.PollenPerGarden; k++)
                {
                    float r = FieldSpec.PollenDiameter * 0.5f;
                    var redFrame = new Vector2(-FieldSpec.WallInner + r + 0.05f + k * (FieldSpec.PollenDiameter + 0.05f), -FieldSpec.WallInner + r + 0.05f);
                    pollen[pi++].PlaceAt(SimWorld.AllianceToWorld(a, redFrame, r + 0.02f));
                }
            // 4 preloads per robot on the field; empty seats leave theirs in the LOADING ZONE against the wall.
            int seatsFilled = 0;
            foreach (var r in robots)
            {
                for (int k = 0; k < FieldSpec.PreloadPollen; k++)
                {
                    var e = pollen[pi++];
                    e.PlaceAt(r.transform.position + Vector3.up * 0.5f + Vector3.right * 0.01f * k);
                    if (!r.Store(e)) e.PlaceAt(r.transform.TransformPoint(new Vector3(Units.In(-3f + k * 3f), Units.In(1.4f), -Units.In(r.Config.lengthIn * 0.5f + 1.5f))));
                }
                seatsFilled++;
            }
            for (int seat = seatsFilled; seat < 4 && pi < pollen.Count; seat++)
            {
                Alliance a = seat % 2 == 0 ? Alliance.Red : Alliance.Blue;
                var lz = FieldSpec.LoadingZoneRed.For(a);
                for (int k = 0; k < FieldSpec.PreloadPollen && pi < pollen.Count; k++)
                {
                    float wallX = a == Alliance.Red ? -FieldSpec.WallInner + 1.5f : FieldSpec.WallInner - 1.5f;
                    float y = lz.Center.y - 4.5f + k * 3f + (seat >= 2 ? 0f : 0f);
                    pollen[pi++].PlaceAt(Units.Field(wallX + (seat >= 2 ? (a == Alliance.Red ? 3f : -3f) : 0f), y, 1.45f));
                }
            }
            while (pi < pollen.Count) pollen[pi++].PlaceAt(Units.Field(0f, 0f, -50f)); // unused (should not happen)

            // 3 NECTAR in each up-CELL against the back wall, from the side nearest that alliance's area.
            StageCellNectar(SimWorld.Field.redHive, nectarRed);
            StageCellNectar(SimWorld.Field.blueHive, nectarBlue);
            // 5 NECTAR in each ALLIANCE AREA, held by the human player.
            humans[0].Stock(nectarRed.GetRange(FieldSpec.NectarInCell, FieldSpec.NectarInAllianceArea));
            humans[1].Stock(nectarBlue.GetRange(FieldSpec.NectarInCell, FieldSpec.NectarInAllianceArea));
        }

        static void StageCellNectar(Hive hive, List<GameElement> nectar)
        {
            int s = hive.UpSign;
            float r = FieldSpec.NectarDiameter * 0.5f;
            float side = hive.alliance == Alliance.Red ? -1f : 1f;
            for (int k = 0; k < FieldSpec.NectarInCell; k++)
            {
                float x = side * (FieldSpec.CellHalfWidth - r - 0.1f - k * (FieldSpec.NectarDiameter + 0.1f));
                var local = new Vector3(x, FieldSpec.CellFloorW + r + 0.15f, s * (FieldSpec.CellInnerV + r + 0.15f)) * Units.MetersPerInch;
                nectar[k].PlaceAt(hive.transform.TransformPoint(local));
            }
        }

        void SpawnRobots(SimSettings s)
        {
            foreach (var r in robots) if (r) { r.DropAll(); r.gameObject.SetActive(false); Destroy(r.gameObject); }
            robots.Clear();
            SimWorld.Robots.Clear();

            var a = s.match.alliance;
            Player = SpawnRobot(s.robot, a, s.assists, true, s.match.start, "Player");
            Player.Source = new DriverSource();

            var partnerAnchor = s.match.start == StartAnchor.WallCenter ? StartAnchor.WallNearAudience : StartAnchor.WallCenter;
            if (s.match.partner == PracticeSeat.Dummy) SpawnDummy(a, partnerAnchor, 2);
            if (s.match.opponent1 == PracticeSeat.Dummy) SpawnDummy(a.Opponent(), StartAnchor.WallCenter, 3);
            if (s.match.opponent2 == PracticeSeat.Dummy) SpawnDummy(a.Opponent(), StartAnchor.WallNearAudience, 4);
            PlayerSpawned?.Invoke(Player);
        }

        void SpawnDummy(Alliance a, StartAnchor anchor, int n)
        {
            var cfg = RobotPresets.All()[n % RobotPresets.All().Length];
            cfg.name = "Practice " + n;
            cfg.teamNumber = 1000 * n + 7;
            cfg.Validate();
            var assists = new AssistSettings { autoIntake = false, autoFire = false, aimAssist = false, fieldCentric = false };
            SpawnRobot(cfg, a, assists, false, anchor, "Dummy" + n).Source = null;
        }

        RobotController SpawnRobot(RobotConfig cfg, Alliance a, AssistSettings assists, bool player, StartAnchor anchor, string name)
        {
            var rig = RobotBuilder.Build(cfg, a, lib, RobotArt.Load(), $"{name}_{a}");
            rig.root.transform.SetParent(robotRoot, false);
            var rc = rig.root.AddComponent<RobotController>();
            rc.Init(cfg, a, rig, assists, player);
            var pose = StartPose(cfg, a, anchor, SettingsStore.Current.match.customStart);
            rc.PlaceAt(pose.pos, pose.yaw);
            robots.Add(rc);
            return rc;
        }

        /// <summary>
        /// G304-legal start poses: on your own half, touching the perimeter wall, out of the LOADING
        /// ZONE and clear of the FLOWERS. Given in red's frame, rotated 180° for blue.
        /// </summary>
        public static (Vector3 pos, float yaw) StartPose(RobotConfig c, Alliance a, StartAnchor anchor, CustomPose custom)
        {
            float back = -FieldSpec.WallInner + c.lengthIn * 0.5f + 0.05f;
            Vector2 p; float heading;
            switch (anchor)
            {
                case StartAnchor.WallNearAudience: p = new Vector2(back, -48f); heading = 90f; break;
                case StartAnchor.WallBehindLoadingZone: p = new Vector2(back, 58.6f); heading = 90f; break;
                case StartAnchor.AudienceWall: p = new Vector2(-36f, back); heading = 0f; break;
                case StartAnchor.Custom: p = new Vector2(custom.xIn, custom.yIn); heading = custom.headingDeg; break;
                default: p = new Vector2(back, 0f); heading = 90f; break;
            }
            return (SimWorld.AllianceToWorld(a, p, 0.05f), SimWorld.AllianceYaw(a, heading));
        }

        // ── Phases ──────────────────────────────────────────────────────────────────────────

        void OnStartPressed()
        {
            if (Phase == MatchPhase.PreMatch) StartMatch();
            else if (Phase == MatchPhase.Ended) ResetMatch();
        }

        public void StartMatch()
        {
            if (Phase != MatchPhase.PreMatch) return;
            foreach (var r in robots) r.SetDeployed(true);
            Cue?.Invoke(MatchCue.AutoStart);
            BeginPhase(MatchPhase.Auto);
            Log("AUTO");
        }

        void BeginPhase(MatchPhase p)
        {
            Phase = p;
            PhaseTime = 0f;
            var s = SettingsStore.Current;
            foreach (var r in robots)
            {
                bool player = r == Player;
                switch (p)
                {
                    case MatchPhase.PreMatch:
                    case MatchPhase.Transition:
                    case MatchPhase.Settling:
                    case MatchPhase.Ended:
                        r.Enabled = false;
                        break;
                    case MatchPhase.Auto:
                        r.Enabled = player;
                        if (player) { r.Source = new AutoSource(s.match.auto); r.ForceAimAssist = true; }
                        break;
                    case MatchPhase.Teleop:
                    case MatchPhase.FreeDrive:
                        r.Enabled = player;
                        if (player) { r.Source = new DriverSource(); r.ForceAimAssist = false; r.SetDeployed(true); }
                        break;
                }
            }
        }

        void Update()
        {
            if (Phase == MatchPhase.PreMatch || Phase == MatchPhase.Ended) return;
            PhaseTime += Time.deltaTime;
            switch (Phase)
            {
                case MatchPhase.Auto:
                    if (PhaseTime >= AutoSeconds) EndAuto();
                    break;
                case MatchPhase.Transition:
                    int cd = Mathf.CeilToInt(TransitionSeconds - PhaseTime);
                    if (cd == 3 && lastCountdown != 3) { lastCountdown = 3; Cue?.Invoke(MatchCue.TransitionCountdown); }
                    if (PhaseTime >= TransitionSeconds) { BeginPhase(MatchPhase.Teleop); Cue?.Invoke(MatchCue.TeleopStart); Log("TELEOP"); }
                    break;
                case MatchPhase.Teleop:
                    if (!flowersOpenCued && SecondsRemaining <= FlowerUnlockRemaining)
                    {
                        flowersOpenCued = true;
                        Cue?.Invoke(MatchCue.FlowersOpen);
                        Log("1:00 - FLOWERS open, human players may enter all NECTAR");
                    }
                    if (!final20Cued && SecondsRemaining <= Final20Remaining) { final20Cued = true; Cue?.Invoke(MatchCue.Final20); }
                    if (PhaseTime >= TeleopSeconds) EndTeleop();
                    break;
                case MatchPhase.Settling:
                    if (AtRest() || PhaseTime > 6f) FinalScore();
                    break;
            }
        }

        void FixedUpdate()
        {
            if (Phase == MatchPhase.PreMatch) { UpdateLiveScore(); return; }
            if (!IsLive && Phase != MatchPhase.Settling) return;
            if (Phase == MatchPhase.Auto) penalties.TickAuto(robots);
            if (Phase == MatchPhase.Teleop || Phase == MatchPhase.FreeDrive) penalties.TickPins(robots, Time.fixedDeltaTime);
            ReturnEscapedElements();
            if (!FinalScored) UpdateLiveScore();
        }

        void EndAuto()
        {
            Cue?.Invoke(MatchCue.AutoEnd);
            // LEAVE and AUTO PARK are assessed at the end of AUTO (§10.5 F).
            foreach (var r in robots)
            {
                var sc = ScoreOf(r.Alliance);
                var fp = r.Footprint();
                if (!fp.TouchesWall()) sc.leaveCount++;
                if (InLoadingZone(r)) sc.autoParkCount++;
            }
            BeginPhase(MatchPhase.Transition);
            Log($"AUTO ends - Red {Red.AutoPoints}, Blue {Blue.AutoPoints}");
        }

        void EndTeleop()
        {
            Cue?.Invoke(MatchCue.MatchEnd);
            // TELEOP PARK at the end of the MATCH (§10.5 G).
            foreach (var r in robots) if (InLoadingZone(r)) ScoreOf(r.Alliance).teleopParkCount++;
            BeginPhase(MatchPhase.Settling);
            settleStart = Time.time;
        }

        bool InLoadingZone(RobotController r)
        {
            var fp = r.Footprint();
            if (fp.Overlaps(FieldSpec.LoadingZoneRed.For(r.Alliance))) return true;
            return !SettingsStore.Current.rules.parkOwnLoadingZoneOnly && fp.Overlaps(FieldSpec.LoadingZoneRed.For(r.Alliance.Opponent()));
        }

        bool AtRest()
        {
            if (PhaseTime < 1f) return false;
            if (SimWorld.Field.redHive.IsSwinging || SimWorld.Field.blueHive.IsSwinging) return false;
            foreach (var e in GameElement.All) if (e.IsFree && e.Body.linearVelocity.sqrMagnitude > 0.0025f) return false;
            return true;
        }

        void FinalScore()
        {
            UpdateLiveScore();
            FinalScored = true;
            BeginPhase(MatchPhase.Ended);
            string winner = Red.Total == Blue.Total ? "TIE" : Red.Total > Blue.Total ? "RED wins" : "BLUE wins";
            Log($"FINAL - Red {Red.Total} ({Red.RankingPoints(Blue)} RP), Blue {Blue.Total} ({Blue.RankingPoints(Red)} RP). {winner}.");
        }

        // ── Scoring ─────────────────────────────────────────────────────────────────────────

        void UpdateLiveScore()
        {
            Red.ResetLive(); Blue.ResetLive();
            var field = SimWorld.Field;
            foreach (var hive in new[] { field.redHive, field.blueHive })
            {
                if (hive.IsSwinging) continue; // contents are leaving; the TIP itself is what scores
                hive.CountCell(hive.UpSign, out int p, out int n);
                ScoreOf(hive.alliance).cellElements += p + n;
            }
            foreach (var f in field.flowers)
            {
                var res = f.Result;
                if (res.owner.HasValue) ScoreOf(res.owner.Value).ownedFlowerElements += res.scoringElements;
                if (res.bottomBonus.HasValue) ScoreOf(res.bottomBonus.Value).bottomBonuses++;
            }
            var redGarden = FieldSpec.GardenRed;
            var blueGarden = FieldSpec.GardenRed.For(Alliance.Blue);
            foreach (var e in GameElement.All)
            {
                if (!e.IsFree) continue;
                Vector2 p = e.FieldPlaneIn; float r = e.RadiusIn;
                if (redGarden.OverlapsCircle(p, r)) Red.gardenElements++;
                if (blueGarden.OverlapsCircle(p, r)) Blue.gardenElements++;
            }
        }

        void OnTipped(Hive hive)
        {
            var sc = ScoreOf(hive.alliance);
            // A TIP completed before TELEOP starts is an AUTO TIP (§10.5 B).
            bool auto = Phase == MatchPhase.Auto || Phase == MatchPhase.Transition;
            if (Phase == MatchPhase.PreMatch || Phase == MatchPhase.Ended) return;
            if (auto) sc.autoTips++; else sc.teleopTips++;
            humans[(int)hive.alliance].tipsEarned++;
            Cue?.Invoke(MatchCue.Tip);
            Log($"{hive.alliance} HIVE TIPPED (+20). Human player may enter a NECTAR.");
        }

        void OnFlowerEntered(Flower f, GameElement e)
        {
            if (!IsLive || !SettingsStore.Current.match.enforcePenalties) return;
            penalties.OnNectarEnteredFlower(e, f, SecondsRemaining, FlowersOpen);
        }

        void OnFoul(Alliance violator, bool major, string rule, string detail)
        {
            if (!SettingsStore.Current.match.enforcePenalties) return;
            var beneficiary = ScoreOf(violator.Opponent());
            if (major) beneficiary.majorFoulsAgainstOpponent++; else beneficiary.minorFoulsAgainstOpponent++;
            Cue?.Invoke(MatchCue.Foul);
            Log($"{(major ? "MAJOR" : "MINOR")} FOUL {rule} on {violator}: {detail}");
        }

        // ── Human player and element logistics ──────────────────────────────────────────────

        public bool TryHumanNectar(Alliance a)
        {
            if (!IsLive) return false;
            var h = humans[(int)a];
            bool allowed = h.CanEnter(SecondsRemaining <= FlowerUnlockRemaining && Phase == MatchPhase.Teleop || Phase == MatchPhase.FreeDrive);
            if (!allowed) { Log($"{a} human player: no NECTAR entitlement yet (one per own TIP, all at 1:00)."); return false; }
            var spot = LoadingZoneDropPoint(a);
            var e = h.Enter(spot);
            if (!e) return false;
            Cue?.Invoke(MatchCue.HumanNectar);
            Log($"{a} human player entered a NECTAR ({h.Remaining} left)");
            return true;
        }

        /// <summary>A point in the LOADING ZONE, against the wall, not under a robot (G427 C).</summary>
        Vector3 LoadingZoneDropPoint(Alliance a)
        {
            var lz = FieldSpec.LoadingZoneRed.For(a);
            float wallX = a == Alliance.Red ? lz.xMin + 2.5f : lz.xMax - 2.5f;
            for (int k = 0; k < 5; k++)
            {
                float y = Mathf.Lerp(lz.yMin + 3f, lz.yMax - 3f, (k * 0.37f + 0.5f) % 1f);
                var p = new Vector2(wallX, y);
                bool clear = true;
                foreach (var r in robots) if (r.Footprint().Contains(p)) { clear = false; break; }
                if (clear) return Units.Field(p.x, p.y, 8f);
            }
            return Units.Field(wallX, lz.Center.y, 30f);
        }

        /// <summary>Elements that leave the FIELD: POLLEN is put back by FIELD STAFF at the nearest wall;
        /// NECTAR goes back to its own drive team (§10.8.2).</summary>
        void ReturnEscapedElements()
        {
            float lim = FieldSpec.WallInner + 2f;
            foreach (var e in GameElement.All)
            {
                if (!e.IsFree) continue;
                Vector2 p = e.FieldPlaneIn;
                bool outside = Mathf.Abs(p.x) > lim || Mathf.Abs(p.y) > lim || e.HeightIn < -6f;
                if (!outside) continue;
                if (e.Kind.IsNectar())
                {
                    humans[(int)e.Kind.NectarAlliance()].Return(e);
                    Log($"{e.Kind.NectarAlliance()} NECTAR left the field; returned to its drive team");
                }
                else
                {
                    float x = Mathf.Clamp(p.x, -FieldSpec.WallInner + 2f, FieldSpec.WallInner - 2f);
                    float y = Mathf.Clamp(p.y, -FieldSpec.WallInner + 2f, FieldSpec.WallInner - 2f);
                    e.PlaceAt(Units.Field(x, y, 2f));
                }
            }
        }

        public void Log(string msg)
        {
            string stamp = Phase == MatchPhase.FreeDrive || Phase == MatchPhase.PreMatch ? "" : $"[{FormatClock(DisplaySeconds)}] ";
            EventLog.Add(stamp + msg);
            if (EventLog.Count > 50) EventLog.RemoveAt(0);
            Logged?.Invoke(stamp + msg);
        }

        public static string FormatClock(float s)
        {
            int t = Mathf.CeilToInt(Mathf.Max(0f, s));
            return $"{t / 60}:{t % 60:00}";
        }
    }
}
