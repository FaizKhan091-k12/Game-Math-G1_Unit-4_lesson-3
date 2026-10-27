using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FillItUp
{
    /// <summary>
    /// Subtitles: shows what Splashy is saying in a pill at the bottom of the screen. It sizes itself to the
    /// line, wraps long lines, and can be lifted above bottom buttons (PLAY, fit/spill) per screen.
    /// Works with sound off too, so the game is playable muted.
    /// </summary>
    public class CaptionBar : MonoBehaviour
    {
        public RectTransform bar;
        public TMP_Text label;
        public CanvasGroup group;

        // MaxWidth keeps the caption clear of the logo card in the bottom-right corner
        const float MaxWidth = 1450f, IconRoom = 60f, Padding = 70f + IconRoom, MinHold = 1.2f;
        float hideAt = -1f;
        bool shown, suppressed;

        /// <summary>While the fit/spill buttons are up the question is already in the banner, so captions stay quiet.</summary>
        public void Suppress(bool on)
        {
            suppressed = on;
            if (on) Hide();
        }

        void Start()
        {
            group.alpha = 0f;
            bar.localScale = Vector3.zero;
            if (AudioManager.I != null)
            {
                AudioManager.I.Spoke += OnSpoke;
                AudioManager.I.Silenced += Hide;
            }
        }

        void OnDestroy()
        {
            if (AudioManager.I == null) return;
            AudioManager.I.Spoke -= OnSpoke;
            AudioManager.I.Silenced -= Hide;
        }

        /// <summary>Distance of the caption from the bottom edge (reference pixels).</summary>
        public void SetLift(float y)
        {
            bar.DOKill(false);
            bar.DOAnchorPosY(y, 0.25f).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        void OnSpoke(string key, float length)
        {
            var text = VoiceText.Get(key);
            if (suppressed || string.IsNullOrEmpty(text)) return;
            label.text = text;
            float w = Mathf.Min(MaxWidth, label.GetPreferredValues(text, MaxWidth - Padding, 0).x + Padding);
            label.rectTransform.sizeDelta = new Vector2(w - Padding, 0);
            bar.sizeDelta = new Vector2(w, Mathf.Max(96f, label.GetPreferredValues(text, w - Padding, 0).y + 34f));
            hideAt = Time.unscaledTime + Mathf.Max(MinHold, length + 0.35f);
            group.DOKill();
            group.DOFade(1f, 0.15f).SetUpdate(true);
            if (!shown) { bar.localScale = Vector3.one * 0.7f; }
            bar.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetUpdate(true);
            shown = true;
        }

        void Hide()
        {
            if (!shown) return;
            shown = false;
            hideAt = -1f;
            group.DOKill();
            group.DOFade(0f, 0.2f).SetUpdate(true);
            bar.DOScale(0.85f, 0.2f).SetUpdate(true);
        }

        void Update()
        {
            if (shown && hideAt > 0f && Time.unscaledTime > hideAt) Hide();
        }

        public static CaptionBar Build(Transform canvas)
        {
            var root = UIFactory.Stretch(canvas, "Captions");
            var c = root.gameObject.AddComponent<CaptionBar>();
            c.group = root.gameObject.AddComponent<CanvasGroup>();
            c.group.blocksRaycasts = false;
            c.group.interactable = false;
            c.bar = UIFactory.Rect(root, "Bar", new Vector2(0.5f, 0f), new Vector2(0, 24), new Vector2(900, 96), new Vector2(0.5f, 0f));
            UIFactory.Rounded(c.bar, Palette.Hex(0x16365C, 0.93f), 40).raycastTarget = false;
            UIFactory.Icon(c.bar, "Drop", GameConfig.Load().droplet, new Vector2(0f, 0.5f), new Vector2(50, 0), 56);
            c.label = UIFactory.Label(c.bar, "Text", "", 50, Color.white);
            c.label.textWrappingMode = TextWrappingModes.Normal;
            var lr = c.label.rectTransform;
            lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f);
            lr.pivot = new Vector2(0.5f, 0.5f);
            lr.anchoredPosition = new Vector2(IconRoom * 0.5f, 2);
            lr.sizeDelta = new Vector2(820, 0);
            c.label.verticalAlignment = VerticalAlignmentOptions.Middle;
            return c;
        }
    }
}
