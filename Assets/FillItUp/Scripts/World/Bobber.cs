using UnityEngine;

namespace FillItUp
{
    /// <summary>Cheap ambient motion: bob, sway, spin or drift (with wrap-around). One per animated prop.</summary>
    public class Bobber : MonoBehaviour
    {
        public float bobHeight = 0.05f;
        public float bobSpeed = 1.5f;
        public float swayDegrees;
        public float spinSpeed;
        public float driftSpeed;
        public float driftWrap = 14f;

        Vector3 basePos;
        Quaternion baseRot;
        float phase;

        void Awake()
        {
            basePos = transform.localPosition;
            baseRot = transform.localRotation;
            phase = Random.value * 10f;
        }

        void Update()
        {
            float t = Time.time + phase;
            var p = basePos;
            p.y += Mathf.Sin(t * bobSpeed) * bobHeight;
            if (driftSpeed != 0f)
            {
                basePos.x += driftSpeed * Time.deltaTime;
                if (basePos.x > driftWrap) basePos.x = -driftWrap;
                p.x = basePos.x;
            }
            transform.localPosition = p;
            if (swayDegrees != 0f || spinSpeed != 0f)
                transform.localRotation = baseRot * Quaternion.Euler(0, spinSpeed * Time.time, Mathf.Sin(t * bobSpeed * 0.7f) * swayDegrees);
        }
    }
}
