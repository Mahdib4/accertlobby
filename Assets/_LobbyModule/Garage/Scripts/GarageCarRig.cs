using System.Collections.Generic;
using Accertone.CarSetup;
using Accertone.UI;
using UnityEngine;

namespace Accertone.Garage
{
    /// <summary>Car areas the Garage points the camera and callouts at.</summary>
    public enum GaragePart { Body, FrontWing, RearWing, Suspension, Brakes, Tyre, Powertrain, Cockpit, Centre }

    /// <summary>
    /// Live customisation of the existing AC-26 model. Works on the real meshes and materials:
    /// <list type="bullet">
    /// <item>Livery: the four livery materials get instances whose baked maps are recoloured on the GPU
    /// (<c>Hidden/Accertone/GarageRecolor</c>), plus metallic / smoothness / clear-coat for the finish.</item>
    /// <item>Rims and tyre sidewall markings: their own materials, recoloured the same way.</item>
    /// <item>Brake discs: front and rear instances whose emission previews heat.</item>
    /// <item>Setup visuals: front-wing flaps and the rear-wing flap rotate about their leading / trailing edges,
    /// the DRS flap opens, wheels take camber and toe about their own centres, and the body takes ride height and rake.</item>
    /// </list>
    /// Original materials and transforms are kept so everything can be restored. Shared material assets are never edited.
    /// </summary>
    [DisallowMultipleComponent]
    public class GarageCarRig : MonoBehaviour
    {
        // Baseline setup the model was built at: visuals are offsets from these values.
        const int BaseFrontWing = 28, BaseRearWing = 26, BaseFrontRide = 36, BaseRearRide = 54;
        const float FrontFlapDegPerClick = 0.22f, RearFlapDegPerClick = 0.24f, DrsOpenDegrees = -24f;

        static readonly int s_BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int s_MainTex = Shader.PropertyToID("_MainTex");
        static readonly int s_EmissionMap = Shader.PropertyToID("_EmissionMap");
        static readonly int s_EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly int s_Metallic = Shader.PropertyToID("_Metallic");
        static readonly int s_Smoothness = Shader.PropertyToID("_Smoothness");
        static readonly int s_CoatMask = Shader.PropertyToID("_ClearCoatMask");
        static readonly int s_CoatSmoothness = Shader.PropertyToID("_ClearCoatSmoothness");
        static readonly int s_Primary = Shader.PropertyToID("_Primary");
        static readonly int s_AccentA = Shader.PropertyToID("_AccentA");
        static readonly int s_AccentB = Shader.PropertyToID("_AccentB");
        static readonly int s_Lettering = Shader.PropertyToID("_Lettering");
        static readonly int s_KeepBody = Shader.PropertyToID("_KeepBody");
        static readonly int s_KeepLettering = Shader.PropertyToID("_KeepLettering");
        static readonly int s_BodyValue = Shader.PropertyToID("_BodyValue");
        static readonly int s_HueA = Shader.PropertyToID("_HueA");
        static readonly int s_HueB = Shader.PropertyToID("_HueB");
        static readonly int s_SatRef = Shader.PropertyToID("_SatRef");

        static readonly string[] s_LiveryMaterials = { "M_Car_Body_Paint", "M_Car_Wing_Paint", "M_Car_Body_Detail", "M_Car_Body_PaintDetail" };
        const string RimMaterial = "M_Car_Wheel_Rim";
        const string SidewallMaterial = "M_Car_Tyre_Sidewall";
        const string DiscMaterial = "M_Car_Brake_Disc";
        const string HaloMaterial = "M_Car_Halo";
        static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");

        /// <summary>A material instance plus the values it started from.</summary>
        class Paintable
        {
            public Material original, instance;
            public Texture baseMap, emissionMap;
            public RenderTexture baseRT, emissionRT;
            public float metallic, smoothness, coatMask, coatSmoothness;
            public Color emissionColor;
        }

        /// <summary>A node posed in its parent's space, optionally rotated about a pivot.</summary>
        class PosedNode
        {
            public Transform t;
            public Vector3 pos;
            public Quaternion rot;
            public Vector3 pivot, axis;     // parent space
            public Vector3 axis2;           // parent space (toe)
            public float side;              // +1 right, -1 left
            public bool front;
            public bool flap;
        }

