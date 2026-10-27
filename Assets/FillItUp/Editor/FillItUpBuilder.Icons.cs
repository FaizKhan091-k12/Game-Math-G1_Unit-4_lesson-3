using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FillItUp.EditorTools
{
    /// <summary>UI icons painted from signed distance shapes (anti-aliased, no art files needed).</summary>
    public static partial class FillItUpBuilder
    {
        class Painter
        {
            public readonly int size;
            public readonly Color[] px;

            public Painter(int size)
            {
                this.size = size;
                px = new Color[size * size];
            }

            /// <summary>Draws a shape given by a signed distance (in 0..1 icon units, negative inside).</summary>
            public void Fill(Func<Vector2, float> sdf, Color c, bool erase = false)
            {
                float aa = 1.2f / size;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        var p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                        float a = Mathf.Clamp01(0.5f - sdf(p) / aa);
                        if (a <= 0f) continue;
                        int i = y * size + x;
                        if (erase) { px[i].a *= 1f - a; continue; }
                        float sa = c.a * a;
                        float da = px[i].a * (1f - sa);
                        float oa = sa + da;
                        if (oa <= 0f) continue;
                        var rgb = (new Vector3(c.r, c.g, c.b) * sa + new Vector3(px[i].r, px[i].g, px[i].b) * da) / oa;
                        px[i] = new Color(rgb.x, rgb.y, rgb.z, oa);
                    }
            }
        }

        // ------------------------------------------------------------------ distance functions

        static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

        static float Box(Vector2 p, Vector2 c, Vector2 half, float r)
        {
            var d = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + Vector2.one * r;
            return new Vector2(Mathf.Max(d.x, 0), Mathf.Max(d.y, 0)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0) - r;
        }

        static float Segment(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            var pa = p - a; var ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - r;
        }

        static float Poly(Vector2 p, Vector2[] v)
        {
            float d = Vector2.Dot(p - v[0], p - v[0]);
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                var e = v[j] - v[i];
                var w = p - v[i];
                var b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }

        /// <summary>Ring arc between angles a0..a1 (degrees, counter-clockwise from +x).</summary>
        static float Arc(Vector2 p, Vector2 c, float r, float thick, float a0, float a1)
        {
            var d = p - c;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            float mid = (a0 + a1) * 0.5f, half = (a1 - a0) * 0.5f;
            float delta = Mathf.Abs(Mathf.DeltaAngle(ang, mid));
            if (delta <= half) return Mathf.Abs(d.magnitude - r) - thick;
            var e1 = c + new Vector2(Mathf.Cos((a0) * Mathf.Deg2Rad), Mathf.Sin(a0 * Mathf.Deg2Rad)) * r;
            var e2 = c + new Vector2(Mathf.Cos((a1) * Mathf.Deg2Rad), Mathf.Sin(a1 * Mathf.Deg2Rad)) * r;
            return Mathf.Min((p - e1).magnitude, (p - e2).magnitude) - thick;
        }

        static float Star(Vector2 p, Vector2 c, float outer, float inner)
        {
            var v = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = (90f + 36f * i) * Mathf.Deg2Rad;
                float r = i % 2 == 0 ? outer : inner;
                v[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }
            return Poly(p, v);
        }

        static float Drop(Vector2 p, Vector2 c, float r)
        {
            // round bottom + pointed top
            float circle = Circle(p, c, r);
            var tri = Poly(p, new[] { c + new Vector2(-r * 0.86f, r * 0.5f), c + new Vector2(0, r * 2.05f), c + new Vector2(r * 0.86f, r * 0.5f) });
            return Mathf.Min(circle, tri);
        }

        static Vector2[] Trapezoid(Vector2 bottom, float wb, float wt, float h) => new[]
        {
            bottom + new Vector2(-wb / 2, 0), bottom + new Vector2(-wt / 2, h), bottom + new Vector2(wt / 2, h), bottom + new Vector2(wb / 2, 0)
        };

        static Vector2 V(float x, float y) => new Vector2(x, y);

        /// <summary>A vessel icon: water filling to a level, then a thick outline.</summary>
        static void Vessel(Painter pt, Vector2 bottom, float wb, float wt, float h, float level, Color line, Color water, float thick)
        {
            var shape = Trapezoid(bottom, wb, wt, h);
            if (level > 0f)
            {
                float top = bottom.y + h * level;
                pt.Fill(p => Mathf.Max(Poly(p, shape), p.y - top), water);
            }
            pt.Fill(p => Mathf.Abs(Poly(p, shape)) - thick, line);
            pt.Fill(p => Box(p, new Vector2(bottom.x, bottom.y + h + 0.004f), new Vector2(wt / 2 + thick, 0.04f), 0f), Color.clear, true);
        }

        // ------------------------------------------------------------------ icon set

        static Sprite Icon(string name, int size, Action<Painter> paint)
        {
            var pt = new Painter(size);
            paint(pt);
            string path = TexDir + "/" + name + ".png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels(pt.px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.maxTextureSize = size;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void BuildIcons(GameConfig cfg)
        {
            var white = Color.white;
            var blue = Palette.Hex(0x3FB6FF);
            var deep = Palette.Hex(0x1C76C2);
            var gold = Palette.Gold;

            // soft circle has a gradient falloff, so it is painted directly
            {
                var pt = new Painter(128);
                for (int y = 0; y < 128; y++)
                    for (int x = 0; x < 128; x++)
                    {
                        float d = Vector2.Distance(new Vector2((x + 0.5f) / 128f, (y + 0.5f) / 128f), V(0.5f, 0.5f)) * 2f;
                        pt.px[y * 128 + x] = new Color(1, 1, 1, Mathf.Clamp01(1f - Mathf.SmoothStep(0.45f, 1f, d)));
                    }
                cfg.softCircle = Icon("SoftCircle", 128, q => Array.Copy(pt.px, q.px, pt.px.Length));
            }
            cfg.star = Icon("Star", 256, pt => pt.Fill(p => Star(p, V(0.5f, 0.47f), 0.47f, 0.21f) - 0.02f, white));
            cfg.droplet = Icon("Droplet", 256, pt =>
            {
                pt.Fill(p => Drop(p, V(0.5f, 0.36f), 0.3f), deep);
                pt.Fill(p => Drop(p, V(0.5f, 0.36f), 0.3f) + 0.035f, blue);
                pt.Fill(p => Segment(p, V(0.36f, 0.3f), V(0.38f, 0.42f), 0.04f), new Color(1, 1, 1, 0.85f));
            });
            Icon("DropWhite", 64, pt => pt.Fill(p => Drop(p, V(0.5f, 0.38f), 0.3f), white));

            cfg.play = Icon("Play", 128, pt => pt.Fill(p => Poly(p, new[] { V(0.3f, 0.2f), V(0.3f, 0.8f), V(0.82f, 0.5f) }) - 0.04f, white));
            cfg.pause = Icon("Pause", 128, pt =>
            {
                pt.Fill(p => Box(p, V(0.35f, 0.5f), V(0.09f, 0.28f), 0.05f), white);
                pt.Fill(p => Box(p, V(0.65f, 0.5f), V(0.09f, 0.28f), 0.05f), white);
            });
            cfg.home = Icon("Home", 128, pt =>
            {
                pt.Fill(p => Poly(p, new[] { V(0.12f, 0.52f), V(0.5f, 0.86f), V(0.88f, 0.52f) }) - 0.03f, white);
                pt.Fill(p => Box(p, V(0.5f, 0.36f), V(0.25f, 0.2f), 0.04f), white);
                pt.Fill(p => Box(p, V(0.5f, 0.27f), V(0.07f, 0.12f), 0.03f), Color.clear, true);
            });
            cfg.replay = Icon("Replay", 128, pt =>
            {
                pt.Fill(p => Arc(p, V(0.5f, 0.48f), 0.27f, 0.06f, 60f, 330f), white);
                pt.Fill(p => Poly(p, new[] { V(0.56f, 0.86f), V(0.78f, 0.66f), V(0.5f, 0.6f) }) - 0.02f, white);
            });
            cfg.next = Icon("Next", 128, pt =>
            {
                pt.Fill(p => Segment(p, V(0.2f, 0.5f), V(0.62f, 0.5f), 0.075f), white);
                pt.Fill(p => Poly(p, new[] { V(0.5f, 0.24f), V(0.5f, 0.76f), V(0.84f, 0.5f) }) - 0.03f, white);
            });
            cfg.back = Icon("Back", 128, pt =>
            {
                pt.Fill(p => Segment(p, V(0.38f, 0.5f), V(0.8f, 0.5f), 0.075f), white);
                pt.Fill(p => Poly(p, new[] { V(0.5f, 0.76f), V(0.5f, 0.24f), V(0.16f, 0.5f) }) - 0.03f, white);
            });
            Action<Painter> speaker = pt =>
            {
                pt.Fill(p => Box(p, V(0.24f, 0.5f), V(0.08f, 0.12f), 0.03f), white);
                pt.Fill(p => Poly(p, new[] { V(0.26f, 0.38f), V(0.26f, 0.62f), V(0.52f, 0.82f), V(0.52f, 0.18f) }) - 0.02f, white);
            };
            cfg.speakerOn = Icon("SpeakerOn", 128, pt =>
            {
                speaker(pt);
                pt.Fill(p => Arc(p, V(0.52f, 0.5f), 0.14f, 0.035f, -50f, 50f), white);
                pt.Fill(p => Arc(p, V(0.52f, 0.5f), 0.27f, 0.035f, -50f, 50f), white);
            });
            cfg.speakerOff = Icon("SpeakerOff", 128, pt =>
            {
                speaker(pt);
                pt.Fill(p => Segment(p, V(0.62f, 0.38f), V(0.84f, 0.62f), 0.04f), white);
                pt.Fill(p => Segment(p, V(0.62f, 0.62f), V(0.84f, 0.38f), 0.04f), white);
            });
            cfg.lockIcon = Icon("Lock", 128, pt =>
            {
                pt.Fill(p => Arc(p, V(0.5f, 0.56f), 0.17f, 0.05f, 0f, 180f), white);
                pt.Fill(p => Box(p, V(0.5f, 0.56f), V(0.17f + 0.05f, 0.02f), 0f), Color.clear, true);
                pt.Fill(p => Segment(p, V(0.33f, 0.5f), V(0.33f, 0.58f), 0.05f), white);
                pt.Fill(p => Segment(p, V(0.67f, 0.5f), V(0.67f, 0.58f), 0.05f), white);
                pt.Fill(p => Box(p, V(0.5f, 0.36f), V(0.27f, 0.2f), 0.06f), gold);
                pt.Fill(p => Circle(p, V(0.5f, 0.4f), 0.055f), Palette.GoldDark);
                pt.Fill(p => Segment(p, V(0.5f, 0.37f), V(0.5f, 0.26f), 0.025f), Palette.GoldDark);
            });
            cfg.check = Icon("Check", 128, pt =>
            {
                pt.Fill(p => Mathf.Min(Segment(p, V(0.2f, 0.52f), V(0.42f, 0.3f), 0.07f), Segment(p, V(0.42f, 0.3f), V(0.82f, 0.72f), 0.07f)), Palette.Green);
            });
            cfg.trophy = Icon("Trophy", 256, pt =>
            {
                var cup = new[] { V(0.24f, 0.84f), V(0.76f, 0.84f), V(0.7f, 0.56f), V(0.58f, 0.44f), V(0.42f, 0.44f), V(0.3f, 0.56f) };
                pt.Fill(p => Arc(p, V(0.24f, 0.68f), 0.11f, 0.03f, 90f, 270f), Palette.GoldDark);
                pt.Fill(p => Arc(p, V(0.76f, 0.68f), 0.11f, 0.03f, -90f, 90f), Palette.GoldDark);
                pt.Fill(p => Poly(p, cup) - 0.01f, gold);
                pt.Fill(p => Box(p, V(0.5f, 0.36f), V(0.05f, 0.08f), 0.01f), Palette.GoldDark);
                pt.Fill(p => Box(p, V(0.5f, 0.2f), V(0.2f, 0.07f), 0.03f), Palette.Hex(0x8A5A2B));
                pt.Fill(p => Star(p, V(0.5f, 0.66f), 0.11f, 0.05f), Color.white);
                pt.Fill(p => Segment(p, V(0.33f, 0.78f), V(0.36f, 0.62f), 0.022f), new Color(1, 1, 1, 0.6f));
            });
            cfg.chart = Icon("Chart", 128, pt =>
            {
                pt.Fill(p => Box(p, V(0.28f, 0.32f), V(0.08f, 0.14f), 0.03f), Palette.Orange);
                pt.Fill(p => Box(p, V(0.5f, 0.42f), V(0.08f, 0.24f), 0.03f), Palette.Pink);
                pt.Fill(p => Box(p, V(0.72f, 0.52f), V(0.08f, 0.34f), 0.03f), Palette.Purple);
            });
            cfg.close = Icon("Close", 128, pt =>
            {
                pt.Fill(p => Segment(p, V(0.28f, 0.28f), V(0.72f, 0.72f), 0.08f), white);
                pt.Fill(p => Segment(p, V(0.28f, 0.72f), V(0.72f, 0.28f), 0.08f), white);
            });
            cfg.flame = Icon("Flame", 128, pt =>
            {
                pt.Fill(p => Drop(p, V(0.5f, 0.36f), 0.27f), Palette.Hex(0xFF5A1F));
                pt.Fill(p => Drop(p, V(0.5f, 0.3f), 0.16f), Palette.Gold);
            });

            // answer icons for level 3
            cfg.fitsIcon = Icon("Fits", 256, pt =>
            {
                Vessel(pt, V(0.5f, 0.12f), 0.42f, 0.58f, 0.7f, 0.55f, white, Palette.Hex(0xBDEBFF), 0.035f);
                pt.Fill(p => Circle(p, V(0.76f, 0.24f), 0.16f), Palette.GreenLip);
                pt.Fill(p => Mathf.Min(Segment(p, V(0.69f, 0.25f), V(0.75f, 0.18f), 0.03f), Segment(p, V(0.75f, 0.18f), V(0.85f, 0.31f), 0.03f)), white);
            });
            cfg.spillsIcon = Icon("Spills", 256, pt =>
            {
                Vessel(pt, V(0.5f, 0.12f), 0.42f, 0.58f, 0.62f, 1f, white, Palette.Hex(0xBDEBFF), 0.035f);
                pt.Fill(p => Mathf.Min(Circle(p, V(0.5f, 0.76f), 0.13f), Circle(p, V(0.36f, 0.78f), 0.09f)), Palette.Hex(0xBDEBFF));
                pt.Fill(p => Drop(p, V(0.15f, 0.55f), 0.055f), Palette.Hex(0xBDEBFF));
                pt.Fill(p => Drop(p, V(0.86f, 0.62f), 0.06f), Palette.Hex(0xBDEBFF));
                pt.Fill(p => Drop(p, V(0.84f, 0.32f), 0.045f), Palette.Hex(0xBDEBFF));
                pt.Fill(p => Box(p, V(0.5f, 0.06f), V(0.42f, 0.035f), 0.035f), Palette.Hex(0xBDEBFF));
            });

            // level card icons
            cfg.level1Icon = Icon("Level1", 256, pt =>
            {
                Vessel(pt, V(0.25f, 0.18f), 0.2f, 0.26f, 0.3f, 0.6f, white, blue, 0.025f);
                Vessel(pt, V(0.66f, 0.18f), 0.36f, 0.48f, 0.62f, 0.6f, white, blue, 0.03f);
            });
            cfg.level2Icon = Icon("Level2", 256, pt =>
            {
                Vessel(pt, V(0.17f, 0.18f), 0.15f, 0.2f, 0.24f, 0.6f, white, blue, 0.022f);
                Vessel(pt, V(0.43f, 0.18f), 0.22f, 0.29f, 0.42f, 0.6f, white, blue, 0.026f);
                Vessel(pt, V(0.76f, 0.18f), 0.3f, 0.4f, 0.64f, 0.6f, white, blue, 0.03f);
            });
            cfg.level3Icon = Icon("Level3", 256, pt =>
            {
                var tilted = new[] { V(0.08f, 0.62f), V(0.3f, 0.88f), V(0.5f, 0.74f), V(0.3f, 0.5f) };
                pt.Fill(p => Poly(p, tilted), blue);
                pt.Fill(p => Mathf.Abs(Poly(p, tilted)) - 0.028f, white);
                pt.Fill(p => Segment(p, V(0.52f, 0.72f), V(0.6f, 0.46f), 0.03f), blue);
                Vessel(pt, V(0.64f, 0.1f), 0.3f, 0.4f, 0.4f, 0.45f, white, blue, 0.03f);
                pt.Fill(p => Star(p, V(0.86f, 0.82f), 0.08f, 0.035f), Palette.Gold);
            });
        }
    }
}
