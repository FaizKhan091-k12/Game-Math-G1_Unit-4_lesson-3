using System;
using System.Collections.Generic;
using UnityEngine;

namespace FillItUp
{
    /// <summary>
    /// Models every container from lathe profiles (used by the editor builder to bake the prefabs).
    /// Each model is normalised so its on-screen area is the same; ContainerCatalog.DisplayScale then
    /// makes containers that hold more look bigger.
    /// </summary>
    public static class ContainerFactory
    {
        const float AreaK = 0.95f;   // projected-area normalisation constant
        const float MaxWidth = 1.3f, MaxHeight = 1.25f;

        class Spec
        {
            public Vector2[] outer;
            public float thickness;
            public Vector3 offset;
            public float n = 2f, ax = 1f, az = 1f;
        }

        static Vector2 P(float r, float y) => new Vector2(r, y);

        public static GameObject Build(ContainerKind kind, Func<Mat, Material> mat)
        {
            var kit = new MeshKit();
            var s = Model(kind, kit, mat);

            var root = new GameObject("Container_" + kind);
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            var art = kit.Bake("Art", model);
            var b = art.GetComponent<MeshFilter>().sharedMesh.bounds;

            // projected area as seen by the gameplay camera (~25 degrees down)
            float area = b.size.x * (b.size.y * 0.9f + b.size.z * 0.42f);
            float k = AreaK / Mathf.Sqrt(Mathf.Max(0.01f, area));
            k = Mathf.Min(k, MaxWidth / b.size.x, MaxHeight / b.size.y);

            // Model keeps its pivot at the base centre (for squash); the art sits inside it.
            art.transform.localScale = Vector3.one * k;
            art.transform.localPosition = new Vector3(-b.center.x * k, -b.min.y * k, -b.center.z * k);

            var vessel = new GameObject("Vessel").transform;
            vessel.SetParent(art.transform, false);
            vessel.localPosition = s.offset;
            var waterGo = new GameObject("Water");
            waterGo.transform.SetParent(vessel, false);
            waterGo.AddComponent<MeshFilter>();
            var wr = waterGo.AddComponent<MeshRenderer>();
            wr.sharedMaterial = mat(Mat.Water);
            wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            wr.receiveShadows = false;
            wr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            wr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            var view = root.AddComponent<ContainerView>();
            view.kind = kind;
            view.model = model;
            view.vessel = vessel;
            view.water = waterGo.GetComponent<MeshFilter>();
            view.inner = MeshKit.Inner(s.outer, s.thickness, true).ToArray();
            view.shapeN = s.n; view.shapeAx = s.ax; view.shapeAz = s.az;
            view.height = b.size.y * k;
            view.width = b.size.x * k;
            var top = s.outer[s.outer.Length - 1];
            view.lipLocal = new Vector3(top.x * s.ax, top.y, 0f);

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0, b.size.y * k * 0.5f, 0);
            col.size = new Vector3(Mathf.Max(0.5f, b.size.x * k), Mathf.Max(0.5f, b.size.y * k), Mathf.Max(0.4f, b.size.z * k));
            return root;
        }

        static Spec Vessel(MeshKit kit, Material m, Vector2[] outer, float t, Vector3 offset = default, float n = 2f, float ax = 1f, float az = 1f, int seg = 28)
        {
            kit.Lathe(m, MeshKit.Vessel(outer, t), MeshKit.At(offset), seg, n, ax, az);
            return new Spec { outer = outer, thickness = t, offset = offset, n = n, ax = ax, az = az };
        }

        static Matrix4x4 Flat(Vector3 pos) => MeshKit.At(pos, new Vector3(90, 0, 0));