        Transform m_GltfRoot;
        readonly List<Paintable> m_Livery = new List<Paintable>();
        Paintable m_Rim, m_Sidewall;
        Paintable m_DiscFront, m_DiscRear, m_Halo;
        Color m_HaloColor;
        readonly Dictionary<Material, Material> m_Swap = new Dictionary<Material, Material>();
        readonly List<Renderer> m_Renderers = new List<Renderer>();
        readonly List<PosedNode> m_Body = new List<PosedNode>();
        readonly List<PosedNode> m_FrontFlaps = new List<PosedNode>();
        readonly List<PosedNode> m_Wheels = new List<PosedNode>();
        PosedNode m_Drs;
        // Hot carbon brakes light the inside of the wheel; the discs themselves sit hidden in the rim barrel.
        readonly List<Light> m_HeatLightsFront = new List<Light>();
        readonly List<Light> m_HeatLightsRear = new List<Light>();
        Vector3 m_BodyPivot, m_BodyAxis, m_BodyUp; // GLTF-root space
        float m_FrontAxleZ = 1.45f, m_RearAxleZ = -2.09f; // car space
        Material m_Recolor;
        bool m_Ready;

        // Visual state: current (animated) and target.
        float m_FrontFlap, m_FrontFlapT, m_RearFlap, m_RearFlapT, m_DrsOpen, m_DrsOpenT;
        float m_CamberF, m_CamberFT, m_CamberR, m_CamberRT, m_ToeF, m_ToeFT, m_ToeR, m_ToeRT;
        float m_RideF, m_RideFT, m_RideR, m_RideRT;
        float m_HeatF, m_HeatFT, m_HeatR, m_HeatRT;
        float m_VelA, m_VelB, m_VelC, m_VelD, m_VelE, m_VelF, m_VelG, m_VelH, m_VelI, m_VelJ, m_VelK;
        bool m_PoseDirty, m_HeatDirty;

        /// <summary>Car-local bounds of every mesh (metres; nose along +Z).</summary>
        public Bounds LocalBounds { get; private set; }
        public float Wheelbase { get; private set; }
        public float TrackFront { get; private set; }
        public float TrackRear { get; private set; }
        public float WheelRadius { get; private set; }
        public int RendererCount { get { return m_Renderers.Count; } }
        public bool HasDrs { get { return m_Drs != null; } }
        public bool HasFrontFlaps { get { return m_FrontFlaps.Count > 0; } }
        public bool HasWheels { get { return m_Wheels.Count >= 4; } }
        public bool HasLivery { get { return m_Livery.Count > 0; } }
        public bool HasRims { get { return m_Rim != null; } }
        public bool HasSidewalls { get { return m_Sidewall != null; } }
        public bool HasDiscs { get { return m_DiscFront != null || m_DiscRear != null; } }
        public bool CanRecolor { get { return m_Recolor != null; } }
        /// <summary>True while any setup visual is still easing toward its target.</summary>
        public bool Animating { get; private set; }

        public static GarageCarRig Attach(GameObject car)
        {
            if (car == null) return null;
            var rig = car.GetComponent<GarageCarRig>();
            if (rig == null) rig = car.AddComponent<GarageCarRig>();
            rig.Initialise();
            return rig;
        }

        void Initialise()
        {
            if (m_Ready) return;
            m_Ready = true;
            var shader = Resources.Load<Shader>("GarageRecolor");
            if (shader != null && shader.isSupported) m_Recolor = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            else Debug.LogWarning("[Accert one] Garage: recolour shader unavailable; livery colours fall back to the factory paint.");

            GetComponentsInChildren(true, m_Renderers);
            MeasureAndFindNodes();
            InstanceMaterials();
        }

