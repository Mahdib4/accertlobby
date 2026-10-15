using System.IO;
using Accertone.Branding;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Accertone.EditorTools
{
    /// <summary>Creates (or refreshes) the ACCERTONE brand system assets: fonts, palette, typography, identity, textures, materials.</summary>
    public static class AccertoneBrandAssets
    {
        public const string Root = "Assets/ACCERTONE";
        public const string FontDir = Root + "/Art/Fonts";
        public const string FontSourceDir = FontDir + "/Source";
        public const string MaterialDir = Root + "/Art/Materials";
        public const string MeshDir = Root + "/Art/Meshes";
        public const string TextureDir = Root + "/Art/Textures";
        public const string BrandingDir = Root + "/Branding";
        public const string BrandingResourcesDir = BrandingDir + "/Resources";
        public const string PrefabDir = Root + "/Prefabs";
        public const string SceneDir = Root + "/Scenes";
        public const string SettingsDir = Root + "/Settings";

        const string Ascii = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~°·•›–—";

        public static void EnsureFolders()
        {
            foreach (var dir in new[] { FontDir, MaterialDir, MeshDir, TextureDir, BrandingResourcesDir, PrefabDir + "/Branding", PrefabDir + "/Lobby", SceneDir, SettingsDir })
                EnsureFolder(dir);
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ------------------------------------------------------------------ Fonts

        public static TMP_FontAsset LoadOrCreateFont(string ttf, string assetName, int sampling, int padding, int atlas)
        {
            string path = FontDir + "/" + assetName + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;

            var font = AssetDatabase.LoadAssetAtPath<UnityEngine.Font>(FontSourceDir + "/" + ttf);
            if (font == null)
            {
                Debug.LogError("[ACCERTONE] Missing source font " + ttf);
                return null;
            }

            var fa = TMP_FontAsset.CreateFontAsset(font, sampling, padding, GlyphRenderMode.SDFAA, atlas, atlas, AtlasPopulationMode.Dynamic, true);
            fa.name = assetName;
            AssetDatabase.CreateAsset(fa, path);
            var tex = fa.atlasTextures[0];
            tex.name = assetName + " Atlas";
            AssetDatabase.AddObjectToAsset(tex, fa);
            var mat = fa.material;
            mat.name = assetName + " Material";
            AssetDatabase.AddObjectToAsset(mat, fa);
            string missing;
            fa.TryAddCharacters(Ascii, out missing);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            return fa;
        }

        // ------------------------------------------------------------------ Brand system

        public static AccertoneBrandIdentity EnsureBrandSystem()
        {
            EnsureFolders();

            var display = LoadOrCreateFont("Roboto-BlackItalic.ttf", "ACCERTONE_Display SDF", 128, 18, 2048);
            var headline = LoadOrCreateFont("Roboto-Black.ttf", "ACCERTONE_Headline SDF", 90, 10, 1024);
            var title = LoadOrCreateFont("Roboto-Bold.ttf", "ACCERTONE_Title SDF", 72, 8, 1024);
            var label = LoadOrCreateFont("Inter-SemiBold.ttf", "ACCERTONE_Label SDF", 72, 8, 1024);
            var body = LoadOrCreateFont("Inter-Regular.ttf", "ACCERTONE_Body SDF", 72, 8, 1024);
            var data = LoadOrCreateFont("RobotoMono-Regular.ttf", "ACCERTONE_Data SDF", 72, 8, 1024);
            var light = LoadOrCreateFont("Roboto-Light.ttf", "ACCERTONE_Light SDF", 90, 10, 1024);

            var palette = LoadOrCreate<AccertoneBrandPalette>(BrandingDir + "/ACCERTONE_BrandPalette.asset");

            var typo = LoadOrCreate<AccertoneTypography>(BrandingDir + "/ACCERTONE_Typography.asset");
            typo.displayFont = display;
            typo.headlineFont = headline;
            typo.titleFont = title;
            typo.labelFont = label;
            typo.bodyFont = body;
            typo.dataFont = data;
            typo.lightFont = light;
            typo.styles = new[]
            {
                // The name is set exactly as "Accert one" (no forced upper case); tracking tuned for lower case.
                Spec(BrandTextRole.Wordmark, display, 62f, 1.5f, false),
                Spec(BrandTextRole.Display, display, 40f, 10f, true),
                Spec(BrandTextRole.Headline, headline, 40f, 1f, true),
                Spec(BrandTextRole.Title, title, 20f, 6f, true),
                Spec(BrandTextRole.Label, label, 14f, 18f, true),
                Spec(BrandTextRole.Body, body, 15f, 0f, false),
                Spec(BrandTextRole.Caption, label, 11f, 26f, true),
                Spec(BrandTextRole.Data, data, 22f, 0f, true),
                Spec(BrandTextRole.DataLarge, light, 44f, 0f, true),
                Spec(BrandTextRole.Tagline, label, 12f, 52f, true),
            };
            EditorUtility.SetDirty(typo);

            var identity = LoadOrCreate<AccertoneBrandIdentity>(BrandingResourcesDir + "/" + AccertoneBrandIdentity.ResourcePath + ".asset");
            identity.palette = palette;
            identity.typography = typo;
            // Official name is "Accert one" (with a space); migrate the legacy spelling.
            if (string.IsNullOrEmpty(identity.gameName) || identity.gameName.Replace(" ", "").ToUpperInvariant() == "ACCERTONE")
                identity.gameName = AccertoneBrandIdentity.OfficialName;
            identity.toneSplitIndex = 6;
            EditorUtility.SetDirty(identity);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return identity;
        }

        static BrandTextStyleSpec Spec(BrandTextRole role, TMP_FontAsset font, float size, float spacing, bool upper)
        {
            return new BrandTextStyleSpec { role = role, font = font, size = size, characterSpacing = spacing, uppercase = upper };
        }

        public static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // ------------------------------------------------------------------ Textures

        public enum Falloff { SoftEllipse, HorizontalFade, VerticalFade, Dot, SoftLine }

        public static Texture2D GenerateTexture(string name, int w, int h, Falloff kind, bool sprite)
        {
            string path = TextureDir + "/" + name + ".png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    float a = 0f;
                    switch (kind)
                    {
                        case Falloff.SoftEllipse:
                        {
                            float dx = (u - 0.5f) * 2f, dy = (v - 0.5f) * 2f;
                            float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                            a = Mathf.Pow(1f - d, 2.2f);
                            break;
                        }
                        case Falloff.HorizontalFade:
                            a = Mathf.Pow(1f - u, 1.7f);
                            break;
                        case Falloff.VerticalFade:
                            a = Mathf.Pow(1f - v, 1.7f);
                            break;
                        case Falloff.Dot:
                        {
                            float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
                            a = Mathf.Clamp01((1f - d) * w * 0.5f);
                            break;
                        }
                        case Falloff.SoftLine:
                        {
                            float dy = Mathf.Abs(v - 0.5f) * 2f;
                            float ends = Mathf.Clamp01(Mathf.Min(u, 1f - u) * 6f);
                            a = Mathf.Exp(-dy * dy * 9f) * Mathf.SmoothStep(0f, 1f, ends);
                            break;
                        }
                    }
                    px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            if (sprite) importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = !sprite;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static Sprite LoadSprite(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(TextureDir + "/" + name + ".png");
        }

        // ------------------------------------------------------------------ Materials

        /// <summary>HDR colour for Material.SetColor in a linear project: intensity is applied in linear space.</summary>
        public static Color Hdr(Color srgb, float intensity)
        {
            Color lin = srgb.linear * intensity;
            Color g = lin.gamma;
            g.a = 1f;
            return g;
        }

        static Material LoadOrCreateMaterial(string name, Shader shader)
        {
            string path = MaterialDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader)
            {
                m.shader = shader;
            }
            return m;
        }

        public static Material Lit(string name, Color baseColor, float metallic, float smoothness, Color? emissionSrgb = null, float emissionIntensity = 0f)
        {
            var m = LoadOrCreateMaterial(name, Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", baseColor);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
            // URP derives _EMISSION from the GI flags on import (AnyEmissive => keyword on), so set both.
            if (emissionSrgb.HasValue && emissionIntensity > 0f)
            {
                m.SetColor("_EmissionColor", Hdr(emissionSrgb.Value, emissionIntensity));
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.EnableKeyword("_EMISSION");
            }
            else
            {
                m.SetColor("_EmissionColor", Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                m.DisableKeyword("_EMISSION");
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material Glow(string name, Texture2D mask, Color srgb, float intensity)
        {
            var m = LoadOrCreateMaterial(name, Shader.Find("ACCERTONE/Additive Glow"));
            m.SetTexture("_MainTex", mask);
            m.SetColor("_Color", srgb);
            m.SetFloat("_Intensity", intensity);
            m.renderQueue = 3000;
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material Beam(string name, Color srgb, float intensity, float falloff, float edge)
        {
            var m = LoadOrCreateMaterial(name, Shader.Find("ACCERTONE/Light Beam"));
            m.SetColor("_Color", srgb);
            m.SetFloat("_Intensity", intensity);
            m.SetFloat("_Falloff", falloff);
            m.SetFloat("_EdgeSoftness", edge);
            m.renderQueue = 3000;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>TMP material preset derived from a font asset's default material.</summary>
        public static Material TmpPreset(string name, TMP_FontAsset font)
        {
            string path = FontDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(font.material) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            else
            {
                m.CopyPropertiesFromMaterial(font.material);
                m.shaderKeywords = font.material.shaderKeywords;
            }
            m.SetTexture(ShaderUtilities.ID_MainTex, font.atlasTexture);
            return m;
        }
    }
}
