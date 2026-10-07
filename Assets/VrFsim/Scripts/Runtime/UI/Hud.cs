using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VrFsim.Game;
using VrFsim.Input;
using VrFsim.Match;
using VrFsim.Robot;
using VrFsim.Settings;
using VrFsim.VR;

namespace VrFsim.UI
{
    /// <summary>
    /// Two displays: a driver-station screen at chest height in front of the player (match clock,
    /// score, robot state, HIVE load, human-player NECTAR, event log, optional minimap and
    /// performance readout), and a large arena scoreboard behind the far wall.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        TextMeshProUGUI clock, phase, redScore, blueScore, robotLine, hiveLine, humanLine, flowerLine, log, perf, hint;
        TextMeshProUGUI boardClock, boardRed, boardBlue, boardPhase;
        RectTransform minimap;
        readonly List<RectTransform> robotDots = new List<RectTransform>();
        Canvas station, board;
        float nextRefresh;
        float fpsSmoothed = 90f;
        readonly StringBuilder sb = new StringBuilder(512);

        void Start()
        {
            BuildStation();
            BuildBoard();
        }

        void BuildStation()
        {
            var parent = ViewManager.Instance ? ViewManager.Instance.Origin.transform : transform;
            station = UiKit.WorldCanvas("DriverStationDisplay", parent, new Vector2(900, 560), 0.5f, false);
            station.transform.localPosition = new Vector3(0f, 0.92f, 0.5f);
            station.transform.localRotation = Quaternion.Euler(42f, 0f, 0f);

            var bg = UiKit.Box(station.transform, "Bg", UiKit.Bg);
            UiKit.Fill(bg.rectTransform);
            var col = UiKit.Rect(station.transform, "Col");
            UiKit.Fill(col, 18, 14, 18, 14);
            UiKit.VStack(col.gameObject, 6);

            var top = UiKit.Rect(col, "Top");
            UiKit.HStack(top.gameObject, 12);
            UiKit.Size(top, 74);
            redScore = UiKit.Text(top, "Red", "0", 60, UiKit.Red, TextAlignmentOptions.Left); UiKit.Size(redScore, -1, 170);
            var mid = UiKit.Rect(top, "Mid"); UiKit.VStack(mid.gameObject, 0); UiKit.Size(mid, -1, -1, 1);
            clock = UiKit.Text(mid, "Clock", "2:30", 50, UiKit.TextMain, TextAlignmentOptions.Center); UiKit.Size(clock, 50);
            phase = UiKit.Text(mid, "Phase", "PRE-MATCH", 20, UiKit.Highlight, TextAlignmentOptions.Center); UiKit.Size(phase, 24);
            blueScore = UiKit.Text(top, "Blue", "0", 60, UiKit.Blue, TextAlignmentOptions.Right); UiKit.Size(blueScore, -1, 170);

            flowerLine = Line(col, 22, UiKit.TextMain);
            robotLine = Line(col, 22, UiKit.TextMain);
            hiveLine = Line(col, 22, UiKit.TextMain);
            humanLine = Line(col, 22, UiKit.TextMain);
            log = Line(col, 18, UiKit.TextDim); UiKit.Size(log, 150);
            hint = Line(col, 17, UiKit.TextDim);
            perf = UiKit.Text(station.transform, "Perf", "", 18, UiKit.Highlight, TextAlignmentOptions.TopRight);
            UiKit.Fill(perf.rectTransform, 0, 6, 12, 0);

            minimap = UiKit.Rect(station.transform, "Minimap");
            minimap.anchorMin = minimap.anchorMax = new Vector2(1f, 0f);
            minimap.pivot = new Vector2(1f, 0f);
            minimap.sizeDelta = new Vector2(200, 200);
            minimap.anchoredPosition = new Vector2(-14, 14);
            var mapBg = UiKit.Box(minimap, "Field", new Color(0.25f, 0.26f, 0.29f, 1f));
            UiKit.Fill(mapBg.rectTransform);
            foreach (var rect in new[] { FieldSpec.LoadingZoneRed, FieldSpec.LoadingZoneRed.For(Alliance.Blue) })
                MapRect(rect, rect.xMin < 0 ? UiKit.Red : UiKit.Blue);
            MapRect(FieldSpec.GardenRed, UiKit.Red);
            MapRect(FieldSpec.GardenRed.For(Alliance.Blue), UiKit.Blue);
            MapRect(new FieldRect(-22.8f, -2.7f, -18.5f, 18.5f), new Color(0.55f, 0.15f, 0.15f));
            MapRect(new FieldRect(2.7f, 22.8f, -18.5f, 18.5f), new Color(0.15f, 0.25f, 0.55f));
        }

