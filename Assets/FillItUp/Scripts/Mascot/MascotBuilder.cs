using System;
using UnityEngine;

namespace FillItUp
{
    /// <summary>Builds Splashy, the water-drop buddy, from procedural parts (used by the editor builder).</summary>
    public static class MascotBuilder
    {
        const float R = 0.42f;      // belly radius
        const float Equator = 0.46f;
        const float Tip = 1.16f;

        public static Mascot Build(Func<Mat, Material> m)
        {
            var root = new GameObject("Splashy");
            var mascot = root.AddComponent<Mascot>();
            var act = Child(root.transform, "Act", Vector3.zero);
            var breath = Child(act, "Breath", Vector3.zero);

            // body: round belly tapering to a drop tip
            var body = new MeshKit();
            var prof = new System.Collections.Generic.List<Vector2>();
            for (int i = 0; i <= 8; i++)
            {
                float a = Mathf.Lerp(-90f, 0f, i / 8f) * Mathf.Deg2Rad;
                prof.Add(new Vector2(R * Mathf.Cos(a), Equator + R * Mathf.Sin(a)));
            }
            for (int i = 1; i <= 10; i++)
            {
                float u = i / 10f;
                prof.Add(new Vector2(R * Mathf.Pow(1f - Mathf.Pow(u, 1.7f), 0.85f), Equator + u * (Tip - Equator)));
            }
            body.Lathe(m(Mat.MascotBody), prof, Matrix4x4.identity, 28);
            // glossy highlight
            body.Sphere(m(Mat.White), new Vector3(-0.2f, 0.78f, -0.25f), new Vector3(0.05f, 0.11f, 0.03f), Quaternion.Euler(0, -35f, 22f), 12);
            body.Sphere(m(Mat.White), new Vector3(-0.27f, 0.62f, -0.3f), new Vector3(0.025f, 0.025f, 0.015f), Quaternion.Euler(0, -40f, 0), 8);
            // swim ring (floatie) with stripes
            for (int i = 0; i < 8; i++)
                body.Torus(i % 2 == 0 ? m(Mat.Red) : m(Mat.White), R + 0.035f, 0.085f, i * 45f, i * 45f + 45f, MeshKit.At(new Vector3(0, 0.26f, 0), new Vector3(90, 0, 0)), 6, 10);
            // cheeks
            body.Sphere(m(Mat.Cheek), new Vector3(-0.25f, 0.42f, -0.33f), new Vector3(0.065f, 0.038f, 0.02f), Quaternion.Euler(0, 38f, 0), 12);
            body.Sphere(m(Mat.Cheek), new Vector3(0.25f, 0.42f, -0.33f), new Vector3(0.065f, 0.038f, 0.02f), Quaternion.Euler(0, -38f, 0), 12);
            body.Bake("BodyArt", breath);

            // eyes (blink = scale Y of the eye root, look = move the pupil)
            mascot.eyeL = Eye(breath, m, new Vector3(-0.15f, 0.58f, -0.345f), 18f, out mascot.pupilL);
            mascot.eyeR = Eye(breath, m, new Vector3(0.15f, 0.58f, -0.345f), -18f, out mascot.pupilR);

            // mouths
            var smile = new MeshKit();
            smile.Torus(m(Mat.Pupil), 0.075f, 0.019f, 200f, 340f, MeshKit.At(new Vector3(0, 0.46f, -0.405f)), 12, 6);
            mascot.mouthSmile = smile.Bake("MouthSmile", breath).transform;
            var open = new MeshKit();
            open.Sphere(m(Mat.Pupil), new Vector3(0, 0, 0), new Vector3(0.075f, 0.065f, 0.03f), Quaternion.identity, 14);
            open.Sphere(m(Mat.Cheek), new Vector3(0, -0.03f, -0.012f), new Vector3(0.045f, 0.025f, 0.022f), Quaternion.identity, 10);
            var openGo = open.Bake("MouthOpen", breath);
            openGo.transform.localPosition = new Vector3(0, 0.395f, -0.405f);
            mascot.mouthOpen = openGo.transform;
            var sad = new MeshKit();
            sad.Torus(m(Mat.Pupil), 0.06f, 0.018f, 25f, 155f, MeshKit.At(new Vector3(0, 0.34f, -0.41f)), 12, 6);
            mascot.mouthSad = sad.Bake("MouthSad", breath).transform;

            // arms (pivot at the shoulder, hanging down)
            mascot.armL = Arm(breath, m, new Vector3(-0.39f, 0.44f, -0.02f));
            mascot.armR = Arm(breath, m, new Vector3(0.39f, 0.44f, -0.02f));

            // feet (do not squash)
            var feet = new MeshKit();
            feet.Sphere(m(Mat.Blue), new Vector3(-0.15f, 0.045f, -0.06f), new Vector3(0.12f, 0.06f, 0.15f), Quaternion.identity, 12);
            feet.Sphere(m(Mat.Blue), new Vector3(0.15f, 0.045f, -0.06f), new Vector3(0.12f, 0.06f, 0.15f), Quaternion.identity, 12);
            mascot.feet = feet.Bake("Feet", act).transform;

            mascot.act = act;
            mascot.breath = breath;
            return mascot;
        }

        static Transform Child(Transform parent, string name, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        static Transform Eye(Transform parent, Func<Mat, Material> m, Vector3 pos, float yaw, out Transform pupil)
        {
            var eye = Child(parent, "Eye", pos);
            eye.localRotation = Quaternion.Euler(0, yaw, 0);
            var white = new MeshKit();
            white.Sphere(m(Mat.EyeWhite), Vector3.zero, new Vector3(0.11f, 0.13f, 0.06f), Quaternion.identity, 16);
            white.Bake("White", eye);
            var p = new MeshKit();
            p.Sphere(m(Mat.Pupil), Vector3.zero, new Vector3(0.065f, 0.075f, 0.03f), Quaternion.identity, 14);
            p.Sphere(m(Mat.White), new Vector3(0.025f, 0.03f, -0.028f), new Vector3(0.022f, 0.022f, 0.01f), Quaternion.identity, 8);
            p.Sphere(m(Mat.White), new Vector3(-0.022f, -0.025f, -0.028f), new Vector3(0.01f, 0.01f, 0.006f), Quaternion.identity, 6);
            pupil = p.Bake("Pupil", eye).transform;
            pupil.localPosition = new Vector3(0, -0.005f, -0.045f);
            return eye;
        }

        static Transform Arm(Transform parent, Func<Mat, Material> m, Vector3 shoulder)
        {
            var pivot = Child(parent, "Arm", shoulder);
            var k = new MeshKit();
            k.Sphere(m(Mat.MascotBody), new Vector3(0, -0.12f, 0), new Vector3(0.055f, 0.14f, 0.055f), Quaternion.identity, 12);
            k.Sphere(m(Mat.MascotBody), new Vector3(0, -0.27f, 0), new Vector3(0.075f, 0.07f, 0.075f), Quaternion.identity, 12);
            k.Bake("ArmArt", pivot);
            return pivot;
        }
    }
}
