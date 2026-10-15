using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Accertone.EditorTools
{
    /// <summary>Procedural meshes for the ACCERTONE showroom (podium, LED rings, light shafts, sign prisms).</summary>
    public static class AccertoneMeshFactory
    {
        public static Mesh SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static void AddQuad(List<int> tris, List<Vector3> v, int a, int b, int c, int d, Vector3 intended)
        {
            Vector3 n = Vector3.Cross(v[c] - v[a], v[d] - v[b]);
            if (Vector3.Dot(n, intended) >= 0f)
            {
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(a); tris.Add(c); tris.Add(d);
            }
            else
            {
                tris.Add(a); tris.Add(d); tris.Add(c);
                tris.Add(a); tris.Add(c); tris.Add(b);
            }
        }

        static Mesh Finish(string name, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)
        {
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        /// <summary>Revolves a (radius, height) profile around Y with hard edges between profile segments.
        /// Order the profile from the axis outward/downward so normals face outward.</summary>
        public static Mesh Lathe(string name, Vector2[] profile, int segments)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();

            var cum = new float[profile.Length];
            for (int i = 1; i < profile.Length; i++) cum[i] = cum[i - 1] + Vector2.Distance(profile[i - 1], profile[i]);
            float total = Mathf.Max(1e-4f, cum[profile.Length - 1]);

            for (int s = 0; s < profile.Length - 1; s++)
            {
                Vector2 a = profile[s], b = profile[s + 1];
                Vector2 d = (b - a).normalized;
                Vector2 nn = new Vector2(-d.y, d.x);
                int start = v.Count;
                for (int i = 0; i <= segments; i++)
                {
                    float ang = i / (float)segments * Mathf.PI * 2f;
                    float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                    v.Add(new Vector3(a.x * cs, a.y, a.x * sn));
                    v.Add(new Vector3(b.x * cs, b.y, b.x * sn));
                    var normal = new Vector3(nn.x * cs, nn.y, nn.x * sn).normalized;
                    n.Add(normal);
                    n.Add(normal);
                    uv.Add(new Vector2(i / (float)segments, cum[s] / total));
                    uv.Add(new Vector2(i / (float)segments, cum[s + 1] / total));
                }
                for (int i = 0; i < segments; i++)
                {
                    int a0 = start + i * 2, b0 = a0 + 1, a1 = a0 + 2, b1 = a0 + 3;
                    float mid = (i + 0.5f) / segments * Mathf.PI * 2f;
                    var intended = new Vector3(nn.x * Mathf.Cos(mid), nn.y, nn.x * Mathf.Sin(mid));
                    AddQuad(t, v, a0, b0, b1, a1, intended);
                }
            }
            return Finish(name, v, n, uv, t);
        }

        /// <summary>Flat ring in the XZ plane facing +Y.</summary>
        public static Mesh Annulus(string name, float inner, float outer, int segments)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float ang = i / (float)segments * Mathf.PI * 2f;
                float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                v.Add(new Vector3(inner * cs, 0f, inner * sn));
                v.Add(new Vector3(outer * cs, 0f, outer * sn));
                n.Add(Vector3.up);
                n.Add(Vector3.up);
                uv.Add(new Vector2(i / (float)segments, 0f));
                uv.Add(new Vector2(i / (float)segments, 1f));
            }
            for (int i = 0; i < segments; i++)
            {
                int a0 = i * 2, b0 = a0 + 1, a1 = a0 + 2, b1 = a0 + 3;
                AddQuad(t, v, a0, b0, b1, a1, Vector3.up);
            }
            return Finish(name, v, n, uv, t);
        }

        /// <summary>Radial dial ticks around a turntable edge (every 5 degrees, longer every 30).</summary>
        public static Mesh DialTicks(string name, float outerRadius, float shortLen, float longLen, float width, int count, int majorEvery)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            for (int i = 0; i < count; i++)
            {
                float ang = i / (float)count * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                var side = new Vector3(-dir.z, 0f, dir.x) * (width * 0.5f);
                float len = i % majorEvery == 0 ? longLen : shortLen;
                Vector3 o = dir * outerRadius, inn = dir * (outerRadius - len);
                int s = v.Count;
                v.Add(inn - side); v.Add(inn + side); v.Add(o + side); v.Add(o - side);
                for (int k = 0; k < 4; k++) n.Add(Vector3.up);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
                AddQuad(t, v, s, s + 1, s + 2, s + 3, Vector3.up);
            }
            return Finish(name, v, n, uv, t);
        }

        /// <summary>Extrudes a convex outline (XY, front face toward -Z) by depth along +Z.</summary>
        public static Mesh Prism(string name, Vector2[] outline, float depth)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            int count = outline.Length;
            Vector2 centre = Vector2.zero;
            foreach (var p in outline) centre += p;
            centre /= count;

            // Front (-Z) and back (+Z) caps.
            for (int cap = 0; cap < 2; cap++)
            {
                float z = cap == 0 ? 0f : depth;
                Vector3 normal = cap == 0 ? Vector3.back : Vector3.forward;
                int c = v.Count;
                v.Add(new Vector3(centre.x, centre.y, z)); n.Add(normal); uv.Add(new Vector2(0.5f, 0.5f));
                for (int i = 0; i < count; i++)
                {
                    v.Add(new Vector3(outline[i].x, outline[i].y, z)); n.Add(normal); uv.Add(outline[i]);
                }
                for (int i = 0; i < count; i++)
                {
                    int a = c + 1 + i, b = c + 1 + (i + 1) % count;
                    Vector3 fn = Vector3.Cross(v[a] - v[c], v[b] - v[c]);
                    if (Vector3.Dot(fn, normal) >= 0f) { t.Add(c); t.Add(a); t.Add(b); }
                    else { t.Add(c); t.Add(b); t.Add(a); }
                }
            }

            // Sides.
            for (int i = 0; i < count; i++)
            {
                Vector2 p0 = outline[i], p1 = outline[(i + 1) % count];
                Vector2 edge = (p1 - p0).normalized;
                Vector2 out2 = new Vector2(edge.y, -edge.x);
                if (Vector2.Dot(out2, (p0 + p1) * 0.5f - centre) < 0f) out2 = -out2;
                var normal = new Vector3(out2.x, out2.y, 0f);
                int s = v.Count;
                v.Add(new Vector3(p0.x, p0.y, 0f)); v.Add(new Vector3(p1.x, p1.y, 0f));
                v.Add(new Vector3(p1.x, p1.y, depth)); v.Add(new Vector3(p0.x, p0.y, depth));
                for (int k = 0; k < 4; k++) n.Add(normal);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
                AddQuad(t, v, s, s + 1, s + 2, s + 3, normal);
            }
            return Finish(name, v, n, uv, t);
        }

        /// <summary>Unit quad in XY facing -Z (centred).</summary>
        public static Mesh Quad(string name)
        {
            var v = new List<Vector3> { new Vector3(-0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(0.5f, -0.5f, 0) };
            var n = new List<Vector3> { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            var uv = new List<Vector2> { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            var t = new List<int>();
            AddQuad(t, v, 0, 1, 2, 3, Vector3.back);
            return Finish(name, v, n, uv, t);
        }
    }
}