        static TextMeshProUGUI Line(Transform parent, float size, Color c)
        {
            var t = UiKit.Text(parent, "Line", "", size, c);
            UiKit.Size(t, size + 8f);
            return t;
        }

        Vector2 MapPoint(Vector2 fieldIn) => fieldIn / (2f * FieldSpec.WallInner) * minimap.sizeDelta.x;

        void MapRect(FieldRect r, Color c)
        {
            var img = UiKit.Box(minimap, "Zone", c);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = MapPoint(r.Center);
            rt.sizeDelta = MapPoint(r.Size);
        }

        void BuildBoard()
        {
            board = UiKit.WorldCanvas("ArenaScoreboard", transform, new Vector2(1600, 520), 3.4f, false);
            board.transform.position = Units.Field(0f, 108f, 96f);
            board.transform.rotation = Quaternion.identity;
            var bg = UiKit.Box(board.transform, "Bg", new Color(0.03f, 0.03f, 0.05f, 1f));
            UiKit.Fill(bg.rectTransform);
            var row = UiKit.Rect(board.transform, "Row");
            UiKit.Fill(row, 30, 30, 30, 30);
            UiKit.HStack(row.gameObject, 20).childForceExpandWidth = true;
            boardRed = UiKit.Text(row, "Red", "0", 220, UiKit.Red, TextAlignmentOptions.Center);
            var mid = UiKit.Rect(row, "Mid"); UiKit.VStack(mid.gameObject, 0).childAlignment = TextAnchor.MiddleCenter;
            boardClock = UiKit.Text(mid, "Clock", "2:30", 170, Color.white, TextAlignmentOptions.Center); UiKit.Size(boardClock, 200);
            boardPhase = UiKit.Text(mid, "Phase", "BIOBUZZ", 60, UiKit.Highlight, TextAlignmentOptions.Center); UiKit.Size(boardPhase, 80);
            boardBlue = UiKit.Text(row, "Blue", "0", 220, UiKit.Blue, TextAlignmentOptions.Center);
        }

