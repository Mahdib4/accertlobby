using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Accertone.EditorTools;
using Accertone.Lobby;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Accertone.LobbyModule.EditorTools
{
    /// <summary>
    /// Accert one Lobby Module: the AC-26 display car on the podium, the presented lobby driver beside it, and the
    /// car/driver art assets.
    ///
    ///   Accert one > Lobby Module > Build Assets       textures, URP materials, AC-26 car prefab, rigged character prefabs
    ///   Accert one > Lobby Module > Stage In Lobby     car, walk area, character, camera follow, reflections
    ///   Accert one > Lobby Module > Build All          both, then rebuilds the lobby scene
    ///
    /// Staging also runs automatically after "Accert one > Build Lobby Scene", so a rebuild never loses the character.
    /// </summary>
    [InitializeOnLoad]
    public static class LobbyModuleBuilder
    {
        public const string Root = "Assets/_LobbyModule";
        const string CarModel = Root + "/Models/Car/aston_martin_aramco_amr25.glb";
        const string CarSource = Root + "/Textures/Car/Source";
        const string CarTex = Root + "/Textures/Car";
        const string LiveryDir = Root + "/Textures/Car/Livery";
        const string DriverTex = Root + "/Textures/Drivers";
        const string CarPrefabPath = Root + "/Prefabs/ACCERTONE_Car_AC26.prefab";

        // Car-local spot beside the cockpit (DriverMark on the AC-26 prefab).
        static readonly Vector3 DriverMarkLocal = new Vector3(-1.32f, 0f, 0.85f);
        const float DriverHeight = 1.78f;
        // Which character is presented in the lobby (index into Drivers).
        const int ActiveCharacter = 0;
        // Showroom presentation (PresentationSpinner): the main platform (car + driver) and the driver's own small
        // spinner start together once the showroom has powered up, with the same ease-in. Negative = counter-clockwise
        // seen from above.
        const float SpinnerStartDelay = 1.6f;
        const float SpinnerSpinUp = 2.5f;
        // Main platform: eases to a full stop with the car at its hero angle, 10° counter-clockwise from the opening.
        const float PlatformTravel = -10f;
        const float PlatformSpinDown = 2.5f;
        // Driver spinner: keeps turning (same direction) after the platform stops, until the driver faces the camera.
        const float DriverSpinnerSpeed = 12f;
        const float DriverSpinnerSpinDown = 3.5f;
        const float DriverSpinnerRadius = 0.46f;
        const float DriverSpinnerHeight = 0.04f;
        // The driver's opening facing: the original pose (looking at the cockpit, back to the camera) turned this many
        // degrees to the driver's own left (counter-clockwise seen from above).
        const float DriverTurnLeft = 30f;

        class DriverSpec
        {
            public string id;        // texture folder / prefix
            public string model;     // GLB file
            public string label;     // roster label
        }

        static readonly DriverSpec[] Drivers =
        {
            new DriverSpec { id = "Driver_01_Hero", model = "d278c77b974e412660f386722abd5ec9.glb", label = "01" },
            new DriverSpec { id = "Driver_02_Horizon", model = "Meshy_AI_White_Horizon_Pilot_1006174733_texture.glb", label = "02" },
            new DriverSpec { id = "Driver_03_Velocity", model = "Meshy_AI_White_Velocity_1006175448_texture.glb", label = "03" },
        };

        static string CharacterPrefabPath(DriverSpec d) { return Root + "/Prefabs/Characters/LobbyCharacter_" + d.label + ".prefab"; }
        static string CharacterMeshPath(DriverSpec d) { return Root + "/Characters/LobbyCharacter_" + d.label + "_Skinned.asset"; }

        static LobbyModuleBuilder()
        {
            AccertoneLobbyBuilder.PostBuild -= OnLobbyBuilt;
            AccertoneLobbyBuilder.PostBuild += OnLobbyBuilt;
        }

        static void OnLobbyBuilt(Scene scene)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath(Drivers[ActiveCharacter])) == null) return;
            Debug.Log(StageInScene(scene));
        }

        // ------------------------------------------------------------------ menus

        [MenuItem("Accert one/Lobby Module/Build Assets", priority = 20)]
        static void BuildAssetsMenu()
        {
            Debug.Log(BuildAssets(true));
        }

        [MenuItem("Accert one/Lobby Module/Stage In Lobby", priority = 21)]
        static void StageMenu()
        {
            Debug.Log(StageInOpenLobby());
        }

        [MenuItem("Accert one/Lobby Module/Build All (Assets + Lobby)", priority = 22)]
        static void BuildAllMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Debug.Log(BuildAssets(true));
            Debug.Log(AccertoneLobbyBuilder.Build());
        }

        // ------------------------------------------------------------------ assets

        public static string BuildAssets(bool bakeLivery)
        {
            var log = new StringBuilder("[Lobby Module]\n");
            foreach (var dir in new[] { "Materials/Car", "Materials/Driver", "Materials/Showroom", "Prefabs", "Prefabs/Characters", "Characters", "Textures/Car/Livery" })
                AccertoneBrandAssets.EnsureFolder(Root + "/" + dir);
            AssetDatabase.Refresh();
            ConfigureImporters();

            if (bakeLivery || !File.Exists(LiveryDir + "/T_Livery_Paint_BaseColor.png"))
                log.AppendLine(BakeCarLivery());
            log.AppendLine(BakeDriverBadges());
            AssetDatabase.Refresh();
            ConfigureImporters();

            var carMats = BuildCarMaterials();
            log.AppendLine("Car materials: " + carMats.Count);
            BuildCarPrefab(carMats);
            log.AppendLine("Car prefab: " + CarPrefabPath);
            foreach (var d in Drivers)
            {
                var mat = BuildDriverMaterial(d);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/Drivers/" + d.model);
                log.AppendLine("Character " + LobbyCharacterRigBuilder.Build(model, mat, CharacterPrefabPath(d), CharacterMeshPath(d), DriverHeight, "LobbyCharacter_" + d.label));
            }
            BuildShowroomMaterials();
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        static void ConfigureImporters()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Textures" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;
                string file = Path.GetFileNameWithoutExtension(path);
                bool normal = file.Contains("_Normal") || file.Contains("Weave_Normal");
                bool linear = file.Contains("MetallicSmoothness") || file.Contains("FabricMask");
                bool tile = file.Contains("Weave") || file.Contains("carbon1");

                bool dirty = false;
                var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (imp.textureType != type) { imp.textureType = type; dirty = true; }
                if (!normal && imp.sRGBTexture == linear) { imp.sRGBTexture = !linear; dirty = true; }
                if (imp.mipmapEnabled != true) { imp.mipmapEnabled = true; dirty = true; }
                if (imp.anisoLevel != 8) { imp.anisoLevel = 8; dirty = true; }
                if (imp.maxTextureSize != 4096) { imp.maxTextureSize = 4096; dirty = true; }
                var wrap = tile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                if (imp.wrapMode != wrap) { imp.wrapMode = wrap; dirty = true; }
                if (file.StartsWith("Logo_") && !imp.alphaIsTransparency) { imp.alphaIsTransparency = true; dirty = true; }
                if (imp.textureCompression != TextureImporterCompression.CompressedHQ) { imp.textureCompression = TextureImporterCompression.CompressedHQ; dirty = true; }
                if (dirty) imp.SaveAndReimport();
            }
        }

        // ------------------------------------------------------------------ livery bakes

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = FindDeep(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        static string BakeCarLivery()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CarModel);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.hideFlags = HideFlags.HideAndDontSave;
            var log = new StringBuilder();
            try
            {
                var space = FindDeep(inst.transform, "GLTF_SceneRootNode") ?? inst.transform;
                var design = new AccertoneCarLivery(
                    LiveryBaker.LoadReadable(LiveryDir + "/Logo_Wordmark.png"),
                    LiveryBaker.LoadReadable(LiveryDir + "/Logo_OctaDezx.png"),
                    LiveryBaker.LoadReadable(LiveryDir + "/Logo_Dezxcorp.png"),
                    LiveryBaker.LoadReadable(LiveryDir + "/Logo_Zeriotic.png"),
                    LiveryBaker.LoadReadable(LiveryDir + "/Logo_13.png"),
                    LiveryBaker.LoadReadable(LiveryDir + "/Logo_Mark.png"));

                var jobs = new[]
                {
                    new KeyValuePair<string, KeyValuePair<string, int>>("force_india_paint", new KeyValuePair<string, int>("Paint", 4096)),
                    new KeyValuePair<string, KeyValuePair<string, int>>("force_india_carbon1_paint", new KeyValuePair<string, int>("Wing", 4096)),
                    new KeyValuePair<string, KeyValuePair<string, int>>("force_india_detail", new KeyValuePair<string, int>("BodyDetail", 2048)),
                    new KeyValuePair<string, KeyValuePair<string, int>>("force_india_paint_detail", new KeyValuePair<string, int>("PaintDetail", 2048)),
                };
                var renderers = inst.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var job in jobs)
                {
                    string matName = job.Key, shortName = job.Value.Key;
                    int size = job.Value.Value;
                    var targets = new List<BakeTarget>();
                    foreach (var r in renderers)
                    {
                        var mf = r.GetComponent<MeshFilter>();
                        if (mf == null || mf.sharedMesh == null) continue;
                        var mats = r.sharedMaterials;
                        for (int i = 0; i < mats.Length; i++)
                            if (mats[i] != null && mats[i].name == matName)
                                targets.Add(new BakeTarget { renderer = r, mesh = mf.sharedMesh, subMesh = Mathf.Min(i, mf.sharedMesh.subMeshCount - 1) });
                    }
                    var baseCol = LiveryBaker.Fill(size, AccertoneCarLivery.Body);
                    var emis = LiveryBaker.Fill(size, Color.black);
                    int texels = LiveryBaker.Bake(space, targets, size, design, baseCol, emis);
                    LiveryBaker.WritePng(LiveryDir + "/T_Livery_" + shortName + "_BaseColor.png", baseCol, size);
                    string emisPath = LiveryDir + "/T_Livery_" + shortName + "_Emission.png";
                    if (LiveryBaker.AnyNonBlack(emis)) LiveryBaker.WritePng(emisPath, emis, size);
                    else if (File.Exists(emisPath)) AssetDatabase.DeleteAsset(emisPath);
                    log.AppendLine("Livery " + shortName + ": " + targets.Count + " meshes, " + texels + " texels @" + size);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(inst);
            }
            return log.ToString();
        }

        static string BakeDriverBadges()
        {
            var mark = LiveryBaker.LoadReadable(LiveryDir + "/Logo_Mark.png");
            var word = LiveryBaker.LoadReadable(LiveryDir + "/Logo_Wordmark.png");
            var log = new StringBuilder();
            foreach (var d in Drivers)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/Drivers/" + d.model);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                inst.hideFlags = HideFlags.HideAndDontSave;
                try
                {
                    var r = inst.GetComponentInChildren<MeshRenderer>(true);
                    var mesh = r.GetComponent<MeshFilter>().sharedMesh;
                    string folder = DriverTex + "/" + d.id + "/";
                    var src = LiveryBaker.LoadReadable(folder + d.id + "_BaseColor_Source.jpg");
                    int size = src.width;
                    var pixels = src.GetPixels32();
                    var design = new DriverSuitBadge(mark, word, mesh.bounds);
                    var targets = new List<BakeTarget> { new BakeTarget { renderer = r, mesh = mesh, subMesh = 0 } };
                    LiveryBaker.Bake(r.transform, targets, size, design, pixels, null);
                    LiveryBaker.WritePng(folder + d.id + "_BaseColor.png", pixels, size);
                    log.AppendLine("Badge baked: " + d.id);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(inst);
                }
            }
            return log.ToString();
        }

        // ------------------------------------------------------------------ material helpers

        static Shader LitShader { get { return Shader.Find("Universal Render Pipeline/Lit"); } }
        static Shader ComplexShader { get { return Shader.Find("Universal Render Pipeline/Complex Lit"); } }

        static T Load<T>(string path) where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        static Material Begin(string folder, string name, Shader shader, out string path, out bool isNew)
        {
            path = Root + "/Materials/" + folder + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            isNew = m == null;
            if (isNew) m = new Material(shader) { name = name };
            else if (m.shader != shader) m.shader = shader;
            return m;
        }

        static void Finish(Material m, string path, bool isNew)
        {
            ApplyUrpKeywords(m);
            if (isNew) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
        }

        static void ApplyUrpKeywords(Material m)
        {
            // URP re-derives keywords from properties on import; run the same code path now so the
            // asset is already consistent (clear coat, transparency, normal / detail / emission maps).
            try
            {
                Type baseGui = null, litGui = null;
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (baseGui == null) baseGui = a.GetType("UnityEditor.BaseShaderGUI");
                    if (litGui == null) litGui = a.GetType("UnityEditor.Rendering.Universal.ShaderGUI.LitGUI");
                }
                var litFn = litGui != null ? litGui.GetMethod("SetMaterialKeywords", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Material) }, null) : null;
                var baseFn = baseGui != null ? baseGui.GetMethod("SetMaterialKeywords", BindingFlags.Public | BindingFlags.Static) : null;
                if (baseFn != null)
                {
                    Action<Material> shading = litFn != null ? (Action<Material>)(mat => litFn.Invoke(null, new object[] { mat })) : null;
                    baseFn.Invoke(null, new object[] { m, shading, null });
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ACCERTONE] URP keyword setup failed for " + m.name + ": " + e.Message);
            }
            if (m.HasProperty("_ClearCoat"))
            {
                if (m.GetFloat("_ClearCoat") > 0.5f) m.EnableKeyword("_CLEARCOAT");
                else m.DisableKeyword("_CLEARCOAT");
            }
        }

        static void Surface(Material m, Texture baseMap, Color tint, float metallic, float smoothness, Texture normal = null, float normalScale = 1f, Texture metallicMap = null)
        {
            m.SetFloat("_WorkflowMode", 1f);
            m.SetTexture("_BaseMap", baseMap);
            m.SetColor("_BaseColor", tint);
            m.SetTexture("_MainTex", baseMap);
            m.SetColor("_Color", tint);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
            m.SetTexture("_MetallicGlossMap", metallicMap);
            m.SetFloat("_SmoothnessTextureChannel", 0f);
            m.SetTexture("_BumpMap", normal);
            m.SetFloat("_BumpScale", normalScale);
            m.SetFloat("_Surface", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.SetFloat("_EnvironmentReflections", 1f);
            m.SetFloat("_SpecularHighlights", 1f);
            m.SetFloat("_ClearCoat", 0f);
            Emission(m, null, Color.black);
        }

        static void Emission(Material m, Texture map, Color hdr)
        {
            m.SetTexture("_EmissionMap", map);
            m.SetColor("_EmissionColor", hdr);
            m.globalIlluminationFlags = hdr.maxColorComponent > 0.001f
                ? MaterialGlobalIlluminationFlags.RealtimeEmissive
                : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }

        static void ClearCoat(Material m, float mask, float smoothness)
        {
            m.SetFloat("_ClearCoat", 1f);
            m.SetFloat("_ClearCoatMask", mask);
            m.SetFloat("_ClearCoatSmoothness", smoothness);
        }

        static void Transparent(Material m, float alpha)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_ZWrite", 0f);
            var c = m.GetColor("_BaseColor");
            c.a = alpha;
            m.SetColor("_BaseColor", c);
            m.SetColor("_Color", c);
        }

        static Color Grey(float v) { return new Color(v, v, v, 1f); }

        static Color Hdr(Color c, float intensity)
        {
            return AccertoneBrandAssets.Hdr(c, intensity);
        }

        // ------------------------------------------------------------------ car materials

        static Dictionary<string, Material> BuildCarMaterials()
        {
            var result = new Dictionary<string, Material>();
            Texture2D Src(string name) { return Load<Texture2D>(CarSource + "/" + name + ".png"); }
            Texture2D Liv(string name) { return Load<Texture2D>(LiveryDir + "/" + name + ".png"); }
            Texture2D Car(string name) { return Load<Texture2D>(CarTex + "/" + name + ".png"); }

            void Paint(string gltf, string label, string livery)
            {
                string path; bool isNew;
                var m = Begin("Car", "M_Car_" + label, ComplexShader, out path, out isNew);
                Surface(m, Liv("T_Livery_" + livery + "_BaseColor"), Color.white, 0.06f, 0.45f);
                ClearCoat(m, 1f, 0.93f);
                var emis = Liv("T_Livery_" + livery + "_Emission");
                if (emis != null) Emission(m, emis, Hdr(Color.white, 3.2f));
                Finish(m, path, isNew);
                result[gltf] = m;
            }

            void Generic(string gltf, string label, Shader shader, Texture tex, Color tint, float metal, float smooth, Action<Material> extra = null)
            {
                string path; bool isNew;
                var m = Begin("Car", "M_Car_" + label, shader, out path, out isNew);
                Surface(m, tex, tint, metal, smooth);
                if (extra != null) extra(m);
                Finish(m, path, isNew);
                result[gltf] = m;
            }

            // Livery body panels: satin base under a high-gloss clear coat.
            Paint("force_india_paint", "Body_Paint", "Paint");
            Paint("force_india_carbon1_paint", "Wing_Paint", "Wing");
            Paint("force_india_detail", "Body_Detail", "BodyDetail");
            Paint("force_india_paint_detail", "Body_PaintDetail", "PaintDetail");
            Generic("force_india_halo", "Halo", ComplexShader, null, AccertoneCarLivery.Body, 0.1f, 0.5f, m => ClearCoat(m, 0.8f, 0.9f));

            // Carbon fibre: weave detail under clear coat.
            Generic("force_india_carbon1", "Carbon_Weave", ComplexShader, Src("force_india_carbon1_BaseColor"), Grey(0.62f), 0.25f, 0.55f, m =>
            {
                m.SetTexture("_BumpMap", Src("force_india_carbon1_Normal"));
                m.SetFloat("_BumpScale", 0.6f);
                ClearCoat(m, 1f, 0.94f);
            });
            Generic("force_india_carbon3", "Carbon_Panel", ComplexShader, Src("force_india_carbon3_BaseColor"), Grey(0.5f), 0.2f, 0.5f, m => ClearCoat(m, 0.85f, 0.9f));
            Generic("cockpit_carbon", "Carbon_Cockpit", ComplexShader, Src("cockpit_carbon_BaseColor"), Grey(0.55f), 0.2f, 0.45f, m => ClearCoat(m, 0.7f, 0.88f));
            Generic("astonmartin_carbon_int", "Carbon_Interior", ComplexShader, null, Grey(0.09f), 0.2f, 0.45f, m => ClearCoat(m, 0.7f, 0.88f));

            // Suspension wishbones: dark titanium-metallic finish.
            Generic("carbon_suspension", "Suspension_Metallic", ComplexShader, Src("carbon_suspension_BaseColor"), new Color(0.42f, 0.43f, 0.47f), 0.9f, 0.62f, m => ClearCoat(m, 0.5f, 0.85f));

            // Wheels: metallic rims, carbon-ceramic discs, matte rubber.
            Generic("force_india_wheel", "Wheel_Rim", LitShader, Car("T_Car_Wheel"), Grey(0.78f), 0.88f, 0.62f);
            Generic("discs", "Brake_Disc", LitShader, Car("T_Car_Wheel"), Grey(0.7f), 0.25f, 0.35f);
            Generic("TIRE_SIDES", "Tyre_Sidewall", LitShader, Car("T_Car_Tyre_Sidewall"), Color.white, 0f, 0.22f);
            Generic("TIRE_TREAD", "Tyre_Tread", LitShader, Car("T_Car_Tyre_Sidewall"), Color.white, 0f, 0.3f);

            // Metal, mirrors, glass.
            Generic("cockpit_metal", "Metal_Cockpit", LitShader, Src("cockpit_metal_BaseColor"), Grey(0.65f), 1f, 0.72f);
            Generic("cockpit_metal_silver", "Metal_Silver", LitShader, Src("cockpit_metal_BaseColor"), Grey(0.85f), 1f, 0.86f);
            Generic("mirror", "Mirror", LitShader, null, Grey(0.95f), 1f, 0.97f);
            Generic("glass", "Glass", LitShader, null, new Color(0.18f, 0.2f, 0.24f), 0f, 0.96f, m => Transparent(m, 0.28f));

            // Lights and cockpit electronics.
            Generic("rear_light", "Rain_Light", LitShader, Src("rear_light_BaseColor"), Color.white, 0f, 0.8f, m => Emission(m, Src("rear_light_BaseColor"), Hdr(new Color(1f, 0.1f, 0.12f), 2.2f)));
            Generic("indicator_lights", "Wheel_LED", LitShader, Src("indicator_lights_BaseColor"), Color.white, 0f, 0.8f, m => Emission(m, null, Hdr(AccertoneCarLivery.Magenta, 1.5f)));
            Generic("indicator_lights_back", "Wheel_LED_Back", LitShader, Src("indicator_lights_back_BaseColor"), Color.white, 0f, 0.6f);
            Generic("LCD_GLASS", "Wheel_Display", LitShader, Src("LCD_GLASS_BaseColor"), Color.white, 0f, 0.92f, m => Emission(m, Src("LCD_GLASS_BaseColor"), Hdr(Color.white, 0.6f)));
            Generic("material", "Wheel_Display_Frame", LitShader, Src("material_BaseColor"), Color.white, 0f, 0.5f);

            // De-branded plates.
            Generic("driver_number", "Nameplate", LitShader, Car("T_Car_DriverNameplate"), Color.white, 0f, 0.5f);
            Generic("force_india_number_14", "Nameplate_Number", LitShader, Car("T_Car_DriverNameplate"), Color.white, 0f, 0.5f);
            Generic("material_31", "Chassis_Plate", LitShader, Car("T_Car_ChassisPlate"), Color.white, 0f, 0.55f);
            Generic("force_india_decal", "Decal_Cleared", LitShader, Car("T_Car_Decal_Blank"), Color.white, 0f, 0.5f, m => Transparent(m, 1f));

            // Everything else: original texture, sensible PBR response.
            Generic("generics", "Generics", LitShader, Src("generics_BaseColor"), Color.white, 0.2f, 0.45f);
            Generic("tcam", "TCam_Pod", LitShader, Src("tcam_BaseColor"), Grey(0.35f), 0.1f, 0.55f);
            Generic("cockpit_foam", "Cockpit_Foam", LitShader, Src("cockpit_foam_BaseColor"), Color.white, 0f, 0.1f);
            Generic("astonmartin_steer", "Steering_Body", LitShader, Src("astonmartin_steer_BaseColor"), Color.white, 0.1f, 0.4f);
            Generic("astonmartin_steer_rubber", "Steering_Grip", LitShader, Src("astonmartin_steer_rubber_BaseColor"), Color.white, 0f, 0.15f);
            Generic("vehicle_revs_type1", "Steering_RevLights", LitShader, Src("vehicle_revs_type1_BaseColor"), Color.white, 0f, 0.6f);
            Generic("astonmartin_steer_decal", "Steering_Decal", LitShader, Src("astonmartin_steer_decal_BaseColor"), Color.white, 0f, 0.5f);
            Generic("astonmartin_steer_decal_head", "Steering_Decal_Head", LitShader, Src("astonmartin_steer_decal_head_BaseColor"), Color.white, 0f, 0.5f, m => Transparent(m, 1f));
            return result;
        }

        static void BuildCarPrefab(Dictionary<string, Material> mats)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CarModel);
            var root = new GameObject("ACCERTONE_Car_AC26");
            try
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                inst.name = "Model_AC26";
                foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    var list = r.sharedMaterials;
                    for (int i = 0; i < list.Length; i++)
                    {
                        Material m;
                        if (list[i] != null && mats.TryGetValue(list[i].name, out m)) list[i] = m;
                    }
                    r.sharedMaterials = list;
                    r.shadowCastingMode = ShadowCastingMode.On;
                }
                var mark = new GameObject("DriverMark");
                mark.transform.SetParent(root.transform, false);
                mark.transform.localPosition = DriverMarkLocal;
                PrefabUtility.SaveAsPrefabAsset(root, CarPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // ------------------------------------------------------------------ drivers

        static Material BuildDriverMaterial(DriverSpec d)
        {
            string folder = DriverTex + "/" + d.id + "/";
            string path; bool isNew;
            var m = Begin("Driver", "M_" + d.id + "_Suit", LitShader, out path, out isNew);
            Surface(m, Load<Texture2D>(folder + d.id + "_BaseColor.png"), Color.white, 0f, 1f,
                Load<Texture2D>(folder + d.id + "_Normal_Source.jpg"), 1f,
                Load<Texture2D>(folder + d.id + "_MetallicSmoothness.png"));
            // Fabric micro-weave on the suit only (helmet and visor are masked out).
            m.SetTexture("_DetailMask", Load<Texture2D>(folder + d.id + "_FabricMask.png"));
            m.SetTexture("_DetailNormalMap", Load<Texture2D>(DriverTex + "/T_Fabric_Weave_Normal.png"));
            m.SetFloat("_DetailNormalMapScale", 0.35f);
            m.SetFloat("_DetailAlbedoMapScale", 1f);
            m.SetTextureScale("_DetailAlbedoMap", new Vector2(36f, 36f));
            m.SetFloat("_Cull", 0f); // the Meshy meshes are authored double-sided
            Finish(m, path, isNew);
            return m;
        }

        static Material BuildShowroomMaterials()
        {
            string path; bool isNew;
            var m = Begin("Showroom", "M_FloorReflection", Shader.Find("ACCERTONE/Planar Reflection Overlay"), out path, out isNew);
            m.SetFloat("_Intensity", 0.5f);
            m.SetFloat("_BaseReflectance", 0.04f);
            m.SetFloat("_FresnelPower", 4f);
            m.SetFloat("_Blur", 2.4f);
            m.SetVector("_FadeCenter", Vector4.zero);
            m.SetFloat("_FadeRadius", 9f);
            if (isNew) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ staging

        public static string StageInOpenLobby()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != AccertoneLobbyBuilder.ScenePath)
                scene = EditorSceneManager.OpenScene(AccertoneLobbyBuilder.ScenePath, OpenSceneMode.Single);
            string result = StageInScene(scene);
            EditorSceneManager.SaveScene(scene);
            var probe = UnityEngine.Object.FindFirstObjectByType<ReflectionProbe>();
            if (probe != null)
            {
                string exr = AccertoneBrandAssets.SceneDir + "/ACCERTONE_Lobby/ReflectionProbe-Showroom.exr";
                UnityEditor.Lightmapping.BakeReflectionProbe(probe, exr);
                AssetDatabase.ImportAsset(exr, ImportAssetOptions.ForceUpdate);
                probe.mode = ReflectionProbeMode.Custom;
                probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Texture>(exr);
                EditorSceneManager.SaveScene(scene);
            }
            return result;
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var g in scene.GetRootGameObjects()) if (g.name == name) return g;
            return null;
        }

        public static string StageInScene(Scene scene)
        {
            var showroom = FindRoot(scene, "ACCERTONE_Showroom");
            var camGo = FindRoot(scene, "Main Camera");
            if (showroom == null || camGo == null) return "[Lobby Module] Lobby showroom not found in " + scene.path;
            var mount = showroom.GetComponentInChildren<CarDisplayMount>(true);
            var lighting = FindDeep(showroom.transform, "Lighting");
            var characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath(Drivers[ActiveCharacter]));
            if (mount == null || characterPrefab == null) return "[Lobby Module] Podium or lobby character prefab missing.";

            // Clear previous staging (including any car and the retired rotate-in-place driver showcase).
            for (int i = mount.carSlot.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(mount.carSlot.GetChild(i).gameObject);
            var oldShowcase = FindDeep(mount.turntable, "DriverShowcase");
            if (oldShowcase != null) UnityEngine.Object.DestroyImmediate(oldShowcase.gameObject);
            foreach (var g in scene.GetRootGameObjects())
                if (g.name.StartsWith("LobbyCharacter_")) UnityEngine.Object.DestroyImmediate(g);
            for (int i = mount.turntable.childCount - 1; i >= 0; i--)
            {
                string childName = mount.turntable.GetChild(i).name;
                if (childName.StartsWith("LobbyCharacter_") || childName == "DriverSpinner")
                    UnityEngine.Object.DestroyImmediate(mount.turntable.GetChild(i).gameObject);
            }
            foreach (var oldSpinner in mount.GetComponents<PresentationSpinner>()) UnityEngine.Object.DestroyImmediate(oldSpinner);
            var oldArea = FindDeep(showroom.transform, "Lobby_WalkArea");
            if (oldArea != null) UnityEngine.Object.DestroyImmediate(oldArea.gameObject);
            if (lighting != null)
                for (int i = lighting.childCount - 1; i >= 0; i--)
                    if (lighting.GetChild(i).name.StartsWith("Module_")) UnityEngine.Object.DestroyImmediate(lighting.GetChild(i).gameObject);
            var oldRefl = FindDeep(showroom.transform, "FloorReflection");
            if (oldRefl != null) UnityEngine.Object.DestroyImmediate(oldRefl.gameObject);

            // The AC-26 on the podium, origin at CarSpawnAnchor (nose along +Z). The turntable is the main display
            // spinner: car and driver both ride it. A PresentationSpinner turns it once and stops (CarDisplayMount's
            // endless turn is off). (Field changes on the podium prefab instance must be recorded as overrides or they
            // revert on save.)
            var carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefabPath);
            mount.carPrefab = carPrefab;
            mount.rotate = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(mount);
            mount.turntable.localRotation = Quaternion.identity;
            if (carPrefab != null)
            {
                var car = (GameObject)PrefabUtility.InstantiatePrefab(carPrefab, mount.carSlot);
                car.transform.localPosition = Vector3.zero;
                car.transform.localRotation = Quaternion.identity;
                if (car.GetComponent<Accertone.CarSetup.VehicleSetupApplier>() == null)
                    car.AddComponent<Accertone.CarSetup.VehicleSetupApplier>();
            }

            // Ground heights (podium / floor) and the car's clearance envelope around the driver's spot.
            var areaGo = new GameObject("Lobby_WalkArea");
            areaGo.transform.SetParent(showroom.transform, false);
            var area = areaGo.AddComponent<LobbyWalkArea>();
            area.carSpawnAnchor = mount.carSlot;
            area.podium = mount.transform;
            area.carFootprint = new Vector2(mount.carEnvelope.x, mount.carEnvelope.z);

            // The driver stands still beside the cockpit on the camera side of the car, on his own small spinner that
            // rides the turntable. The spinners turn him; he never walks, steps or turns (LobbyCharacterPresentation +
            // ProceduralLocomotion.holdPose).
            var cam = camGo.transform;
            var character = (GameObject)PrefabUtility.InstantiatePrefab(characterPrefab, scene);
            Vector3 left = area.LoopPoint(76f, 0.2f), right = area.LoopPoint(284f, 0.2f);
            Vector3 start = (cam.position - left).sqrMagnitude <= (cam.position - right).sqrMagnitude ? left : right;
            // Opening facing: looking at the cockpit, then turned to the driver's own left.
            Vector3 toCar = mount.carSlot.TransformPoint(new Vector3(0f, 0.8f, 0.2f)) - start;
            toCar.y = 0f;
            var driverSpinner = BuildDriverSpinner(mount.turntable, start);
            character.transform.SetPositionAndRotation(start + Vector3.up * DriverSpinnerHeight,
                Quaternion.LookRotation(toCar.normalized) * Quaternion.Euler(0f, -DriverTurnLeft, 0f));
            character.transform.SetParent(driverSpinner, true);
            var ctrl = character.GetComponent<LobbyCharacterController>();
            ctrl.walkArea = area;
            ctrl.viewCamera = cam;
            ctrl.attentionTarget = null;
            ctrl.acceptPlayerInput = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(ctrl);
            var loco = character.GetComponent<ProceduralLocomotion>();
            loco.walkArea = area;
            loco.controller = ctrl;
            loco.holdPose = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(loco);
            var presentation = character.GetComponent<LobbyCharacterPresentation>();
            if (presentation != null)
            {
                presentation.character = ctrl;
                presentation.locomotion = loco;
                PrefabUtility.RecordPrefabInstancePropertyModifications(presentation);
            }

            // Both spinners start together and ease in alike. The platform stops at the car's hero angle; the driver's
            // spinner keeps going (same direction) and stops with him facing the camera's showroom position.
            var platform = mount.gameObject.AddComponent<PresentationSpinner>();
            platform.target = mount.turntable;
            platform.travelDegrees = PlatformTravel;
            platform.startDelay = SpinnerStartDelay;
            platform.spinUpSeconds = SpinnerSpinUp;
            platform.spinDownSeconds = PlatformSpinDown;
            platform.cruiseSpeed = 10f;
            var driverSpin = driverSpinner.gameObject.AddComponent<PresentationSpinner>();
            driverSpin.target = driverSpinner;
            driverSpin.travelDegrees = TravelToFaceCamera(mount.turntable, PlatformTravel, character.transform, cam.position);
            driverSpin.startDelay = SpinnerStartDelay;
            driverSpin.spinUpSeconds = SpinnerSpinUp;
            driverSpin.spinDownSeconds = DriverSpinnerSpinDown;
            driverSpin.cruiseSpeed = DriverSpinnerSpeed;
            var follow = character.GetComponentInChildren<CharacterFollowLight>(true);
            if (follow != null)
            {
                follow.viewCamera = cam;
                PrefabUtility.RecordPrefabInstancePropertyModifications(follow);
            }

            // The showroom camera holds its framing while the platform presents car and driver.
            var rig = camGo.GetComponent<LobbyCameraRig>();
            if (rig != null) rig.followTarget = null;

            if (lighting != null)
            {
                var rimRight = FindDeep(lighting, "Rim_Right");
                if (rimRight != null) { var l = rimRight.GetComponent<Light>(); l.color = AccertoneCarLivery.Hex(0xA06BFF); l.intensity = 46f; }
            }

            // Clean planar ground reflections (character, podium LED ring, brand wall).
            var floor = FindDeep(showroom.transform, "Floor");
            if (floor != null)
            {
                var floorMat = floor.GetComponent<MeshRenderer>().sharedMaterial;
                floorMat.SetFloat("_EnvironmentReflections", 0f);
                floorMat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                EditorUtility.SetDirty(floorMat);

                var refl = GameObject.CreatePrimitive(PrimitiveType.Plane);
                refl.name = "FloorReflection";
                refl.layer = 4; // Water: excluded from the reflection camera
                refl.transform.SetParent(showroom.transform, false);
                refl.transform.position = floor.position + Vector3.up * 0.002f;
                refl.transform.localScale = floor.localScale;
                UnityEngine.Object.DestroyImmediate(refl.GetComponent<Collider>());
                var rr = refl.GetComponent<MeshRenderer>();
                rr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Showroom/M_FloorReflection.mat");
                rr.shadowCastingMode = ShadowCastingMode.Off;
                rr.receiveShadows = false;
                var pfr = refl.AddComponent<PlanarFloorReflection>();
                pfr.sourceCamera = camGo.GetComponent<Camera>();
                pfr.planeHeight = floor.position.y;
            }

            var probe = showroom.GetComponentInChildren<ReflectionProbe>(true);
            if (probe != null) probe.resolution = 1024;

            // Navigation between HOME, GARAGE (car setup) and SETTINGS, plus the settings screen itself.
            var lobbyCtrl = UnityEngine.Object.FindFirstObjectByType<LobbyController>();
            if (lobbyCtrl != null)
            {
                var coord = lobbyCtrl.GetComponent<Accertone.Settings.LobbyNavigationCoordinator>();
                if (coord == null) coord = lobbyCtrl.gameObject.AddComponent<Accertone.Settings.LobbyNavigationCoordinator>();
                Debug.Log(Accertone.Settings.EditorTools.SettingsBuilder.StageInScene(scene));
                coord.EnsureScreensCreated();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            return "[Lobby Module] Staged AC-26 car + presented driver " + character.name + " in " + scene.path;
        }

        /// <summary>The driver's small display spinner: a low disc in the podium's style (turntable top, dial ticks,
        /// violet LED ring that powers up with the podium ring) on the turntable at <paramref name="worldPos"/>.</summary>
        static Transform BuildDriverSpinner(Transform turntable, Vector3 worldPos)
        {
            var spinner = new GameObject("DriverSpinner").transform;
            spinner.SetParent(turntable, false);
            spinner.position = worldPos;
            spinner.localRotation = Quaternion.identity;

            string dir = Root + "/Models/";
            const float r = DriverSpinnerRadius, h = DriverSpinnerHeight;
            var top = AccertoneMeshFactory.SaveMesh(AccertoneMeshFactory.Lathe("DriverSpinner_Top", new[]
            {
                new Vector2(0f, h), new Vector2(r - 0.012f, h), new Vector2(r, h - 0.01f), new Vector2(r, 0f)
            }, 96), dir + "DriverSpinner_Top.asset");
            var led = AccertoneMeshFactory.SaveMesh(AccertoneMeshFactory.Annulus("DriverSpinner_LED", r - 0.040f, r - 0.022f, 96), dir + "DriverSpinner_LED.asset");
            var dial = AccertoneMeshFactory.SaveMesh(AccertoneMeshFactory.DialTicks("DriverSpinner_Dial", r - 0.054f, 0.025f, 0.06f, 0.008f, 36, 6), dir + "DriverSpinner_Dial.asset");
            const string mats = "Assets/ACCERTONE/Art/Materials/";
            SpinnerPart("DriverSpinner_Top", spinner, top, AssetDatabase.LoadAssetAtPath<Material>(mats + "M_Podium_Turntable.mat"), 0f, true);
            SpinnerPart("DriverSpinner_Dial", spinner, dial, AssetDatabase.LoadAssetAtPath<Material>(mats + "M_Turntable_Dial.mat"), h + 0.0003f, false);
            var ring = SpinnerPart("DriverSpinner_LED", spinner, led, AssetDatabase.LoadAssetAtPath<Material>(mats + "M_LED_Accent.mat"), h + 0.0006f, false);
            var power = ring.AddComponent<ShowroomPoweredElement>();
            power.group = 3;
            power.colorProperty = "_EmissionColor";
            power.pulseAmplitude = 0.06f;
            return spinner;
        }

        static GameObject SpinnerPart(string name, Transform parent, Mesh mesh, Material mat, float y, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            if (!shadows) r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Signed degrees the driver's spinner must turn (the shorter way round) so that, once the platform
        /// has turned <paramref name="platformTravel"/>, the driver faces <paramref name="cameraPos"/>.</summary>
        static float TravelToFaceCamera(Transform turntable, float platformTravel, Transform driver, Vector3 cameraPos)
        {
            Quaternion platform = Quaternion.AngleAxis(platformTravel, turntable.up);
            Vector3 pos = turntable.position + platform * (driver.position - turntable.position);
            Vector3 forward = platform * driver.forward;
            Vector3 toCamera = cameraPos - pos;
            float facing = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            float wanted = Mathf.Atan2(toCamera.x, toCamera.z) * Mathf.Rad2Deg;
            return Mathf.DeltaAngle(facing, wanted);
        }

        static GameObject Spot(Transform parent, string name, Vector3 pos, Vector3 target, float angle, float inner, float range, float intensity, Color color, LightShadows shadows)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation((target - pos).normalized);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.spotAngle = angle;
            l.innerSpotAngle = inner;
            l.range = range;
            l.intensity = intensity;
            l.color = color;
            l.shadows = shadows;
            return go;
        }

        static void AddPower(GameObject go, int group)
        {
            var e = go.AddComponent<ShowroomPoweredElement>();
            e.group = group;
        }
    }
}
