using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FillItUp
{
    /// <summary>
    /// On a phone held upright the side-by-side containers get tiny, so this asks the child to turn the device
    /// sideways (an animated phone rotates). It hides by itself once the screen is landscape; "Play anyway"
    /// dismisses it for the session. Desktop and landscape screens never see it.
    /// </summary>
    public class RotatePrompt : MonoBehaviour
    {
        public CanvasGroup group;
        public RectTransform phone;
        public Button playAnyway;

        const float PortraitAspect = 0.9f;
        bool dismissed, visible;
        Tween spin;

        void Start()
        {
            playAnyway.onClick.AddListener(() => { dismissed = true; SetVisible(false); });
            group.alpha = 0f;
            group.blocksRaycasts = false;
            gameObject.SetActive(true);
        }

        void Update()
        {
            bool portrait = Screen.height > 0 && (float)Screen.width / Screen.height < PortraitAspect;
            SetVisible(portrait && !dismissed);
        }

        void SetVisible(bool on)
        {
            if (on == visible) return;
            visible = on;
            group.DOKill();
            group.DOFade(on ? 1f : 0f, 0.25f).SetUpdate(true);
            group.blocksRaycasts = on;
            group.interactable = on;
            spin?.Kill();
            if (!on) return;
            phone.localRotation = Quaternion.identity;
            spin = DOTween.Sequence().SetUpdate(true).SetLoops(-1)
                .AppendInterval(0.5f)
                .Append(phone.DOLocalRotate(new Vector3(0, 0, -90f), 0.6f).SetEase(Ease.InOutBack))
                .AppendInterval(0.9f)
                .Append(phone.DOLocalRotate(Vector3.zero, 0.4f).SetEase(Ease.InOutSine));
        }

        public static RotatePrompt Build(Transform canvas, GameConfig cfg)
        {
            var root = UIFactory.Stretch(canvas, "RotatePrompt");
            var p = root.gameObject.AddComponent<RotatePrompt>();
            p.group = root.gameObject.AddComponent<CanvasGroup>();
            var bg = root.gameObject.AddComponent<Image>();
            bg.color = Palette.Hex(0x36A9F7, 0.97f);

            // The canvas expands vertically in portrait, so content is centred and sized for the 1920-wide reference.
            var phoneRt = UIFactory.Rect(root, "Phone", new Vector2(0.5f, 0.5f), new Vector2(0, 380), new Vector2(380, 640));
            UIFactory.Rounded(phoneRt, Palette.Hex(0x1E3A5F), 70).raycastTarget = false;
            var screen = UIFactory.Stretch(phoneRt, "Screen", 26);
            UIFactory.Rounded(screen, Palette.SkyBottom, 48).raycastTarget = false;
            UIFactory.Icon(screen, "Drop", cfg.droplet, new Vector2(0.5f, 0.5f), Vector2.zero, 200);
            p.phone = phoneRt;

            var arrow = UIFactory.Icon(root, "Arrow", cfg.replay, new Vector2(0.5f, 0.5f), new Vector2(330, 650), 170);
            arrow.rectTransform.localScale = new Vector3(-1, 1, 1);

            var title = UIFactory.Rect(root, "Title", new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(1800, 200));
            var t = UIFactory.Label(title, "T", "Turn your device sideways!", 120, Color.white, TextAlignmentOptions.Center, true);
            t.textWrappingMode = TextWrappingModes.Normal;
            var sub = UIFactory.Rect(root, "Sub", new Vector2(0.5f, 0.5f), new Vector2(0, -220), new Vector2(1700, 100));
            UIFactory.Label(sub, "T", "Splashy needs more room to play.", 72, new Color(1, 1, 1, 0.92f));

            p.playAnyway = UIFactory.Button(root, "PlayAnyway", "Play anyway", new Vector2(0.5f, 0.5f), new Vector2(0, -460),
                new Vector2(640, 170), Palette.Orange, Palette.OrangeLip, 76);
            return p;
        }
    }
}
