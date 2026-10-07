using System;
using System.Collections.Generic;
using UnityEngine;

namespace VrFsim.Game
{
    /// <summary>
    /// A FLOWER on the perimeter wall. Tracks which elements are at least partially inside its
    /// scoring volume (between the top ring and the middle ring, §10.5.2) and reports each NECTAR
    /// that newly enters it, so the match can apply G410.
    /// </summary>
    public class Flower : MonoBehaviour
    {
        public int index;
        public Vector2 centerIn;   // bore centre, field plane
        public Vector2 inward;     // from the wall into the field

        public FlowerResult Result { get; private set; }
        public readonly List<GameElement> Scoring = new List<GameElement>(12);

        /// <summary>Raised for each element that newly enters the scoring volume.</summary>
        public event Action<Flower, GameElement> ElementEntered;

        readonly HashSet<GameElement> inside = new HashSet<GameElement>();
        readonly List<FlowerEntry> entries = new List<FlowerEntry>(12);
        readonly List<GameElement> stale = new List<GameElement>(4);

        const float BoreRadiusIn = FieldSpec.FlowerTopBore * 0.5f + 0.4f;

        public Vector3 TopWorld => Units.Field(centerIn.x, centerIn.y, FieldSpec.FlowerTopRingTop);

        public bool InScoringVolume(GameElement e)
        {
            if (!e.IsFree) return false;
            Vector2 p = e.FieldPlaneIn;
            if ((p - centerIn).sqrMagnitude > BoreRadiusIn * BoreRadiusIn) return false;
            float z = e.HeightIn, r = e.RadiusIn;
            return z + r > FieldSpec.FlowerScoreZMin && z - r < FieldSpec.FlowerScoreZMax;
        }

        /// <summary>The lowest POLLEN sitting in the retrieval opening, if any.</summary>
        public GameElement BottomPollen()
        {
            GameElement best = null;
            float bestZ = float.MaxValue;
            foreach (var e in GameElement.All)
            {
                if (!e.IsFree || e.Kind != ElementKind.Pollen) continue;
                if ((e.FieldPlaneIn - centerIn).sqrMagnitude > BoreRadiusIn * BoreRadiusIn) continue;
                float z = e.HeightIn;
                if (z < FieldSpec.FlowerMidRingBottom && z < bestZ) { best = e; bestZ = z; }
            }
            return best;
        }

        public void ResetState()
        {
            inside.Clear();
            Scoring.Clear();
            Result = default;
        }

        void FixedUpdate()
        {
            Scoring.Clear();
            entries.Clear();
            foreach (var e in GameElement.All)
            {
                if (!InScoringVolume(e)) continue;
                Scoring.Add(e);
                entries.Add(new FlowerEntry(e.Kind, e.HeightIn));
                if (inside.Add(e)) ElementEntered?.Invoke(this, e);
            }
            stale.Clear();
            foreach (var e in inside) if (!e || !Scoring.Contains(e)) stale.Add(e);
            foreach (var e in stale) inside.Remove(e);
            Result = FlowerRules.Evaluate(entries);
        }
    }
}
