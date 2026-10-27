using System.Collections.Generic;
using UnityEngine;

namespace FillItUp
{
    public enum Fx { Sparkle, StarBurst, Confetti, Splash, SmallSplash, Puff, Ring, Bubbles, Hearts }

    /// <summary>Pooled particle bursts configured in code (few particles, no shadows, unlit materials).</summary>
    public class FXPool : MonoBehaviour
    {
        const int PerType = 3;
        public static FXPool I { get; private set; }

        readonly Dictionary<Fx, List<ParticleSystem>> pool = new Dictionary<Fx, List<ParticleSystem>>();
        readonly Dictionary<Fx, int> next = new Dictionary<Fx, int>();
        GameConfig cfg;

        void Awake()
        {
            I = this;
            cfg = GameConfig.Load();
        }

        public void Play(Fx type, Vector3 position, float scale = 1f, Color? tint = null)
        {
            if (!pool.TryGetValue(type, out var list))
            {
                list = new List<ParticleSystem>();
                for (int i = 0; i < PerType; i++) list.Add(Create(type));
                pool[type] = list;
                next[type] = 0;
            }
            var ps = list[next[type]];
            next[type] = (next[type] + 1) % list.Count;
            ps.transform.position = position;
            ps.transform.localScale = Vector3.one * scale;
            if (tint.HasValue)
            {
                var main = ps.main;
                main.startColor = tint.Value;
            }
            ps.Clear(true);
            ps.Play(true);
        }

        ParticleSystem Create(Fx type)
        {
            var go = new GameObject("FX_" + type);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            var emission = ps.emission;
            emission.rateOverTime = 0;
            var shape = ps.shape;
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = cfg.particleSoft;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;

            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                         new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.6f), new GradientAlphaKey(0, 1) });
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = fade;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 1, 1, 0.2f));

            switch (type)
            {
                case Fx.Sparkle:
                    rend.sharedMaterial = cfg.particleStar;
                    main.duration = 0.6f;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.26f);
                    main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
                    main.startColor = new ParticleSystem.MinMaxGradient(Palette.Gold, Color.white);
                    main.gravityModifier = 0.2f;
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0, 20) });
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.2f;
                    break;
                case Fx.StarBurst:
                    rend.sharedMaterial = cfg.particleStar;
                    main.duration = 0.8f;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.36f);
                    main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
                    main.startColor = Palette.Gold;
                    main.gravityModifier = 0.5f;
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0, 28) });
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.1f;
                    break;
                case Fx.Confetti:
                    main.duration = 1.5f;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 2.8f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 9f);
                    main.startSize3D = true;
                    main.startSizeX = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);
                    main.startSizeY = new ParticleSystem.MinMaxCurve(0.04f, 0.07f);
                    main.startSizeZ = 1f;
                    main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
                    var colors = new Gradient { mode = GradientMode.Fixed };
                    var keys = new GradientColorKey[Palette.Confetti.Length];
                    for (int i = 0; i < keys.Length; i++) keys[i] = new GradientColorKey(Palette.Confetti[i], (i + 1f) / keys.Length);
                    colors.SetKeys(keys, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
                    main.startColor = new ParticleSystem.MinMaxGradient(colors) { mode = ParticleSystemGradientMode.RandomColor };
                    main.gravityModifier = 0.45f;
                    main.maxParticles = 90;
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0, 80) });
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 38f;
                    shape.radius = 0.2f;
                    shape.rotation = new Vector3(-90, 0, 0);
                    var rot = ps.rotationOverLifetime;
                    rot.enabled = true;
                    rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
                    var limit = ps.limitVelocityOverLifetime;
                    limit.enabled = true;
                    limit.drag = 2.4f;
                    limit.limit = 12f;
                    break;
                case Fx.Splash:
                case Fx.SmallSplash:
                    bool small = type == Fx.SmallSplash;
                    rend.sharedMaterial = cfg.particleDrop;
                    main.duration = 0.4f;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
                    main.startSpeed = small ? new ParticleSystem.MinMaxCurve(1f, 2f) : new ParticleSystem.MinMaxCurve(2f, 4f);
                    main.startSize = small ? new ParticleSystem.MinMaxCurve(0.05f, 0.1f) : new ParticleSystem.MinMaxCurve(0.08f, 0.17f);
                    main.startColor = new ParticleSystem.MinMaxGradient(Palette.Hex(0x7FD3FF), Palette.Hex(0xD6F2FF));
                    main.gravityModifier = 1.3f;
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0, small ? 8 : 26) });
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = small ? 30f : 42f;
                    shape.radius = small ? 0.05f : 0.15f;
                    shape.rotation = new Vector3(-90, 0, 0);
                    rend.renderMode = ParticleSystemRenderMode.Stretch;
                    rend.velocityScale = 0.06f;
                    rend.lengthScale = 1.2f;
                    break;
                case Fx.Puff:
                    main.duration = 0.4f;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
                    main.startColor = new Color(1, 1, 1, 0.8f);
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0, 12) });
                    shape.shapeType = ParticleSystemShapeType.Circle;
                    shape.radius = 0.3f;
                    shape.rotation = new Vector3(90, 0, 0);
                    break;
                case Fx.Ring:
                    rend.sharedMaterial = cfg.particleStar;
                    main.duration = 0.5f;
                    main.startLifetime = 0.7f;
                    main.startSpeed = 3.2f;
                    main.startSize = 0.2f;
                    main.startColor = new ParticleSystem.MinMaxGradient(Palette.Hex(0x9B6BFF), Palette.Hex(0x36A9F7));
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0, 24) });
                    shape.shapeType = ParticleSystemShapeType.Circle;
                    shape.radius = 0.05f;
                    shape.radiusThickness = 0f;
                    shape.arcMode = ParticleSystemShapeMultiModeValue.Loop;
                    shape.rotation = new Vector3(90, 0, 0);
                    break;
                case Fx.Bubbles:
                    main.duration = 1.2f;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
                    main.startColor = new Color(0.85f, 0.95f, 1f, 0.9f);
                    main.gravityModifier = -0.15f;
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0, 14) });
                    shape.shapeType = ParticleSystemShapeType.Circle;
                    shape.radius = 0.25f;
                    shape.rotation = new Vector3(90, 0, 0);
                    var noise = ps.noise;
                    noise.enabled = true;
                    noise.strength = 0.4f;
                    noise.frequency = 1.2f;
                    noise.quality = ParticleSystemNoiseQuality.Low;
                    break;
                case Fx.Hearts:
                    rend.sharedMaterial = cfg.particleStar;
                    main.duration = 0.6f;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.25f);
                    main.startColor = new ParticleSystem.MinMaxGradient(Palette.Pink, Palette.Gold);
                    main.gravityModifier = -0.3f;
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0, 10) });
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.3f;
                    break;
            }
            return ps;
        }
    }
}
