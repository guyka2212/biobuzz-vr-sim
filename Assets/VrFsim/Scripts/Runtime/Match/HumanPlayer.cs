using System.Collections.Generic;
using UnityEngine;
using VrFsim.Game;

namespace VrFsim.Match
{
    /// <summary>
    /// An alliance's HUMAN PLAYER and the NECTAR staged in its ALLIANCE AREA. G426: one NECTAR may
    /// be entered per TIP of that alliance's HIVE, and all remaining NECTAR once 60 s or less remain.
    /// G427: entered by hand, onto the tiles of the alliance's own LOADING ZONE.
    /// </summary>
    public class HumanPlayer
    {
        public readonly Alliance alliance;
        public int tipsEarned;
        int perTipEntered;
        readonly List<GameElement> stock = new List<GameElement>(8);
        Transform station;
        readonly List<Transform> rack = new List<Transform>(8);

        public HumanPlayer(Alliance a) { alliance = a; }

        public int Remaining => stock.Count;
        public int PerTipAvailable => Mathf.Max(0, tipsEarned - perTipEntered);

        public void Reset()
        {
            tipsEarned = 0;
            perTipEntered = 0;
            stock.Clear();
        }

        public bool CanEnter(bool allRemainingOpen) => stock.Count > 0 && (allRemainingOpen || PerTipAvailable > 0);

        public void Stock(List<GameElement> nectar)
        {
            foreach (var e in nectar) Return(e);
        }

        /// <summary>Put an element on the alliance-area rack (held, out of play).</summary>
        public void Return(GameElement e)
        {
            EnsureStation();
            if (!stock.Contains(e)) stock.Add(e);
            int i = stock.IndexOf(e);
            e.Grab(null, alliance, rack[Mathf.Min(i, rack.Count - 1)]);
        }

        public GameElement Enter(Vector3 at)
        {
            if (stock.Count == 0) return null;
            var e = stock[stock.Count - 1];
            stock.RemoveAt(stock.Count - 1);
            if (tipsEarned > perTipEntered) perTipEntered++;
            e.PlaceAt(at);
            return e;
        }

        void EnsureStation()
        {
            if (station) return;
            // A small rack in the ALLIANCE AREA, beside the LOADING ZONE, at table height.
            var lz = FieldSpec.LoadingZoneRed.For(alliance);
            float x = alliance == Alliance.Red ? -FieldSpec.WallInner - 8f : FieldSpec.WallInner + 8f;
            station = new GameObject($"HumanStation_{alliance}").transform;
            station.position = Units.Field(x, lz.Center.y, 30f);
            // A small table under the rack, so the NECTAR does not float.
            var lib = MaterialLibrary.Get();
            FieldBuilder.Visual(station, "Table", Units.Field(2f, 0f, -2.3f), Units.Size(10f, 18f, 1f), Quaternion.identity, lib.wallFrame);
            FieldBuilder.Visual(station, "TableLeg", Units.Field(2f, 0f, -16.4f), Units.Size(2f, 2f, 27.4f), Quaternion.identity, lib.robotDark);
            for (int i = 0; i < 8; i++)
            {
                var t = new GameObject("Rack" + i).transform;
                t.SetParent(station, false);
                t.localPosition = Units.Field((i / 4) * 4f, (i % 4 - 1.5f) * 4f, 0f);
                rack.Add(t);
            }
        }
    }
}