        static Spec Model(ContainerKind kind, MeshKit kit, Func<Mat, Material> m)
        {
            switch (kind)
            {
                case ContainerKind.Spoon:
                {
                    var s = Vessel(kit, m(Mat.Metal), new[] { P(0.12f, 0), P(0.2f, 0.03f), P(0.25f, 0.07f), P(0.27f, 0.11f) }, 0.02f, Vector3.zero, 2f, 1f, 0.72f);
                    kit.Box(m(Mat.Metal), new Vector3(0.52f, 0.1f, 0), new Vector3(0.62f, 0.035f, 0.08f), Quaternion.Euler(0, 0, 9f));
                    kit.Sphere(m(Mat.Metal), new Vector3(0.84f, 0.15f, 0), new Vector3(0.07f, 0.025f, 0.06f), Quaternion.Euler(0, 0, 9f), 10);
                    return s;
                }
                case ContainerKind.Teacup:
                {
                    var off = new Vector3(0, 0.04f, 0);
                    var s = Vessel(kit, m(Mat.White), new[] { P(0.15f, 0), P(0.19f, 0.04f), P(0.29f, 0.24f), P(0.35f, 0.42f) }, 0.025f, off);
                    kit.Cylinder(m(Mat.Pink), 0.46f, 0.04f, MeshKit.At(Vector3.zero), 28, 0.4f);
                    kit.Torus(m(Mat.White), 0.1f, 0.026f, -90f, 90f, MeshKit.At(new Vector3(0.3f, 0.27f, 0)), 14, 8);
                    kit.Torus(m(Mat.Pink), 0.35f, 0.016f, 0, 360, Flat(off + new Vector3(0, 0.4f, 0)), 28, 6, true);
                    return s;
                }
                case ContainerKind.Glass:
                {
                    kit.Cylinder(m(Mat.Glass), 0.22f, 0.05f, MeshKit.At(Vector3.zero), 24);
                    return Vessel(kit, m(Mat.Glass), new[] { P(0.22f, 0), P(0.27f, 0.75f) }, 0.022f);
                }
                case ContainerKind.Mug:
                {
                    var s = Vessel(kit, m(Mat.Orange), new[] { P(0.26f, 0), P(0.28f, 0.03f), P(0.28f, 0.6f) }, 0.04f);
                    kit.Torus(m(Mat.Orange), 0.16f, 0.045f, -80f, 80f, MeshKit.At(new Vector3(0.3f, 0.31f, 0)), 14, 8);
                    kit.Torus(m(Mat.White), 0.282f, 0.018f, 0, 360, Flat(new Vector3(0, 0.45f, 0)), 28, 6, true);
                    return s;
                }
                case ContainerKind.Bowl:
                {
                    var off = new Vector3(0, 0.03f, 0);
                    kit.Cylinder(m(Mat.Teal), 0.2f, 0.04f, MeshKit.At(Vector3.zero), 24, 0.22f);
                    return Vessel(kit, m(Mat.Teal), new[] { P(0.22f, 0), P(0.36f, 0.07f), P(0.5f, 0.2f), P(0.56f, 0.34f) }, 0.035f, off);
                }
                case ContainerKind.Bottle:
                {
                    var s = Vessel(kit, m(Mat.Glass), new[] { P(0.2f, 0), P(0.22f, 0.02f), P(0.22f, 0.58f), P(0.19f, 0.7f), P(0.11f, 0.82f), P(0.085f, 0.86f), P(0.085f, 0.98f) }, 0.018f);
                    kit.Cylinder(m(Mat.Green), 0.226f, 0.1f, MeshKit.At(new Vector3(0, 0.28f, 0)), 24);
                    kit.Torus(m(Mat.Blue), 0.09f, 0.018f, 0, 360, Flat(new Vector3(0, 0.96f, 0)), 18, 6, true);
                    return s;
                }
                case ContainerKind.Jug:
                {
                    var s = Vessel(kit, m(Mat.Glass), new[] { P(0.24f, 0), P(0.29f, 0.07f), P(0.32f, 0.34f), P(0.27f, 0.62f), P(0.26f, 0.72f), P(0.3f, 0.8f) }, 0.022f);
                    kit.Torus(m(Mat.Blue), 0.2f, 0.04f, -75f, 75f, MeshKit.At(new Vector3(0.3f, 0.42f, 0)), 16, 8);
                    kit.Torus(m(Mat.Blue), 0.3f, 0.018f, 0, 360, Flat(new Vector3(0, 0.79f, 0)), 24, 6, true);
                    return s;
                }
                case ContainerKind.Vase:
                {
                    var s = Vessel(kit, m(Mat.Purple), new[] { P(0.18f, 0), P(0.29f, 0.14f), P(0.33f, 0.34f), P(0.21f, 0.64f), P(0.14f, 0.8f), P(0.2f, 0.95f) }, 0.03f);
                    kit.Torus(m(Mat.Gold), 0.325f, 0.02f, 0, 360, Flat(new Vector3(0, 0.34f, 0)), 28, 6, true);
                    return s;
                }
                case ContainerKind.Fishbowl:
                {
                    var outer = new List<Vector2>();
                    foreach (float a in new[] { -70f, -50f, -30f, -10f, 10f, 30f, 45f, 55f })
                    {
                        float r = a * Mathf.Deg2Rad;
                        outer.Add(P(0.45f * Mathf.Cos(r), 0.43f + 0.45f * Mathf.Sin(r) - 0.007f));
                    }
                    outer[0] = P(outer[0].x, 0f);
                    outer.Add(P(0.29f, 0.83f));
                    var s = Vessel(kit, m(Mat.Glass), outer.ToArray(), 0.02f, Vector3.zero, 2f, 1f, 1f, 30);
                    kit.Cylinder(m(Mat.Pink), 0.24f, 0.07f, MeshKit.At(new Vector3(0, 0.02f, 0)), 20, 0.26f);
                    kit.Cone(m(Mat.Leaf), 0.06f, 0.3f, MeshKit.At(new Vector3(-0.08f, 0.07f, 0.06f), new Vector3(0, 0, 8)), 8);
                    kit.Cone(m(Mat.Leaf), 0.05f, 0.22f, MeshKit.At(new Vector3(0.04f, 0.07f, 0.1f), new Vector3(0, 0, -12)), 8);
                    return s;
                }
                case ContainerKind.WateringCan:
                {
                    var s = Vessel(kit, m(Mat.Green), new[] { P(0.3f, 0), P(0.3f, 0.5f), P(0.26f, 0.56f) }, 0.025f);
                    kit.Cylinder(m(Mat.Green), 0.035f, 0.55f, MeshKit.At(new Vector3(0.25f, 0.12f, 0), new Vector3(0, 0, -50f)), 12);
                    var tip = new Vector3(0.25f + 0.55f * Mathf.Sin(50f * Mathf.Deg2Rad), 0.12f + 0.55f * Mathf.Cos(50f * Mathf.Deg2Rad), 0);
                    kit.Cylinder(m(Mat.Yellow), 0.04f, 0.1f, MeshKit.At(tip, new Vector3(0, 0, -50f)), 12, 0.08f);
                    kit.Torus(m(Mat.Green), 0.17f, 0.035f, 90f, 270f, MeshKit.At(new Vector3(-0.3f, 0.3f, 0)), 14, 8);
                    kit.Torus(m(Mat.Yellow), 0.302f, 0.02f, 0, 360, Flat(new Vector3(0, 0.25f, 0)), 28, 6, true);
                    return s;
                }
                case ContainerKind.Pot:
                {
                    var s = Vessel(kit, m(Mat.Metal), new[] { P(0.42f, 0), P(0.44f, 0.02f), P(0.44f, 0.5f) }, 0.03f);
                    kit.Torus(m(Mat.Black), 0.07f, 0.025f, -90f, 90f, MeshKit.At(new Vector3(0.44f, 0.4f, 0)), 10, 6);
                    kit.Torus(m(Mat.Black), 0.07f, 0.025f, 90f, 270f, MeshKit.At(new Vector3(-0.44f, 0.4f, 0)), 10, 6);
                    kit.Torus(m(Mat.Red), 0.442f, 0.018f, 0, 360, Flat(new Vector3(0, 0.12f, 0)), 32, 6, true);
                    return s;
                }
                case ContainerKind.Bucket:
                {
                    var s = Vessel(kit, m(Mat.Yellow), new[] { P(0.3f, 0), P(0.32f, 0.02f), P(0.42f, 0.65f), P(0.43f, 0.68f) }, 0.025f);
                    kit.Torus(m(Mat.Metal), 0.42f, 0.016f, 12f, 168f, MeshKit.At(new Vector3(0, 0.62f, 0), new Vector3(55f, 0, 0)), 20, 6);
                    kit.Torus(m(Mat.Orange), 0.43f, 0.02f, 0, 360, Flat(new Vector3(0, 0.66f, 0)), 32, 6, true);
                    return s;
                }
                case ContainerKind.FishTank:
                {
                    const float n = 12f, ax = 1.5f, az = 0.8f;
                    var s = Vessel(kit, m(Mat.Glass), new[] { P(0.5f, 0), P(0.5f, 0.85f) }, 0.018f, Vector3.zero, n, ax, az, 32);
                    kit.Lathe(m(Mat.Black), new[] { P(0, -0.05f), P(0.52f, -0.05f), P(0.52f, -0.05f), P(0.52f, 0.03f), P(0.52f, 0.03f), P(0, 0.03f) }, Matrix4x4.identity, 32, n, ax, az);
                    kit.Lathe(m(Mat.Black), new[] { P(0.515f, 0.81f), P(0.515f, 0.88f), P(0.515f, 0.88f), P(0.48f, 0.88f), P(0.48f, 0.88f), P(0.48f, 0.81f) }, Matrix4x4.identity, 32, n, ax, az);
                    kit.Box(m(Mat.Sand), new Vector3(0, 0.02f, 0), new Vector3(1.44f, 0.08f, 0.74f), Quaternion.identity);
                    kit.Box(m(Mat.Stone), new Vector3(0.42f, 0.08f, 0.12f), new Vector3(0.18f, 0.2f, 0.16f), Quaternion.identity);
                    kit.Cone(m(Mat.Red), 0.12f, 0.12f, MeshKit.At(new Vector3(0.42f, 0.28f, 0.12f)), 4);
                    kit.Cone(m(Mat.Leaf), 0.06f, 0.42f, MeshKit.At(new Vector3(-0.5f, 0.08f, 0.15f), new Vector3(0, 0, 6)), 8);
                    kit.Cone(m(Mat.Leaf), 0.05f, 0.3f, MeshKit.At(new Vector3(-0.38f, 0.08f, 0.2f), new Vector3(0, 0, -10)), 8);
                    kit.Cone(m(Mat.Leaf), 0.05f, 0.34f, MeshKit.At(new Vector3(0.1f, 0.08f, 0.22f), new Vector3(0, 0, 4)), 8);
                    return s;
                }
                case ContainerKind.Barrel:
                {
                    var outer = new[] { P(0.36f, 0), P(0.41f, 0.25f), P(0.44f, 0.5f), P(0.41f, 0.75f), P(0.36f, 1.0f) };
                    var s = Vessel(kit, m(Mat.Wood), outer, 0.04f, Vector3.zero, 2f, 1f, 1f, 24);
                    foreach (var y in new[] { 0.12f, 0.5f, 0.88f })
                    {
                        float r = y < 0.5f ? Mathf.Lerp(0.36f, 0.44f, y / 0.5f) : Mathf.Lerp(0.44f, 0.36f, (y - 0.5f) / 0.5f);
                        kit.Torus(m(Mat.Metal), r + 0.004f, 0.022f, 0, 360, Flat(new Vector3(0, y, 0)), 24, 6, true);
                    }
                    return s;
                }
                case ContainerKind.Bathtub:
                {
                    const float n = 4f, ax = 1.7f, az = 0.85f;
                    var off = new Vector3(0, 0.08f, 0);
                    var s = Vessel(kit, m(Mat.White), new[] { P(0.42f, 0), P(0.46f, 0.06f), P(0.5f, 0.5f), P(0.54f, 0.55f) }, 0.04f, off, n, ax, az, 32);
                    foreach (var p in new[] { new Vector3(-0.6f, 0.05f, -0.25f), new Vector3(0.6f, 0.05f, -0.25f), new Vector3(-0.6f, 0.05f, 0.25f), new Vector3(0.6f, 0.05f, 0.25f) })
                        kit.Sphere(m(Mat.Gold), p, new Vector3(0.07f, 0.07f, 0.07f), Quaternion.identity, 10);
                    kit.Cylinder(m(Mat.Metal), 0.03f, 0.32f, MeshKit.At(new Vector3(-0.72f, 0.55f, 0.3f)), 10);
                    kit.Cylinder(m(Mat.Metal), 0.025f, 0.16f, MeshKit.At(new Vector3(-0.72f, 0.85f, 0.3f), new Vector3(0, 0, -90f)), 10);
                    kit.Sphere(m(Mat.Yellow), new Vector3(0.5f, 0.66f, -0.1f), new Vector3(0.08f, 0.07f, 0.07f), Quaternion.identity, 10); // rubber duck
                    kit.Sphere(m(Mat.Yellow), new Vector3(0.55f, 0.74f, -0.1f), new Vector3(0.05f, 0.05f, 0.05f), Quaternion.identity, 10);
                    kit.Cone(m(Mat.Orange), 0.02f, 0.05f, MeshKit.At(new Vector3(0.6f, 0.74f, -0.1f), new Vector3(0, 0, -90f)), 6);
                    return s;
                }
                default: // Pool
                {
                    var s = Vessel(kit, m(Mat.White), new[] { P(0.95f, 0), P(1.0f, 0.3f) }, 0.05f, Vector3.zero, 2f, 1f, 1f, 36);
                    for (int i = 0; i < 8; i++)
                        kit.Torus(i % 2 == 0 ? m(Mat.Red) : m(Mat.Blue), 1.03f, 0.1f, i * 45f, i * 45f + 45f, Flat(new Vector3(0, 0.3f, 0)), 6, 10);
                    kit.Torus(m(Mat.Yellow), 1.0f, 0.09f, 0, 360, Flat(new Vector3(0, 0.1f, 0)), 36, 8, true);
                    return s;
                }
            }
        }
    }
}
