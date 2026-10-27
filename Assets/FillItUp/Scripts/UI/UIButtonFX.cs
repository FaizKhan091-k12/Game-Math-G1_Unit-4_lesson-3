using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FillItUp
{
    /// <summary>DOTween feel for buttons: hover grow, press sink, click bounce + pop sound + a little burst.</summary>
    public class UIButtonFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public RectTransform face;
        public bool burst = true;
        public bool idlePulse;

        Vector3 baseScale = Vector3.one;
        Button button;
        bool hovered;
        Tween scaleTween, pulseTween;

        void Awake()
        {
            baseScale = transform.localScale;
            button = GetComponent<Button>();
        }

        void OnEnable()
        {
            if (idlePulse)
                pulseTween = face.DOScale(1.06f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true);
        }

        bool Active => button == null || button.interactable;

        public void OnPointerEnter(PointerEventData e)
        {
            if (!Active) return;
            hovered = true;
            scaleTween?.Kill();
            scaleTween = transform.DOScale(baseScale * 1.07f, 0.18f).SetEase(Ease.OutBack).SetUpdate(true);
            if (e.pointerId < 0) AudioManager.I?.Play(Sfx.UiHover, 1f, 0.35f);
        }

        public void OnPointerExit(PointerEventData e)
        {
            hovered = false;
            scaleTween?.Kill();
            scaleTween = transform.DOScale(baseScale, 0.18f).SetUpdate(true);
            if (face != null) face.DOAnchorPos(Vector2.zero, 0.1f).SetUpdate(true);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (!Active || face == null) return;
            face.DOKill(); pulseTween = null;
            face.localScale = Vector3.one;
            face.DOAnchorPos(new Vector2(0, -10f), 0.06f).SetUpdate(true);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (face == null) return;
            face.DOKill();
            face.DOAnchorPos(Vector2.zero, 0.14f).SetEase(Ease.OutBack).SetUpdate(true);
            if (idlePulse && isActiveAndEnabled)
                pulseTween = face.DOScale(1.06f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (!Active) return;
            AudioManager.I?.Play(Sfx.UiClick);
            scaleTween?.Kill();
            transform.localScale = baseScale * (hovered ? 1.07f : 1f);
            scaleTween = transform.DOPunchScale(Vector3.one * 0.12f, 0.3f, 8, 0.7f).SetUpdate(true);
            if (burst) UIBurst.Play(transform as RectTransform, Palette.Gold);
        }

        void OnDisable()
        {
            scaleTween?.Kill();
            transform.localScale = baseScale;
            if (face != null) { face.DOKill(); face.anchoredPosition = Vector2.zero; face.localScale = Vector3.one; }
        }
    }

    /// <summary>Tiny UI confetti made of Images; for button clicks, stars and droplets.</summary>
    public static class UIBurst
    {
        public static void Play(RectTransform at, Color color, int count = 8, float distance = 110f)
        {
            if (at == null) return;
            var canvas = at.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var root = canvas.rootCanvas.transform;
            var cfg = GameConfig.Load();
            for (int i = 0; i < count; i++)
            {
                var rt = UIFactory.Rect(root, "Burst", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24, 24));
                rt.position = at.position;
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = i % 2 == 0 ? cfg.star : cfg.softCircle;
                img.color = i % 3 == 0 ? Color.white : color;
                img.raycastTarget = false;
                float a = (360f / count * i + Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float d = distance * Random.Range(0.7f, 1.2f);
                var s = DOTween.Sequence().SetUpdate(true).SetLink(rt.gameObject);
                s.Append(rt.DOAnchorPos(rt.anchoredPosition + dir * d, 0.45f).SetEase(Ease.OutCubic));
                s.Join(rt.DOScale(0.2f, 0.45f).SetEase(Ease.InQuad));
                s.Join(img.DOFade(0f, 0.45f).SetEase(Ease.InQuad));
                s.OnComplete(() => Object.Destroy(rt.gameObject));
            }
        }
    }
}
