using DG.Tweening;
using TMPro;
using UnityEngine;

namespace FillItUp
{
    /// <summary>Base for every screen: a CanvasGroup that pops in and out.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIScreen : MonoBehaviour
    {
        public RectTransform content; // the part that scales in (whole screen when null)

        CanvasGroup group;

        public bool IsVisible => gameObject.activeSelf && Group.alpha > 0.5f;

        CanvasGroup Group => group != null ? group : group = GetComponent<CanvasGroup>();

        public virtual void Show()
        {
            gameObject.SetActive(true);
            var g = Group;
            g.DOKill();
            g.alpha = 0f;
            g.interactable = g.blocksRaycasts = true;
            g.DOFade(1f, 0.25f).SetUpdate(true);
            var c = content != null ? content : (RectTransform)transform;
            c.DOKill();
            c.localScale = Vector3.one * 0.85f;
            c.DOScale(1f, 0.4f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        public virtual void Hide(bool instant = false)
        {
            var g = Group;
            g.DOKill();
            g.interactable = g.blocksRaycasts = false;
            if (instant || !gameObject.activeSelf)
            {
                g.alpha = 0f;
                gameObject.SetActive(false);
                return;
            }
            g.DOFade(0f, 0.2f).SetUpdate(true).OnComplete(() => gameObject.SetActive(false));
        }
    }
}
