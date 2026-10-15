using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Accertone.Settings.EditorTools
{
    /// <summary>
    /// Builds everything the settings system needs and stages it into the lobby:
    /// the settings catalog asset, the Audio Mixer (groups + exposed volumes), the Driving action map in the
    /// project Input Actions, and the settings screen under the lobby canvas. Runs as part of the lobby rebuild.
    /// </summary>
    public static class SettingsBuilder
    {
        public const string Root = "Assets/_LobbyModule/Settings";
        public const string ResourcesDir = Root + "/Resources";
        public const string CatalogPath = ResourcesDir + "/" + SettingsCatalog.ResourcePath + ".asset";
        public const string MixerPath = ResourcesDir + "/" + AudioSettingsSystem.MixerResource + ".mixer";
        const string ActionsPath = "Assets/InputSystem_Actions.inputactions";
        const string Kbm = InputSettingsSystem.KeyboardGroup;
        const string Pad = InputSettingsSystem.GamepadGroup;

        [MenuItem("Accert one/Settings/Build Assets")]
        public static void BuildAssetsMenu()
        {
            Debug.Log(BuildAssets());
        }

        [MenuItem("Accert one/Settings/Stage In Lobby")]
        public static void StageMenu()
        {
            var scene = SceneManager.GetActiveScene();
            Debug.Log(StageInScene(scene));
            EditorSceneManager.SaveScene(scene);
        }

        public static string BuildAssets()
        {
            if (!AssetDatabase.IsValidFolder(ResourcesDir)) AssetDatabase.CreateFolder(Root, "Resources");
            var log = new List<string>();
            log.Add(BuildCatalog());
            log.Add(BuildMixer());
            log.Add(BuildDrivingActions());
            AssetDatabase.SaveAssets();
            return "[Settings] " + string.Join(" | ", log.ToArray());
        }

        // ------------------------------------------------------------------ catalog
        static string BuildCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SettingsCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<SettingsCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            SettingsCatalogContent.Fill(catalog);
            EditorUtility.SetDirty(catalog);
            int n = 0;
            foreach (var c in catalog.categories) foreach (var p in c.pages) foreach (var s in p.sections) n += s.settings.Count;
            return "catalog: " + catalog.categories.Count + " categories, " + n + " settings";
        }

        // ------------------------------------------------------------------ audio mixer
        static string BuildMixer()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (existing != null && HasAllParameters(existing)) return "mixer: ok";
            if (existing != null) AssetDatabase.DeleteAsset(MixerPath);

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var ctrlType = Type.GetType("UnityEditor.Audio.AudioMixerController, UnityEditor");
            var groupType = Type.GetType("UnityEditor.Audio.AudioMixerGroupController, UnityEditor");
            var pathType = Type.GetType("UnityEditor.Audio.AudioGroupParameterPath, UnityEditor");
            if (ctrlType == null || groupType == null || pathType == null) return "mixer: editor API unavailable";

            var mixer = ctrlType.GetMethod("CreateMixerControllerAtPath", flags).Invoke(null, new object[] { MixerPath });
            var master = ctrlType.GetProperty("masterGroup", flags).GetValue(mixer, null);
            var createGroup = ctrlType.GetMethod("CreateNewGroup", flags);
            var addChild = ctrlType.GetMethod("AddChildToParent", flags);
            var addExposed = ctrlType.GetMethod("AddExposedParameter", flags);
            var volumeGuid = groupType.GetMethod("GetGUIDForVolume", flags);
            var pathCtor = pathType.GetConstructor(new[] { groupType, typeof(GUID) });

            var groups = new Dictionary<string, object> { { "Master", master } };
            foreach (var ch in AudioSettingsSystem.Channels)
            {
                if (ch == "Master") continue;
                var g = createGroup.Invoke(mixer, new object[] { ch, false });
                addChild.Invoke(mixer, new[] { g, master });
                groups[ch] = g;
            }
            var guidToName = new Dictionary<GUID, string>();
            foreach (var kv in groups)
            {
                var guid = (GUID)volumeGuid.Invoke(kv.Value, null);
                guidToName[guid] = "Vol_" + kv.Key;
                addExposed.Invoke(mixer, new[] { pathCtor.Invoke(new[] { kv.Value, (object)guid }) });
            }

            var exposedProp = ctrlType.GetProperty("exposedParameters", flags);
            var arr = (Array)exposedProp.GetValue(mixer, null);
            var elemType = arr.GetType().GetElementType();
            var guidField = elemType.GetField("guid", flags);
            var nameField = elemType.GetField("name", flags);
            for (int i = 0; i < arr.Length; i++)
            {
                object e = arr.GetValue(i);
                var guid = (GUID)guidField.GetValue(e);
                string name;
                if (guidToName.TryGetValue(guid, out name)) { nameField.SetValue(e, name); arr.SetValue(e, i); }
            }
            exposedProp.SetValue(mixer, arr, null);
            EditorUtility.SetDirty((UnityEngine.Object)mixer);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(MixerPath);
            var built = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            return built != null && HasAllParameters(built) ? "mixer: built (" + groups.Count + " groups)" : "mixer: built, parameters NOT verified";
        }

        static bool HasAllParameters(AudioMixer mixer)
        {
            foreach (var ch in AudioSettingsSystem.Channels)
            {
                float v;
                if (!mixer.GetFloat("Vol_" + ch, out v)) return false;
            }
            return mixer.FindMatchingGroups(string.Empty).Length >= AudioSettingsSystem.Channels.Length;
        }

        // ------------------------------------------------------------------ driving actions
        static string BuildDrivingActions()
        {
            if (!File.Exists(ActionsPath)) return "input: " + ActionsPath + " missing";
            // Work on a copy: the project-wide asset is enabled in the Editor and can't be edited in place.
            var asset = InputActionAsset.FromJson(File.ReadAllText(ActionsPath));
            if (asset.FindActionMap(InputSettingsSystem.MapName) != null) { UnityEngine.Object.DestroyImmediate(asset); return "input: Driving map ok"; }

            var map = asset.AddActionMap(InputSettingsSystem.MapName);
            Axis(map, "Throttle", new[] { "<Keyboard>/w", "<Keyboard>/upArrow" }, new[] { "<Gamepad>/rightTrigger", "" });
            Axis(map, "Brake", new[] { "<Keyboard>/s", "<Keyboard>/downArrow" }, new[] { "<Gamepad>/leftTrigger", "" });
            Buttons(map, "SteerLeft", new[] { "<Keyboard>/a", "<Keyboard>/leftArrow" }, new string[0]);
            Buttons(map, "SteerRight", new[] { "<Keyboard>/d", "<Keyboard>/rightArrow" }, new string[0]);
            Axis(map, "Steer", new string[0], new[] { "<Gamepad>/leftStick/x" });
            Buttons(map, "ShiftUp", new[] { "<Keyboard>/e", "" }, new[] { "<Gamepad>/buttonSouth", "" });
            Buttons(map, "ShiftDown", new[] { "<Keyboard>/q", "" }, new[] { "<Gamepad>/buttonWest", "" });
            Buttons(map, "Drs", new[] { "<Keyboard>/f", "" }, new[] { "<Gamepad>/buttonNorth", "" });
            Buttons(map, "LookBack", new[] { "<Keyboard>/c", "" }, new[] { "<Gamepad>/rightStickPress", "" });
            Buttons(map, "ChangeCamera", new[] { "<Keyboard>/v", "" }, new[] { "<Gamepad>/buttonEast", "" });
            Buttons(map, "PitLimiter", new[] { "<Keyboard>/l", "" }, new[] { "<Gamepad>/leftShoulder", "" });
            Buttons(map, "Pause", new[] { "<Keyboard>/escape" }, new[] { "<Gamepad>/start" });

            int count = map.actions.Count;
            File.WriteAllText(ActionsPath, asset.ToJson());
            UnityEngine.Object.DestroyImmediate(asset);
            AssetDatabase.ImportAsset(ActionsPath);
            return "input: Driving map added (" + count + " actions)";
        }

        static void Axis(InputActionMap map, string name, string[] keys, string[] pad)
        {
            var a = map.AddAction(name, InputActionType.Value, expectedControlLayout: "Axis");
            foreach (var k in keys) a.AddBinding(k, groups: Kbm);
            foreach (var p in pad) a.AddBinding(p, groups: Pad);
        }

        static void Buttons(InputActionMap map, string name, string[] keys, string[] pad)
        {
            var a = map.AddAction(name, InputActionType.Button);
            foreach (var k in keys) a.AddBinding(k, groups: Kbm);
            foreach (var p in pad) a.AddBinding(p, groups: Pad);
        }

        // ------------------------------------------------------------------ scene staging
        public static string StageInScene(Scene scene)
        {
            GameObject canvas = null;
            foreach (var g in scene.GetRootGameObjects()) if (g.name == "UI_Lobby") canvas = g;
            if (canvas == null) return "[Settings] UI_Lobby canvas not found in " + scene.path;
            var ct = canvas.transform;

            // Retire the previous settings screen.
            foreach (var oldName in new[] { "Screen_GameSettings", "Screen_Settings", "Screen_Settings_Overlay" })
            {
                var old = ct.Find(oldName);
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            }

            var screenGo = new GameObject("Screen_Settings", typeof(RectTransform));
            screenGo.layer = 5;
            screenGo.transform.SetParent(ct, false);
            var backdrop = ct.Find("Backdrop_Shades");
            screenGo.transform.SetSiblingIndex(backdrop != null ? backdrop.GetSiblingIndex() + 1 : 0);
            var screen = screenGo.AddComponent<SettingsScreen>();
            screenGo.AddComponent<CanvasGroup>();

            var overlayGo = new GameObject("Screen_Settings_Overlay", typeof(RectTransform));
            overlayGo.layer = 5;
            overlayGo.transform.SetParent(ct, false);
            var transition = ct.Find("ACCERTONE_TransitionOverlay");
            if (transition != null) overlayGo.transform.SetSiblingIndex(transition.GetSiblingIndex());
            else overlayGo.transform.SetAsLastSibling();
            var ort = (RectTransform)overlayGo.transform;
            ort.anchorMin = Vector2.zero; ort.anchorMax = Vector2.one; ort.offsetMin = ort.offsetMax = Vector2.zero;
            screen.overlay = ort;
            var srt = (RectTransform)screenGo.transform;
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.offsetMin = srt.offsetMax = Vector2.zero;
            screenGo.SetActive(false);

            if (canvas.GetComponent<CanvasScaler>() != null && canvas.GetComponent<SettingsCanvasScale>() == null)
                canvas.AddComponent<SettingsCanvasScale>();

            // Track length follows the speed units setting.
            var lengthValue = FindDeep(ct, "Stat_LENGTH_Value");
            if (lengthValue != null)
            {
                var label = lengthValue.GetComponent<SpeedUnitsLabel>();
                if (label == null) label = lengthValue.gameObject.AddComponent<SpeedUnitsLabel>();
                label.valueText = lengthValue.GetComponent<TMP_Text>();
                var unit = FindDeep(ct, "Stat_LENGTH_Unit");
                label.unitText = unit != null ? unit.GetComponent<TMP_Text>() : null;
                float km;
                if (label.valueText != null && float.TryParse(label.valueText.text, NumberStyles.Float, CultureInfo.InvariantCulture, out km)) label.metricValue = km;
                label.decimals = 3;
                label.quantity = SpeedUnitsLabel.Quantity.Distance;
            }

            var coord = UnityEngine.Object.FindFirstObjectByType<LobbyNavigationCoordinator>(FindObjectsInactive.Include);
            if (coord == null)
            {
                var lobby = UnityEngine.Object.FindFirstObjectByType<Accertone.Lobby.LobbyController>(FindObjectsInactive.Include);
                if (lobby != null) coord = lobby.gameObject.AddComponent<LobbyNavigationCoordinator>();
            }
            if (coord != null)
            {
                coord.settingsView = screen;
                coord.showroomPanels.RemoveAll(p => p == null || p.name == "ACCERTONE_Wordmark");
                EditorUtility.SetDirty(coord);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            return "[Settings] Staged Screen_Settings + overlay in " + scene.path;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = FindDeep(c, name); if (f != null) return f; }
            return null;
        }
    }
}
