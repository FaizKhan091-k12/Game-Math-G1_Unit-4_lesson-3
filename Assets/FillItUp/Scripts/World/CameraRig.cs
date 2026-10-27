using DG.Tweening;
using UnityEngine;

namespace FillItUp
{
    public enum Shot { Menu, Map, Play, Results }

    /// <summary>Glides between a few framed shots, with a gentle idle drift and a soft shake.</summary>
    public class CameraRig : MonoBehaviour
    {
        [System.Serializable]
        public struct ShotDef
        {
            public Vector3 position;
            public Vector3 lookAt;
            public float halfWidth; // world half-width that must stay visible at the look point
        }

        public Camera cam;
        public ShotDef menu = new ShotDef { position = new Vector3(0, 2.1f, -7.2f), lookAt = new Vector3(0, 1.0f, 0), halfWidth = 4.2f };
        public ShotDef map = new ShotDef { position = new Vector3(0, 3.4f, -8.5f), lookAt = new Vector3(0, 1.3f, 1f), halfWidth = 5f };
        public ShotDef play = new ShotDef { position = new Vector3(0, 2.35f, -6.9f), lookAt = new Vector3(0, 0.95f, 0.2f), halfWidth = 4.35f };
        public ShotDef results = new ShotDef { position = new Vector3(0, 1.9f, -6.6f), lookAt = new Vector3(0, 1.0f, 0), halfWidth = 3.8f };

        Vector3 basePos, baseLook;
        float halfWidth = 5f;
        Vector3 shake;
        Tween moveTween;

        void Awake()
        {
            if (cam == null) cam = GetComponent<Camera>();
            Snap(Shot.Menu);
        }

        ShotDef Get(Shot s) => s == Shot.Menu ? menu : s == Shot.Map ? map : s == Shot.Play ? play : results;

        public void Snap(Shot s)
        {
            var d = Get(s);
            basePos = d.position;
            baseLook = d.lookAt;
            halfWidth = d.halfWidth;
            Apply();
        }

        public Tween MoveTo(Shot s, float duration = 0.9f)
        {
            var d = Get(s);
            moveTween?.Kill();
            Vector3 p0 = basePos, l0 = baseLook;
            float w0 = halfWidth;
            moveTween = DOTween.To(() => 0f, t =>
            {
                basePos = Vector3.LerpUnclamped(p0, d.position, t);
                baseLook = Vector3.LerpUnclamped(l0, d.lookAt, t);
                halfWidth = Mathf.LerpUnclamped(w0, d.halfWidth, t);
            }, 1f, duration).SetEase(Ease.InOutCubic).SetLink(gameObject);
            return moveTween;
        }

        public void Shake(float strength = 0.08f)
        {
            DOTween.Kill(this);
            DOTween.Shake(() => shake, v => shake = v, 0.35f, strength, 18, 90f, true, true).SetTarget(this)
                .OnComplete(() => shake = Vector3.zero);
        }

        void LateUpdate() => Apply();

        /// <summary>
        /// Keeps the shot's width on screen at any aspect ratio. Wide screens just show more scenery; narrow
        /// ones (4:3 tablets, phones in portrait) first widen the field of view up to MaxFov, then pull the
        /// camera back along its view line instead of distorting further.
        /// </summary>
        void Apply()
        {
            const float MinFov = 30f, MaxFov = 62f;
            float t = Time.time;
            var drift = new Vector3(Mathf.Sin(t * 0.31f) * 0.06f, Mathf.Sin(t * 0.43f) * 0.04f, 0f);
            var dir = (baseLook - basePos).normalized;
            float dist = Vector3.Distance(basePos, baseLook);
            float aspect = cam != null ? Mathf.Max(0.3f, cam.aspect) : 16f / 9f;

            float hFov = 2f * Mathf.Atan(halfWidth / dist);
            float vFov = 2f * Mathf.Atan(Mathf.Tan(hFov * 0.5f) / aspect) * Mathf.Rad2Deg;
            float fov = Mathf.Clamp(vFov, MinFov, MaxFov);
            float tanHalfH = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * aspect;
            float needed = halfWidth / tanHalfH;
            var pos = needed > dist ? baseLook - dir * needed : basePos;

            transform.position = pos + drift + shake;
            transform.rotation = Quaternion.LookRotation(dir);
            if (cam != null) cam.fieldOfView = fov;
        }
    }
}
