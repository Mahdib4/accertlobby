using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Accertone.LobbyModule.EditorTools
{
    /// <summary>A livery evaluated in 3D: position/normal in "design space" -> colour (+ emission).</summary>
    public interface ILiveryDesign
    {
        void Evaluate(Vector3 p, Vector3 n, ref Color baseColor, ref Color emission);
    }

    public struct BakeTarget
    {
        public Renderer renderer;
        public Mesh mesh;
        public int subMesh;
    }

    /// <summary>
    /// Bakes a 3D livery into a mesh's existing UV atlas: every triangle is rasterised in UV space and
    /// each covered texel is shaded from its interpolated design-space position and normal.
    /// This lets graphics (flames, logos, numbers) flow continuously across the body regardless of how
    /// the source model's UVs were laid out.
    /// </summary>
    public static class LiveryBaker
    {
        public static int Bake(Transform designSpace, List<BakeTarget> targets, int size, ILiveryDesign design, Color32[] baseColor, Color32[] emission)
        {
            var written = new bool[size * size];
            int count = 0;
            foreach (var t in targets)
            {
                var mesh = t.mesh;
                var verts = mesh.vertices;
                var norms = mesh.normals;
                var uvs = mesh.uv;
                if (uvs == null || uvs.Length != verts.Length) continue;
                Matrix4x4 m = designSpace.worldToLocalMatrix * t.renderer.transform.localToWorldMatrix;
                var P = new Vector3[verts.Length];
                var N = new Vector3[verts.Length];
                for (int i = 0; i < verts.Length; i++)
                {
                    P[i] = m.MultiplyPoint3x4(verts[i]);
                    N[i] = (norms != null && norms.Length == verts.Length ? m.MultiplyVector(norms[i]) : Vector3.up).normalized;
                }
                var tris = mesh.GetTriangles(t.subMesh);
                for (int k = 0; k < tris.Length; k += 3)
                    count += Raster(tris[k], tris[k + 1], tris[k + 2], uvs, P, N, size, design, baseColor, emission, written);
            }
            Dilate(baseColor, emission, written, size, 4);
            return count;
        }

        static float Edge(Vector2 a, Vector2 b, Vector2 p)
        {
            return (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
        }

        static int Raster(int i0, int i1, int i2, Vector2[] uvs, Vector3[] P, Vector3[] N, int size,
            ILiveryDesign design, Color32[] baseColor, Color32[] emission, bool[] written)
        {
            Vector2 a = uvs[i0] * size - new Vector2(0.5f, 0.5f);
            Vector2 b = uvs[i1] * size - new Vector2(0.5f, 0.5f);
            Vector2 c = uvs[i2] * size - new Vector2(0.5f, 0.5f);
            float area = Edge(a, b, c);
            if (Mathf.Abs(area) < 1e-7f) return 0;

            const float pad = 0.85f; // texels of conservative coverage to avoid seams
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - 1f));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)) + 1f));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)) - 1f));
            int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)) + 1f));
            if (maxX < minX || maxY < minY) return 0;

            float lbc = Vector2.Distance(b, c), lca = Vector2.Distance(c, a), lab = Vector2.Distance(a, b);
            float sign = Mathf.Sign(area);
            int n = 0;
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var p = new Vector2(x, y);
                    float e0 = Edge(b, c, p) * sign;
                    float e1 = Edge(c, a, p) * sign;
                    float e2 = Edge(a, b, p) * sign;
                    if (e0 < -pad * lbc || e1 < -pad * lca || e2 < -pad * lab) continue;

                    float w0 = Mathf.Max(0f, e0), w1 = Mathf.Max(0f, e1), w2 = Mathf.Max(0f, e2);
                    float sum = w0 + w1 + w2;
                    if (sum <= 0f) { w0 = w1 = w2 = 1f / 3f; }
                    else { w0 /= sum; w1 /= sum; w2 /= sum; }

                    Vector3 pos = P[i0] * w0 + P[i1] * w1 + P[i2] * w2;
                    Vector3 nor = (N[i0] * w0 + N[i1] * w1 + N[i2] * w2).normalized;
                    int idx = y * size + x;
                    Color col = baseColor[idx];
                    Color emi = emission != null ? (Color)emission[idx] : Color.black;
                    design.Evaluate(pos, nor, ref col, ref emi);
                    baseColor[idx] = col;
                    if (emission != null) emission[idx] = emi;
                    written[idx] = true;
                    n++;
                }
            }
            return n;
        }

        /// <summary>Grows written texels into empty neighbours so mip-mapping never pulls in the gutter.</summary>
        static void Dilate(Color32[] col, Color32[] emi, bool[] written, int size, int passes)
        {
            var next = new bool[written.Length];
            for (int pass = 0; pass < passes; pass++)
            {
                System.Array.Copy(written, next, written.Length);
                for (int y = 0; y < size; y++)
                {
                    int row = y * size;
                    for (int x = 0; x < size; x++)
                    {
                        int i = row + x;
                        if (written[i]) continue;
                        int src = -1;
                        if (x > 0 && written[i - 1]) src = i - 1;
                        else if (x < size - 1 && written[i + 1]) src = i + 1;
                        else if (y > 0 && written[i - size]) src = i - size;
                        else if (y < size - 1 && written[i + size]) src = i + size;
                        if (src < 0) continue;
                        col[i] = col[src];
                        if (emi != null) emi[i] = emi[src];
                        next[i] = true;
                    }
                }
                System.Array.Copy(next, written, written.Length);
            }
        }

        public static Color32[] Fill(int size, Color color)
        {
            var arr = new Color32[size * size];
            Color32 c = color;
            for (int i = 0; i < arr.Length; i++) arr[i] = c;
            return arr;
        }

        public static bool AnyNonBlack(Color32[] arr)
        {
            for (int i = 0; i < arr.Length; i += 7)
                if (arr[i].r > 2 || arr[i].g > 2 || arr[i].b > 2) return true;
            return false;
        }

        /// <summary>Reads an image file into a CPU-readable texture (no importer changes needed).</summary>
        public static Texture2D LoadReadable(string assetPath, bool linear = false)
        {
            string full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, linear);
            tex.LoadImage(File.ReadAllBytes(full));
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        public static void WritePng(string assetPath, Color32[] pixels, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply(false);
            string full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
