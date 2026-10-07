using System.Collections.Generic;
using UnityEngine;

namespace VrFsim
{
    /// <summary>Small procedural meshes for the greybox and for parts no FBX replaces.</summary>
    public static class MeshUtil
    {
        /// <summary>
        /// Extrude a convex polygon (in the XY plane) along +Z from z0 to z1 as a shell: side faces
        /// facing both ways, the z0 end capped, the z1 end open. Used for the HIVE cells.
        /// </summary>
        public static Mesh OpenPrism(IList<Vector2> poly, float z0, float z1, string name)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            int n = poly.Count;
            // Each face gets its own vertices on each side, so normals stay crisp and never cancel out.
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int s0 = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                t.AddRange(new[] { s0, s0 + 1, s0 + 2, s0, s0 + 2, s0 + 3 });
                int s1 = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                t.AddRange(new[] { s1, s1 + 2, s1 + 1, s1, s1 + 3, s1 + 2 });
            }
            for (int i = 0; i < n; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % n];
                Quad(new Vector3(a.x, a.y, z0), new Vector3(b.x, b.y, z0), new Vector3(b.x, b.y, z1), new Vector3(a.x, a.y, z1));
            }
            for (int side = 0; side < 2; side++)
            {
                int c = v.Count;
                foreach (var p in poly) v.Add(new Vector3(p.x, p.y, z0));
                for (int i = 1; i < n - 1; i++)
                    t.AddRange(side == 0 ? new[] { c, c + i, c + i + 1 } : new[] { c, c + i + 1, c + i });
            }
            var m = new Mesh { name = name };
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Low-poly UV sphere (8 × 6 by default ≈ 80 triangles) for scoring elements.</summary>
        public static Mesh Sphere(int lon = 10, int lat = 7, string name = "LowSphere")
        {
            var v = new List<Vector3>();
            var nrm = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            for (int y = 0; y <= lat; y++)
            {
                float a = Mathf.PI * y / lat;
                for (int x = 0; x <= lon; x++)
                {
                    float b = 2f * Mathf.PI * x / lon;
                    var p = new Vector3(Mathf.Sin(a) * Mathf.Cos(b), Mathf.Cos(a), Mathf.Sin(a) * Mathf.Sin(b));
                    v.Add(p * 0.5f); nrm.Add(p); uv.Add(new Vector2((float)x / lon, (float)y / lat));
                }
            }
            for (int y = 0; y < lat; y++)
                for (int x = 0; x < lon; x++)
                {
                    int i = y * (lon + 1) + x;
                    t.AddRange(new[] { i, i + 1, i + lon + 1, i + 1, i + lon + 2, i + lon + 1 });
                }
            var m = new Mesh { name = name };
            m.SetVertices(v); m.SetNormals(nrm); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
