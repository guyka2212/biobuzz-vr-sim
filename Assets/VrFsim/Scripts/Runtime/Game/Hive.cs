using System;
using UnityEngine;
using VrFsim.Settings;

namespace VrFsim.Game
{
    /// <summary>
    /// One alliance's HIVE: a bar with a CELL at each end, pivoting 43.95 in above the tiles and
    /// resting 30° one way or the other. The tray is a kinematic rigidbody, so launched elements
    /// collide with the real cell geometry, and a tip physically dumps the contents.
    ///
    /// Tray frame: local X across the cell, local Y = "w" (up when level), local Z = "v" along the
    /// bar (field +y). A cell's sign is +1 for the +y (rear) end, −1 for the −y (audience) end.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Hive : MonoBehaviour
    {
        public Alliance alliance;

        /// <summary>Sign (±1) of the cell that is currently up.</summary>
        public int UpSign { get; private set; } = -1;
        public bool IsSwinging { get; private set; }
        public int TipCount { get; private set; }

        public int UpPollen { get; private set; }
        public int UpNectar { get; private set; }

        /// <summary>Raised when the moving damper meets the frame (§10.5.1 B).</summary>
        public event Action<Hive> Tipped;

        Rigidbody body;
        float swingStart, swingFrom, swingTo;
        float loadAboveThresholdSince = -1f;
        const float SettleSeconds = 0.25f;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        public static float AngleFor(int upSign) => -upSign * FieldSpec.HiveTiltDeg;

        public void ResetTo(int upSign)
        {
            UpSign = upSign;
            IsSwinging = false;
            TipCount = 0;
            loadAboveThresholdSince = -1f;
            var r = Quaternion.Euler(AngleFor(upSign), 0f, 0f);
            transform.localRotation = r;
            if (body) body.rotation = transform.rotation;
        }

        /// <summary>
        /// The point a launch should pass through: just inside the middle of the up-CELL's open mouth
        /// (about 58 in above the tiles when the cell is up). Aiming here keeps the arc clear of
        /// both the mouth's bottom lip and its roof edge.
        /// </summary>
        public Vector3 UpCellAimPoint()
        {
            float v = UpSign * (FieldSpec.CellOuterV - 2.5f);
            const float w = 5.5f;
            return transform.TransformPoint(new Vector3(0f, Units.In(w), Units.In(v)));
        }

        /// <summary>Is a world point inside the cell with the given sign (prism test, inches)?</summary>
        public bool InCell(Vector3 world, int sign, float marginIn = 0f)
        {
            Vector3 p = transform.InverseTransformPoint(world) / Units.MetersPerInch;
            float v = p.z * sign, w = p.y, x = Mathf.Abs(p.x);
            if (v < FieldSpec.CellInnerV - marginIn || v > FieldSpec.CellOuterV + marginIn) return false;
            if (x > FieldSpec.CellHalfWidth + marginIn || w < FieldSpec.CellFloorW - marginIn) return false;
            float roof = FieldSpec.CellEaveW + (FieldSpec.CellHalfWidth - Mathf.Min(x, FieldSpec.CellHalfWidth)) *
                ((FieldSpec.CellPeakW - FieldSpec.CellEaveW) / FieldSpec.CellHalfWidth);
            return w <= roof + marginIn;
        }

        /// <summary>Count free elements in a cell.</summary>
        public void CountCell(int sign, out int pollen, out int nectar)
        {
            pollen = nectar = 0;
            foreach (var e in GameElement.All)
            {
                if (!e.IsFree || !InCell(e.Position, sign)) continue;
                if (e.Kind.IsNectar()) nectar++; else pollen++;
            }
        }

        public int UpCellElementCount()
        {
            CountCell(UpSign, out int p, out int n);
            return p + n;
        }

        void FixedUpdate()
        {
            if (IsSwinging)
            {
                float T = Mathf.Max(0.1f, SettingsStore.Current.rules.hiveSwingSeconds);
                float u = Mathf.Clamp01((Time.time - swingStart) / T);
                // A seesaw past its balance point accelerates until the damper stops it.
                float eased = u * u;
                float angle = Mathf.Lerp(swingFrom, swingTo, eased);
                body.MoveRotation(transform.parent.rotation * Quaternion.Euler(angle, 0f, 0f));
                if (u >= 1f)
                {
                    IsSwinging = false;
                    TipCount++;
                    Tipped?.Invoke(this);
                }
                return;
            }

            CountCell(UpSign, out int p, out int n);
            UpPollen = p; UpNectar = n;
            if (TipModel.Tips(p, n, SettingsStore.Current.rules))
            {
                if (loadAboveThresholdSince < 0f) loadAboveThresholdSince = Time.time;
                else if (Time.time - loadAboveThresholdSince >= SettleSeconds) BeginSwing();
            }
            else loadAboveThresholdSince = -1f;
        }

        void BeginSwing()
        {
            IsSwinging = true;
            swingStart = Time.time;
            swingFrom = AngleFor(UpSign);
            UpSign = -UpSign;
            swingTo = AngleFor(UpSign);
            loadAboveThresholdSince = -1f;
        }
    }
}
