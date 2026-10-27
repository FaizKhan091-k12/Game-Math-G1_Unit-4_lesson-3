using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;

namespace FillItUp.EditorTools
{
    /// <summary>
    /// One-click generator for "Fill It Up!": icons, materials, font, audio settings, container prefabs,
    /// the environment and the single game scene, plus WebGL-friendly project settings.
    /// Re-running it rebuilds everything generated (hand edits to the generated scene are overwritten).
    /// </summary>
    public static partial class FillItUpBuilder
    {
        public const string Root = "Assets/FillItUp";
        const string Gen = Root + "/Generated";
        const string MatDir = Gen + "/Materials";
        const string MeshDir = Gen + "/Meshes";
        const string TexDir = Gen + "/Textures";
        const string PrefabDir = Root + "/Prefabs";
        const string ResourcesDir = Root + "/Resources";
        const string AudioDir = Root + "/Audio";
        public const string ScenePath = Root + "/Scenes/FillItUp.unity";

        [MenuItem("Fill It Up/Build Game (all)", priority = 0)]
        public static void BuildAll()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Fill It Up", "Assets", 0.1f);
                var cfg = BuildAssets();
                EditorUtility.DisplayProgressBar("Fill It Up", "Containers", 0.35f);
                BuildContainers(cfg);
                EditorUtility.SetDirty(cfg);
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayProgressBar("Fill It Up", "Scene", 0.6f);
                BuildScene(cfg);
                ConfigureProject();
                ConfigureRendering();
                AssetDatabase.SaveAssets();
                Debug.Log("[FillItUp] Build complete.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ------------------------------------------------------------------ helpers

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        /// <summary>Stores every procedural mesh under root as sub-assets, so prefabs and the scene reference real assets.</summary>
        static void SaveMeshes(GameObject root, string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(assetPath) != null) AssetDatabase.DeleteAsset(assetPath);
            var container = ScriptableObject.CreateInstance<MeshContainer>();
            AssetDatabase.CreateAsset(container, assetPath);
            int i = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var m = mf.sharedMesh;
                if (m == null || AssetDatabase.Contains(m)) continue;
                m.name = mf.gameObject.name + "_" + i++;
                AssetDatabase.AddObjectToAsset(m, container);
            }
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ assets

        static GameConfig BuildAssets()
        {
            foreach (var f in new[] { MatDir, MeshDir, TexDir, PrefabDir, ResourcesDir, Root + "/Scenes" }) EnsureFolder(f);
            var cfg = LoadOrCreate<GameConfig>(ResourcesDir + "/" + GameConfig.ResourcePath + ".asset");
            BuildIcons(cfg);
            BuildMaterials(cfg);
            BuildFont(cfg);
            SetupAudio(cfg);
            cfg.edovuLogo = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Edovu Logo .prefab");
            cfg.logoSprite = BuildLogo("Assets/Grade 6 L1/Logo High.png", 512);
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            return cfg;
        }

