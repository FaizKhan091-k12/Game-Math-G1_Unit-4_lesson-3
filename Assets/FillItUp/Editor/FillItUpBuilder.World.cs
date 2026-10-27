using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FillItUp.EditorTools
{
    public static partial class FillItUpBuilder
    {
        static GameConfig C;
        static Material M(Mat m) => C.Get(m);

        static void BuildScene(GameConfig cfg)
        {
            C = cfg;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // lighting: one warm sun, gradient ambient, no shadows (blob shadows are baked into the props)
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Palette.Hex(0xCDEBFF);
            RenderSettings.ambientEquatorColor = Palette.Hex(0xD9EFD6);
            RenderSettings.ambientGroundColor = Palette.Hex(0x9CC48A);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 0.3f;

            var lightGo = new GameObject("Sun Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Palette.Hex(0xFFF4E0);
            light.intensity = 1.15f;
            light.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            // camera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.SkyBottom;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 120f;
            cam.fieldOfView = 34f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<PhysicsRaycaster>();
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = false;
            camData.antialiasing = AntialiasingMode.None;
            camData.renderShadows = false;
            camData.requiresDepthTexture = false;
            camData.requiresColorTexture = false;
            var rig = camGo.AddComponent<CameraRig>();
            rig.cam = cam;

            var env = BuildEnvironment();
            var stage = BuildStage();
            var mascot = MascotBuilder.Build(C.Get);
            AddBlobShadow(mascot.transform, 1.25f);
            var col = mascot.gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 0.6f, 0);
            col.radius = 0.5f;
            col.height = 1.3f;

            var spots = new GameObject("MascotSpots").transform;
            Transform Spot(string n, Vector3 p, float s)
            {
                var t = new GameObject(n).transform;
                t.SetParent(spots, false);
                t.position = p;
                t.localScale = Vector3.one * s;
                return t;
            }
            var menuSpot = Spot("Menu", new Vector3(0f, 0f, 0f), 1.45f);
            var mapSpot = Spot("Map", new Vector3(3.55f, 0f, 0f), 0.95f);
            var playSpot = Spot("Play", new Vector3(-3.95f, 0f, 0.6f), 1.05f);
            var resultsSpot = Spot("Results", new Vector3(-2.35f, 0f, 0.6f), 1.15f);
            mascot.transform.position = menuSpot.position;
            mascot.transform.localScale = menuSpot.localScale;

            // game systems
            var game = new GameObject("Game");
            var flow = game.AddComponent<GameFlow>();
            var audio = new GameObject("Audio").AddComponent<AudioManager>();
            audio.transform.SetParent(game.transform, false);
            var fx = new GameObject("FX").AddComponent<FXPool>();
            fx.transform.SetParent(game.transform, false);

            // UI
            UIFactory.EnsureEventSystem();
            var canvas = UIFactory.Canvas(null, "UI", 10);
            var safe = UIFactory.Stretch(canvas.transform, "SafeArea");
            safe.gameObject.AddComponent<SafeArea>();
            var hud = HudScreen.Build(safe, C);
            var menu = MenuScreen.Build(safe, C);
            var map = MapScreen.Build(safe, C);
            var results = ResultsScreen.Build(safe, C);
            var progress = ProgressScreen.Build(safe, C);
            var pause = PauseScreen.Build(safe, C);
            var captions = CaptionBar.Build(safe);
            UIFactory.EdovuLogo(safe, C.edovuLogo, C.logoSprite);
            RotatePrompt.Build(canvas.transform, C); // full screen, over everything

            flow.stage = stage;
            flow.mascot = mascot;
            flow.rig = rig;
            flow.audioManager = audio;
            flow.mascotMenuSpot = menuSpot;
            flow.mascotMapSpot = mapSpot;
            flow.mascotPlaySpot = playSpot;
            flow.mascotResultsSpot = resultsSpot;
            flow.menu = menu;
            flow.map = map;
            flow.hud = hud;
            flow.results = results;
            flow.progress = progress;
            flow.pause = pause;
            flow.captions = captions;

            SaveMeshes(env, MeshDir + "/Scene_Environment.asset");
            SaveMeshes(stage.gameObject, MeshDir + "/Scene_Stage.asset");
            SaveMeshes(mascot.gameObject, MeshDir + "/Scene_Splashy.asset");
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Flat quad on the XZ plane with UVs (for textured shadow / glow decals).</summary>
        static Mesh FlatQuad(string name)
        {
            var m = new Mesh { name = name };
            m.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.RecalculateBounds();
            return m;
        }

        static GameObject Decal(Transform parent, string name, Material mat, float y, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, y, 0);
            go.transform.localScale = new Vector3(size, 1, size);
            go.AddComponent<MeshFilter>().sharedMesh = FlatQuad(name);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        static void AddBlobShadow(Transform parent, float size) => Decal(parent, "Shadow", M(Mat.Shadow), 0.012f, size);

        static Material ExtraUnlit(string name, Color c) => UnlitMat(name, c);

        static Material GlowMat()
        {
            var m = ParticleMat("Glow", C.softCircle.texture, true, 2995);
            m.SetColor("_BaseColor", new Color(1f, 0.9f, 0.5f, 0.9f));
            return m;
        }

        // ------------------------------------------------------------------ environment

        static GameObject BuildEnvironment()
        {
            var root = new GameObject("Environment");
            var kit = new MeshKit();
            var rnd = new System.Random(7);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

            // ground and the wooden play deck
            kit.Lathe(M(Mat.Grass), new[] { new Vector2(0, -0.4f), new Vector2(45, -0.4f), new Vector2(45, -0.4f), new Vector2(45, -0.02f), new Vector2(45, -0.02f), new Vector2(0, -0.02f) }, Matrix4x4.identity, 48);
            kit.Lathe(M(Mat.Wood), new[] { new Vector2(0, -0.1f), new Vector2(1, -0.1f), new Vector2(1, -0.1f), new Vector2(1, 0f), new Vector2(1, 0f), new Vector2(0, 0f) }, Matrix4x4.identity, 48, 2f, 6.4f, 3.4f);
            kit.Lathe(M(Mat.WoodDark), new[] { new Vector2(1f, -0.1f), new Vector2(1.02f, -0.1f), new Vector2(1.02f, -0.1f), new Vector2(1.02f, 0.03f), new Vector2(1.02f, 0.03f), new Vector2(1f, 0.03f) }, Matrix4x4.identity, 48, 2f, 6.4f, 3.4f);
            for (int i = -5; i <= 5; i++)
                kit.Box(M(Mat.WoodDark), new Vector3(i * 1.1f, 0f, 0), new Vector3(0.03f, 0.004f, 6.2f * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(i * 1.1f / 6.4f, 2f)))), Quaternion.identity, 4f);

            // hills
            kit.Sphere(M(Mat.GrassDark), new Vector3(-15, -1.2f, 24), new Vector3(11, 4.5f, 6), Quaternion.identity, 24);
            kit.Sphere(M(Mat.GrassDark), new Vector3(3, -1.8f, 28), new Vector3(15, 5.5f, 7), Quaternion.identity, 24);
            kit.Sphere(M(Mat.GrassDark), new Vector3(18, -1.2f, 22), new Vector3(10, 4.2f, 5), Quaternion.identity, 24);
            kit.Sphere(M(Mat.Grass), new Vector3(-8, -1f, 17), new Vector3(7, 2.6f, 4), Quaternion.identity, 20);
            kit.Sphere(M(Mat.Grass), new Vector3(10, -1f, 16), new Vector3(7, 2.4f, 4), Quaternion.identity, 20);

            // trees
            var trees = new[]
            {
                new Vector3(-8.6f, 0, 5f), new Vector3(-6.6f, 0, 8.5f), new Vector3(8.4f, 0, 6f), new Vector3(6.3f, 0, 9.8f),
                new Vector3(-11.5f, 0, 10f), new Vector3(12f, 0, 11f), new Vector3(-3.4f, 0, 11.5f), new Vector3(3.2f, 0, 13f),
                new Vector3(-13f, 0, 3f), new Vector3(13.5f, 0, 3.5f)
            };
            foreach (var t in trees)
            {
                float s = R(0.85f, 1.3f);
                kit.Cylinder(M(Mat.WoodDark), 0.18f * s, 1.5f * s, MeshKit.At(t), 10, 0.13f * s);
                kit.Sphere(M(Mat.Leaf), t + new Vector3(0, 2.0f, 0) * s, Vector3.one * 1.0f * s, Quaternion.identity, 14);
                kit.Sphere(M(Mat.Leaf), t + new Vector3(-0.55f, 1.6f, -0.1f) * s, Vector3.one * 0.72f * s, Quaternion.identity, 12);
                kit.Sphere(M(Mat.GrassDark), t + new Vector3(0.55f, 1.65f, 0.1f) * s, Vector3.one * 0.78f * s, Quaternion.identity, 12);
            }

            // bushes along the back of the deck
            foreach (var b in new[] { new Vector3(-5.8f, 0, 3.6f), new Vector3(5.9f, 0, 3.4f), new Vector3(-7.6f, 0, 1.2f), new Vector3(7.7f, 0, 1.0f), new Vector3(-2.5f, 0, 4.6f), new Vector3(2.8f, 0, 4.8f) })
            {
                kit.Sphere(M(Mat.Leaf), b, new Vector3(0.7f, 0.55f, 0.6f), Quaternion.identity, 12);
                kit.Sphere(M(Mat.GrassDark), b + new Vector3(0.55f, -0.05f, 0.1f), new Vector3(0.5f, 0.42f, 0.45f), Quaternion.identity, 12);
                kit.Sphere(M(Mat.Leaf), b + new Vector3(-0.5f, -0.08f, 0.05f), new Vector3(0.45f, 0.38f, 0.4f), Quaternion.identity, 10);
            }

            // flowers
            var petals = new[] { Mat.Pink, Mat.Yellow, Mat.Purple, Mat.Red, Mat.White };
            for (int i = 0; i < 26; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var p = new Vector3(side * R(6.8f, 10.5f), 0, R(-3.5f, 4f));
                float h = R(0.25f, 0.4f);
                kit.Cylinder(M(Mat.Leaf), 0.025f, h, MeshKit.At(p), 6);
                kit.Sphere(petals[i % petals.Length] == Mat.White ? M(Mat.White) : M(petals[i % petals.Length]), p + Vector3.up * h, Vector3.one * 0.1f, Quaternion.identity, 8);
                kit.Sphere(M(Mat.Yellow), p + Vector3.up * h + new Vector3(0, 0, -0.06f), Vector3.one * 0.04f, Quaternion.identity, 6);
            }

            // white picket fence behind the deck
            for (float x = -6.6f; x <= 6.61f; x += 0.55f)
            {
                kit.Box(M(Mat.White), new Vector3(x, 0, 6.8f), new Vector3(0.14f, 0.75f, 0.06f), Quaternion.identity, 6f);
                kit.Cone(M(Mat.White), 0.09f, 0.12f, MeshKit.At(new Vector3(x, 0.75f, 6.8f)), 4);
            }
            kit.Box(M(Mat.White), new Vector3(0, 0.22f, 6.84f), new Vector3(13.4f, 0.08f, 0.04f), Quaternion.identity, 6f);
            kit.Box(M(Mat.White), new Vector3(0, 0.52f, 6.84f), new Vector3(13.4f, 0.08f, 0.04f), Quaternion.identity, 6f);

            // pond with stones
            var pond = new Vector3(7.2f, 0, 4.4f);
            kit.Lathe(M(Mat.Blue), new[] { new Vector2(0, 0f), new Vector2(1, 0f), new Vector2(1, 0f), new Vector2(1, 0.02f), new Vector2(1, 0.02f), new Vector2(0, 0.02f) }, MeshKit.At(pond), 28, 2f, 2.0f, 1.2f);
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f;
                kit.Sphere(M(Mat.Stone), pond + new Vector3(Mathf.Cos(a) * 2.05f, 0.02f, Mathf.Sin(a) * 1.25f), new Vector3(0.22f, 0.13f, 0.18f), Quaternion.Euler(0, R(0, 90), 0), 8);
            }

            var envGo = kit.Bake("EnvironmentArt", root.transform);
            GameObjectUtility.SetStaticEditorFlags(envGo, StaticEditorFlags.BatchingStatic);

            // sky gradient backdrop (vertex colours)
            var sky = new GameObject("Sky");
            sky.transform.SetParent(root.transform, false);
            var skyMesh = new Mesh { name = "SkyGradient" };
            const float w = 90f, z = 45f;
            skyMesh.vertices = new[] { new Vector3(-w, -6, z), new Vector3(-w, 14, z), new Vector3(-w, 60, z), new Vector3(w, -6, z), new Vector3(w, 14, z), new Vector3(w, 60, z) };
            skyMesh.colors = new[] { Palette.SkyBottom, Palette.Hex(0x9EDCFF), Palette.SkyTop, Palette.SkyBottom, Palette.Hex(0x9EDCFF), Palette.SkyTop };
            skyMesh.triangles = new[] { 0, 1, 4, 0, 4, 3, 1, 2, 5, 1, 5, 4 };
            skyMesh.RecalculateBounds();
            sky.AddComponent<MeshFilter>().sharedMesh = skyMesh;
            var sr = sky.AddComponent<MeshRenderer>();
            sr.sharedMaterial = M(Mat.Sky);
            sr.shadowCastingMode = ShadowCastingMode.Off;

            // sun with a soft glow, and a rainbow over the hills
            var sun = new MeshKit();
            sun.Sphere(M(Mat.Sun), new Vector3(-12f, 12.5f, 34f), new Vector3(2.3f, 2.3f, 0.5f), Quaternion.identity, 20);
            var rainbow = new[] { 0xFF8A8A, 0xFFC27A, 0xFFF08A, 0x9BEA9B, 0x8FD0FF, 0xC5A8FF };
            for (int i = 0; i < rainbow.Length; i++)
                sun.Torus(ExtraUnlit("Rainbow" + i, Palette.Hex((uint)rainbow[i])), 12.6f - i * 0.55f, 0.29f, 8f, 172f, MeshKit.At(new Vector3(13f, -3f, 38f)), 40, 6);
            sun.Bake("SunAndRainbow", root.transform);
            var glow = Decal(root.transform, "SunGlow", GlowMat(), 0, 9f);
            glow.transform.position = new Vector3(-12f, 12.5f, 33.8f);
            glow.transform.rotation = Quaternion.Euler(-90f, 0, 0);

            // drifting clouds
            for (int i = 0; i < 6; i++)
            {
                var ck = new MeshKit();
                int puffs = 4 + i % 2;
                for (int k = 0; k < puffs; k++)
                {
                    float r = R(0.9f, 1.5f);
                    ck.Sphere(M(Mat.Cloud), new Vector3(k * 1.1f - puffs * 0.55f, R(-0.1f, 0.5f), R(-0.2f, 0.2f)), new Vector3(r, r * 0.72f, r * 0.6f), Quaternion.identity, 14);
                }
                var cloud = ck.Bake("Cloud" + i, root.transform);
                cloud.transform.position = new Vector3(-24f + i * 9f + R(-2, 2), R(6.5f, 10.5f), R(24f, 32f));
                var bob = cloud.AddComponent<Bobber>();
                bob.bobHeight = 0.15f;
                bob.bobSpeed = 0.4f;
                bob.driftSpeed = R(0.15f, 0.35f);
                bob.driftWrap = 30f;
            }

            // rubber ducks bobbing on the pond
            for (int i = 0; i < 2; i++)
            {
                var dk = new MeshKit();
                dk.Sphere(M(Mat.Rubber), new Vector3(0, 0.12f, 0), new Vector3(0.22f, 0.15f, 0.17f), Quaternion.identity, 12);
                dk.Sphere(M(Mat.Rubber), new Vector3(-0.12f, 0.3f, 0), new Vector3(0.11f, 0.11f, 0.11f), Quaternion.identity, 10);
                dk.Cone(M(Mat.Orange), 0.045f, 0.1f, MeshKit.At(new Vector3(-0.22f, 0.3f, 0), new Vector3(0, 0, 90f)), 6);
                dk.Sphere(M(Mat.Pupil), new Vector3(-0.17f, 0.34f, -0.08f), Vector3.one * 0.022f, Quaternion.identity, 6);
                var duck = dk.Bake("Duck" + i, root.transform);
                duck.transform.position = pond + new Vector3(i == 0 ? -0.6f : 0.7f, 0.02f, i == 0 ? -0.2f : 0.35f);
                duck.transform.rotation = Quaternion.Euler(0, i == 0 ? 30f : 200f, 0);
                var bob = duck.AddComponent<Bobber>();
                bob.bobHeight = 0.03f;
                bob.bobSpeed = 2.2f;
                bob.swayDegrees = 7f;
            }
            return root;
        }

        // ------------------------------------------------------------------ stage

        static Stage BuildStage()
        {
            var go = new GameObject("Stage");
            var stage = go.AddComponent<Stage>();
            var glowMat = GlowMat();

            for (int i = 0; i < 3; i++)
            {
                var p = new GameObject("Pedestal" + i);
                p.transform.SetParent(go.transform, false);
                var ped = p.AddComponent<Pedestal>();
                var bk = new MeshKit();
                bk.Cylinder(M(Mat.PedestalRim), 0.7f, 0.2f, Matrix4x4.identity, 32, 0.66f);
                bk.Torus(M(Mat.White), 0.68f, 0.035f, 0, 360, MeshKit.At(new Vector3(0, 0.1f, 0), new Vector3(90, 0, 0)), 32, 6, true);
                bk.Bake("Base", p.transform);
                var tk = new MeshKit();
                tk.Cylinder(M(Mat.Pedestal), 0.64f, 0.05f, MeshKit.At(new Vector3(0, 0.2f, 0)), 32);
                ped.top = tk.Bake("Top", p.transform).GetComponent<Renderer>();
                ped.glow = Decal(p.transform, "Glow", glowMat, 0.03f, 2.4f).transform;
                AddBlobShadow(p.transform, 1.9f);
                ped.height = 0.25f;
                var box = p.AddComponent<BoxCollider>();
                box.center = new Vector3(0, 0.12f, 0);
                box.size = new Vector3(1.4f, 0.26f, 1.4f);
                stage.pedestals[i] = ped;
            }

            var stand = new GameObject("PourStand");
            stand.transform.SetParent(go.transform, false);
            var sk = new MeshKit();
            sk.Cylinder(M(Mat.PedestalRim), 0.45f, 0.94f, Matrix4x4.identity, 28, 0.38f);
            sk.Cylinder(M(Mat.Pedestal), 0.55f, 0.06f, MeshKit.At(new Vector3(0, 0.94f, 0)), 28);
            foreach (var y in new[] { 0.3f, 0.62f })
                sk.Torus(M(Mat.White), 0.45f - y * 0.07f, 0.03f, 0, 360, MeshKit.At(new Vector3(0, y, 0), new Vector3(90, 0, 0)), 28, 6, true);
            sk.Bake("StandArt", stand.transform);
            AddBlobShadow(stand.transform, 1.6f);
            stage.pourStand = stand.transform;
            stage.standHeight = 1.0f;

            var streamGo = new GameObject("WaterStream");
            streamGo.transform.SetParent(go.transform, false);
            var ws = streamGo.AddComponent<WaterStream>();
            var ck = new MeshKit();
            ck.Lathe(M(Mat.Water), new[] { new Vector2(0, -1), new Vector2(1, -1), new Vector2(1, -1), new Vector2(1, 0), new Vector2(1, 0), new Vector2(0, 0) }, Matrix4x4.identity, 12);
            ws.column = ck.Bake("Column", streamGo.transform).transform;
            ws.column.gameObject.SetActive(false);
            stage.stream = ws;

            var pk = new MeshKit();
            pk.Lathe(M(Mat.Water), new[] { new Vector2(0, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0.012f), new Vector2(0.5f, 0.012f), new Vector2(0, 0.012f) }, Matrix4x4.identity, 32);
            var puddle = pk.Bake("Puddle", go.transform);
            stage.puddle = puddle.transform;

            var cr = new GameObject("Containers");
            cr.transform.SetParent(go.transform, false);
            stage.containersRoot = cr.transform;
            return stage;
        }
    }
}
