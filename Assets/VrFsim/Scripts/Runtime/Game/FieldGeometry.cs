using UnityEngine;

namespace VrFsim.Game
{
    /// <summary>An oriented rectangle on the field plane (inches): a robot footprint.</summary>
    public struct FieldObb
    {
        public Vector2 center, axisX, axisY; // axisY = robot forward
        public float halfX, halfY;

        public Vector2 Corner(int i)
        {
            float sx = (i & 1) == 0 ? -1f : 1f, sy = (i & 2) == 0 ? -1f : 1f;
            return center + axisX * (sx * halfX) + axisY * (sy * halfY);
        }

        public bool Contains(Vector2 p)
        {
            Vector2 d = p - center;
            return Mathf.Abs(Vector2.Dot(d, axisX)) <= halfX && Mathf.Abs(Vector2.Dot(d, axisY)) <= halfY;
        }

        /// <summary>Separating-axis overlap test with an axis-aligned field rectangle.</summary>
        public bool Overlaps(FieldRect r)
        {
            var axes = new[] { Vector2.right, Vector2.up, axisX, axisY };
            Vector2 rc = r.Center, rh = r.Size * 0.5f;
            foreach (var ax in axes)
            {
                float obbR = halfX * Mathf.Abs(Vector2.Dot(axisX, ax)) + halfY * Mathf.Abs(Vector2.Dot(axisY, ax));
                float rectR = rh.x * Mathf.Abs(ax.x) + rh.y * Mathf.Abs(ax.y);
                if (Mathf.Abs(Vector2.Dot(center - rc, ax)) > obbR + rectR) return false;
            }
            return true;
        }

        /// <summary>Is any corner within <paramref name="tol"/> of (or past) a perimeter wall?</summary>
        public bool TouchesWall(float tol = 0.35f)
        {
            float lim = FieldSpec.WallInner - tol;
            for (int i = 0; i < 4; i++)
            {
                var c = Corner(i);
                if (Mathf.Abs(c.x) >= lim || Mathf.Abs(c.y) >= lim) return true;
            }
            return false;
        }

        /// <summary>Fully on one alliance's half (red x &lt; 0, blue x &gt; 0)?</summary>
        public bool FullyOnSide(Alliance a)
        {
            for (int i = 0; i < 4; i++)
            {
                float x = Corner(i).x;
                if (a == Alliance.Red ? x > 0f : x < 0f) return false;
            }
            return true;
        }

        public bool FullyOnOpponentSide(Alliance a) => FullyOnSide(a.Opponent());
    }
}