        /// <summary>
        /// The source logo is 4096 px but shows at ~150 px, and on WebGL Unity caps it at 2048 and compresses it,
        /// which smears the small tagline. This bakes a clean copy at `size` px (successive 2x GPU halvings act as
        /// a box filter, so thin text stays sharp), imported uncompressed on every platform with trilinear mipmaps.
        /// The original file is not touched.
        /// </summary>
        static Sprite BuildLogo(string sourcePath, int size)
        {
            if (!File.Exists(sourcePath)) { Debug.LogWarning("[FillItUp] logo not found: " + sourcePath); return null; }
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            src.LoadImage(File.ReadAllBytes(sourcePath));
            Texture current = src;
            var temps = new List<RenderTexture>();
            int w = src.width, h = src.height;
            while (w > size || h > size)
            {
                w = Mathf.Max(size, w / 2);
                h = Mathf.Max(size, h / 2);
                var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                rt.filterMode = FilterMode.Bilinear;
                Graphics.Blit(current, rt);
                temps.Add(rt);
                current = rt;
            }
            var prev = RenderTexture.active;
            var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            if (current is RenderTexture last)
            {
                RenderTexture.active = last;
                outTex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                outTex.Apply();
            }
            else outTex.SetPixels32(src.GetPixels32());
            RenderTexture.active = prev;
            foreach (var t in temps) RenderTexture.ReleaseTemporary(t);

            string path = TexDir + "/EdovuLogo" + size + ".png";
            File.WriteAllBytes(path, outTex.EncodeToPNG());
            Object.DestroyImmediate(src);
            Object.DestroyImmediate(outTex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.sRGBTexture = true;
            ti.mipmapEnabled = true;            // smooth when the canvas scales it down on small screens
            ti.mipmapFilter = TextureImporterMipFilter.BoxFilter;
            ti.filterMode = FilterMode.Trilinear;
            ti.anisoLevel = 0;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.maxTextureSize = size;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            foreach (var platform in new[] { "WebGL", "Standalone", "Android", "iPhone" })
            {
                var ps = ti.GetPlatformTextureSettings(platform);
                ps.overridden = true;
                ps.maxTextureSize = size;
                ps.format = TextureImporterFormat.RGBA32;
                ps.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SetPlatformTextureSettings(ps);
            }
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Material LitMat(string name, Color color, float smooth = 0.25f, float spec = 0.15f)
        {
            string path = MatDir + "/FIU_" + name + ".mat";
            var shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            m.SetColor("_BaseColor", color);
            m.SetColor("_SpecColor", new Color(spec, spec, spec, 1f));
            m.SetFloat("_Smoothness", smooth);
            m.EnableKeyword("_SPECULAR_COLOR");
            m.SetFloat("_SpecularHighlights", 1f);
            m.SetFloat("_ReceiveShadows", 0f);
            m.EnableKeyword("_RECEIVE_SHADOWS_OFF");
            m.enableInstancing = false;
            SetOpaque(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material UnlitMat(string name, Color color)
        {
            string path = MatDir + "/FIU_" + name + ".mat";
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            m.SetColor("_BaseColor", color);
            SetOpaque(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void SetOpaque(Material m)
        {
            m.SetFloat("_Surface", 0f);
            m.SetOverrideTag("RenderType", "Opaque");
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)BlendMode.Zero);
            m.SetFloat("_ZWrite", 1f);
            m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = -1;
        }

        static void SetTransparent(Material m, int queue, bool additive = false)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_SrcBlendAlpha", 1f);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = queue;
        }

        static Material ParticleMat(string name, Texture tex, bool additive, int queue = 3100)
        {
            string path = MatDir + "/FIU_" + name + ".mat";
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            SetTransparent(m, queue, additive);
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BaseMap", tex);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void BuildMaterials(GameConfig cfg)
        {
            var mats = new Material[System.Enum.GetValues(typeof(Mat)).Length];
            void L(Mat id, uint hex, float smooth = 0.25f, float spec = 0.15f) => mats[(int)id] = LitMat(id.ToString(), Palette.Hex(hex), smooth, spec);
            L(Mat.Grass, 0x7FD15A, 0.1f, 0.05f);
            L(Mat.GrassDark, 0x5BB54A, 0.1f, 0.05f);
            L(Mat.Sand, 0xF5D9A0, 0.1f, 0.05f);
            L(Mat.Wood, 0xD9A066, 0.2f, 0.1f);
            L(Mat.WoodDark, 0xA8723F, 0.2f, 0.1f);
            L(Mat.Stone, 0xB8C2CC, 0.2f, 0.1f);
            L(Mat.White, 0xF7F9FC, 0.55f, 0.3f);
            L(Mat.Cream, 0xFFF3D6, 0.3f, 0.15f);
            L(Mat.Red, 0xFF5A5F, 0.5f, 0.25f);
            L(Mat.Orange, 0xFF9F2E, 0.5f, 0.25f);
            L(Mat.Yellow, 0xFFD23F, 0.5f, 0.25f);
            L(Mat.Green, 0x4CD964, 0.5f, 0.25f);
            L(Mat.Leaf, 0x3FBF5A, 0.15f, 0.05f);
            L(Mat.Teal, 0x26C6C6, 0.5f, 0.25f);
            L(Mat.Blue, 0x36A9F7, 0.55f, 0.3f);
            L(Mat.Navy, 0x1E3A5F, 0.4f, 0.2f);
            L(Mat.Purple, 0x9B6BFF, 0.55f, 0.3f);
            L(Mat.Pink, 0xFF6FA8, 0.5f, 0.25f);
            L(Mat.Metal, 0xC9D3DD, 0.8f, 0.6f);
            L(Mat.Gold, 0xFFC93C, 0.8f, 0.6f);
            L(Mat.Black, 0x2B2F38, 0.5f, 0.3f);
            L(Mat.Clay, 0xE07A4F, 0.2f, 0.1f);
            L(Mat.MascotBody, 0x55C8FF, 0.85f, 0.55f);
            L(Mat.Cheek, 0xFF8FB8, 0.3f, 0.1f);
            L(Mat.EyeWhite, 0xFFFFFF, 0.7f, 0.3f);
            L(Mat.Pupil, 0x1B2440, 0.85f, 0.6f);
            L(Mat.Pedestal, 0xFFF3D6, 0.3f, 0.15f);
            L(Mat.PedestalRim, 0x8FD3FF, 0.4f, 0.2f);
            L(Mat.Fish, 0xFF8A3D, 0.5f, 0.25f);
            L(Mat.Rubber, 0xFFD23F, 0.6f, 0.3f);

            // URP re-derives the queue from _QueueOffset, so water (0) draws before glass (5)
            var glass = LitMat("Glass", new Color(0.62f, 0.84f, 1f, 0.2f), 0.85f, 0.18f);
            SetTransparent(glass, 3005);
            glass.SetFloat("_QueueOffset", 5f);
            mats[(int)Mat.Glass] = glass;
            var water = LitMat("Water", Palette.Hex(0x1E9BFF, 0.82f), 0.9f, 0.45f);
            SetTransparent(water, 3000);
            water.SetFloat("_QueueOffset", 0f);
            mats[(int)Mat.Water] = water;

            mats[(int)Mat.Cloud] = UnlitMat("Cloud", Color.white);
            mats[(int)Mat.Sun] = UnlitMat("Sun", Palette.Hex(0xFFE27A));

            var softTex = cfg.softCircle.texture;
            mats[(int)Mat.Sky] = ParticleMat("Sky", null, false, 1000);
            SetOpaque(mats[(int)Mat.Sky]);
            mats[(int)Mat.Sky].renderQueue = 1000;
            mats[(int)Mat.Shadow] = ParticleMat("Shadow", softTex, false, 2990);
            mats[(int)Mat.Shadow].SetColor("_BaseColor", new Color(0.05f, 0.2f, 0.1f, 0.35f));

            cfg.materials = mats;
            cfg.particleSoft = ParticleMat("ParticleSoft", softTex, false);
            cfg.particleStar = ParticleMat("ParticleStar", cfg.star.texture, false);
            cfg.particleDrop = ParticleMat("ParticleDrop", AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/DropWhite.png"), false);
        }

        static void BuildFont(GameConfig cfg)
        {
            string ttfPath = Root + "/Fonts/LilitaOne-Regular.ttf";
            string assetPath = Root + "/Fonts/LilitaOne SDF.asset";
            const string chars = " ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!?.,:;'\"+-x%/()<>*=&#";
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (fa == null && font != null)
            {
                fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
                fa.name = "LilitaOne SDF";
                AssetDatabase.CreateAsset(fa, assetPath);
                fa.atlasTextures[0].name = "LilitaOne Atlas";
                AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
                fa.material.name = "LilitaOne Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
                fa.TryAddCharacters(chars);
                EditorUtility.SetDirty(fa);
                AssetDatabase.SaveAssets();
            }
            if (fa == null) { Debug.LogWarning("[FillItUp] Lilita One font missing, using the TMP default."); return; }
            if (fa.atlasPopulationMode == AtlasPopulationMode.Dynamic)
            {
                // every string is plain ASCII: bake it and freeze the atlas (stable and cheap on WebGL)
                fa.TryAddCharacters(chars);
                fa.atlasPopulationMode = AtlasPopulationMode.Static;
                EditorUtility.SetDirty(fa);
            }
            cfg.font = fa;

            string outlinePath = Root + "/Fonts/LilitaOne Outline.mat";
            var outline = AssetDatabase.LoadAssetAtPath<Material>(outlinePath);
            if (outline == null) { outline = new Material(fa.material); AssetDatabase.CreateAsset(outline, outlinePath); }
            outline.CopyPropertiesFromMaterial(fa.material);
            outline.EnableKeyword("OUTLINE_ON");
            outline.SetFloat("_OutlineWidth", 0.2f);
            outline.SetColor("_OutlineColor", Palette.Hex(0x16365C));
            outline.SetFloat("_FaceDilate", 0.2f);
            outline.EnableKeyword("UNDERLAY_ON");
            outline.SetColor("_UnderlayColor", new Color(0.05f, 0.12f, 0.25f, 0.5f));
            outline.SetFloat("_UnderlayOffsetY", -0.8f);
            outline.SetFloat("_UnderlaySoftness", 0.15f);
            EditorUtility.SetDirty(outline);
            cfg.fontOutline = outline;

            // make Lilita the TMP default so nothing falls back to Liberation Sans
            var settings = TMP_Settings.instance;
            if (settings != null)
            {
                var so = new SerializedObject(settings);
                var p = so.FindProperty("m_defaultFontAsset");
                if (p != null) { p.objectReferenceValue = fa; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
        }

        static void SetupAudio(GameConfig cfg)
        {
            var vol = new Dictionary<Sfx, float>
            {
                { Sfx.UiClick, 0.8f }, { Sfx.UiHover, 0.5f }, { Sfx.Select, 0.8f }, { Sfx.Correct, 0.85f }, { Sfx.Wrong, 0.55f },
                { Sfx.Splash, 0.7f }, { Sfx.Pour, 0.6f }, { Sfx.Fill, 0.7f }, { Sfx.Overflow, 0.75f }, { Sfx.Sparkle, 0.6f },
                { Sfx.Droplet, 0.8f }, { Sfx.Streak, 0.8f }, { Sfx.LevelComplete, 0.85f }, { Sfx.Confetti, 0.7f },
                { Sfx.Whoosh, 0.5f }, { Sfx.Unlock, 0.85f }, { Sfx.Boing, 0.6f }, { Sfx.Land, 0.7f }, { Sfx.Giggle, 0.6f },
                { Sfx.Aww, 0.6f }, { Sfx.StarStamp, 0.85f }, { Sfx.Drumroll, 0.6f }, { Sfx.Yawn, 0.6f }, { Sfx.Tick, 0.5f },
            };
            cfg.sfx.Clear();
            foreach (Sfx id in System.Enum.GetValues(typeof(Sfx)))
            {
                string file = Regex.Replace(id.ToString(), "(?<=[a-z])([A-Z])", "_$1").ToLowerInvariant();
                string path = AudioDir + "/SFX/" + file + ".mp3";
                ConfigureClip(path, AudioClipLoadType.DecompressOnLoad, 0.55f, true);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) { Debug.LogWarning("[FillItUp] missing sfx " + path); continue; }
                cfg.sfx.Add(new GameConfig.SfxEntry { id = id, clip = clip, volume = vol.TryGetValue(id, out var v) ? v : 0.8f });
            }
            cfg.voiceLines.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir + "/Voice" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ConfigureClip(path, AudioClipLoadType.CompressedInMemory, 0.5f, true);
                cfg.voiceLines.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
            }
            string music = AudioDir + "/Music/music_main.mp3";
            ConfigureClip(music, AudioClipLoadType.Streaming, 0.4f, false);
            cfg.music = AssetDatabase.LoadAssetAtPath<AudioClip>(music);
        }

        static void ConfigureClip(string path, AudioClipLoadType load, float quality, bool mono)
        {
            var ai = AssetImporter.GetAtPath(path) as AudioImporter;
            if (ai == null) return;
            ai.forceToMono = mono;
            ai.loadInBackground = load == AudioClipLoadType.Streaming;
            var s = ai.defaultSampleSettings;
            s.loadType = load;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = quality;
            s.preloadAudioData = load != AudioClipLoadType.Streaming;
            ai.defaultSampleSettings = s;
            ai.SaveAndReimport();
        }

        // ------------------------------------------------------------------ prefabs

        static void BuildContainers(GameConfig cfg)
        {
            cfg.containers.Clear();
            foreach (var kind in ContainerCatalog.All)
            {
                var go = ContainerFactory.Build(kind, cfg.Get);
                SaveMeshes(go, MeshDir + "/Container_" + kind + ".asset");
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/Container_" + kind + ".prefab");
                Object.DestroyImmediate(go);
                cfg.containers.Add(new GameConfig.ContainerEntry { kind = kind, prefab = prefab });
            }
        }

        // ------------------------------------------------------------------ project & rendering

        static void ConfigureProject()
        {
            PlayerSettings.productName = "Fill It Up!";
            PlayerSettings.companyName = "Grade Wise";
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.runInBackground = true;
            // full-window, any-orientation page with a water-drop loader (Assets/WebGLTemplates/FillItUp)
            PlayerSettings.WebGL.template = "PROJECT:FillItUp";
            // Uncompressed output: plain .data/.wasm/.framework.js files that the post-build "WebGL auto fix" tool
            // renames to build.*, and that run on any static server (VS Code Live Server, LMS) without
            // Content-Encoding headers. (Decompression Fallback would name them .unityweb, which the tool skips.)
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.stripEngineCode = true;
            // Low stripping keeps DOTween's reflection-based bits safe.
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
            UnityEditor.WebGL.UserBuildSettings.codeOptimization = UnityEditor.WebGL.WasmCodeOptimization.RuntimeSpeed;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        /// <summary>Light URP setup for WebGL / low-end PCs: no shadows (blob shadows instead), no HDR, no post.</summary>
        static void ConfigureRendering()
        {
            foreach (var path in new[] { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" })
            {
                var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (rp == null) continue;
                bool mobile = path.Contains("Mobile");
                rp.msaaSampleCount = mobile ? 1 : 2;
                rp.supportsHDR = false;
                rp.renderScale = 1f;
                rp.shadowDistance = 0f;
                rp.supportsCameraDepthTexture = false;
                rp.supportsCameraOpaqueTexture = false;
                var so = new SerializedObject(rp);
                SetProp(so, "m_MainLightShadowsSupported", false);
                SetProp(so, "m_AdditionalLightsRenderingMode", 0);
                SetProp(so, "m_AdditionalLightShadowsSupported", false);
                SetProp(so, "m_SoftShadowsSupported", false);
                SetProp(so, "m_UseSRPBatcher", true);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(rp);
            }
            PlayerSettings.runInBackground = true;
        }

        static void SetProp(SerializedObject so, string name, object value)
        {
            var p = so.FindProperty(name);
            if (p == null) { Debug.LogWarning("[FillItUp] URP property " + name + " not found"); return; }
            switch (value)
            {
                case bool b: p.boolValue = b; break;
                case int i: p.intValue = i; break;
                case float f: p.floatValue = f; break;
            }
        }
    }
}