        void Update()
        {
            fpsSmoothed = Mathf.Lerp(fpsSmoothed, 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f), 0.05f);
            // The scoreboard faces whichever alliance the player is on.
            var a = SettingsStore.Current.match.alliance;
            board.transform.SetPositionAndRotation(Units.Field(a == Alliance.Red ? 112f : -112f, 0f, 96f),
                Quaternion.Euler(0f, a == Alliance.Red ? 90f : -90f, 0f));
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.1f;
            Refresh();
        }

        void Refresh()
        {
            var m = MatchController.Instance;
            if (!m) return;
            var s = SettingsStore.Current;
            string phaseName = PhaseName(m);
            bool free = m.Phase == MatchPhase.FreeDrive;
            clock.text = free ? "FREE" : MatchController.FormatClock(m.DisplaySeconds);
            phase.text = phaseName;
            // Like the real field display, the score reads 0 until the match starts.
            bool pre = m.Phase == MatchPhase.PreMatch;
            redScore.text = pre ? "0" : m.Red.Total.ToString();
            blueScore.text = pre ? "0" : m.Blue.Total.ToString();
            boardClock.text = clock.text;
            boardPhase.text = phaseName;
            boardRed.text = redScore.text;
            boardBlue.text = blueScore.text;

            flowerLine.text = free ? "FLOWERS open (free drive)"
                : m.FlowersOpen ? "<color=#7CFC7C>FLOWERS OPEN</color> - NECTAR may enter FLOWERS"
                : m.Phase == MatchPhase.Teleop ? $"FLOWERS open in {MatchController.FormatClock(m.SecondsRemaining - MatchController.FlowerUnlockRemaining)} (G410)"
                : "FLOWERS open at 1:00 (G410)";

            var r = m.Player;
            if (r)
            {
                sb.Clear();
                sb.Append("Robot: ");
                int p = 0, n = 0;
                foreach (var e in r.Stored) { if (e.Kind.IsNectar()) n++; else p++; }
                sb.Append(p).Append(" POLLEN, ").Append(n).Append(" NECTAR  (").Append(r.StoredCount).Append('/').Append(r.Config.storage).Append(')');
                if (r.Config.drivetrain == DrivetrainType.Butterfly) sb.Append(r.TractionMode ? "   TRACTION" : "   MECANUM");
                if (r.Assists.fieldCentric) sb.Append("   field-centric");
                if (r.FlipFront) sb.Append("   FLIPPED");
                if (r.LastCommand.slow) sb.Append("   SLOW");
                if (r.RampDeployed) sb.Append("   RAMP DOWN");
                robotLine.text = sb.ToString();

                var hive = SimWorld.Field.HiveOf(r.Alliance);
                int need = TipModel.PollenNeeded(hive.UpNectar, s.rules) - hive.UpPollen;
                hiveLine.text = hive.IsSwinging ? "Your HIVE is TIPPING"
                    : $"Your HIVE up-CELL: {hive.UpPollen} POLLEN + {hive.UpNectar} NECTAR  ->  {Mathf.Max(0, need)} more POLLEN to tip   (tips: {hive.TipCount})";

                var h = m.HumanOf(r.Alliance);
                humanLine.text = $"Human player NECTAR: {h.Remaining} left, {(m.FlowersOpen ? h.Remaining : h.PerTipAvailable)} ready  [{Label("HumanNectar")}]";
            }

            sb.Clear();
            int start = Mathf.Max(0, m.EventLog.Count - 5);
            for (int i = start; i < m.EventLog.Count; i++) sb.AppendLine(m.EventLog[i]);
            if (m.FinalScored) sb.AppendLine(ResultsLine(m));
            log.gameObject.SetActive(s.match.showEventLog || m.FinalScored);
            log.text = sb.ToString();

            hint.text = m.Phase == MatchPhase.PreMatch ? $"[{Label("StartMatch")}] start match    [{Label("Menu")}] menu    [{Label("CameraCycle")}] camera    [{Label("Recenter")}] recenter"
                : m.Phase == MatchPhase.Ended ? $"[{Label("StartMatch")}] new match    [{Label("Restart")}] reset    [{Label("Menu")}] menu"
                : $"[{Label("Menu")}] menu    [{Label("Restart")}] reset    [{Label("CameraCycle")}] camera: {s.camera.view}";

            perf.gameObject.SetActive(s.graphics.perfOverlay != PerfOverlay.Off);
            if (s.graphics.perfOverlay == PerfOverlay.Fps) perf.text = $"{fpsSmoothed:0} fps";
            else if (s.graphics.perfOverlay == PerfOverlay.Full)
                perf.text = $"{fpsSmoothed:0} fps  {1000f / fpsSmoothed:0.0} ms\n{(ViewManager.Instance && ViewManager.Instance.XrActive ? "VR" : "Desktop")}  scale {s.graphics.renderScale:0.00}\nbodies {GameElement.All.Count + SimWorld.Robots.Count}";

            minimap.gameObject.SetActive(s.graphics.minimap);
            if (s.graphics.minimap) UpdateMinimap();
        }

        void UpdateMinimap()
        {
            var robots = SimWorld.Robots;
            while (robotDots.Count < robots.Count)
            {
                var dot = UiKit.Box(minimap, "Robot", Color.white).rectTransform;
                dot.anchorMin = dot.anchorMax = new Vector2(0.5f, 0.5f);
                robotDots.Add(dot);
            }
            for (int i = 0; i < robotDots.Count; i++)
            {
                bool on = i < robots.Count;
                robotDots[i].gameObject.SetActive(on);
                if (!on) continue;
                var r = robots[i];
                var fp = r.Footprint();
                robotDots[i].anchoredPosition = MapPoint(fp.center);
                robotDots[i].sizeDelta = MapPoint(new Vector2(r.Config.widthIn, r.Config.lengthIn));
                robotDots[i].localRotation = Quaternion.Euler(0f, 0f, -r.transform.eulerAngles.y);
                robotDots[i].GetComponent<Image>().color = r.IsPlayer ? UiKit.Highlight : r.Alliance == Alliance.Red ? UiKit.Red : UiKit.Blue;
            }
        }

        static string Label(string action) => InputHub.Instance ? InputHub.Instance.BindingLabel(action) : action;

        static string PhaseName(MatchController m)
        {
            switch (m.Phase)
            {
                case MatchPhase.PreMatch: return "PRE-MATCH";
                case MatchPhase.Auto: return "AUTO";
                case MatchPhase.Transition: return "PICK UP CONTROLLERS";
                case MatchPhase.Teleop: return m.SecondsRemaining <= 20f ? "TELEOP - FINAL 20" : "TELEOP";
                case MatchPhase.Settling: return "COMING TO REST";
                case MatchPhase.Ended: return "FINAL";
                default: return "FREE DRIVE";
            }
        }

        static string ResultsLine(MatchController m)
        {
            string Row(string name, AllianceScore a, AllianceScore o) =>
                $"{name}: {a.Total}  (auto {a.AutoPoints}, tips {a.Tips}, cell {a.cellElements}, flower {a.ownedFlowerElements}+{a.bottomBonuses} bonus, garden {a.gardenElements}, park {a.ParkPoints}, fouls +{a.FoulPoints})  RP {a.RankingPoints(o)}";
            return Row("RED", m.Red, m.Blue) + "\n" + Row("BLUE", m.Blue, m.Red);
        }
    }
}