        // =====================================================================================================
        // DISCOVERY
        // =====================================================================================================
        void MeasureAndFindNodes()
        {
            Vector3 mn = Vector3.one * 1e9f, mx = -Vector3.one * 1e9f;
            foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var l = transform.InverseTransformPoint(mf.transform.TransformPoint(c));
                    mn = Vector3.Min(mn, l);
                    mx = Vector3.Max(mx, l);
                }
            }
            if (mx.x < mn.x) { mn = new Vector3(-1f, 0f, -2.8f); mx = new Vector3(1f, 1.2f, 2.8f); }
            var bounds = new Bounds();
            bounds.SetMinMax(mn, mx);
            LocalBounds = bounds;

            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == "GLTF_SceneRootNode") { m_GltfRoot = t; break; }
            if (m_GltfRoot == null) return;

            // Wheels: WHEEL_* (tyre + rim) and DISC_* turn together about the wheel centre for camber and toe.
            Vector3 frontSum = Vector3.zero, rearSum = Vector3.zero;
            int frontCount = 0, rearCount = 0;
            float radius = 0f;
            foreach (Transform hub in m_GltfRoot)
            {
                if (!hub.name.StartsWith("HUB_")) continue;
                Transform wheel = null;
                foreach (Transform c in hub) if (c.name.StartsWith("WHEEL_")) wheel = c;
                if (wheel == null) continue;
                Bounds wb;
                if (!CarLocalBounds(wheel, out wb)) continue;
                Vector3 centreCar = wb.center;
                radius = Mathf.Max(radius, wb.extents.y);
                bool front = centreCar.z > 0f;
                float side = centreCar.x >= 0f ? 1f : -1f;
                if (front) { frontSum += centreCar; frontCount++; } else { rearSum += centreCar; rearCount++; }
                Vector3 pivot = hub.InverseTransformPoint(transform.TransformPoint(centreCar));
                Vector3 camberAxis = hub.InverseTransformDirection(transform.forward).normalized;
                Vector3 toeAxis = hub.InverseTransformDirection(transform.up).normalized;
                AddHeatLight(hub, centreCar, front, side);
                foreach (Transform c in hub)
                {
                    if (!c.name.StartsWith("WHEEL_") && !c.name.StartsWith("DISC_")) continue;
                    m_Wheels.Add(new PosedNode { t = c, pos = c.localPosition, rot = c.localRotation, pivot = pivot, axis = camberAxis, axis2 = toeAxis, side = side, front = front });
                }
            }
            WheelRadius = radius;
            if (frontCount > 0 && rearCount > 0)
            {
                m_FrontAxleZ = (frontSum / frontCount).z;
                m_RearAxleZ = (rearSum / rearCount).z;
                Wheelbase = m_FrontAxleZ - m_RearAxleZ;
            }
            Vector3 bodyPivotCar = new Vector3(0f, 0f, (m_FrontAxleZ + m_RearAxleZ) * 0.5f);
            m_BodyPivot = m_GltfRoot.InverseTransformPoint(transform.TransformPoint(bodyPivotCar));
            m_BodyAxis = m_GltfRoot.InverseTransformDirection(transform.right).normalized;
            m_BodyUp = m_GltfRoot.InverseTransformDirection(transform.up).normalized;
            TrackFront = TrackWidth(true);
            TrackRear = TrackWidth(false);

            // Body: every direct child of the GLTF root except the wheel assemblies takes ride height / rake.
            foreach (Transform c in m_GltfRoot)
            {
                if (c.name.StartsWith("HUB_")) continue;
                var node = new PosedNode { t = c, pos = c.localPosition, rot = c.localRotation };
                m_Body.Add(node);
                if (c.name.StartsWith("frontflap_")) { node.flap = true; m_FrontFlaps.Add(node); }
            }

            // Front flaps rotate about a lateral axis through their leading edge (forward-most, mid height).
            if (m_FrontFlaps.Count > 0)
            {
                Bounds fb = new Bounds();
                bool any = false;
                foreach (var n in m_FrontFlaps)
                {
                    Bounds b;
                    if (!CarLocalBounds(n.t, out b)) continue;
                    if (!any) { fb = b; any = true; } else fb.Encapsulate(b);
                }
                Vector3 pivotCar = new Vector3(0f, fb.center.y, fb.max.z - 0.04f);
                foreach (var n in m_FrontFlaps)
                {
                    n.pivot = m_GltfRoot.InverseTransformPoint(transform.TransformPoint(pivotCar));
                    n.axis = m_GltfRoot.InverseTransformDirection(transform.right).normalized;
                }
            }

            // Rear-wing flap (the DRS element) rotates about its trailing edge.
            foreach (var t in m_GltfRoot.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("DRS_") || t.parent == null) continue;
                Bounds db;
                if (!CarLocalBounds(t, out db)) continue;
                Vector3 pivotCar = new Vector3(0f, db.max.y - 0.01f, db.min.z + 0.01f);
                m_Drs = new PosedNode
                {
                    t = t, pos = t.localPosition, rot = t.localRotation,
                    pivot = t.parent.InverseTransformPoint(transform.TransformPoint(pivotCar)),
                    axis = t.parent.InverseTransformDirection(transform.right).normalized,
                };
                break;
            }
        }

        void AddHeatLight(Transform hub, Vector3 centreCar, bool front, float side)
        {
            var go = new GameObject("GarageBrakeGlow");
            go.transform.SetParent(hub, false);
            // A little inboard of the wheel centre, inside the rim barrel.
            go.transform.position = transform.TransformPoint(centreCar - new Vector3(side * 0.16f, 0f, 0f));
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 0.95f;
            l.intensity = 0f;
            l.shadows = LightShadows.None;
            l.color = new Color(1f, 0.42f, 0.1f);
            l.enabled = false;
            (front ? m_HeatLightsFront : m_HeatLightsRear).Add(l);
        }

        float TrackWidth(bool front)
        {
            float l = 0f, r = 0f;
            bool hl = false, hr = false;
            foreach (var w in m_Wheels)
            {
                if (w.front != front || !w.t.name.StartsWith("WHEEL_")) continue;
                Bounds b;
                if (!CarLocalBounds(w.t, out b)) continue;
                if (w.side > 0f) { r = b.center.x; hr = true; } else { l = b.center.x; hl = true; }
            }
            return hl && hr ? r - l : 0f;
        }

        bool CarLocalBounds(Transform root, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var l = transform.InverseTransformPoint(mf.transform.TransformPoint(c));
                    if (!any) { bounds = new Bounds(l, Vector3.zero); any = true; }
                    else bounds.Encapsulate(l);
                }
            }
            return any;
        }

        void InstanceMaterials()
        {
            foreach (var r in m_Renderers)
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    Material inst;
                    if (!m_Swap.TryGetValue(m, out inst))
                    {
                        inst = CreateInstance(m, r);
                        if (inst == null) continue;
                    }
                    else if (BaseName(m) == DiscMaterial)
                    {
                        // Discs: front and rear get separate instances so brake bias can show.
                        inst = IsFrontWheelPart(r.transform) ? (m_DiscFront != null ? m_DiscFront.instance : inst) : (m_DiscRear != null ? m_DiscRear.instance : inst);
                    }
                    mats[i] = inst;
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        Material CreateInstance(Material m, Renderer r)
        {
            string name = BaseName(m);
            Paintable p = null;
            bool livery = System.Array.IndexOf(s_LiveryMaterials, name) >= 0;
            if (livery) { p = Make(m); m_Livery.Add(p); }
            else if (name == RimMaterial) { p = Make(m); m_Rim = p; }
            else if (name == SidewallMaterial) { p = Make(m); m_Sidewall = p; }
            else if (name == HaloMaterial) { p = Make(m); m_Halo = p; m_HaloColor = m.HasProperty(s_BaseColor) ? m.GetColor(s_BaseColor) : Color.black; }
            else if (name == DiscMaterial)
            {
                if (m_DiscFront == null) { m_DiscFront = Make(m); m_DiscFront.instance.name = m.name + " (Garage Front)"; }
                if (m_DiscRear == null) { m_DiscRear = Make(m); m_DiscRear.instance.name = m.name + " (Garage Rear)"; }
                m_DiscFront.instance.EnableKeyword("_EMISSION");
                m_DiscRear.instance.EnableKeyword("_EMISSION");
                m_DiscFront.instance.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                m_DiscRear.instance.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                m_Swap[m] = m_DiscFront.instance;
                return IsFrontWheelPart(r.transform) ? m_DiscFront.instance : m_DiscRear.instance;
            }
            if (p == null) return null;
            m_Swap[m] = p.instance;
            return p.instance;
        }

        static Paintable Make(Material m)
        {
            var inst = new Material(m) { name = m.name + " (Garage)" };
            var p = new Paintable { original = m, instance = inst };
            if (m.HasProperty(s_BaseMap)) p.baseMap = m.GetTexture(s_BaseMap);
            if (m.HasProperty(s_EmissionMap)) p.emissionMap = m.GetTexture(s_EmissionMap);
            if (m.HasProperty(s_Metallic)) p.metallic = m.GetFloat(s_Metallic);
            if (m.HasProperty(s_Smoothness)) p.smoothness = m.GetFloat(s_Smoothness);
            if (m.HasProperty(s_CoatMask)) p.coatMask = m.GetFloat(s_CoatMask);
            if (m.HasProperty(s_CoatSmoothness)) p.coatSmoothness = m.GetFloat(s_CoatSmoothness);
            if (m.HasProperty(s_EmissionColor)) p.emissionColor = m.GetColor(s_EmissionColor);
            return p;
        }

        static string BaseName(Material m)
        {
            string n = m.name;
            int i = n.IndexOf(" (");
            return i > 0 ? n.Substring(0, i) : n;
        }

        bool IsFrontWheelPart(Transform t)
        {
            for (var p = t; p != null && p != transform; p = p.parent)
            {
                if (!p.name.StartsWith("HUB_")) continue;
                Bounds b;
                if (CarLocalBounds(p, out b)) return b.center.z > 0f;
                return p.name.Contains("F_");
            }
            return true;
        }

        // =====================================================================================================
        // APPEARANCE
        // =====================================================================================================
        /// <summary>Applies paint, finish, rims and sidewall markings. <paramref name="compound"/> drives the marking colour.</summary>
        public void ApplyAppearance(GarageAppearance a, TireCompound compound)
        {
            Initialise();
            if (a == null) a = GarageAppearance.Factory();

            // Livery colours.
            bool source = a.UsesSourceLivery || m_Recolor == null;
            Color lettering = a.ResolvedLettering() == LetteringMode.Dark ? GarageCatalog.DarkLettering : GarageCatalog.WhiteLettering;
            foreach (var p in m_Livery)
            {
                if (source) RestoreMaps(p);
                else
                {
                    Recolor(p, false, a.primary, a.accentA, a.accentB, lettering, false, false);
                    Recolor(p, true, a.primary, a.accentA, a.accentB, lettering, true, true);
                }
                ApplyFinish(p, a.finish);
                // Livery glow: the baked emission map scaled by the player's glow level.
                if (p.instance.HasProperty(s_EmissionColor)) p.instance.SetColor(s_EmissionColor, p.emissionColor * Mathf.Clamp(a.glow, 0f, 1.6f));
            }

            // The halo is untextured and painted in the body colour (factory: the livery graphite).
            if (m_Halo != null)
            {
                if (m_Halo.instance.HasProperty(s_BaseColor)) m_Halo.instance.SetColor(s_BaseColor, a.UsesSourceLivery ? m_HaloColor : a.primary);
                ApplyFinish(m_Halo, a.finish);
            }

            // Rims.
            if (m_Rim != null)
            {
                var style = GarageCatalog.FindRim(a.rimId) ?? GarageCatalog.Rims[0];
                if (style.source || m_Recolor == null) RestoreMaps(m_Rim);
                else
                {
                    Color rp = style.matchLivery ? a.primary : style.primary;
                    Color ra = style.matchLivery ? a.accentB : style.accent;
                    Recolor(m_Rim, false, rp, ra, ra, lettering, false, true);
                }
                SetFloat(m_Rim.instance, s_Metallic, style.metallic);
                SetFloat(m_Rim.instance, s_Smoothness, style.smoothness);
            }

            // Tyre sidewall markings: compound colour (F1 practice) or the team magenta of the source art.
            if (m_Sidewall != null)
            {
                if (a.sidewall == SidewallMarking.Team || m_Recolor == null) RestoreMaps(m_Sidewall);
                else
                {
                    Color c = GarageCatalog.CompoundColor(compound);
                    Recolor(m_Sidewall, false, Color.black, c, c, lettering, true, true);
                }
            }
        }

        void ApplyFinish(Paintable p, PaintFinish finish)
        {
            float metallic = p.metallic, smooth = p.smoothness, coat = p.coatMask, coatSmooth = p.coatSmoothness;
            switch (finish)
            {
                case PaintFinish.Metallic: metallic = 0.62f; smooth = 0.66f; coat = 1f; coatSmooth = 0.95f; break;
                case PaintFinish.Satin: metallic = 0.18f; smooth = 0.36f; coat = 0.35f; coatSmooth = 0.62f; break;
                case PaintFinish.Matte: metallic = 0.02f; smooth = 0.16f; coat = 0f; coatSmooth = 0f; break;
            }
            SetFloat(p.instance, s_Metallic, metallic);
            SetFloat(p.instance, s_Smoothness, smooth);
            SetFloat(p.instance, s_CoatMask, coat);
            SetFloat(p.instance, s_CoatSmoothness, coatSmooth);
        }

        static void SetFloat(Material m, int id, float v)
        {
            if (m != null && m.HasProperty(id)) m.SetFloat(id, v);
        }

        void RestoreMaps(Paintable p)
        {
            if (p.instance.HasProperty(s_BaseMap)) p.instance.SetTexture(s_BaseMap, p.baseMap);
            if (p.instance.HasProperty(s_MainTex)) p.instance.SetTexture(s_MainTex, p.baseMap);
            if (p.emissionMap != null && p.instance.HasProperty(s_EmissionMap)) p.instance.SetTexture(s_EmissionMap, p.emissionMap);
            Release(ref p.baseRT);
            Release(ref p.emissionRT);
        }

        void Recolor(Paintable p, bool emission, Color primary, Color accentA, Color accentB, Color lettering, bool keepBody, bool keepLettering)
        {
            var src = emission ? p.emissionMap : p.baseMap;
            if (src == null || m_Recolor == null) return;
            int w = src.width, h = src.height;
            if (emission) { w = Mathf.Max(256, w / 2); h = Mathf.Max(256, h / 2); }
            ref RenderTexture rt = ref (emission ? ref p.emissionRT : ref p.baseRT);
            if (rt == null || rt.width != w || rt.height != h)
            {
                Release(ref rt);
                rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    name = p.original.name + (emission ? "_GarageEmission" : "_GarageBase"),
                    useMipMap = true,
                    autoGenerateMips = true,
                    wrapMode = src.wrapMode,
                    filterMode = FilterMode.Trilinear,
                    anisoLevel = 8,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                rt.Create();
            }
            // Raw gamma values: SetVector skips the sRGB -> linear conversion SetColor would apply.
            m_Recolor.SetVector(s_Primary, primary);
            m_Recolor.SetVector(s_AccentA, accentA);
            m_Recolor.SetVector(s_AccentB, accentB);
            m_Recolor.SetVector(s_Lettering, lettering);
            m_Recolor.SetFloat(s_KeepBody, keepBody ? 1f : 0f);
            m_Recolor.SetFloat(s_KeepLettering, keepLettering ? 1f : 0f);
            m_Recolor.SetFloat(s_BodyValue, Mathf.Max(GarageCatalog.SourceBody.r, Mathf.Max(GarageCatalog.SourceBody.g, GarageCatalog.SourceBody.b)));
            m_Recolor.SetFloat(s_HueA, 0.7225f);
            m_Recolor.SetFloat(s_HueB, 0.851f);
            m_Recolor.SetFloat(s_SatRef, 0.75f);
            Graphics.Blit(src, rt, m_Recolor);
            if (emission) p.instance.SetTexture(s_EmissionMap, rt);
            else
            {
                if (p.instance.HasProperty(s_BaseMap)) p.instance.SetTexture(s_BaseMap, rt);
                if (p.instance.HasProperty(s_MainTex)) p.instance.SetTexture(s_MainTex, rt);
            }
        }

        static void Release(ref RenderTexture rt)
        {
            if (rt == null) return;
            rt.Release();
            Destroy(rt);
            rt = null;
        }

        /// <summary>Heat glow on the brake discs (0..1 front and rear), eased.</summary>
        public void SetBrakeHeat(float front, float rear, bool snap = false)
        {
            m_HeatFT = Mathf.Clamp01(front);
            m_HeatRT = Mathf.Clamp01(rear);
            if (snap) { m_HeatF = m_HeatFT; m_HeatR = m_HeatRT; }
            m_HeatDirty = true;
        }

        void ApplyHeat()
        {
            ApplyDiscHeat(m_DiscFront, m_HeatF);
            ApplyDiscHeat(m_DiscRear, m_HeatR);
            ApplyHeatLights(m_HeatLightsFront, m_HeatF);
            ApplyHeatLights(m_HeatLightsRear, m_HeatR);
        }

        static void ApplyHeatLights(List<Light> lights, float heat)
        {
            foreach (var l in lights)
            {
                if (l == null) continue;
                l.enabled = heat > 0.01f;
                l.intensity = heat * heat * 2.6f;
                l.color = Color.Lerp(new Color(1f, 0.16f, 0.04f), new Color(1f, 0.5f, 0.14f), heat);
            }
        }

        static void ApplyDiscHeat(Paintable p, float heat)
        {
            if (p == null || !p.instance.HasProperty(s_EmissionColor)) return;
            // Carbon brakes glow from deep red to orange as they heat up.
            Color hot = Color.Lerp(new Color(0.9f, 0.08f, 0.01f), new Color(1f, 0.45f, 0.08f), heat);
            p.instance.SetColor(s_EmissionColor, hot * (heat * heat * 5.5f));
        }

        // =====================================================================================================
        // SETUP VISUALS
        // =====================================================================================================
        /// <summary>Sets the visual pose from a setup (wing angles, camber, toe, ride height). Eased unless snap.</summary>
        public void ApplySetupVisuals(F1CarSetupData s, bool snap = false)
        {
            Initialise();
            if (s == null) s = new F1CarSetupData();
            m_FrontFlapT = (s.frontWingAngle - BaseFrontWing) * FrontFlapDegPerClick;
            m_RearFlapT = (s.rearWingAngle - BaseRearWing) * RearFlapDegPerClick;
            m_CamberFT = s.frontCamber;
            m_CamberRT = s.rearCamber;
            m_ToeFT = s.frontToe;
            m_ToeRT = s.rearToe;
            m_RideFT = (s.frontRideHeight - BaseFrontRide) * 0.001f;
            m_RideRT = (s.rearRideHeight - BaseRearRide) * 0.001f;
            if (snap) SnapVisuals();
            m_PoseDirty = true;
        }

        /// <summary>Opens the DRS flap for inspection (a visual preview; the setup is unchanged).</summary>
        public void SetDrsPreview(bool open, bool snap = false)
        {
            m_DrsOpenT = open ? 1f : 0f;
            if (snap) m_DrsOpen = m_DrsOpenT;
            m_PoseDirty = true;
        }

        public bool DrsPreviewOpen { get { return m_DrsOpenT > 0.5f; } }

        void SnapVisuals()
        {
            m_FrontFlap = m_FrontFlapT; m_RearFlap = m_RearFlapT; m_DrsOpen = m_DrsOpenT;
            m_CamberF = m_CamberFT; m_CamberR = m_CamberRT; m_ToeF = m_ToeFT; m_ToeR = m_ToeRT;
            m_RideF = m_RideFT; m_RideR = m_RideRT;
            m_VelA = m_VelB = m_VelC = m_VelD = m_VelE = m_VelF = m_VelG = m_VelH = m_VelI = 0f;
        }

        /// <summary>Puts every moved node back exactly where the model placed it.</summary>
        public void RestoreFactoryPose()
        {
            m_FrontFlapT = m_RearFlapT = m_DrsOpenT = m_RideFT = m_RideRT = 0f;
            m_CamberFT = m_CamberRT = m_ToeFT = m_ToeRT = 0f;
            SnapVisuals();
            foreach (var n in m_Body) { n.t.localPosition = n.pos; n.t.localRotation = n.rot; }
            foreach (var n in m_Wheels) { n.t.localPosition = n.pos; n.t.localRotation = n.rot; }
            if (m_Drs != null) { m_Drs.t.localPosition = m_Drs.pos; m_Drs.t.localRotation = m_Drs.rot; }
            m_PoseDirty = false;
        }

        void Update()
        {
            if (!m_Ready) return;
            float dt = Time.unscaledDeltaTime;
            if (m_PoseDirty)
            {
                const float st = 0.22f;
                m_FrontFlap = Mathf.SmoothDamp(m_FrontFlap, m_FrontFlapT, ref m_VelA, st, Mathf.Infinity, dt);
                m_RearFlap = Mathf.SmoothDamp(m_RearFlap, m_RearFlapT, ref m_VelB, st, Mathf.Infinity, dt);
                m_DrsOpen = Mathf.SmoothDamp(m_DrsOpen, m_DrsOpenT, ref m_VelC, 0.28f, Mathf.Infinity, dt);
                m_CamberF = Mathf.SmoothDamp(m_CamberF, m_CamberFT, ref m_VelD, st, Mathf.Infinity, dt);
                m_CamberR = Mathf.SmoothDamp(m_CamberR, m_CamberRT, ref m_VelE, st, Mathf.Infinity, dt);
                m_ToeF = Mathf.SmoothDamp(m_ToeF, m_ToeFT, ref m_VelF, st, Mathf.Infinity, dt);
                m_ToeR = Mathf.SmoothDamp(m_ToeR, m_ToeRT, ref m_VelG, st, Mathf.Infinity, dt);
                m_RideF = Mathf.SmoothDamp(m_RideF, m_RideFT, ref m_VelH, 0.3f, Mathf.Infinity, dt);
                m_RideR = Mathf.SmoothDamp(m_RideR, m_RideRT, ref m_VelI, 0.3f, Mathf.Infinity, dt);
                if (AccertoneMotion.Scale <= 0.01f) SnapVisuals();
                ApplyPose();
                bool settled = Near(m_FrontFlap, m_FrontFlapT) && Near(m_RearFlap, m_RearFlapT) && Near(m_DrsOpen, m_DrsOpenT) &&
                               Near(m_CamberF, m_CamberFT) && Near(m_CamberR, m_CamberRT) && Near(m_ToeF, m_ToeFT) &&
                               Near(m_ToeR, m_ToeRT) && Mathf.Abs(m_RideF - m_RideFT) < 1e-5f && Mathf.Abs(m_RideR - m_RideRT) < 1e-5f;
                if (settled) { SnapVisuals(); ApplyPose(); m_PoseDirty = false; }
                Animating = !settled;
            }
            else Animating = false;

            if (m_HeatDirty)
            {
                m_HeatF = Mathf.SmoothDamp(m_HeatF, m_HeatFT, ref m_VelJ, 0.35f, Mathf.Infinity, dt);
                m_HeatR = Mathf.SmoothDamp(m_HeatR, m_HeatRT, ref m_VelK, 0.35f, Mathf.Infinity, dt);
                if (AccertoneMotion.Scale <= 0.01f) { m_HeatF = m_HeatFT; m_HeatR = m_HeatRT; }
                ApplyHeat();
                if (Near(m_HeatF, m_HeatFT) && Near(m_HeatR, m_HeatRT)) { m_HeatF = m_HeatFT; m_HeatR = m_HeatRT; ApplyHeat(); m_HeatDirty = false; }
            }
        }

        static bool Near(float a, float b) { return Mathf.Abs(a - b) < 1e-3f; }

        void ApplyPose()
        {
            if (m_GltfRoot == null) return;
            // Body ride height and rake: a pitch about the lateral axis plus a lift, matched at both axles
            // (a positive pitch angle lowers the nose, so a higher front needs a negative angle).
            float pitch = -Mathf.Atan2(m_RideF - m_RideR, Mathf.Max(0.5f, m_FrontAxleZ - m_RearAxleZ)) * Mathf.Rad2Deg;
            Vector3 pivot = m_BodyPivot;
            Quaternion bodyRot = Quaternion.AngleAxis(pitch, m_BodyAxis);
            Vector3 lift = m_BodyUp * ((m_RideF + m_RideR) * 0.5f);

            Quaternion flapRot = Quaternion.identity;
            if (m_FrontFlaps.Count > 0) flapRot = Quaternion.AngleAxis(m_FrontFlap, m_FrontFlaps[0].axis);
            foreach (var n in m_Body)
            {
                Vector3 p = n.pos;
                Quaternion r = n.rot;
                if (n.flap)
                {
                    p = n.pivot + flapRot * (p - n.pivot);
                    r = flapRot * r;
                }
                n.t.localPosition = pivot + bodyRot * (p - pivot) + lift;
                n.t.localRotation = bodyRot * r;
            }

            if (m_Drs != null)
            {
                float angle = m_RearFlap + m_DrsOpen * DrsOpenDegrees;
                Quaternion q = Quaternion.AngleAxis(angle, m_Drs.axis);
                m_Drs.t.localPosition = m_Drs.pivot + q * (m_Drs.pos - m_Drs.pivot);
                m_Drs.t.localRotation = q * m_Drs.rot;
            }

            foreach (var w in m_Wheels)
            {
                float camber = w.front ? m_CamberF : m_CamberR;
                float toe = w.front ? m_ToeF : m_ToeR;
                // Negative camber leans the top of the wheel inboard; front toe is "out", rear toe is "in".
                Quaternion q = Quaternion.AngleAxis(-w.side * camber, w.axis) * Quaternion.AngleAxis(w.side * toe * (w.front ? 1f : -1f), w.axis2);
                w.t.localPosition = w.pivot + q * (w.pos - w.pivot);
                w.t.localRotation = q * w.rot;
            }
        }

        // =====================================================================================================
        // POINTS OF INTEREST
        // =====================================================================================================
        /// <summary>A point on the car (world space) for the callout line and camera focus. Picks the camera side.</summary>
        public Vector3 PartPoint(GaragePart part, Vector3 viewer)
        {
            Vector3 local = PartLocal(part);
            if (Mathf.Abs(local.x) > 0.01f)
            {
                Vector3 v = transform.InverseTransformPoint(viewer);
                local.x = Mathf.Abs(local.x) * (v.x >= 0f ? 1f : -1f);
            }
            return transform.TransformPoint(local);
        }

        public Vector3 PartLocal(GaragePart part)
        {
            var b = LocalBounds;
            float halfW = b.extents.x;
            float front = b.max.z, rear = b.min.z;
            float wf = m_FrontAxleZ;
            float r = WheelRadius > 0f ? WheelRadius : 0.36f;
            switch (part)
            {
                case GaragePart.FrontWing: return new Vector3(halfW * 0.62f, 0.3f, front - 0.36f);
                case GaragePart.RearWing: return new Vector3(halfW * 0.35f, 0.94f, rear + 0.22f);
                case GaragePart.Suspension: return new Vector3(halfW * 0.5f, r * 1.25f, wf + 0.05f);
                case GaragePart.Brakes: return new Vector3(halfW * 0.74f, r, wf + 0.06f);
                case GaragePart.Tyre: return new Vector3(halfW * 0.98f, r * 1.05f, wf + 0.06f);
                case GaragePart.Powertrain: return new Vector3(halfW * 0.32f, 0.86f, b.center.z - 0.95f);
                case GaragePart.Cockpit: return new Vector3(0f, 0.86f, b.center.z + 0.25f);
                case GaragePart.Body: return new Vector3(halfW * 0.6f, 0.55f, b.center.z - 0.55f);
            }
            return b.center;
        }

        void OnDestroy()
        {
            foreach (var p in m_Livery) DestroyPaintable(p);
            DestroyPaintable(m_Rim);
            DestroyPaintable(m_Sidewall);
            DestroyPaintable(m_DiscFront);
            DestroyPaintable(m_DiscRear);
            DestroyPaintable(m_Halo);
            if (m_Recolor != null) Destroy(m_Recolor);
        }

        void DestroyPaintable(Paintable p)
        {
            if (p == null) return;
            Release(ref p.baseRT);
            Release(ref p.emissionRT);
            if (p.instance != null) Destroy(p.instance);
        }
    }
}
