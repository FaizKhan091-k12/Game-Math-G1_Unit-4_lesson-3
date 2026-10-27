using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FillItUp
{
    public enum PedestalState { Idle, Hover, Right, Wrong, Off }

    /// <summary>A round stand a container sits on. Its top lights up, and taps on it count as taps on the container.</summary>
    public class Pedestal : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public Renderer top;
        public Transform glow;
        public float height = 0.25f;

        public ContainerView Container { get; set; }

        Material topMat;
        PedestalState state;

        void Awake()
        {
            if (top != null)
            {
                topMat = new Material(top.sharedMaterial);
                top.sharedMaterial = topMat;
                topMat.SetColor("_BaseColor", Palette.PedestalIdle);
            }
            if (glow != null) glow.localScale = Vector3.zero;
        }

        public Vector3 TopWorld => transform.TransformPoint(new Vector3(0, height, 0));

        public void SetState(PedestalState s)
        {
            state = s;
            Color c;
            switch (s)
            {
                case PedestalState.Hover: c = Palette.PedestalHover; break;
                case PedestalState.Right: c = Palette.PedestalRight; break;
                case PedestalState.Wrong: c = Palette.PedestalWrong; break;
                case PedestalState.Off: c = Palette.PedestalOff; break;
                default: c = Palette.PedestalIdle; break;
            }
            if (topMat != null)
            {
                DOTween.Kill(topMat);
                topMat.DOColor(c, "_BaseColor", 0.18f).SetTarget(topMat);
            }
            if (glow != null)
            {
                glow.DOKill();
                bool on = s == PedestalState.Hover || s == PedestalState.Right;
                glow.DOScale(on ? Vector3.one : Vector3.zero, 0.2f).SetEase(on ? Ease.OutBack : Ease.InQuad);
                var gr = glow.GetComponent<Renderer>();
                if (gr != null && on)
                {
                    var mpb = new MaterialPropertyBlock();
                    mpb.SetColor("_BaseColor", s == PedestalState.Right ? new Color(0.5f, 1f, 0.7f, 0.85f) : new Color(1f, 0.85f, 0.4f, 0.85f));
                    gr.SetPropertyBlock(mpb);
                }
            }
        }

        public void Punch()
        {
            transform.DOKill(true);
            transform.DOPunchScale(new Vector3(0.12f, -0.15f, 0.12f), 0.35f, 6, 0.6f);
        }

        public void OnPointerClick(PointerEventData e) { if (Container != null) Container.OnPointerClick(e); }
        public void OnPointerEnter(PointerEventData e) { if (Container != null) Container.OnPointerEnter(e); }
        public void OnPointerExit(PointerEventData e) { if (Container != null) Container.OnPointerExit(e); }

        void OnDestroy()
        {
            if (topMat != null) Destroy(topMat);
        }
    }
}
