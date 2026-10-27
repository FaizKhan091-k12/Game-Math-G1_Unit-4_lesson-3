using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FillItUp
{
    /// <summary>
    /// One container on the stage: tap/hover input, water that really fills its inner shape, and juicy
    /// pop/wobble/shake reactions. The water mesh is regenerated from the inner wall profile as the level
    /// changes, so bulgy shapes (fishbowl, vase) fill correctly.
    /// </summary>
    public class ContainerView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        const float MaxFill = 0.93f;

        public ContainerKind kind;
        public Transform model;      // wobble / squash target (pivot at the base)
        public Transform vessel;     // frame of the lathe profile
        public MeshFilter water;
        public Vector2[] inner;      // inner wall, floor to rim, in vessel space
        public float shapeN = 2f, shapeAx = 1f, shapeAz = 1f;
        public float height = 1f, width = 1f; // at display scale 1
        public Vector3 lipLocal;     // vessel-space rim point the water pours from

        public System.Action<ContainerView> Clicked;
        public System.Action<ContainerView, bool> Hovered;

        public bool Interactive { get; set; }
        public float Capacity { get; set; }
        public int Slot { get; set; }
        public float Fill { get; private set; }
        public float DisplayScale { get; private set; } = 1f;

        Mesh waterMesh;
        MeshRenderer waterRenderer;
        static readonly List<Vector3> verts = new List<Vector3>();
        static readonly List<int> tris = new List<int>();
        static readonly List<Vector2> prof = new List<Vector2>();

        void Awake()
        {
            if (water != null)
            {
                waterMesh = new Mesh { name = "Water" };
                waterMesh.MarkDynamic();
                water.sharedMesh = waterMesh;
                waterRenderer = water.GetComponent<MeshRenderer>();
            }
            SetFill(0f);
        }

        public float TopY => height * DisplayScale;

        public void SetDisplayScale(float s)
        {
            DisplayScale = s;
            transform.localScale = Vector3.one * s;
        }

        // ------------------------------------------------------------------ water

        public void SetFill(float f)
        {
            Fill = Mathf.Clamp01(f);
            if (waterMesh == null || inner == null || inner.Length < 2) return;
            bool visible = Fill > 0.005f;
            if (waterRenderer != null) waterRenderer.enabled = visible;
            if (!visible) return;

            float floor = inner[0].y, top = inner[inner.Length - 1].y;
            float level = Mathf.Lerp(floor, top, Fill * MaxFill);
            float r0 = RadiusAt(floor);
            prof.Clear();
            prof.Add(new Vector2(0, floor));
            prof.Add(new Vector2(r0 * 0.995f, floor));
            prof.Add(new Vector2(r0 * 0.995f, floor));
            for (int i = 0; i < inner.Length; i++)
                if (inner[i].y > floor + 0.0001f && inner[i].y < level) prof.Add(new Vector2(inner[i].x * 0.995f, inner[i].y));
            float rl = RadiusAt(level) * 0.995f;
            prof.Add(new Vector2(rl, level));
            prof.Add(new Vector2(rl, level));
            prof.Add(new Vector2(0, level));

            verts.Clear(); tris.Clear();
            MeshKit.LatheInto(verts, tris, prof, 24, shapeN, shapeAx, shapeAz);
            waterMesh.Clear();
            waterMesh.SetVertices(verts);
            waterMesh.SetTriangles(tris, 0);
            waterMesh.RecalculateNormals();
            waterMesh.RecalculateBounds();
        }

        float RadiusAt(float y)
        {
            if (y <= inner[0].y) return inner[0].x;
            for (int i = 1; i < inner.Length; i++)
                if (y <= inner[i].y)
                    return Mathf.Lerp(inner[i - 1].x, inner[i].x, Mathf.InverseLerp(inner[i - 1].y, inner[i].y, y));
            return inner[inner.Length - 1].x;
        }

        public Tween DOFill(float to, float duration) =>
            DOTween.To(() => Fill, SetFill, to, duration).SetEase(Ease.OutSine).SetLink(gameObject);

        /// <summary>World position of the water surface centre (for splash effects).</summary>
        public Vector3 SurfaceWorld()
        {
            float floor = inner[0].y, top = inner[inner.Length - 1].y;
            return vessel.TransformPoint(new Vector3(0, Mathf.Lerp(floor, top, Mathf.Max(Fill, 0.05f) * MaxFill), 0));
        }

        public Vector3 RimWorld() => vessel.TransformPoint(new Vector3(0, inner[inner.Length - 1].y, 0));
        public Vector3 LipWorld() => vessel.TransformPoint(lipLocal);

        // ------------------------------------------------------------------ juice

        public Tween PopIn(float delay)
        {
            model.DOKill();
            model.localScale = Vector3.zero;
            return model.DOScale(1f, 0.45f).SetEase(Ease.OutBack, 2.2f).SetDelay(delay).SetLink(gameObject);
        }

        public Tween PopOut(float delay)
        {
            model.DOKill();
            return model.DOScale(0f, 0.25f).SetEase(Ease.InBack).SetDelay(delay).SetLink(gameObject);
        }

        public void Wobble(float strength = 1f)
        {
            model.DOKill(true);
            model.localScale = Vector3.one;
            model.DOPunchScale(new Vector3(0.08f, -0.1f, 0.08f) * strength, 0.4f, 6, 0.6f).SetLink(gameObject);
            model.DOPunchRotation(new Vector3(0, 0, 6f) * strength, 0.45f, 7, 0.5f).SetLink(gameObject);
        }

        public void Shake()
        {
            model.DOKill(true);
            model.localScale = Vector3.one;
            model.DOShakeRotation(0.5f, new Vector3(0, 0, 14f), 18, 90f, true, ShakeRandomnessMode.Harmonic).SetLink(gameObject);
        }

        public void Bounce()
        {
            model.DOKill(true);
            model.localScale = Vector3.one;
            var s = DOTween.Sequence().SetLink(gameObject);
            s.Append(model.DOScale(new Vector3(1.15f, 0.82f, 1.15f), 0.1f));
            s.Append(model.DOScale(new Vector3(0.9f, 1.18f, 0.9f), 0.12f));
            s.Append(model.DOScale(1f, 0.35f).SetEase(Ease.OutElastic));
        }

        public void ResetPose()
        {
            model.DOKill();
            transform.DOKill();
            model.localScale = Vector3.one;
            model.localRotation = Quaternion.identity;
            transform.localRotation = Quaternion.identity;
            SetFill(0f);
        }

        // ------------------------------------------------------------------ input

        public void OnPointerClick(PointerEventData e)
        {
            if (Interactive) Clicked?.Invoke(this);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (Interactive) Hovered?.Invoke(this, true);
        }

        public void OnPointerExit(PointerEventData e)
        {
            Hovered?.Invoke(this, false);
        }
    }
}
