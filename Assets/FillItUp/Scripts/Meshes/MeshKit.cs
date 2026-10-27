using System.Collections.Generic;
using UnityEngine;

namespace FillItUp
{
    /// <summary>
    /// Procedural low-poly modelling. Shapes are added per material and baked into one mesh with one
    /// sub-mesh per material, so a whole prop is a single renderer (cheap on WebGL).
    ///
    /// Lathe profiles are (radius, height) points revolved around Y. The cross-section is a superellipse:
    /// n = 2 gives a circle, larger n gives a rounded box; ax/az stretch it. Repeating a point makes a hard edge.
    /// The surface faces the side to the right of the walking direction: walk up an outer wall, inwards over
    /// a top, down an inner wall.
    /// </summary>
    public class MeshKit
    {
        class Part
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<int> t = new List<int>();
        }

        readonly List<Material> order = new List<Material>();
        readonly Dictionary<Material, Part> parts = new Dictionary<Material, Part>();
        static readonly List<Vector3> tmpV = new List<Vector3>();
        static readonly List<int> tmpT = new List<int>();

        // ------------------------------------------------------------------ generic lathe (also used at runtime)

        public static void LatheInto(List<Vector3> verts, List<int> tris, IList<Vector2> profile, int seg, float n, float ax, float az)
        {
            int start = verts.Count;
            float e = 2f / Mathf.Max(0.5f, n);
            for (int i = 0; i < profile.Count; i++)
            {
                for (int j = 0; j < seg; j++)
                {
                    float a = j * Mathf.PI * 2f / seg;
                    float c = Mathf.Cos(a), s = Mathf.Sin(a);
                    float x = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), e) * ax;
                    float z = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), e) * az;
                    verts.Add(new Vector3(x * profile[i].x, profile[i].y, z * profile[i].x));
                }
            }
            for (int i = 0; i < profile.Count - 1; i++)
            {
                if (profile[i] == profile[i + 1]) continue; // hard edge: rings are separate
                for (int j = 0; j < seg; j++)
                {
                    int a = start + i * seg + j;
                    int b = start + (i + 1) * seg + j;
                    int c2 = start + (i + 1) * seg + (j + 1) % seg;
                    int d = start + i * seg + (j + 1) % seg;
                    tris.Add(a); tris.Add(b); tris.Add(c2);
                    tris.Add(a); tris.Add(c2); tris.Add(d);
                }
            }
        }

        // ------------------------------------------------------------------ shapes

        public void Lathe(Material m, IList<Vector2> profile, Matrix4x4 xf, int seg = 28, float n = 2f, float ax = 1f, float az = 1f)
        {
            tmpV.Clear(); tmpT.Clear();
            LatheInto(tmpV, tmpT, profile, seg, n, ax, az);
            Append(m, xf);
        }

        /// <summary>Tube along an arc of radius R in the XY plane, from angle a0 to a1 (degrees).</summary>
        public void Torus(Material m, float R, float r, float a0, float a1, Matrix4x4 xf, int seg = 20, int tube = 10, bool closed = false)
        {
            tmpV.Clear(); tmpT.Clear();
            int rings = closed ? seg : seg + 1;
            for (int i = 0; i < rings; i++)
            {
                float phi = Mathf.Lerp(a0, a1, (float)i / seg) * Mathf.Deg2Rad;
                var nrm = new Vector3(Mathf.Cos(phi), Mathf.Sin(phi), 0f);
                for (int j = 0; j < tube; j++)
                {
                    float psi = j * Mathf.PI * 2f / tube;
                    tmpV.Add(nrm * R + (Mathf.Cos(psi) * nrm + Mathf.Sin(psi) * Vector3.forward) * r);
                }
            }
            for (int i = 0; i < seg; i++)
            {
                int i1 = closed ? (i + 1) % seg : i + 1;
                for (int j = 0; j < tube; j++)
                {
                    int a = i * tube + j, b = i1 * tube + j, c = i1 * tube + (j + 1) % tube, d = i * tube + (j + 1) % tube;
                    tmpT.Add(a); tmpT.Add(b); tmpT.Add(c);
                    tmpT.Add(a); tmpT.Add(c); tmpT.Add(d);
                }
            }
            Append(m, xf);
            if (closed) return;
            // round end caps
            float rs = r * ScaleOf(xf);
            Sphere(m, xf.MultiplyPoint3x4(new Vector3(Mathf.Cos(a0 * Mathf.Deg2Rad), Mathf.Sin(a0 * Mathf.Deg2Rad)) * R), Vector3.one * rs, Quaternion.identity, 8);
            Sphere(m, xf.MultiplyPoint3x4(new Vector3(Mathf.Cos(a1 * Mathf.Deg2Rad), Mathf.Sin(a1 * Mathf.Deg2Rad)) * R), Vector3.one * rs, Quaternion.identity, 8);
        }

        static float ScaleOf(Matrix4x4 xf) => xf.MultiplyVector(Vector3.right).magnitude;

        public void Sphere(Material m, Vector3 center, Vector3 radii, Quaternion rot, int seg = 16)
        {
            int rings = Mathf.Max(4, seg / 2);
            var p = new Vector2[rings + 1];
            for (int i = 0; i <= rings; i++)
            {
                float a = Mathf.PI * i / rings;
                p[i] = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
            }
            tmpV.Clear(); tmpT.Clear();
            LatheInto(tmpV, tmpT, p, seg, 2f, 1f, 1f);
            Append(m, Matrix4x4.TRS(center, rot, radii));
        }

        public void Cylinder(Material m, float r, float h, Matrix4x4 xf, int seg = 18, float topR = -1f)
        {
            if (topR < 0) topR = r;
            Lathe(m, new[] { new Vector2(0, 0), new Vector2(r, 0), new Vector2(r, 0), new Vector2(topR, h), new Vector2(topR, h), new Vector2(0, h) }, xf, seg);
        }

        public void Cone(Material m, float r, float h, Matrix4x4 xf, int seg = 14)
        {
            Lathe(m, new[] { new Vector2(0, 0), new Vector2(r, 0), new Vector2(r, 0), new Vector2(0, h) }, xf, seg);
        }

        /// <summary>Box with slightly rounded vertical edges; pivot at the bottom centre.</summary>
        public void Box(Material m, Vector3 bottomCenter, Vector3 size, Quaternion rot, float roundness = 10f)
        {
            float h = size.y;
            var prof = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, h), new Vector2(1, h), new Vector2(0, h) };
            Lathe(m, prof, Matrix4x4.TRS(bottomCenter, rot, Vector3.one), 24, roundness, size.x * 0.5f, size.z * 0.5f);
        }

        // ------------------------------------------------------------------ assembly

        void Append(Material m, Matrix4x4 xf)
        {
            if (!parts.TryGetValue(m, out var part))
            {
                part = new Part();
                parts[m] = part;
                order.Add(m);
            }
            // normals from the shape in its final pose (handles non-uniform scale)
            var temp = new Mesh();
            var world = new Vector3[tmpV.Count];
            for (int i = 0; i < world.Length; i++) world[i] = xf.MultiplyPoint3x4(tmpV[i]);
            bool flip = xf.determinant < 0f;
            temp.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            temp.vertices = world;
            var tris = tmpT.ToArray();
            if (flip) for (int i = 0; i < tris.Length; i += 3) { int x = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = x; }
            temp.triangles = tris;
            temp.RecalculateNormals();
            var normals = temp.normals;
            Object.DestroyImmediate(temp);

            int baseIndex = part.v.Count;
            part.v.AddRange(world);
            part.n.AddRange(normals);
            foreach (var t in tris) part.t.Add(baseIndex + t);
        }

        public bool IsEmpty => order.Count == 0;

        public Mesh Build(string name, out Material[] materials)
        {
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            foreach (var m in order) { v.AddRange(parts[m].v); n.AddRange(parts[m].n); }
            if (v.Count < 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.subMeshCount = order.Count;
            int offset = 0;
            for (int i = 0; i < order.Count; i++)
            {
                var p = parts[order[i]];
                var tris = new int[p.t.Count];
                for (int k = 0; k < tris.Length; k++) tris[k] = p.t[k] + offset;
                mesh.SetTriangles(tris, i);
                offset += p.v.Count;
            }
            mesh.RecalculateBounds();
            materials = order.ToArray();
            return mesh;
        }

        public GameObject Bake(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mesh = Build(name, out var mats);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return go;
        }

        // ------------------------------------------------------------------ helpers

        public static Matrix4x4 At(Vector3 pos) => Matrix4x4.Translate(pos);
        public static Matrix4x4 At(Vector3 pos, Vector3 euler) => Matrix4x4.TRS(pos, Quaternion.Euler(euler), Vector3.one);
        public static Matrix4x4 At(Vector3 pos, Vector3 euler, Vector3 scale) => Matrix4x4.TRS(pos, Quaternion.Euler(euler), scale);

        /// <summary>Hollow vessel from an outer wall (bottom edge to rim) with a wall thickness.</summary>
        public static List<Vector2> Vessel(IList<Vector2> outer, float thickness)
        {
            var p = new List<Vector2> { new Vector2(0, 0), outer[0], outer[0] };
            for (int i = 0; i < outer.Count; i++) p.Add(outer[i]);
            var top = outer[outer.Count - 1];
            p.Add(top);
            var innerTop = new Vector2(Mathf.Max(0.001f, top.x - thickness), top.y);
            p.Add(innerTop);
            p.Add(innerTop);
            foreach (var q in Inner(outer, thickness, false)) p.Add(q);
            var last = p[p.Count - 1];
            p.Add(last);
            p.Add(new Vector2(0, last.y));
            return p;
        }

        /// <summary>Inner wall points from rim down to the inner floor (or floor up to rim when upward).</summary>
        public static List<Vector2> Inner(IList<Vector2> outer, float thickness, bool upward)
        {
            var list = new List<Vector2>();
            for (int i = outer.Count - 1; i >= 0; i--)
            {
                var o = outer[i];
                var q = new Vector2(Mathf.Max(0.001f, o.x - thickness), Mathf.Max(thickness, o.y));
                if (list.Count > 0 && Mathf.Approximately(list[list.Count - 1].y, thickness) && Mathf.Approximately(q.y, thickness))
                    continue; // points clamped onto the floor add nothing
                list.Add(q);
            }
            if (upward) list.Reverse();
            return list;
        }
    }
}
