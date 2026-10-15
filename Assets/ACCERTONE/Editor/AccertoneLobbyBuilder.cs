using System.Collections.Generic;
using System.Linq;
using Accertone.Branding;
using Accertone.Lobby;
using Accertone.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Accertone.EditorTools
{
    /// <summary>
    /// Builds the ACCERTONE lobby / showroom scene from scratch.
    /// Re-runnable: ACCERTONE > Build Lobby Scene.
    /// </summary>
    public static class AccertoneLobbyBuilder
    {
        public const string ScenePath = AccertoneBrandAssets.SceneDir + "/ACCERTONE_Lobby.unity";
        const string WordmarkPrefabPath = AccertoneBrandAssets.PrefabDir + "/Branding/ACCERTONE_Wordmark.prefab";
        const string WallSignPrefabPath = AccertoneBrandAssets.PrefabDir + "/Branding/ACCERTONE_WallSign.prefab";
        const string TransitionPrefabPath = AccertoneBrandAssets.PrefabDir + "/Branding/ACCERTONE_TransitionOverlay.prefab";
        const string PodiumPrefabPath = AccertoneBrandAssets.PrefabDir + "/Lobby/ACCERTONE_DisplayPodium.prefab";

        /// <summary>Raised after the showroom and UI are built, before the scene is saved and probes are baked.
        /// Modules (e.g. _LobbyModule car + driver staging) hook in here so a rebuild never loses them.</summary>
        public static event System.Action<Scene> PostBuild;

        static AccertoneBrandIdentity s_Id;
        static AccertoneBrandPalette P { get { return s_Id.palette; } }
        static AccertoneTypography T { get { return s_Id.typography; } }

        class Mats
        {
            public Material floor, wallPanel, backing, ceiling, podium, turntable, trim, paint, paintAccent;
            public Material lightBox, ledWhite, ledSoft, ledFaint, ledAccent, signAccent, ticks, blade, glowWall, glowAccent, floorGlow, beam;
            public Material signFace, signHalo;
        }

        class Meshes
        {
            public Mesh podiumBase, turntable, podiumLed, dialTicks, floorRingA, floorRingB, ceilingRing, beam, quad;
        }

        [MenuItem("Accert one/Build Lobby Scene", priority = 0)]
        static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Debug.Log(Build());
        }

        public static string Build()
        {
            s_Id = AccertoneBrandAssets.EnsureBrandSystem();
            if (AccertoneBrandIdentity.Default == null)
                return "[ACCERTONE] Brand identity could not be loaded from Resources.";

            AccertoneBrandAssets.GenerateTexture("T_SoftEllipse", 256, 128, AccertoneBrandAssets.Falloff.SoftEllipse, true);
            AccertoneBrandAssets.GenerateTexture("T_GradientH", 256, 8, AccertoneBrandAssets.Falloff.HorizontalFade, true);
            AccertoneBrandAssets.GenerateTexture("T_GradientV", 8, 256, AccertoneBrandAssets.Falloff.VerticalFade, true);
            AccertoneBrandAssets.GenerateTexture("T_Dot", 64, 64, AccertoneBrandAssets.Falloff.Dot, true);
            AccertoneBrandAssets.GenerateTexture("T_SoftLine", 512, 32, AccertoneBrandAssets.Falloff.SoftLine, false);
            AccertoneBrandAssets.GenerateTexture("T_RadialGlow", 256, 256, AccertoneBrandAssets.Falloff.SoftEllipse, false);

            var mats = BuildMaterials();
            var meshes = BuildMeshes();
            var post = BuildVolumeProfile();
            EnsurePipelineSettings();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            ConfigureEnvironmentLighting();
            var showroom = new GameObject("ACCERTONE_Showroom").transform;
            var powerUp = showroom.gameObject.AddComponent<ShowroomPowerUp>();
            BuildRoom(showroom, mats);
            BuildPodium(showroom, mats, meshes);
            BuildWallSign(showroom, mats, meshes);
            BuildLights(showroom, meshes, mats);
            var probe = BuildReflectionProbe(showroom);
            BuildPostVolume(post);
            BuildCamera();
            BuildUI();

            if (PostBuild != null)
            {
                try { PostBuild(scene); }
                catch (System.Exception e) { Debug.LogException(e); }
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            FixEmissionKeywords();
            BakeProbe(probe);
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterInBuildSettings();
            PlayerSettings.productName = AccertoneBrandIdentity.OfficialName;
            AssetDatabase.SaveAssets();

            return "[ACCERTONE] Lobby built: " + ScenePath + " (" + showroom.GetComponentsInChildren<ShowroomPoweredElement>(true).Length + " powered elements, power-up " + (powerUp != null) + ")";
        }

        // =================================================================== Assets

        static Mats BuildMaterials()
        {
            var glowTex = AssetDatabase.LoadAssetAtPath<Texture2D>(AccertoneBrandAssets.TextureDir + "/T_SoftEllipse.png");
            var lineTex = AssetDatabase.LoadAssetAtPath<Texture2D>(AccertoneBrandAssets.TextureDir + "/T_SoftLine.png");
            Color ledCool = AccertoneBrandPalette.Hex(0xDCE6FF);

            var m = new Mats();
            m.floor = AccertoneBrandAssets.Lit("M_Floor_PolishedGraphite", AccertoneBrandPalette.Hex(0x0B0C0F), 0f, 0.88f);
            m.wallPanel = AccertoneBrandAssets.Lit("M_Wall_Panel", AccertoneBrandPalette.Hex(0x1E2128), 0f, 0.36f);
            m.backing = AccertoneBrandAssets.Lit("M_Wall_Backing", AccertoneBrandPalette.Hex(0x040405), 0f, 0.1f);
            m.ceiling = AccertoneBrandAssets.Lit("M_Ceiling", AccertoneBrandPalette.Hex(0x0A0B0D), 0f, 0.2f);
            m.podium = AccertoneBrandAssets.Lit("M_Podium_Base", AccertoneBrandPalette.Hex(0x23262D), 0.45f, 0.6f);
            m.turntable = AccertoneBrandAssets.Lit("M_Podium_Turntable", AccertoneBrandPalette.Hex(0x2B2F37), 0.2f, 0.72f);
            m.lightBox = AccertoneBrandAssets.Lit("M_LightBox", Color.black, 0f, 0.2f, AccertoneBrandPalette.Hex(0xE8EEFF), 1.6f);
            m.trim = AccertoneBrandAssets.Lit("M_Trim_BrushedMetal", AccertoneBrandPalette.Hex(0x707680), 1f, 0.55f);
            m.paint = AccertoneBrandAssets.Lit("M_Floor_Marking", AccertoneBrandPalette.Hex(0x30343C), 0f, 0.6f);
            m.paintAccent = AccertoneBrandAssets.Lit("M_Floor_Marking_Accent", P.accentDeep * 0.75f, 0f, 0.6f);
            m.ledWhite = AccertoneBrandAssets.Lit("M_LED_White", Color.black, 0f, 0.5f, ledCool, 3.2f);
            m.ledSoft = AccertoneBrandAssets.Lit("M_LED_White_Soft", Color.black, 0f, 0.5f, ledCool, 1.4f);
            m.ledFaint = AccertoneBrandAssets.Lit("M_LED_White_Faint", Color.black, 0f, 0.5f, ledCool, 0.45f);
            m.ledAccent = AccertoneBrandAssets.Lit("M_LED_Accent", AccertoneBrandPalette.Hex(0x1A0306), 0f, 0.6f, P.accent, 2.0f);
            m.signAccent = AccertoneBrandAssets.Lit("M_Sign_Accent", AccertoneBrandPalette.Hex(0x1A0306), 0.2f, 0.7f, P.accent, 1.9f);
            m.ticks = AccertoneBrandAssets.Lit("M_Turntable_Dial", AccertoneBrandPalette.Hex(0x5C626C), 0.3f, 0.5f, P.silver, 0.18f);
            m.blade = AccertoneBrandAssets.Lit("M_Wall_Blade", AccertoneBrandPalette.Hex(0x111317), 0.6f, 0.5f);
            m.glowWall = AccertoneBrandAssets.Glow("M_Glow_WallHalo", glowTex, AccertoneBrandPalette.Hex(0x9DB6FF), 0.11f);
            m.glowAccent = AccertoneBrandAssets.Glow("M_Glow_AccentLine", lineTex, P.accent, 0.55f);
            var radial = AssetDatabase.LoadAssetAtPath<Texture2D>(AccertoneBrandAssets.TextureDir + "/T_RadialGlow.png");
            m.floorGlow = AccertoneBrandAssets.Glow("M_Glow_FloorAccent", radial, P.accent, 0.2f);
            m.beam = AccertoneBrandAssets.Beam("M_LightShaft", ledCool, 0.035f, 1.5f, 2.4f);

            // Sign lettering (TextMeshPro material presets on the display font).
            var display = T.displayFont;
            m.signFace = AccertoneBrandAssets.TmpPreset("ACCERTONE_Display Sign Face", display);
            m.signFace.SetColor("_FaceColor", AccertoneBrandAssets.Hdr(Color.white, 0.94f));
            m.signFace.SetFloat("_OutlineWidth", 0f);
            m.signFace.DisableKeyword("UNDERLAY_ON");
            EditorUtility.SetDirty(m.signFace);

            m.signHalo = AccertoneBrandAssets.TmpPreset("ACCERTONE_Display Sign Halo", display);
            m.signHalo.SetColor("_FaceColor", new Color(1f, 1f, 1f, 0f));
            m.signHalo.SetFloat("_OutlineWidth", 0f);
            m.signHalo.EnableKeyword("UNDERLAY_ON");
            m.signHalo.SetColor("_UnderlayColor", new Color(0.72f, 0.82f, 1f, 0.3f));
            m.signHalo.SetFloat("_UnderlaySoftness", 1f);
            m.signHalo.SetFloat("_UnderlayDilate", 0f);
            m.signHalo.SetFloat("_UnderlayOffsetX", 0f);
            m.signHalo.SetFloat("_UnderlayOffsetY", 0f);
            EditorUtility.SetDirty(m.signHalo);
            return m;
        }

        static Meshes BuildMeshes()
        {
            string dir = AccertoneBrandAssets.MeshDir + "/";
            var x = new Meshes();
            x.podiumBase = AccertoneMeshFactory.SaveMesh(AccertoneMeshFactory.Lathe("Podium_Base", new[]
            {
                new Vector2(0f, 0.18f), new Vector2(3.70f, 0.18f), new Vector2(3.78f, 0.145f),
                new Vector2(3.78f, 0.03f), new Vector2(3.84f, 0f)
            }, 192), dir + "Podium_Base.asset");
            x.turntable = AccertoneMeshFactory.SaveMesh(AccertoneMeshFactory.Lathe("Podium_Turntable", new[]
            {
                new Vector2(0f, 0.21f), new Vector2(3.30f, 0.21f), new Vector2(3.34f, 0.195f), new Vector2(3.34f, 0.175f)
            }, 192), dir + "Podium_Turntable.asset");
            x.podiumLed = AccertoneMeshFactory.SaveMesh(AccertoneMeshFactory.Annulus("Podium_LEDRing", 3.46f, 3.53f, 192), dir + "Podium_LEDRing.asset");
            x.dialTicks = AccertoneMeshFactory.SaveMesh(AccertoneMeshFactory.DialTicks("Turntable_Dial", 3.24f, 0.11f, 0.26f, 0.014f, 72, 6), dir + "Turntable_Dial.asset");
            x.floorRingA = AccertoneMeshFactory.SaveMesh(AccertoneMeshFactory.Annulus("Floor_RingA", 4.36f, 4.39f, 192), dir + "Floor_RingA.asset");
            x.floorRingB = AccertoneMeshFactory.SaveMesh(AccertoneMeshFactory.Annulus("Floor_RingB", 5.20f, 5.212f, 192), dir + "Floor_RingB.asset");
            x.ceilingRing = AccertoneMeshFactory.SaveMesh(AccertoneMeshFactory.Annulus("Ceiling_Ring", 3.55f, 3.68f, 192), dir + "Ceiling_Ring.asset");
            x.beam = AccertoneMeshFactory.SaveMesh(AccertoneMeshFactory.Lathe("LightShaft", new[]
            {
                new Vector2(0.32f, 0f), new Vector2(3.6f, -7.15f)
            }, 96), dir + "LightShaft.asset");
            x.quad = AccertoneMeshFactory.SaveMesh(AccertoneMeshFactory.Quad("Quad"), dir + "Quad.asset");
            return x;
        }

        static VolumeProfile BuildVolumeProfile()
        {
            string path = AccertoneBrandAssets.SettingsDir + "/ACCERTONE_Lobby_PostFX.asset";
            // Rebuild the profile in place: deleting and recreating the asset would leave the open scene's Volume
            // pointing at a destroyed object (no post-processing) until the next reload.
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            else
            {
                profile.components.Clear();
                foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (sub is VolumeComponent) Object.DestroyImmediate(sub, true);
            }

            var tone = Add<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.ACES);

            var bloom = Add<Bloom>(profile);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.75f);
            bloom.scatter.Override(0.72f);
            bloom.highQualityFiltering.Override(true);

            var color = Add<ColorAdjustments>(profile);
            color.postExposure.Override(0.5f);
            color.contrast.Override(14f);
            color.saturation.Override(-6f);

            var wb = Add<WhiteBalance>(profile);
            wb.temperature.Override(-7f);

            var vignette = Add<Vignette>(profile);
            vignette.intensity.Override(0.34f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(Color.black);

            var grain = Add<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.12f);
            grain.response.Override(0.85f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        static TComp Add<TComp>(VolumeProfile profile) where TComp : VolumeComponent
        {
            var c = profile.Add<TComp>(true);
            c.name = typeof(TComp).Name;
            AssetDatabase.AddObjectToAsset(c, profile);
            return c;
        }

        static void EnsurePipelineSettings()
        {
            var rp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (rp == null) return;
            var so = new SerializedObject(rp);
            var p = so.FindProperty("m_AdditionalLightShadowsSupported");
            if (p != null) p.boolValue = true;
            p = so.FindProperty("m_SupportsHDR");
            if (p != null) p.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // =================================================================== Environment

        static void ConfigureEnvironmentLighting()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = AccertoneBrandPalette.Hex(0x1D2028);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = AccertoneBrandPalette.Hex(0x07080A);
            RenderSettings.fogStartDistance = 16f;
            RenderSettings.fogEndDistance = 52f;
            RenderSettings.sun = null;
        }

        static void SetStatic(GameObject go, bool batching)
        {
            var flags = StaticEditorFlags.ReflectionProbeStatic;
            if (batching) flags |= StaticEditorFlags.BatchingStatic;
            GameObjectUtility.SetStaticEditorFlags(go, flags);
        }

        static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool batching = true, bool shadows = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            if (!shadows) r.shadowCastingMode = ShadowCastingMode.Off;
            SetStatic(go, batching);
            return go;
        }

        static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 euler, bool isStatic = true, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            if (!shadows) r.shadowCastingMode = ShadowCastingMode.Off;
            if (isStatic) SetStatic(go, false);
            return go;
        }

        static ShowroomPoweredElement Power(GameObject go, int group, bool flicker = false, string property = "_EmissionColor", float pulse = 0f)
        {
            var e = go.AddComponent<ShowroomPoweredElement>();
            e.group = group;
            e.flicker = flicker;
            e.colorProperty = property;
            e.pulseAmplitude = pulse;
            return e;
        }

        static void BuildRoom(Transform root, Mats m)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(root, false);
            floor.transform.localPosition = new Vector3(0f, 0f, -4.5f);
            floor.transform.localScale = new Vector3(1.8f, 1f, 2.6f);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.GetComponent<MeshRenderer>().sharedMaterial = m.floor;
            SetStatic(floor, true);

            // Rear brand wall: split panel rhythm with a horizontal light reveal.
            var rear = new GameObject("RearWall").transform;
            rear.SetParent(root, false);
            Box("Backing", rear, new Vector3(0f, 3.8f, 7.12f), new Vector3(16.4f, 7.6f, 0.12f), m.backing);
            for (int i = 0; i < 10; i++)
            {
                float x = -8f + 0.8f + i * 1.6f;
                Box("Panel_Low_" + i, rear, new Vector3(x, 1.1f, 6.98f), new Vector3(1.575f, 2.2f, 0.12f), m.wallPanel);
                Box("Panel_High_" + i, rear, new Vector3(x, 4.85f, 6.98f), new Vector3(1.575f, 5.22f, 0.12f), m.wallPanel);
            }

            // Side walls with vertical light blades.
            for (int s = -1; s <= 1; s += 2)
            {
                string side = s < 0 ? "L" : "R";
                var wall = new GameObject("SideWall_" + side).transform;
                wall.SetParent(root, false);
                float x = 8f * s;
                Box("Backing", wall, new Vector3(x + 0.12f * s, 3.8f, -4.5f), new Vector3(0.12f, 7.6f, 23.2f), m.backing);
                int count = 12;
                float depth = 23f / count;
                for (int i = 0; i < count; i++)
                {
                    float z = -16f + depth * 0.5f + i * depth;
                    Box("Panel_" + i, wall, new Vector3(x, 3.72f, z), new Vector3(0.12f, 7.44f, depth - 0.03f), m.wallPanel);
                }
                float[] blades = { -7f, -2.4f, 2.4f };
                for (int i = 0; i < blades.Length; i++)
                {
                    Box("Blade_" + i, wall, new Vector3(x - 0.33f * s, 3.72f, blades[i]), new Vector3(0.6f, 7.44f, 0.16f), m.blade);
                }
            }

            // Ceiling with long LED runs.
            var ceiling = new GameObject("Ceiling").transform;
            ceiling.SetParent(root, false);
            Box("Slab", ceiling, new Vector3(0f, 7.5f, -4.5f), new Vector3(16.2f, 0.1f, 23.2f), m.ceiling);
            float[] runs = { -5.4f, -2.6f, 2.6f, 5.4f };
            for (int i = 0; i < runs.Length; i++)
                Power(Box("LED_Run_" + i, ceiling, new Vector3(runs[i], 7.43f, -5.5f), new Vector3(0.07f, 0.04f, 19f), m.ledWhite, false, false), 0, true);
        }

        static void BuildPodium(Transform root, Mats m, Meshes x)
        {
            var podium = new GameObject("DisplayPodium").transform;
            podium.SetParent(root, false);

            MeshObject("Podium_Base", podium, x.podiumBase, m.podium, Vector3.zero, Vector3.zero);
            var led = MeshObject("Podium_LEDRing", podium, x.podiumLed, m.ledAccent, new Vector3(0f, 0.1812f, 0f), Vector3.zero, true, false);
            Power(led, 3, false, "_EmissionColor", 0.06f);

            var turntable = new GameObject("Turntable").transform;
            turntable.SetParent(podium, false);
            MeshObject("Turntable_Top", turntable, x.turntable, m.turntable, Vector3.zero, Vector3.zero, false);
            MeshObject("Turntable_Dial", turntable, x.dialTicks, m.ticks, new Vector3(0f, 0.2112f, 0f), Vector3.zero, false, false);

            // The future car's reference point: everything (gizmo envelope, walk clearance) is built around it.
            var slot = new GameObject("CarSpawnAnchor").transform;
            slot.SetParent(turntable, false);
            slot.localPosition = new Vector3(0f, 0.211f, 0f);
            slot.localEulerAngles = new Vector3(0f, 214f, 0f);

            var mount = podium.gameObject.AddComponent<CarDisplayMount>();
            mount.turntable = turntable;
            mount.carSlot = slot;

            // Floor markings: precision rings, a runway and a grid line in muted accent.
            var marks = new GameObject("FloorMarkings").transform;
            marks.SetParent(root, false);
            MeshObject("Ring_Inner", marks, x.floorRingA, m.paint, new Vector3(0f, 0.002f, 0f), Vector3.zero, true, false);
            MeshObject("Ring_Outer", marks, x.floorRingB, m.paint, new Vector3(0f, 0.002f, 0f), Vector3.zero, true, false);
            Box("Runway_L", marks, new Vector3(-1.65f, 0.002f, -8.4f), new Vector3(0.05f, 0.004f, 7.6f), m.paint, true, false);
            Box("Runway_R", marks, new Vector3(1.65f, 0.002f, -8.4f), new Vector3(0.05f, 0.004f, 7.6f), m.paint, true, false);
            Box("GridLine", marks, new Vector3(0f, 0.003f, -4.62f), new Vector3(3.3f, 0.004f, 0.09f), m.paintAccent, true, false);

            // The LED ring's spill on the polished floor.
            var pool = MeshObject("AccentFloorGlow", marks, x.quad, m.floorGlow, new Vector3(0f, 0.006f, 0f), new Vector3(90f, 0f, 0f), true, false);
            pool.transform.localScale = new Vector3(11.5f, 11.5f, 1f);
            Power(pool, 3, false, "_Color", 0.05f);

            PrefabUtility.SaveAsPrefabAssetAndConnect(podium.gameObject, PodiumPrefabPath, InteractionMode.AutomatedAction);
        }

        static void BuildWallSign(Transform root, Mats m, Meshes x)
        {
            var display = T.displayFont;
            var go = new GameObject("ACCERTONE_WallSign");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0f, 4.5f, 6.80f);
            var sign = go.AddComponent<AccertoneWallSign>();

            var letters = new GameObject("Letters").transform;
            letters.SetParent(go.transform, false);

            // Measure at a reference size, then scale to the target width.
            string brandName = s_Id.DisplayName; // "Accert one", never re-cased
            var face = Tmp3D("Face", letters, display, m.signFace);
            face.text = brandName;
            face.fontSize = 10f;
            float w10 = face.GetPreferredValues(brandName).x;
            const float targetWidth = 4.8f;
            float fontSize = 10f * targetWidth / Mathf.Max(0.01f, w10);
            face.fontSize = fontSize;
            face.ForceMeshUpdate();
            float capH = Mathf.Max(0.1f, face.textBounds.size.y);
            float width = face.GetPreferredValues(brandName).x;

            float markW = 0.82f * capH, gap = 0.34f * capH;
            float total = markW + gap + width;
            float left = -total * 0.5f;
            float lettersX = left + markW + gap;
            LayoutTmp3D(face, lettersX, 0f, width, capH);

            var depth = new List<TMP_Text>();
            for (int i = 1; i <= 6; i++)
            {
                var d = Tmp3D("Depth_" + i, letters, display, display.material);
                d.text = brandName;
                d.fontSize = fontSize;
                LayoutTmp3D(d, lettersX, 0.0095f * i, width, capH);
                depth.Add(d);
            }

            // Accent slashes: the same forward-leaning mark as the UI wordmark, extruded.
            var mark = new GameObject("Mark").transform;
            mark.SetParent(go.transform, false);
            float h = capH, hb = -capH * 0.5f, ht = capH * 0.5f;
            var slashA = AccertoneMeshFactory.SaveMesh(AccertoneMeshFactory.Prism("Sign_SlashA", new[]
            {
                new Vector2(left, hb), new Vector2(left + 0.26f * h, ht), new Vector2(left + 0.53f * h, ht), new Vector2(left + 0.27f * h, hb)
            }, 0.07f), AccertoneBrandAssets.MeshDir + "/Sign_SlashA.asset");
            float x0 = left + 0.39f * h;
            var slashB = AccertoneMeshFactory.SaveMesh(AccertoneMeshFactory.Prism("Sign_SlashB", new[]
            {
                new Vector2(x0, hb), new Vector2(x0 + 0.26f * h, ht), new Vector2(x0 + 0.43f * h, ht), new Vector2(x0 + 0.17f * h, hb)
            }, 0.07f), AccertoneBrandAssets.MeshDir + "/Sign_SlashB.asset");
            Power(MeshObject("Slash_A", mark, slashA, m.signAccent, Vector3.zero, Vector3.zero, true, false), 3);
            var b = MeshObject("Slash_B", mark, slashB, m.signAccent, Vector3.zero, Vector3.zero, true, false);
            Power(b, 3);

            // Controlled backlight wash on the wall behind the lettering.
            var card = MeshObject("BacklightWash", go.transform, x.quad, m.glowWall, new Vector3(0f, -0.05f * capH, 0.105f), Vector3.zero, true, false);
            card.transform.localScale = new Vector3(total * 1.55f, capH * 3.6f, 1f);
            Power(card, 3, false, "_Color");

            var logo = new GameObject("OfficialLogo");
            logo.transform.SetParent(go.transform, false);
            var sr = logo.AddComponent<SpriteRenderer>();
            logo.SetActive(false);

            sign.face = face;
            sign.depthLayers = depth.ToArray();
            sign.haloLayer = null;
            sign.markRoot = mark.gameObject;
            sign.lettersRoot = letters.gameObject;
            sign.logoRenderer = sr;
            sign.Refresh();

            PrefabUtility.SaveAsPrefabAssetAndConnect(go, WallSignPrefabPath, InteractionMode.AutomatedAction);
        }

        static TextMeshPro Tmp3D(string name, Transform parent, TMP_FontAsset font, Material mat)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshPro>();
            t.font = font;
            t.fontSharedMaterial = mat;
            t.characterSpacing = 1.5f;
            t.fontStyle = FontStyles.Normal; // the name is shown exactly as "Accert one"
            t.alignment = TextAlignmentOptions.CaplineLeft;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.richText = true;
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            SetStatic(go, false);
            return t;
        }

        static void LayoutTmp3D(TMP_Text t, float x, float z, float width, float capH)
        {
            var rt = t.rectTransform;
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(width + capH, capH * 2.4f);
            rt.localPosition = new Vector3(x, 0f, z);
            t.ForceMeshUpdate();
        }

        static void BuildLights(Transform root, Meshes x, Mats m)
        {
            var lights = new GameObject("Lighting").transform;
            lights.SetParent(root, false);

            var ring = MeshObject("Ceiling_HaloRing", lights, x.ceilingRing, m.ledWhite, new Vector3(0f, 7.42f, 0f), new Vector3(180f, 0f, 0f), true, false);
            Power(ring, 1);

            // Studio light box: a large soft overhead diffuser. Out of frame, but it is what the glossy
            // floor (and later the car's paintwork) reflects.
            var box = Box("Ceiling_LightBox", lights, new Vector3(0f, 7.4f, -0.4f), new Vector3(8.4f, 0.04f, 3.4f), m.lightBox, false, false);
            Power(box, 1, true);
            var boxRear = Box("Ceiling_LightBox_Rear", lights, new Vector3(0f, 7.4f, 4.6f), new Vector3(12f, 0.04f, 0.5f), m.lightBox, false, false);
            Power(boxRear, 2, true);
            var shaft = MeshObject("LightShaft", lights, x.beam, m.beam, new Vector3(0f, 7.38f, 0f), Vector3.zero, false, false);
            Power(shaft, 1, false, "_Color");

            Power(Spot("Key_Overhead", lights, new Vector3(0f, 7.25f, 0f), new Vector3(0f, 0f, 0f), 64f, 34f, 14f, 58f, new Color(1f, 0.97f, 0.94f), LightShadows.Soft), 1);
            Power(Spot("Rim_Left", lights, new Vector3(-6.4f, 5.6f, 5.6f), new Vector3(0f, 0.5f, 0f), 42f, 20f, 17f, 42f, new Color(0.8f, 0.87f, 1f), LightShadows.Soft), 2);
            Power(Spot("Rim_Right", lights, new Vector3(6.4f, 5.6f, 5.6f), new Vector3(0f, 0.5f, 0f), 42f, 20f, 17f, 42f, new Color(0.8f, 0.87f, 1f), LightShadows.Soft), 2);
            Power(Spot("Fill_Front", lights, new Vector3(2.2f, 4.2f, -8.5f), new Vector3(0f, 0.6f, 0f), 36f, 18f, 18f, 16f, new Color(1f, 0.96f, 0.92f), LightShadows.None), 2);

            // Grazing wall-washers: tall, narrow fans of light up the brand wall.
            float[] washers = { -6.4f, -3.2f, 3.2f, 6.4f };
            for (int i = 0; i < washers.Length; i++)
                Power(Spot("WallWash_" + i, lights, new Vector3(washers[i], 0.1f, 5.7f), new Vector3(washers[i], 5.2f, 7.0f), 44f, 12f, 10f, 26f, new Color(0.78f, 0.85f, 1f), LightShadows.None), 2);

            // Soft top-down wash on the brand wall so its panel rhythm reads.
            Power(Spot("WallWash_Top", lights, new Vector3(0f, 7.3f, 3.2f), new Vector3(0f, 2.6f, 7.0f), 78f, 30f, 12f, 14f, new Color(0.86f, 0.9f, 1f), LightShadows.None), 2);
        }

        static GameObject Spot(string name, Transform parent, Vector3 pos, Vector3 target, float angle, float inner, float range, float intensity, Color color, LightShadows shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            Vector3 dir = target - pos;
            if (target == Vector3.zero && pos.x == 0f && pos.z == 0f) dir = Vector3.down;
            go.transform.rotation = Quaternion.LookRotation(dir.normalized, Mathf.Abs(Vector3.Dot(dir.normalized, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.spotAngle = angle;
            l.innerSpotAngle = inner;
            l.range = range;
            l.intensity = intensity;
            l.color = color;
            l.shadows = shadows;
            l.shadowStrength = 0.9f;
            return go;
        }

        static GameObject Point(string name, Transform parent, Vector3 pos, float range, float intensity, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = range;
            l.intensity = intensity;
            l.color = color;
            l.shadows = LightShadows.None;
            return go;
        }

        static ReflectionProbe BuildReflectionProbe(Transform root)
        {
            var go = new GameObject("ReflectionProbe_Showroom");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0f, 2.2f, -1.5f);
            var p = go.AddComponent<ReflectionProbe>();
            p.mode = ReflectionProbeMode.Baked;
            p.boxProjection = true;
            p.size = new Vector3(16f, 7.6f, 23f);
            p.center = new Vector3(0f, 1.6f, -3f);
            p.resolution = 512;
            p.hdr = true;
            p.intensity = 1f;
            return p;
        }

        static void BakeProbe(ReflectionProbe probe)
        {
            if (probe == null) return;
            string dir = AccertoneBrandAssets.SceneDir + "/ACCERTONE_Lobby";
            AccertoneBrandAssets.EnsureFolder(dir);
            string path = dir + "/ReflectionProbe-Showroom.exr";
            UnityEditor.Lightmapping.BakeReflectionProbe(probe, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            // No lightmap bake means no LightingData asset to hold a "Baked" probe, so bind the cubemap explicitly.
            var cube = AssetDatabase.LoadAssetAtPath<Texture>(path);
            if (cube != null)
            {
                probe.mode = ReflectionProbeMode.Custom;
                probe.customBakedTexture = cube;
            }
        }

        static void BuildPostVolume(VolumeProfile profile)
        {
            var go = new GameObject("PostFX_Volume");
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = 1f;
            v.sharedProfile = profile;
        }

        static void BuildCamera()
        {
            var focus = new GameObject("CameraFocus");
            focus.transform.position = new Vector3(0.5f, 1.3f, 0f);

            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(-1.8f, 1.5f, -13.2f);
            go.transform.rotation = Quaternion.LookRotation(focus.transform.position - go.transform.position);
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 34f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 80f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = AccertoneBrandPalette.Hex(0x050506);
            cam.allowHDR = true;
            go.AddComponent<AudioListener>();
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.dithering = true;

            var rig = go.AddComponent<LobbyCameraRig>();
            rig.focus = focus.transform;
        }

        // =================================================================== UI

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static RectTransform Pin(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            return rt;
        }

        static RectTransform Pin(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            Pin(rt, anchor, pivot, pos);
            rt.sizeDelta = size;
            return rt;
        }

        static void Stretch(RectTransform rt, float l = 0f, float r = 0f, float t = 0f, float b = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
        }

        static readonly Vector2 TL = new Vector2(0f, 1f);
        static readonly Vector2 TR = new Vector2(1f, 1f);
        static readonly Vector2 BL = new Vector2(0f, 0f);
        static readonly Vector2 BR = new Vector2(1f, 0f);
        static readonly Vector2 ML = new Vector2(0f, 0.5f);
        static readonly Vector2 MR = new Vector2(1f, 0.5f);
        static readonly Vector2 C = new Vector2(0.5f, 0.5f);

        static TextMeshProUGUI Text(Transform parent, string name, string text, BrandTextRole role, BrandColorRole color, float alpha = 1f, float size = 0f, float extraSpacing = 0f, bool applyColor = true, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.alignment = align;
            t.richText = true;
            t.text = text;
            var st = rt.gameObject.AddComponent<BrandTextStyle>();
            st.role = role;
            st.color = color;
            st.alpha = alpha;
            st.sizeOverride = size;
            st.extraSpacing = extraSpacing;
            st.applyColor = applyColor;
            st.Apply();
            Vector2 pref = t.GetPreferredValues(text);
            rt.sizeDelta = new Vector2(Mathf.Ceil(pref.x) + 2f, Mathf.Ceil(pref.y));
            return t;
        }

        static AccertoneShape Shape(Transform parent, string name, BrandColorRole? role, float alpha, float skew = 0f, float chamfer = 0f, ChamferCorners corners = ChamferCorners.None, bool outline = false, float outlineWidth = 1f)
        {
            var rt = NewRect(name, parent);
            var s = rt.gameObject.AddComponent<AccertoneShape>();
            s.raycastTarget = false;
            s.skew = skew;
            s.chamfer = chamfer;
            s.corners = corners;
            s.outline = outline;
            s.outlineWidth = outlineWidth;
            if (role.HasValue)
            {
                var bc = rt.gameObject.AddComponent<BrandColor>();
                bc.role = role.Value;
                bc.alpha = alpha;
                bc.Apply();
            }
            return s;
        }

        static Image SpriteImage(Transform parent, string name, string sprite, BrandColorRole role, float alpha)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = AccertoneBrandAssets.LoadSprite(sprite);
            img.raycastTarget = false;
            var bc = rt.gameObject.AddComponent<BrandColor>();
            bc.role = role;
            bc.alpha = alpha;
            bc.Apply();
            return img;
        }

        static UIRevealGroup Reveal(GameObject go, float delay, Vector2 offset)
        {
            go.AddComponent<CanvasGroup>();
            var r = go.AddComponent<UIRevealGroup>();
            r.delay = delay;
            r.offset = offset;
            r.duration = 0.8f;
            return r;
        }

        static void BuildUI()
        {
            var canvasGo = new GameObject("UI_Lobby", typeof(RectTransform));
            canvasGo.layer = 5;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = canvasGo.transform;

            BuildShades(root);

            var wordmarkGo = BuildWordmarkUI(root);
            var wordmarkPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(wordmarkGo, WordmarkPrefabPath, InteractionMode.AutomatedAction);
            var wmRect = (RectTransform)wordmarkGo.transform;
            Pin(wmRect, TL, TL, new Vector2(64f, -46f));

            BuildPlayerCard(root);
            var nav = BuildNav(root);
            BuildSessionPanel(root);
            BuildCarInfo(root);
            var raceButton = BuildRaceAction(root);
            BuildHints(root);
            var transition = BuildTransition(root, wordmarkPrefab);

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();

            var controllerGo = new GameObject("LobbyController");
            var controller = controllerGo.AddComponent<LobbyController>();
            controller.navBar = nav;
            controller.raceButton = raceButton;
            controller.transition = transition;
        }

        static void BuildShades(Transform root)
        {
            var shades = NewRect("Backdrop_Shades", root);
            Stretch(shades);

            var left = SpriteImage(shades, "Shade_Left", "T_GradientH", BrandColorRole.Graphite, 0.82f);
            var lrt = left.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(0f, 1f);
            lrt.pivot = new Vector2(0f, 0.5f);
            lrt.sizeDelta = new Vector2(860f, 0f);
            lrt.anchoredPosition = Vector2.zero;

            var right = SpriteImage(shades, "Shade_Right", "T_GradientH", BrandColorRole.Graphite, 0.86f);
            var rrt = right.rectTransform;
            rrt.anchorMin = new Vector2(1f, 0f);
            rrt.anchorMax = new Vector2(1f, 1f);
            rrt.pivot = new Vector2(0.5f, 0.5f);
            rrt.sizeDelta = new Vector2(820f, 0f);
            rrt.anchoredPosition = new Vector2(-410f, 0f);
            rrt.localScale = new Vector3(-1f, 1f, 1f);

            var top = SpriteImage(shades, "Shade_Top", "T_GradientV", BrandColorRole.Graphite, 0.7f);
            var trt = top.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(0f, 260f);
            trt.anchoredPosition = new Vector2(0f, -130f);
            trt.localScale = new Vector3(1f, -1f, 1f);

            var bottom = SpriteImage(shades, "Shade_Bottom", "T_GradientV", BrandColorRole.Graphite, 0.85f);
            var brt = bottom.rectTransform;
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.sizeDelta = new Vector2(0f, 300f);
            brt.anchoredPosition = Vector2.zero;
        }

        /// <summary>The reusable UI wordmark (temporary typographic logo).</summary>
        static GameObject BuildWordmarkUI(Transform parent)
        {
            var root = NewRect("ACCERTONE_Wordmark", parent);
            root.gameObject.AddComponent<CanvasGroup>();
            var wm = root.gameObject.AddComponent<AccertoneWordmark>();

            const float rowH = 72f;
            var mask = NewRect("TypeMask", root);
            mask.gameObject.AddComponent<CanvasGroup>();
            var rm = mask.gameObject.AddComponent<RectMask2D>();

            var type = Text(mask, "Type", s_Id.DisplayName, BrandTextRole.Wordmark, BrandColorRole.White, 1f, 0f, 0f, false, TextAlignmentOptions.CaplineLeft);
            float typeW = type.rectTransform.sizeDelta.x;
            var spec = T.Get(BrandTextRole.Wordmark);
            float capH = Mathf.Round(spec.size * T.displayFont.faceInfo.capLine / T.displayFont.faceInfo.pointSize);

            float skew = Mathf.Round(capH * 0.27f);
            float aW = skew + Mathf.Round(capH * 0.27f);
            float bX = aW - skew + Mathf.Round(capH * 0.12f);
            float bW = skew + Mathf.Round(capH * 0.15f);
            float markW = bX + bW;
            float typeX = markW + Mathf.Round(capH * 0.36f);

            var mark = Pin(NewRect("Mark", root), TL, TL, Vector2.zero, new Vector2(markW, rowH));
            var slashA = Shape(mark, "Slash_A", null, 1f, skew);
            Pin(slashA.rectTransform, ML, ML, new Vector2(0f, 0f), new Vector2(aW, capH));
            var slashB = Shape(mark, "Slash_B", null, 1f, skew);
            Pin(slashB.rectTransform, ML, ML, new Vector2(bX, 0f), new Vector2(bW, capH));

            Pin(mask, TL, TL, new Vector2(typeX, 0f), new Vector2(typeW + 36f, rowH));
            Pin(type.rectTransform, ML, ML, new Vector2(2f, 0f), new Vector2(typeW, rowH));

            var tagline = Pin(NewRect("Tagline", root), TL, TL, new Vector2(typeX + 2f, -rowH - 2f), new Vector2(typeW, 18f));
            tagline.gameObject.AddComponent<CanvasGroup>();
            var rule = Shape(tagline, "Rule", BrandColorRole.Metallic, 0.9f);
            rule.alphaLeft = 1f;
            rule.alphaRight = 0.2f;
            Pin(rule.rectTransform, ML, ML, Vector2.zero, new Vector2(38f, 1.5f));
            var tagText = Text(tagline, "Text", "FORMULA RACING", BrandTextRole.Tagline, BrandColorRole.Metallic, 1f, 0f, 0f, true, TextAlignmentOptions.Left);
            Pin(tagText.rectTransform, ML, ML, new Vector2(52f, 0f));

            var logo = NewRect("OfficialLogo", root);
            Pin(logo, TL, TL, Vector2.zero, new Vector2(typeX + typeW, rowH));
            var logoImg = logo.gameObject.AddComponent<Image>();
            logoImg.preserveAspect = true;
            logoImg.raycastTarget = false;
            logo.gameObject.SetActive(false);

            root.sizeDelta = new Vector2(typeX + typeW + 36f, rowH + 22f);
            root.pivot = TL;

            wm.markRoot = mark;
            wm.markSlashes = new Graphic[] { slashA, slashB };
            wm.typeMask = rm;
            wm.typeText = type;
            wm.taglineRoot = tagline;
            wm.taglineRule = rule;
            wm.taglineText = tagText;
            wm.officialLogoImage = logoImg;
            wm.Refresh();

            // Pivot for the draw-in of the slashes and the rule.
            slashA.rectTransform.pivot = ML;
            slashB.rectTransform.pivot = ML;
            rule.rectTransform.pivot = ML;

            root.gameObject.AddComponent<AccertoneWordmarkReveal>();
            return root.gameObject;
        }

        static void BuildPlayerCard(Transform root)
        {
            var card = Pin(NewRect("PlayerCard", root), TL, TL, new Vector2(64f + 48f, -168f), new Vector2(460f, 64f));
            Reveal(card.gameObject, 1.2f, new Vector2(-16f, 0f));

            var av = Pin(NewRect("Avatar", card), TL, TL, Vector2.zero, new Vector2(60f, 60f));
            var avBg = Shape(av, "Fill", BrandColorRole.Panel, 0.95f, 0f, 12f, ChamferCorners.TopRight | ChamferCorners.BottomLeft);
            Stretch(avBg.rectTransform);
            var avLine = Shape(av, "Border", BrandColorRole.Hairline, 1f, 0f, 12f, ChamferCorners.TopRight | ChamferCorners.BottomLeft, true, 1f);
            Stretch(avLine.rectTransform);
            var avTick = Shape(av, "AccentTick", BrandColorRole.Accent, 1f);
            Pin(avTick.rectTransform, BL, BL, new Vector2(0f, 0f), new Vector2(18f, 3f));
            var initials = Text(av, "Initials", "01", BrandTextRole.Headline, BrandColorRole.TextPrimary, 1f, 24f, 0f, true, TextAlignmentOptions.Center);
            Pin(initials.rectTransform, C, C, new Vector2(0f, 1f));

            float x = 76f;
            var name = Text(card, "Name", "DRIVER 01", BrandTextRole.Title, BrandColorRole.TextPrimary);
            Pin(name.rectTransform, TL, TL, new Vector2(x, -1f));

            var dot = SpriteImage(card, "StatusDot", "T_Dot", BrandColorRole.Positive, 1f);
            Pin(dot.rectTransform, TL, ML, new Vector2(x + 1f, -38f), new Vector2(7f, 7f));
            var online = Text(card, "Status", "ONLINE", BrandTextRole.Caption, BrandColorRole.Positive);
            Pin(online.rectTransform, TL, ML, new Vector2(x + 14f, -38f));
            float cx = x + 14f + online.rectTransform.sizeDelta.x + 10f;
            var meta = Text(card, "Meta", "·   LICENCE A", BrandTextRole.Caption, BrandColorRole.TextMuted);
            Pin(meta.rectTransform, TL, ML, new Vector2(cx, -38f));

            var lvl = Text(card, "Level", "LVL 24", BrandTextRole.Caption, BrandColorRole.Silver, 1f, 10f);
            Pin(lvl.rectTransform, TL, ML, new Vector2(x, -56f));
            float barX = x + lvl.rectTransform.sizeDelta.x + 10f;
            const float barW = 150f;
            var barBg = Shape(card, "XP_Track", BrandColorRole.Hairline, 1f, 2f);
            Pin(barBg.rectTransform, TL, ML, new Vector2(barX, -56f), new Vector2(barW, 3f));
            var barFill = Shape(card, "XP_Fill", BrandColorRole.Silver, 0.95f, 2f);
            Pin(barFill.rectTransform, TL, ML, new Vector2(barX, -56f), new Vector2(barW * 0.68f, 3f));
            var pct = Text(card, "XP_Value", "68%", BrandTextRole.Caption, BrandColorRole.TextMuted, 1f, 10f, -8f);
            Pin(pct.rectTransform, TL, ML, new Vector2(barX + barW + 10f, -56f));
        }

        static LobbyNavBar BuildNav(Transform root)
        {
            var nav = Pin(NewRect("Navigation", root), TR, TR, new Vector2(-48f, -40f), new Vector2(10f, 48f));
            Reveal(nav.gameObject, 0.9f, new Vector2(0f, 10f));
            var layout = nav.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var fitter = nav.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var bar = nav.gameObject.AddComponent<LobbyNavBar>();
            string[] labels = { "HOME", "GARAGE", "CAREER", "ONLINE", "SETTINGS" };
            var tabs = new List<LobbyNavBar.Tab>();
            for (int i = 0; i < labels.Length; i++)
            {
                var tab = NewRect("Tab_" + labels[i], nav);
                var hit = tab.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                var button = tab.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.targetGraphic = hit;
                var hover = tab.gameObject.AddComponent<UIHoverState>();
                var hl = tab.gameObject.AddComponent<HorizontalLayoutGroup>();
                hl.padding = new RectOffset(16, 16, 12, 16);
                hl.childControlWidth = true;
                hl.childControlHeight = true;
                hl.childForceExpandWidth = false;
                hl.childForceExpandHeight = false;
                var label = Text(tab, "Label", labels[i], BrandTextRole.Label, i == 0 ? BrandColorRole.TextPrimary : BrandColorRole.TextMuted, 1f, 14f, 0f, true);
                label.GetComponent<BrandTextStyle>().applyColor = false;
                tabs.Add(new LobbyNavBar.Tab { id = labels[i].ToLowerInvariant(), button = button, label = label, hover = hover });
            }

            var indicator = Shape(nav, "ActiveIndicator", BrandColorRole.Accent, 1f, 2f);
            var le = indicator.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
            Pin(indicator.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(40f, 4f), new Vector2(40f, 2f));

            bar.tabs = tabs.ToArray();
            bar.indicator = indicator.rectTransform;
            bar.selectedIndex = 0;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(nav);
            bar.Refresh(true);

            var rule = Shape(root, "TopRule", BrandColorRole.Hairline, 1f);
            rule.alphaLeft = 0f;
            rule.alphaRight = 1f;
            Pin(rule.rectTransform, TR, TR, new Vector2(-64f, -96f), new Vector2(780f, 1f));
            Reveal(rule.gameObject, 1.0f, Vector2.zero);

            var season = Text(root, "SeasonTag", "SEASON 2026   ·   CHAMPIONSHIP  P4   ·   128 PTS", BrandTextRole.Caption, BrandColorRole.TextMuted, 1f, 10f);
            Pin(season.rectTransform, TR, TR, new Vector2(-64f, -106f));
            Reveal(season.gameObject, 1.1f, Vector2.zero);
            return bar;
        }

        static readonly Vector2[] Circuit =
        {
            new Vector2(0.10f, 0.16f), new Vector2(0.40f, 0.14f), new Vector2(0.64f, 0.15f), new Vector2(0.80f, 0.12f),
            new Vector2(0.92f, 0.20f), new Vector2(0.90f, 0.34f), new Vector2(0.76f, 0.40f), new Vector2(0.66f, 0.50f),
            new Vector2(0.72f, 0.62f), new Vector2(0.86f, 0.66f), new Vector2(0.94f, 0.78f), new Vector2(0.88f, 0.90f),
            new Vector2(0.74f, 0.88f), new Vector2(0.60f, 0.78f), new Vector2(0.46f, 0.84f), new Vector2(0.34f, 0.92f),
            new Vector2(0.20f, 0.88f), new Vector2(0.14f, 0.74f), new Vector2(0.24f, 0.62f), new Vector2(0.34f, 0.56f),
            new Vector2(0.30f, 0.44f), new Vector2(0.14f, 0.40f), new Vector2(0.05f, 0.30f)
        };

        static void BuildSessionPanel(Transform root)
        {
            const float W = 420f, H = 552f, PAD = 28f;
            var panel = Pin(NewRect("SessionPanel", root), MR, MR, new Vector2(-64f, 34f), new Vector2(W, H));
            Reveal(panel.gameObject, 1.35f, new Vector2(18f, 0f));

            var bg = Shape(panel, "Fill", BrandColorRole.Carbon, 0.9f, 0f, 22f, ChamferCorners.TopRight | ChamferCorners.BottomLeft);
            Stretch(bg.rectTransform);
            var border = Shape(panel, "Border", BrandColorRole.Hairline, 1f, 0f, 22f, ChamferCorners.TopRight | ChamferCorners.BottomLeft, true, 1f);
            Stretch(border.rectTransform);
            var tick = Shape(panel, "AccentTick", BrandColorRole.Accent, 1f);
            Pin(tick.rectTransform, TL, TL, Vector2.zero, new Vector2(44f, 3f));

            var header = Text(panel, "Header", "NEXT SESSION", BrandTextRole.Caption, BrandColorRole.TextMuted);
            Pin(header.rectTransform, TL, TL, new Vector2(PAD, -28f));
            var round = Text(panel, "Round", "ROUND 07 / 24", BrandTextRole.Caption, BrandColorRole.Silver, 1f, 0f, 0f, true, TextAlignmentOptions.TopRight);
            Pin(round.rectTransform, TR, TR, new Vector2(-PAD, -28f));

            var track = Text(panel, "TrackName", "VALCORSA", BrandTextRole.Headline, BrandColorRole.TextPrimary, 1f, 42f);
            Pin(track.rectTransform, TL, TL, new Vector2(PAD - 2f, -48f));
            var sub = Text(panel, "TrackType", "INTERNATIONAL CIRCUIT", BrandTextRole.Caption, BrandColorRole.TextSecondary);
            Pin(sub.rectTransform, TL, TL, new Vector2(PAD, -100f));

            // Circuit map.
            var map = Pin(NewRect("CircuitMap", panel), TL, TL, new Vector2(PAD, -128f), new Vector2(W - PAD * 2f, 176f));
            var road = NewRect("Road", map);
            Stretch(road, 10f, 10f, 10f, 10f);
            var roadLine = road.gameObject.AddComponent<UIPolyline>();
            roadLine.raycastTarget = false;
            roadLine.SetPoints(Circuit, true);
            roadLine.thickness = 9f;
            var roadColor = road.gameObject.AddComponent<BrandColor>();
            roadColor.role = BrandColorRole.Hairline;
            roadColor.alpha = 0.9f;
            roadColor.Apply();

            var lineRt = NewRect("Racing_Line", map);
            Stretch(lineRt, 10f, 10f, 10f, 10f);
            var line = lineRt.gameObject.AddComponent<UIPolyline>();
            line.raycastTarget = false;
            line.SetPoints(Circuit, true);
            line.thickness = 2.2f;
            var lineColor = lineRt.gameObject.AddComponent<BrandColor>();
            lineColor.role = BrandColorRole.Silver;
            lineColor.alpha = 0.95f;
            lineColor.Apply();

            var start = Shape(map, "StartFinish", BrandColorRole.Accent, 1f);
            Pin(start.rectTransform, C, C, Vector2.zero, new Vector2(3f, 15f));
            var startMarker = start.gameObject.AddComponent<TrackMapMarker>();
            startMarker.path = line;
            startMarker.animate = false;
            startMarker.startOffset = 0.035f;
            startMarker.alignToPath = true;
            start.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);

            var carDot = SpriteImage(map, "LiveCar", "T_Dot", BrandColorRole.Accent, 1f);
            Pin(carDot.rectTransform, C, C, Vector2.zero, new Vector2(9f, 9f));
            var dotGlow = SpriteImage(carDot.transform, "Glow", "T_SoftEllipse", BrandColorRole.Accent, 0.55f);
            Pin(dotGlow.rectTransform, C, C, Vector2.zero, new Vector2(34f, 34f));
            dotGlow.transform.SetAsFirstSibling();
            var liveMarker = carDot.gameObject.AddComponent<TrackMapMarker>();
            liveMarker.path = line;
            liveMarker.lapTime = 18f;
            liveMarker.startOffset = 0.3f;

            var sectorLabel = Text(map, "MapCaption", "LIVE  ·  LAP RECORD PACE", BrandTextRole.Caption, BrandColorRole.TextMuted, 1f, 9f);
            Pin(sectorLabel.rectTransform, BR, BR, new Vector2(0f, 0f));

            // Key numbers.
            float y = -320f;
            var div1 = Shape(panel, "Divider_A", BrandColorRole.Hairline, 1f);
            Pin(div1.rectTransform, TL, TL, new Vector2(PAD, y), new Vector2(W - PAD * 2f, 1f));
            string[] labels = { "LAPS", "LENGTH", "TURNS" };
            string[] values = { "15", "5.218", "17" };
            string[] units = { "", "KM", "" };
            float colW = (W - PAD * 2f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                float cx = PAD + colW * i;
                var l = Text(panel, "Stat_" + labels[i] + "_Label", labels[i], BrandTextRole.Caption, BrandColorRole.TextMuted);
                Pin(l.rectTransform, TL, TL, new Vector2(cx, y - 18f));
                var v = Text(panel, "Stat_" + labels[i] + "_Value", values[i], BrandTextRole.Data, BrandColorRole.TextPrimary, 1f, 26f);
                Pin(v.rectTransform, TL, TL, new Vector2(cx - 1f, y - 36f));
                if (!string.IsNullOrEmpty(units[i]))
                {
                    var u = Text(panel, "Stat_" + labels[i] + "_Unit", units[i], BrandTextRole.Caption, BrandColorRole.TextMuted, 1f, 10f);
                    Pin(u.rectTransform, TL, TL, new Vector2(cx + v.rectTransform.sizeDelta.x + 3f, y - 52f));
                }
            }

            y = -404f;
            var div2 = Shape(panel, "Divider_B", BrandColorRole.Hairline, 1f);
            Pin(div2.rectTransform, TL, TL, new Vector2(PAD, y), new Vector2(W - PAD * 2f, 1f));
            string[,] rows =
            {
                { "SESSION", "GRAND PRIX" },
                { "CONDITIONS", "DRY  ·  24°C" },
                { "GRID", "20 DRIVERS" },
                { "LAP RECORD", "1:29.874" }
            };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                float ry = y - 22f - i * 30f;
                var l = Text(panel, "Row_" + i + "_Label", rows[i, 0], BrandTextRole.Caption, BrandColorRole.TextMuted);
                Pin(l.rectTransform, TL, ML, new Vector2(PAD, ry - 8f));
                bool data = i == 3;
                var v = Text(panel, "Row_" + i + "_Value", rows[i, 1], data ? BrandTextRole.Data : BrandTextRole.Label, BrandColorRole.TextPrimary, 1f, data ? 15f : 13f, data ? 0f : -6f, true, TextAlignmentOptions.Right);
                Pin(v.rectTransform, TR, MR, new Vector2(-PAD, ry - 8f));
            }
        }

        static void BuildCarInfo(Transform root)
        {
            var info = Pin(NewRect("ChassisInfo", root), BL, BL, new Vector2(64f + 48f, 70f), new Vector2(480f, 176f));
            Reveal(info.gameObject, 1.5f, new Vector2(-16f, 0f));

            var label = Text(info, "Label", "CHASSIS", BrandTextRole.Caption, BrandColorRole.TextMuted);
            Pin(label.rectTransform, TL, TL, Vector2.zero);
            var idx = Text(info, "Index", "01 / 01", BrandTextRole.Caption, BrandColorRole.Silver);
            Pin(idx.rectTransform, TL, TL, new Vector2(label.rectTransform.sizeDelta.x + 14f, 0f));

            var model = Text(info, "Model", "AC-26", BrandTextRole.Display, BrandColorRole.TextPrimary, 1f, 54f, 2f);
            Pin(model.rectTransform, TL, TL, new Vector2(-3f, -14f));
            var desc = Text(info, "Description", "WORKS PROTOTYPE   ·   HYBRID V6", BrandTextRole.Caption, BrandColorRole.TextSecondary);
            Pin(desc.rectTransform, TL, TL, new Vector2(0f, -80f));

            string[] stats = { "POWER", "AERO", "GRIP" };
            int[] filled = { 8, 7, 9 };
            string[] vals = { "8.4", "7.2", "8.9" };
            for (int r = 0; r < stats.Length; r++)
            {
                float ry = -112f - r * 22f;
                var l = Text(info, "Stat_" + stats[r], stats[r], BrandTextRole.Caption, BrandColorRole.TextMuted);
                Pin(l.rectTransform, TL, ML, new Vector2(0f, ry));
                for (int s = 0; s < 10; s++)
                {
                    bool on = s < filled[r];
                    var seg = Shape(info, "Seg_" + stats[r] + "_" + s, on ? BrandColorRole.Silver : BrandColorRole.Hairline, on ? 0.9f : 1f, 3f);
                    Pin(seg.rectTransform, TL, ML, new Vector2(84f + s * 22f, ry), new Vector2(18f, 6f));
                }
                var v = Text(info, "Value_" + stats[r], vals[r], BrandTextRole.Data, BrandColorRole.TextSecondary, 1f, 13f);
                Pin(v.rectTransform, TL, ML, new Vector2(84f + 10f * 22f + 8f, ry));
            }
        }

        static Button BuildRaceAction(Transform root)
        {
            const float W = 420f, H = 92f;
            var group = Pin(NewRect("RaceAction", root), BR, BR, new Vector2(-64f, 64f), new Vector2(W, H + 40f));
            Reveal(group.gameObject, 1.65f, new Vector2(0f, -14f));

            var status = Pin(NewRect("Status", group), TL, TL, new Vector2(0f, 0f), new Vector2(W, 20f));
            var dot = SpriteImage(status, "Dot", "T_Dot", BrandColorRole.Positive, 1f);
            Pin(dot.rectTransform, ML, ML, new Vector2(1f, 0f), new Vector2(7f, 7f));
            var ready = Text(status, "Ready", "GRID READY", BrandTextRole.Caption, BrandColorRole.Positive);
            Pin(ready.rectTransform, ML, ML, new Vector2(14f, 0f));
            var meta = Text(status, "Meta", "·   EU-WEST   ·   24 MS", BrandTextRole.Caption, BrandColorRole.TextMuted);
            Pin(meta.rectTransform, ML, ML, new Vector2(14f + ready.rectTransform.sizeDelta.x + 10f, 0f));

            var btnRt = Pin(NewRect("RaceButton", group), BL, BL, Vector2.zero, new Vector2(W, H));
            var glow = SpriteImage(btnRt, "Glow", "T_SoftEllipse", BrandColorRole.Accent, 0.25f);
            Stretch(glow.rectTransform, -60f, -60f, -46f, -46f);

            var content = NewRect("Content", btnRt);
            Stretch(content);

            var fill = Shape(content, "Fill", null, 1f, 26f);
            Stretch(fill.rectTransform);
            fill.raycastTarget = true;
            fill.color = P.accent;
            var sheen = Shape(content, "Sheen", BrandColorRole.White, 0.07f, 13f);
            sheen.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            sheen.rectTransform.anchorMax = new Vector2(1f, 1f);
            sheen.rectTransform.offsetMin = new Vector2(13f, 0f);
            sheen.rectTransform.offsetMax = Vector2.zero;

            var label = Text(content, "Label", "RACE", BrandTextRole.Display, BrandColorRole.White, 1f, 44f, 6f);
            Pin(label.rectTransform, ML, ML, new Vector2(44f, 8f));
            var sub = Text(content, "Subtitle", "DEPLOY TO GRID", BrandTextRole.Caption, BrandColorRole.White, 0.82f);
            Pin(sub.rectTransform, ML, ML, new Vector2(40f, -24f));

            var chip = Pin(NewRect("KeyChip", content), ML, ML, new Vector2(40f + sub.rectTransform.sizeDelta.x + 12f, -24f), new Vector2(44f, 16f));
            var chipLine = Shape(chip, "Outline", BrandColorRole.White, 0.5f, 0f, 0f, ChamferCorners.None, true, 1f);
            Stretch(chipLine.rectTransform);
            var chipText = Text(chip, "Key", "ENTER", BrandTextRole.Caption, BrandColorRole.White, 0.85f, 8f, -12f, true, TextAlignmentOptions.Center);
            Pin(chipText.rectTransform, C, C, new Vector2(1f, 0f));

            var chev = Pin(NewRect("Chevrons", content), MR, MR, new Vector2(-40f, 0f), new Vector2(58f, 28f));
            float[] alphas = { 0.3f, 0.6f, 1f };
            for (int i = 0; i < 3; i++)
            {
                var c = Shape(chev, "Chevron_" + i, BrandColorRole.White, alphas[i], 10f);
                Pin(c.rectTransform, ML, ML, new Vector2(i * 20f, 0f), new Vector2(16f, 28f));
            }

            var button = btnRt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = fill;
            btnRt.gameObject.AddComponent<UIHoverState>();
            var action = btnRt.gameObject.AddComponent<AccertoneActionButton>();
            action.fill = fill;
            action.glow = glow;
            action.chevrons = chev;
            action.content = content;
            Object.DestroyImmediate(glow.GetComponent<BrandColor>());
            return button;
        }

        static void BuildHints(Transform root)
        {
            string key = "<color=#" + ColorUtility.ToHtmlStringRGB(P.silver) + ">";
            string hint = key + "ENTER</color>  RACE          " + key + "Q / E</color>  NAVIGATE          " + key + "ESC</color>  MENU";
            var t = Text(root, "InputHints", hint, BrandTextRole.Caption, BrandColorRole.TextMuted, 1f, 10f, 0f, true, TextAlignmentOptions.Center);
            Pin(t.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f));
            Reveal(t.gameObject, 1.9f, Vector2.zero);
        }

        static AccertoneTransitionOverlay BuildTransition(Transform root, GameObject wordmarkPrefab)
        {
            var overlay = NewRect("ACCERTONE_TransitionOverlay", root);
            Stretch(overlay);
            var bg = overlay.gameObject.AddComponent<Image>();
            bg.raycastTarget = true;
            var bgColor = overlay.gameObject.AddComponent<BrandColor>();
            bgColor.role = BrandColorRole.Graphite;
            bgColor.Apply();
            var group = overlay.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var wmGo = (GameObject)PrefabUtility.InstantiatePrefab(wordmarkPrefab, overlay);
            var wmRt = (RectTransform)wmGo.transform;
            wmRt.anchorMin = wmRt.anchorMax = C;
            wmRt.pivot = TL;
            wmRt.localScale = new Vector3(1.25f, 1.25f, 1f);
            float visibleWidth = wmRt.sizeDelta.x - 36f; // exclude the mask's italic-overhang margin
            wmRt.anchoredPosition = new Vector2(-visibleWidth * 1.25f * 0.5f, wmRt.sizeDelta.y * 1.25f * 0.5f + 34f);
            var reveal = wmGo.GetComponent<AccertoneWordmarkReveal>();
            reveal.playOnEnable = false;
            reveal.delay = 0.05f;

            var status = Text(overlay, "Status", "DEPLOYING TO GRID", BrandTextRole.Caption, BrandColorRole.Silver, 1f, 0f, 0f, true, TextAlignmentOptions.Center);
            Pin(status.rectTransform, C, C, new Vector2(0f, -54f));
            status.rectTransform.sizeDelta = new Vector2(420f, status.rectTransform.sizeDelta.y);

            var track = Shape(overlay, "ProgressTrack", BrandColorRole.Hairline, 1f);
            Pin(track.rectTransform, C, C, new Vector2(0f, -84f), new Vector2(320f, 2f));
            var fill = Shape(track.transform, "ProgressFill", BrandColorRole.Accent, 1f);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;

            var t = overlay.gameObject.AddComponent<AccertoneTransitionOverlay>();
            t.wordmarkReveal = reveal;
            t.statusText = status;
            t.progressFill = fill.rectTransform;

            PrefabUtility.SaveAsPrefabAssetAndConnect(overlay.gameObject, TransitionPrefabPath, InteractionMode.AutomatedAction);
            return t;
        }

        // =================================================================== Project

        /// <summary>
        /// URP re-derives _EMISSION from the material's GI flags whenever a material is (re)imported,
        /// so emissive materials must carry an emissive GI flag or they go dark.
        /// </summary>
        public static int FixEmissionKeywords()
        {
            int fixedCount = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { AccertoneBrandAssets.MaterialDir }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat == null || mat.shader == null || mat.shader.name != "Universal Render Pipeline/Lit") continue;
                bool emissive = mat.GetColor("_EmissionColor").maxColorComponent > 0.001f;
                var flags = emissive ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                if (emissive != mat.IsKeywordEnabled("_EMISSION") || mat.globalIlluminationFlags != flags) fixedCount++;
                mat.globalIlluminationFlags = flags;
                if (emissive) mat.EnableKeyword("_EMISSION");
                else mat.DisableKeyword("_EMISSION");
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
            return fixedCount;
        }

        static void RegisterInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
