using System;
using System.Collections.Generic;
using System.Linq;
using Accertone.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Accertone.LobbyModule.EditorTools
{
    /// <summary>
    /// Turns a static driver mesh (no skeleton, no clips) into a walkable lobby character:
    /// measures the body (feet, ankles, knees, hips, spine, shoulders, free arm), builds a 14-bone skeleton,
    /// computes region-gated skin weights and writes a skinned mesh + character prefab.
    /// The right arm (carrying the helmet) is weighted rigidly to the chest, the way a driver carries a helmet.
    /// </summary>
    public static class LobbyCharacterRigBuilder
    {
        public const int Hips = 0, Spine = 1, Chest = 2, Neck = 3, Head = 4;
        public const int UpperLegL = 5, LowerLegL = 6, FootL = 7, UpperLegR = 8, LowerLegR = 9, FootR = 10;
        public const int UpperArmL = 11, LowerArmL = 12, HandL = 13;
        static readonly string[] BoneNames =
        {
            "Hips", "Spine", "Chest", "Neck", "Head",
            "UpperLeg.L", "LowerLeg.L", "Foot.L", "UpperLeg.R", "LowerLeg.R", "Foot.R",
            "UpperArm.L", "LowerArm.L", "Hand.L"
        };
        static readonly int[] Parent = { -1, 0, 1, 2, 3, 0, 5, 6, 0, 8, 9, 2, 11, 12 };

        public class Landmarks
        {
            public float height;
            public Vector3[] joints = new Vector3[14];
            public float hipJointY, crotchY;
            public float[] leftBoundary, rightBoundary; // x boundary per band (NaN = none)
            public float bandStart, bandStep;
            public float[] split;                       // x of the gap between the legs, per slice from splitStart
            public float splitStart, splitStep;
            public string report;
        }

        // ------------------------------------------------------------------ public entry

        public static string Build(GameObject model, Material suit, string prefabPath, string meshPath, float height, string characterName)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.hideFlags = HideFlags.HideAndDontSave;
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            try
            {
                var srcRenderer = inst.GetComponentInChildren<MeshRenderer>(true);
                var src = srcRenderer.GetComponent<MeshFilter>().sharedMesh;
                var m = srcRenderer.transform.localToWorldMatrix;

                // 1. Character space: feet on y = 0, facing +Z, height normalised.
                var raw = src.vertices;
                var verts = new Vector3[raw.Length];
                for (int i = 0; i < raw.Length; i++) verts[i] = m.MultiplyPoint3x4(raw[i]);
                float minY = verts.Min(v => v.y), maxY = verts.Max(v => v.y);
                float scale = height / (maxY - minY);
                var bounds = new Bounds(verts[0], Vector3.zero);
                foreach (var v in verts) bounds.Encapsulate(v);
                for (int i = 0; i < verts.Length; i++)
                    verts[i] = new Vector3((verts[i].x - bounds.center.x) * scale, (verts[i].y - minY) * scale, (verts[i].z - bounds.center.z) * scale);

                var lm = Measure(verts, src.triangles, height);
                // Re-centre so the root sits between the feet.
                Vector3 recentre = new Vector3((lm.joints[FootL].x + lm.joints[FootR].x) * 0.5f, 0f, (lm.joints[FootL].z + lm.joints[FootR].z) * 0.5f);
                for (int i = 0; i < verts.Length; i++) verts[i] -= recentre;
                for (int j = 0; j < lm.joints.Length; j++) lm.joints[j] -= recentre;
                if (lm.leftBoundary != null)
                    for (int b = 0; b < lm.leftBoundary.Length; b++)
                    {
                        lm.leftBoundary[b] -= recentre.x;
                        lm.rightBoundary[b] -= recentre.x;
                    }
                for (int k = 0; k < lm.split.Length; k++) lm.split[k] -= recentre.x;

                var srcNormals = src.normals;
                var normals = new Vector3[verts.Length];
                for (int i = 0; i < normals.Length; i++) normals[i] = m.MultiplyVector(srcNormals.Length == verts.Length ? srcNormals[i] : Vector3.up).normalized;

                // 2. Skin weights (+ which body region each vertex belongs to).
                var weights = new BoneWeight[verts.Length];
                var regions = new int[verts.Length];
                var sides = new int[verts.Length];
                for (int i = 0; i < verts.Length; i++) weights[i] = Weigh(verts[i], lm, out regions[i], out sides[i]);

                // 3. Contact seams: the static pose has the hand / helmet touching the thigh and waist, and the
                // mesh is welded there. Those hidden bridging faces would stretch into slivers when the arm
                // swings or the legs walk, so they are removed.
                var srcTris = src.triangles;
                var keep = new List<int>(srcTris.Length);
                int removed = 0;
                float seamTop = lm.joints[UpperArmL].y - 0.10f;
                for (int t = 0; t < srcTris.Length; t += 3)
                {
                    int a = srcTris[t], b = srcTris[t + 1], c = srcTris[t + 2];
                    int ra = regions[a], rb = regions[b], rc = regions[c];
                    bool arm = IsArm(ra) || IsArm(rb) || IsArm(rc);
                    bool leg = ra == RegionLeg || rb == RegionLeg || rc == RegionLeg;
                    bool leftArm = ra == RegionLeftArm || rb == RegionLeftArm || rc == RegionLeftArm;
                    bool torso = ra == RegionTorso || rb == RegionTorso || rc == RegionTorso;
                    float lowest = Mathf.Min(verts[a].y, Mathf.Min(verts[b].y, verts[c].y));
                    float highest = Mathf.Max(verts[a].y, Mathf.Max(verts[b].y, verts[c].y));
                    // Inner thighs / knees welded together in the static pose: the faces bridging the two legs.
                    bool bothLegs = ra == RegionLeg && rb == RegionLeg && rc == RegionLeg && (sides[a] != sides[b] || sides[b] != sides[c]);
                    if ((arm && leg) || (leftArm && torso && lowest < seamTop) || (bothLegs && highest < lm.crotchY - 0.01f))
                    {
                        removed++;
                        continue;
                    }
                    keep.Add(a); keep.Add(b); keep.Add(c);
                }
                lm.report += ", contact seams removed: " + removed + " tris";

                // 4. Skeleton (bind pose: identity rotations, character root at origin).
                var root = new GameObject(characterName);
                var rig = new GameObject("Rig").transform;
                rig.SetParent(root.transform, false);
                var bones = new Transform[BoneNames.Length];
                for (int b = 0; b < BoneNames.Length; b++)
                {
                    bones[b] = new GameObject(BoneNames[b]).transform;
                    bones[b].SetParent(Parent[b] < 0 ? rig : bones[Parent[b]], false);
                    bones[b].position = lm.joints[b];
                    bones[b].rotation = Quaternion.identity;
                }

                var body = new GameObject("Body");
                body.transform.SetParent(root.transform, false);
                var bindposes = new Matrix4x4[bones.Length];
                for (int b = 0; b < bones.Length; b++) bindposes[b] = bones[b].worldToLocalMatrix * body.transform.localToWorldMatrix;

                var mesh = new Mesh { name = characterName + "_Skinned" };
                mesh.indexFormat = verts.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.vertices = verts;
                mesh.normals = normals;
                mesh.uv = src.uv;
                mesh.triangles = keep.ToArray();
                mesh.boneWeights = weights;
                mesh.bindposes = bindposes;
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                mesh = AccertoneMeshFactory.SaveMesh(mesh, meshPath);

                var smr = body.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = mesh;
                smr.bones = bones;
                smr.rootBone = bones[Hips];
                smr.sharedMaterial = suit;
                smr.updateWhenOffscreen = false;
                smr.localBounds = new Bounds(new Vector3(0f, -0.02f, 0f), new Vector3(1.4f, 2.3f, 1.6f));
                smr.shadowCastingMode = ShadowCastingMode.On;
                smr.quality = SkinQuality.Bone4;

                // 4. Behaviour.
                var controller = root.AddComponent<LobbyCharacterController>();
                var loco = root.AddComponent<ProceduralLocomotion>();
                loco.controller = controller;
                loco.bones.hips = bones[Hips];
                loco.bones.spine = bones[Spine];
                loco.bones.chest = bones[Chest];
                loco.bones.neck = bones[Neck];
                loco.bones.head = bones[Head];
                loco.bones.upperLegL = bones[UpperLegL];
                loco.bones.lowerLegL = bones[LowerLegL];
                loco.bones.footL = bones[FootL];
                loco.bones.upperLegR = bones[UpperLegR];
                loco.bones.lowerLegR = bones[LowerLegR];
                loco.bones.footR = bones[FootR];
                loco.bones.upperArmL = bones[UpperArmL];
                loco.bones.lowerArmL = bones[LowerArmL];
                loco.bones.handL = bones[HandL];
                var presentation = root.AddComponent<LobbyCharacterPresentation>();
                presentation.character = controller;
                presentation.locomotion = loco;

                // Broadcast-style key + violet rim that follow the character.
                var lights = new GameObject("CharacterLights");
                lights.transform.SetParent(root.transform, false);
                var follow = lights.AddComponent<CharacterFollowLight>();
                follow.character = root.transform;
                follow.keyLight = MakeSpot(lights.transform, "KeyLight", 30f, 14f, 9f, 13f, new Color(1f, 0.96f, 0.92f), LightShadows.Soft);
                follow.rimLight = MakeSpot(lights.transform, "RimLight", 36f, 16f, 8f, 20f, new Color(0.72f, 0.48f, 1f), LightShadows.None);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                UnityEngine.Object.DestroyImmediate(root);
                return characterName + ": " + verts.Length + " verts, 14 bones. " + lm.report;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(inst);
            }
        }

        static Light MakeSpot(Transform parent, string name, float angle, float inner, float range, float intensity, Color color, LightShadows shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.spotAngle = angle;
            l.innerSpotAngle = inner;
            l.range = range;
            l.intensity = intensity;
            l.color = color;
            l.shadows = shadows;
            return l;
        }

        // ------------------------------------------------------------------ measuring

        static float Median(List<float> values)
        {
            if (values.Count == 0) return float.NaN;
            values.Sort();
            return values[values.Count / 2];
        }

        static Vector3 Mean(Vector3[] v, Func<Vector3, bool> filter)
        {
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (var p in v)
                if (filter(p)) { sum += p; n++; }
            return n > 0 ? sum / n : new Vector3(float.NaN, float.NaN, float.NaN);
        }

        static bool Valid(Vector3 p) { return !float.IsNaN(p.x); }

        static Landmarks Measure(Vector3[] v, int[] tris, float H)
        {
            var lm = new Landmarks { height = H };

            // Midline from the shins (only the two legs exist at that height).
            var shinX = new List<float>();
            foreach (var p in v) if (p.y > 0.18f * H && p.y < 0.25f * H) shinX.Add(p.x);
            float x0 = Median(shinX);

            Func<float, float, float, Vector3> legMean = (y0, y1, side) =>
                Mean(v, p => p.y > y0 && p.y < y1 && (p.x - x0) * side > 0.005f && Mathf.Abs(p.x - x0) < 0.12f * H);

            Vector3 ankleL = legMean(0.055f, 0.10f, -1f), ankleR = legMean(0.055f, 0.10f, 1f);
            Vector3 kneeL = legMean(0.27f * H, 0.30f * H, -1f), kneeR = legMean(0.27f * H, 0.30f * H, 1f);
            Vector3 thighL = legMean(0.40f * H, 0.45f * H, -1f), thighR = legMean(0.40f * H, 0.45f * H, 1f);

            // Torso depth profile.
            Func<float, float> zAt = y =>
            {
                var zs = new List<float>();
                foreach (var p in v) if (Mathf.Abs(p.y - y) < 0.02f && Mathf.Abs(p.x - x0) < 0.06f * H) zs.Add(p.z);
                float z = Median(zs);
                return float.IsNaN(z) ? 0f : z;
            };

            float hipJointY = 0.515f * H;
            lm.hipJointY = hipJointY;
            lm.crotchY = 0.47f * H;
            var J = lm.joints;
            J[Hips] = new Vector3(x0, 0.535f * H, (Valid(thighL) && Valid(thighR) ? (thighL.z + thighR.z) * 0.5f : zAt(0.53f * H)));
            J[Spine] = new Vector3(x0, 0.62f * H, zAt(0.62f * H));
            J[Chest] = new Vector3(x0, 0.72f * H, zAt(0.72f * H));
            J[Neck] = new Vector3(x0, 0.845f * H, zAt(0.845f * H));
            J[Head] = new Vector3(x0, 0.885f * H, zAt(0.9f * H));

            // The gap between the legs, tracked slice by slice from the ankles to the crotch. Drivers often stand with
            // the pelvis shifted over one leg, so the shin midline is not where the legs part.
            float kneeY = 0.285f * H;
            lm.splitStart = 0.10f;
            lm.splitStep = 0.02f;
            var splits = new List<float>();
            float xm = Valid(ankleL) && Valid(ankleR) ? (ankleL.x + ankleR.x) * 0.5f : x0;
            lm.crotchY = 0.47f * H;
            for (float y = lm.splitStart; y < hipJointY; y += lm.splitStep)
            {
                float gap = LegGap(Section(v, tris, y), xm);
                if (float.IsNaN(gap))
                {
                    if (y > kneeY + 0.05f) { lm.crotchY = y - lm.splitStep * 0.5f; break; }
                    gap = xm; // shins / ankles touching: keep tracking
                }
                splits.Add(gap);
                xm = gap;
            }
            lm.split = splits.Count > 0 ? splits.ToArray() : new[] { x0 };
            float pelvisX = lm.split[lm.split.Length - 1];

            float hipHalf = Valid(kneeL) && Valid(kneeR) ? Mathf.Clamp((kneeR.x - kneeL.x) * 0.5f, 0.07f, 0.11f) : 0.05f * H;
            J[Hips].x = pelvisX;
            J[UpperLegL] = new Vector3(pelvisX - hipHalf, hipJointY, Valid(thighL) ? thighL.z : J[Hips].z);
            J[UpperLegR] = new Vector3(pelvisX + hipHalf, hipJointY, Valid(thighR) ? thighR.z : J[Hips].z);
            J[LowerLegL] = Valid(kneeL) ? new Vector3(kneeL.x, 0.285f * H, kneeL.z) : new Vector3(x0 - hipHalf, 0.285f * H, J[Hips].z);
            J[LowerLegR] = Valid(kneeR) ? new Vector3(kneeR.x, 0.285f * H, kneeR.z) : new Vector3(x0 + hipHalf, 0.285f * H, J[Hips].z);
            J[FootL] = Valid(ankleL) ? new Vector3(ankleL.x, 0.08f, ankleL.z) : new Vector3(J[LowerLegL].x, 0.08f, J[LowerLegL].z);
            J[FootR] = Valid(ankleR) ? new Vector3(ankleR.x, 0.08f, ankleR.z) : new Vector3(J[LowerLegR].x, 0.08f, J[LowerLegR].z);

            // Torso / arm boundaries per band: a clear gap between the body (torso, or the thigh below the crotch)
            // and something further out, or the neck where a hand / helmet rests against it. No arm below the
            // lowest band where one was found; contact bands in between are interpolated.
            lm.bandStart = 0.36f * H;
            lm.bandStep = 0.02f * H;
            int bands = Mathf.CeilToInt((0.84f * H - lm.bandStart) / lm.bandStep);
            lm.leftBoundary = new float[bands];
            lm.rightBoundary = new float[bands];
            for (int b = 0; b < bands; b++)
            {
                float y = lm.bandStart + b * lm.bandStep;
                var sec = new List<Vector2>();
                for (int plane = -1; plane <= 1; plane++) sec.AddRange(Section(v, tris, y + plane * lm.bandStep * 0.33f));
                float start = y < lm.crotchY + 0.02f ? SplitX(lm, y) : x0;
                lm.leftBoundary[b] = FindGap(sec, start, -1f, H);
                lm.rightBoundary[b] = FindGap(sec, start, 1f, H);
            }
            FillGaps(lm.leftBoundary);
            FillGaps(lm.rightBoundary);

            // Free (left) arm chain from the arm cluster.
            Func<float, Vector3> armMean = y =>
            {
                float bx = Boundary(lm, lm.leftBoundary, y);
                if (float.IsNaN(bx)) return new Vector3(float.NaN, float.NaN, float.NaN);
                return Mean(v, p => Mathf.Abs(p.y - y) < 0.025f && p.x < bx);
            };
            Vector3 elbow = armMean(0.63f * H), wrist = armMean(0.49f * H), upper = armMean(0.75f * H);
            float shoulderY = 0.815f * H;
            J[UpperArmL] = new Vector3(Valid(upper) ? upper.x + 0.02f : x0 - 0.105f * H, shoulderY, J[Chest].z);
            J[LowerArmL] = Valid(elbow) ? new Vector3(elbow.x, 0.63f * H, elbow.z) : new Vector3(x0 - 0.12f * H, 0.63f * H, J[Spine].z);
            J[HandL] = Valid(wrist) ? new Vector3(wrist.x, 0.49f * H, wrist.z) : new Vector3(x0 - 0.12f * H, 0.49f * H, J[Spine].z);

            lm.report = string.Format("midline x={0:F3}, hips {1:F2} m, knees {2:F2} m, ankle spread {3:F2} m, arm detected={4}",
                x0, J[Hips].y, J[LowerLegL].y, Mathf.Abs(J[FootR].x - J[FootL].x), Valid(elbow) && Valid(wrist));
            return lm;
        }

        /// <summary>Points along the mesh's cross-section at height y (triangle / plane intersections, ~3 mm apart), as (x, z).</summary>
        static List<Vector2> Section(Vector3[] v, int[] tris, float y)
        {
            var pts = new List<Vector2>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                Vector3 a = v[tris[t]], b = v[tris[t + 1]], c = v[tris[t + 2]];
                Vector2 p0 = Vector2.zero, p1 = Vector2.zero;
                int n = 0;
                Edge(a, b, y, ref p0, ref p1, ref n);
                Edge(b, c, y, ref p0, ref p1, ref n);
                Edge(c, a, y, ref p0, ref p1, ref n);
                if (n < 2) continue;
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(p0, p1) / 0.003f));
                for (int k = 0; k <= steps; k++) pts.Add(Vector2.Lerp(p0, p1, k / (float)steps));
            }
            return pts;
        }

        static void Edge(Vector3 p, Vector3 q, float y, ref Vector2 p0, ref Vector2 p1, ref int n)
        {
            if ((p.y <= y) == (q.y <= y)) return;
            float f = (y - p.y) / (q.y - p.y);
            var pt = new Vector2(p.x + (q.x - p.x) * f, p.z + (q.z - p.z) * f);
            if (n == 0) p0 = pt; else p1 = pt;
            n++;
        }

        /// <summary>Centre of the empty run between the legs nearest to <paramref name="xm"/>, or NaN (legs joined).</summary>
        static float LegGap(List<Vector2> sec, float xm)
        {
            const float bin = 0.004f;
            const int nb = 60; // +-12 cm around the previous gap
            float left = xm - nb * bin * 0.5f;
            var occ = new bool[nb];
            foreach (var q in sec)
            {
                int i = (int)Mathf.Floor((q.x - left) / bin);
                if (i >= 0 && i < nb) occ[i] = true;
            }
            float best = float.NaN, bestDist = float.MaxValue;
            for (int i = 0; i < nb;)
            {
                if (occ[i]) { i++; continue; }
                int j = i;
                while (j < nb && !occ[j]) j++;
                if (i > 0 && j < nb) // bounded by a leg on both sides
                {
                    float c = left + (i + j) * 0.5f * bin;
                    if (Mathf.Abs(c - xm) < bestDist) { bestDist = Mathf.Abs(c - xm); best = c; }
                }
                i = j;
            }
            return bestDist < 0.08f ? best : float.NaN;
        }

        static float SplitX(Landmarks lm, float y)
        {
            var s = lm.split;
            float f = (y - lm.splitStart) / lm.splitStep;
            int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, s.Length - 1);
            int j = Mathf.Clamp(i + 1, 0, s.Length - 1);
            return Mathf.Lerp(s[i], s[j], Mathf.Clamp01(f - i));
        }

        /// <summary>
        /// Scans outward from <paramref name="start"/> (inside the body, or the gap between the legs) on one side of
        /// a cross-section. Returns the x where an arm / hand / helmet begins: the first clear gap with something
        /// beyond it, or, when the two are in contact, the neck where the section's front-to-back thickness dips
        /// between the body and the outer part. NaN when nothing lies beyond the body.
        /// </summary>
        static float FindGap(List<Vector2> sec, float start, float side, float H)
        {
            const float bin = 0.006f;
            int bins = Mathf.CeilToInt(0.34f * H / bin);
            var occ = new bool[bins];
            var zMin = new float[bins];
            var zMax = new float[bins];
            for (int i = 0; i < bins; i++) { zMin[i] = float.MaxValue; zMax[i] = float.MinValue; }
            foreach (var q in sec)
            {
                float d = (q.x - start) * side;
                if (d < 0f) continue;
                int i = (int)(d / bin);
                if (i >= bins) continue;
                occ[i] = true;
                zMin[i] = Mathf.Min(zMin[i], q.y);
                zMax[i] = Mathf.Max(zMax[i], q.y);
            }
            int first = Array.IndexOf(occ, true);
            if (first < 0) return float.NaN;
            int last = first;
            for (int i = first; i < bins; i++)
            {
                if (occ[i]) { last = i; continue; }
                if (i + 1 < bins && !occ[i + 1])
                {
                    for (int k = i + 2; k < bins; k++)
                        if (occ[k]) return start + side * (i + 1) * bin; // clear gap, something beyond
                    break; // nothing beyond the body
                }
            }

            // Contact: look for a neck (thickness minimum followed by a rise) between the body and an outer blob.
            var th = new float[bins];
            for (int i = first; i <= last; i++) th[i] = occ[i] ? zMax[i] - zMin[i] : (i > first ? th[i - 1] : 0f);
            int peak = first;
            for (int i = first; i <= last; i++) if (th[i] > th[peak]) peak = i;
            float tMin = th[peak];
            int iMin = -1;
            for (int i = peak + 1; i <= last; i++)
            {
                if (th[i] < tMin) { tMin = th[i]; iMin = i; }
                else if (iMin >= 0 && tMin < 0.6f * th[peak] && th[i] > tMin * 1.6f && last - iMin >= 5)
                    return start + side * (iMin + 0.5f) * bin;
            }
            return float.NaN;
        }

        /// <summary>Interpolates NaN bands between measured ones and copies the top value upward (arm joining the
        /// shoulder). Bands below the lowest measurement stay NaN: no arm hangs there.</summary>
        static void FillGaps(float[] values)
        {
            int n = values.Length;
            int lowest = Array.FindIndex(values, x => !float.IsNaN(x));
            if (lowest < 0) return;
            for (int i = lowest + 1; i < n; i++)
            {
                if (!float.IsNaN(values[i])) continue;
                int lo = i - 1, hi = i + 1;
                while (hi < n && float.IsNaN(values[hi])) hi++;
                values[i] = hi < n ? Mathf.Lerp(values[lo], values[hi], (i - lo) / (float)(hi - lo)) : values[lo];
            }
        }

        static float Boundary(Landmarks lm, float[] values, float y)
        {
            float f = (y - lm.bandStart) / lm.bandStep;
            int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, values.Length - 1);
            int j = Mathf.Clamp(i + 1, 0, values.Length - 1);
            if (float.IsNaN(values[i])) return values[j];
            if (float.IsNaN(values[j])) return values[i];
            return Mathf.Lerp(values[i], values[j], Mathf.Clamp01(f - i));
        }

        // ------------------------------------------------------------------ weights

        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        const int RegionTorso = 0, RegionLeftArm = 1, RegionRightArm = 2, RegionLeg = 3;
        static bool IsArm(int r) { return r == RegionLeftArm || r == RegionRightArm; }

        static BoneWeight Weigh(Vector3 p, Landmarks lm, out int region, out int side)
        {
            region = RegionTorso;
            side = -1;
            var J = lm.joints;
            var w = new float[14];
            float H = lm.height;
            float y = p.y;
            float shoulderY = J[UpperArmL].y;
            float elbowY = J[LowerArmL].y, wristY = J[HandL].y;
            bool inArmBand = y > lm.bandStart && y < shoulderY + 0.04f;
            if (inArmBand && p.x > Boundary(lm, lm.rightBoundary, y))
            {
                // Right arm + helmet: carried rigidly with the upper body.
                region = RegionRightArm;
                w[Chest] = 0.8f;
                w[Spine] = 0.2f;
            }
            else if (inArmBand && y > wristY - 0.22f && p.x < Boundary(lm, lm.leftBoundary, y))
            {
                region = RegionLeftArm;
                float shoulderBlend = Smooth(shoulderY - 0.10f, shoulderY + 0.03f, y) * 0.65f;
                float armW = 1f - shoulderBlend;
                if (y > elbowY + 0.05f) w[UpperArmL] = armW;
                else if (y > elbowY - 0.05f)
                {
                    float t = Mathf.InverseLerp(elbowY - 0.05f, elbowY + 0.05f, y);
                    w[UpperArmL] = t * armW;
                    w[LowerArmL] = (1f - t) * armW;
                }
                else if (y > wristY + 0.03f) w[LowerArmL] = armW;
                else if (y > wristY - 0.03f)
                {
                    float t = Mathf.InverseLerp(wristY - 0.03f, wristY + 0.03f, y);
                    w[LowerArmL] = t * armW;
                    w[HandL] = (1f - t) * armW;
                }
                else w[HandL] = armW;
                w[Chest] += shoulderBlend;
            }
            else if (y < lm.hipJointY + 0.07f)
            {
                region = RegionLeg;
                float pelvis = Smooth(lm.hipJointY - 0.16f, lm.hipJointY + 0.07f, y);
                // Below the crotch each vertex belongs to the leg on its side of the measured gap, never a mix of
                // both legs (a blend would stretch into webbing between the knees as the legs scissor); the crotch
                // blends softly.
                float splitX = SplitX(lm, y);
                float hard = p.x > splitX ? 1f : 0f;
                float soft = Smooth(-0.035f, 0.035f, p.x - SplitX(lm, lm.crotchY));
                float right = Mathf.Lerp(hard, soft, Smooth(lm.crotchY - 0.03f, lm.crotchY + 0.04f, y));
                side = right >= 0.5f ? 1 : 0;
                float[] chain = new float[3]; // upper, lower, foot
                float kneeY = (J[LowerLegL].y + J[LowerLegR].y) * 0.5f;
                float ankleY = (J[FootL].y + J[FootR].y) * 0.5f;
                if (y > kneeY + 0.06f) chain[0] = 1f;
                else if (y > kneeY - 0.06f)
                {
                    float t = Smooth(kneeY - 0.06f, kneeY + 0.06f, y);
                    chain[0] = t;
                    chain[1] = 1f - t;
                }
                else if (y > ankleY + 0.03f) chain[1] = 1f;
                else if (y > ankleY - 0.01f)
                {
                    float t = Smooth(ankleY - 0.01f, ankleY + 0.03f, y);
                    chain[1] = t;
                    chain[2] = 1f - t;
                }
                else chain[2] = 1f;
                float legW = 1f - pelvis;
                w[UpperLegL] = chain[0] * legW * (1f - right);
                w[LowerLegL] = chain[1] * legW * (1f - right);
                w[FootL] = chain[2] * legW * (1f - right);
                w[UpperLegR] = chain[0] * legW * right;
                w[LowerLegR] = chain[1] * legW * right;
                w[FootR] = chain[2] * legW * right;
                w[Hips] += pelvis;
            }
            else
            {
                float[] ys = { J[Hips].y, J[Spine].y, J[Chest].y, J[Neck].y, J[Head].y };
                int[] ids = { Hips, Spine, Chest, Neck, Head };
                if (y <= ys[0]) w[Hips] = 1f;
                else if (y >= ys[4]) w[Head] = 1f;
                else
                {
                    for (int k = 0; k < 4; k++)
                    {
                        if (y >= ys[k] && y <= ys[k + 1])
                        {
                            float t = Mathf.InverseLerp(ys[k], ys[k + 1], y);
                            t = Smooth(0.2f, 0.8f, t);
                            w[ids[k]] = 1f - t;
                            w[ids[k + 1]] = t;
                            break;
                        }
                    }
                }
            }

            // Keep the 4 largest, normalise.
            var order = Enumerable.Range(0, 14).OrderByDescending(i => w[i]).Take(4).ToArray();
            float sum = 0f;
            foreach (var i in order) sum += w[i];
            if (sum <= 0f) { order[0] = Hips; w[Hips] = 1f; sum = 1f; }
            var bw = new BoneWeight
            {
                boneIndex0 = order[0], weight0 = w[order[0]] / sum,
                boneIndex1 = order[1], weight1 = w[order[1]] / sum,
                boneIndex2 = order[2], weight2 = w[order[2]] / sum,
                boneIndex3 = order[3], weight3 = w[order[3]] / sum,
            };
            return bw;
        }
    }
}
