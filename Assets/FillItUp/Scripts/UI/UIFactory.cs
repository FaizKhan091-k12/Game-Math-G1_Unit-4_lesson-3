using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.UI.ProceduralImage;

namespace FillItUp
{
    /// <summary>
    /// Code-built UI kit: rounded Procedural Image panels, chunky "toy" buttons with a lip, TMP labels.
    /// Works in edit mode so the builder can bake every screen into the scene.
    /// </summary>
    public static class UIFactory
    {
        public static readonly Vector2 Reference = new Vector2(1920, 1080);
        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        public static Canvas Canvas(Transform parent, string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            c.pixelPerfect = false;
            var s = go.AddComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = Reference;
            // Expand: the whole 1920x1080 layout always fits (4:3 tablets, phones in portrait, ultra-wide);
            // extra space goes to the edges, where corner-anchored buttons follow it.
            s.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();
            try { module.AssignDefaultActions(); }
            catch (System.Exception e) { Debug.LogWarning("[FillItUp] UI input defaults: " + e.Message); }
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? Center;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(Transform parent, string name, float inset = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        /// <summary>Rounded rectangle; radius &lt; 0 makes a pill / circle.</summary>
        public static ProceduralImage Rounded(RectTransform rt, Color color, float radius, float border = 0f)
        {
            ProceduralImageModifier mod;
            if (radius < 0) mod = rt.gameObject.AddComponent<RoundModifier>();
            else mod = rt.gameObject.AddComponent<UniformModifier>();
            var img = rt.gameObject.AddComponent<ProceduralImage>();
            img.color = color;
            img.BorderWidth = border;
            img.FalloffDistance = 1.5f;
            if (mod is UniformModifier um) um.Radius = radius;
            return img;
        }

        /// <summary>Panel = root (position it) with a drop shadow and a face. Put content under the returned face.</summary>
        public static ProceduralImage Panel(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color, float radius, bool shadow = true)
        {
            var rt = Rect(parent, name, anchor, pos, size);
            if (shadow)
            {
                var sh = Stretch(rt, "Shadow");
                sh.offsetMin = new Vector2(0, -12); sh.offsetMax = new Vector2(0, -12);
                Rounded(sh, Palette.UiShadow, radius).raycastTarget = false;
            }
            var face = Stretch(rt, "Face");
            return Rounded(face, color, radius);
        }

        public static TextMeshProUGUI Label(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, bool outline = false)
        {
            var rt = Stretch(parent, name);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            var cfg = GameConfig.Load();
            if (cfg != null && cfg.font != null)
            {
                t.font = cfg.font;
                if (outline && cfg.fontOutline != null) t.fontSharedMaterial = cfg.fontOutline;
            }
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.richText = true;
            return t;
        }

        public static Image Icon(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 pos, float size, Color? color = null)
        {
            var rt = Rect(parent, name, anchor, pos, new Vector2(size, size));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.color = color ?? Color.white;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Chunky toy button: darker lip under a bright face, shine band, bouncy DOTween feedback.</summary>
        public static Button Button(Transform parent, string name, string text, Vector2 anchor, Vector2 pos, Vector2 size,
            Color face, Color lip, float fontSize = 64f, Sprite icon = null)
        {
            var rt = Rect(parent, name, anchor, pos, size);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            float radius = Mathf.Min(size.x, size.y) * 0.34f;
            var lipRt = Stretch(rt, "Lip");
            lipRt.offsetMin = new Vector2(0, -12); lipRt.offsetMax = new Vector2(0, -12);
            Rounded(lipRt, lip, radius).raycastTarget = false;
            var faceRt = Stretch(rt, "Face");
            Rounded(faceRt, face, radius).raycastTarget = false;
            float shineH = Mathf.Min(size.y * 0.26f, 40f);
            var shine = Rect(faceRt, "Shine", new Vector2(0.5f, 1f), new Vector2(0, -8), new Vector2(size.x - Mathf.Max(34f, radius * 1.2f), shineH), new Vector2(0.5f, 1f));
            Rounded(shine, new Color(1, 1, 1, 0.25f), shineH * 0.5f).raycastTarget = false;

            float textLeft = 14f;
            if (icon != null)
            {
                float s = size.y * 0.62f;
                var ic = Icon(faceRt, "Icon", icon, new Vector2(0, 0.5f), new Vector2(size.y * 0.5f + 6f, 2f), s);
                textLeft = size.y * 0.85f;
                if (string.IsNullOrEmpty(text))
                {
                    ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = Center;
                    ic.rectTransform.anchoredPosition = new Vector2(0, 2);
                }
            }
            if (!string.IsNullOrEmpty(text))
            {
                var label = Label(faceRt, "Label", text, fontSize, Color.white, TextAlignmentOptions.Center, true);
                label.rectTransform.offsetMin = new Vector2(textLeft, 6);
                label.rectTransform.offsetMax = new Vector2(-14, 0);
            }
            var b = rt.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.targetGraphic = hit;
            var fx = rt.gameObject.AddComponent<UIButtonFX>();
            fx.face = faceRt;
            return b;
        }

        /// <summary>Round icon-only button.</summary>
        public static Button RoundButton(Transform parent, string name, Sprite icon, Vector2 anchor, Vector2 pos, float diameter, Color face, Color lip)
        {
            var b = Button(parent, name, null, anchor, pos, new Vector2(diameter, diameter), face, lip, 40, icon);
            foreach (var p in b.GetComponentsInChildren<ProceduralImage>(true))
            {
                if (p.name == "Shine") { p.gameObject.SetActive(false); continue; }
                var um = p.GetComponent<UniformModifier>();
                if (um != null) um.Radius = diameter * 0.5f;
            }
            return b;
        }

        public static Image Star(Transform parent, string name, Vector2 anchor, Vector2 pos, float size, bool lit)
        {
            var cfg = GameConfig.Load();
            var img = Icon(parent, name, cfg != null ? cfg.star : null, anchor, pos, size);
            img.color = lit ? Palette.Gold : new Color(0.12f, 0.2f, 0.35f, 0.2f);
            return img;
        }

        public static Image Dim(Transform parent, float alpha)
        {
            var rt = Stretch(parent, "Dim");
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.05f, 0.12f, 0.25f, alpha);
            return img;
        }

        /// <summary>
        /// Edovu logo in the bottom-right corner: the crisp pre-scaled sprite on a soft white card so the small
        /// tagline stays readable over any background. Falls back to the original prefab if the sprite is missing.
        /// </summary>
        public static RectTransform EdovuLogo(Transform parent, GameObject prefab, Sprite crisp)
        {
            const float Size = 150f, Pad = 8f;
            if (crisp != null)
            {
                var card = Rect(parent, "EdovuLogo", new Vector2(1f, 0f), new Vector2(-12f, 12f), new Vector2(Size + Pad * 2, Size + Pad * 2), new Vector2(1f, 0f));
                var sh = Stretch(card, "Shadow");
                sh.offsetMin = new Vector2(0, -5); sh.offsetMax = new Vector2(0, -5);
                Rounded(sh, new Color(0.05f, 0.15f, 0.3f, 0.18f), 26).raycastTarget = false;
                Rounded(Stretch(card, "Card"), new Color(1f, 1f, 1f, 0.94f), 26).raycastTarget = false;
                var img = Icon(card, "Logo", crisp, new Vector2(0.5f, 0.5f), Vector2.zero, Size);
                img.preserveAspect = true;
                return card;
            }
            if (prefab == null) return null;
            var rt = (RectTransform)Object.Instantiate(prefab, parent, false).transform;
            rt.name = "EdovuLogo";
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-10f, 10f);
            foreach (var g in rt.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            return rt;
        }

        public static string FormatTime(float seconds)
        {
            int s = Mathf.FloorToInt(seconds);
            if (s < 60) return s + "s";
            if (s < 3600) return (s / 60) + "m " + (s % 60) + "s";
            return (s / 3600) + "h " + (s / 60 % 60) + "m";
        }
    }
}
