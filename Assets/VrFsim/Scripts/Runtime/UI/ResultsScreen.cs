using TMPro;
using UnityEngine;
using VrFsim.Game;
using VrFsim.Input;
using VrFsim.Match;
using VrFsim.VR;

namespace VrFsim.UI
{
    /// <summary>
    /// End-of-match results: appears in front of the player once the field is at rest and the
    /// final score is assessed. Shows each alliance's points line by line (Table 10-2), fouls,
    /// totals and ranking points (Table 10-3).
    /// </summary>
    public class ResultsScreen : MonoBehaviour
    {
        Canvas canvas;
        TextMeshProUGUI banner, red, blue, labels, footer;
        bool shown;

        void Start() => Build();

        void Build()
        {
            canvas = UiKit.WorldCanvas("MatchResults", transform, new Vector2(1100, 780), 1.05f, false);
            canvas.sortingOrder = 5;
            var bg = UiKit.Box(canvas.transform, "Bg", new Color(0.07f, 0.08f, 0.1f, 1f)); // opaque: nothing shows through
            UiKit.Fill(bg.rectTransform);

            banner = UiKit.Text(canvas.transform, "Banner", "", 46, UiKit.Highlight, TextAlignmentOptions.Center);
            var b = banner.rectTransform;
            b.anchorMin = new Vector2(0, 1); b.anchorMax = new Vector2(1, 1); b.pivot = new Vector2(0.5f, 1);
            b.sizeDelta = new Vector2(-40, 70); b.anchoredPosition = new Vector2(0, -16);

            var table = UiKit.Rect(canvas.transform, "Table");
            UiKit.Fill(table, 30, 100, 30, 70);
            UiKit.HStack(table.gameObject, 10).childForceExpandWidth = false;
            labels = UiKit.Text(table, "Labels", "", 24, UiKit.TextMain); UiKit.Size(labels, -1, 520);
            red = UiKit.Text(table, "Red", "", 24, UiKit.Red, TextAlignmentOptions.Right); UiKit.Size(red, -1, 230);
            blue = UiKit.Text(table, "Blue", "", 24, UiKit.Blue, TextAlignmentOptions.Right); UiKit.Size(blue, -1, 230);
            foreach (var t in new[] { labels, red, blue }) { t.alignment = t == labels ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.TopRight; t.lineSpacing = 6f; }

            footer = UiKit.Text(canvas.transform, "Footer", "", 22, UiKit.TextDim, TextAlignmentOptions.Center);
            var f = footer.rectTransform;
            f.anchorMin = new Vector2(0, 0); f.anchorMax = new Vector2(1, 0); f.pivot = new Vector2(0.5f, 0);
            f.sizeDelta = new Vector2(-40, 50); f.anchoredPosition = new Vector2(0, 14);
            canvas.gameObject.SetActive(false);
        }

        void Update()
        {
            var m = MatchController.Instance;
            bool show = m && m.Phase == MatchPhase.Ended && m.FinalScored;
            if (show && !shown) Show(m);
            if (!show && shown) { canvas.gameObject.SetActive(false); shown = false; }
        }

        void Show(MatchController m)
        {
            shown = true;
            Fill(m);
            var vm = ViewManager.Instance;
            if (vm)
            {
                Vector3 fwd = vm.Head.forward; fwd.y = 0f;
                if (fwd.sqrMagnitude < 1e-3f) fwd = vm.Origin.transform.forward;
                fwd.Normalize();
                canvas.transform.position = vm.Head.position + fwd * 1.3f + Vector3.up * 0.05f;
                canvas.transform.rotation = Quaternion.LookRotation(fwd);
            }
            canvas.gameObject.SetActive(true);
        }

        static string Line(int count, int each) => count == 0 ? "-" : $"{count} x {each} = {count * each}";

        void Fill(MatchController m)
        {
            AllianceScore r = m.Red, b = m.Blue;
            banner.text = r.Total == b.Total ? $"TIE  {r.Total} - {b.Total}"
                : r.Total > b.Total ? $"<color=#E63333>RED WINS</color>  {r.Total} - {b.Total}"
                : $"<color=#4080FF>BLUE WINS</color>  {b.Total} - {r.Total}";

            labels.text =
                "<b>AUTO</b>\n" +
                "  LEAVE\n" +
                "  PARK in LOADING ZONE\n" +
                "  HIVE TIPS\n" +
                "<b>TELEOP</b>\n" +
                "  HIVE TIPS\n" +
                "  Elements left in up-CELL\n" +
                "  Elements in owned FLOWERS\n" +
                "  Bottom NECTAR bonus\n" +
                "  Elements in GARDEN\n" +
                "  PARK in LOADING ZONE\n" +
                "<b>FOULS</b> (opponent's MINOR / MAJOR)\n" +
                "<b>TOTAL</b>\n" +
                "<b>RANKING POINTS</b>\n" +
                "  Win 3 / Tie 1\n" +
                "  SWARM (LEAVE + PARK >= 16)\n" +
                "  POLLINATOR 1 (4 TIPS)  /  2 (7 TIPS)\n" +
                "  <b>Total RP</b>";

            red.text = Column(r, b);
            blue.text = Column(b, r);
            footer.text = $"[{Label("StartMatch")}] next match     [{Label("Restart")}] reset     [{Label("Menu")}] menu";
        }

        static string Column(AllianceScore a, AllianceScore o)
        {
            int win = a.Total > o.Total ? Points.WinRp : a.Total == o.Total ? Points.TieRp : 0;
            string Yes(bool v) => v ? "1" : "0";
            return
                $"<b>{a.AutoPoints}</b>\n" +
                $"{Line(a.leaveCount, Points.Leave)}\n" +
                $"{Line(a.autoParkCount, Points.Park)}\n" +
                $"{Line(a.autoTips, Points.HiveTip)}\n" +
                $"<b>{a.TeleopPoints}</b>\n" +
                $"{Line(a.teleopTips, Points.HiveTip)}\n" +
                $"{Line(a.cellElements, Points.CellElement)}\n" +
                $"{Line(a.ownedFlowerElements, Points.OwnedFlowerElement)}\n" +
                $"{Line(a.bottomBonuses, Points.BottomNectarBonus)}\n" +
                $"{Line(a.gardenElements, Points.GardenElement)}\n" +
                $"{Line(a.teleopParkCount, Points.Park)}\n" +
                $"<b>{a.FoulPoints}</b> ({a.minorFoulsAgainstOpponent} / {a.majorFoulsAgainstOpponent})\n" +
                $"<b>{a.Total}</b>\n" +
                "\n" +
                $"{win}\n" +
                $"{Yes(a.SwarmRp)}\n" +
                $"{Yes(a.Pollinator1Rp)}  /  {Yes(a.Pollinator2Rp)}\n" +
                $"<b>{a.RankingPoints(o)}</b>";
        }

        static string Label(string action) => InputHub.Instance ? InputHub.Instance.BindingLabel(action) : action;
    }
}
