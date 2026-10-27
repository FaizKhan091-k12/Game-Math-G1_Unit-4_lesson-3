using UnityEngine;

namespace FillItUp
{
    /// <summary>A wobbly column of water between two points (pours and "magic water" fills).</summary>
    public class WaterStream : MonoBehaviour
    {
        public Transform column; // unit cylinder, pivot at the top, extends down -Y by 1
        public float width = 0.09f;

        Vector3 from, to;
        float amount; // 0..1 thickness
        float splashTimer;

        public bool Active => amount > 0.01f;

        public void Set(Vector3 start, Vector3 end, float thickness)
        {
            from = start;
            to = end;
            amount = thickness;
        }

        public void Stop() => amount = 0f;

        void LateUpdate()
        {
            bool on = amount > 0.01f;
            if (column.gameObject.activeSelf != on) column.gameObject.SetActive(on);
            if (!on) return;
            var d = to - from;
            float len = d.magnitude;
            if (len < 0.001f) return;
            column.position = from;
            column.rotation = Quaternion.FromToRotation(Vector3.down, d / len);
            float w = width * amount * (1f + Mathf.Sin(Time.time * 37f) * 0.08f);
            column.localScale = new Vector3(w, len, w);

            splashTimer -= Time.deltaTime;
            if (splashTimer <= 0f && FXPool.I != null)
            {
                splashTimer = 0.12f;
                FXPool.I.Play(Fx.SmallSplash, to, 0.8f + amount * 0.4f);
            }
        }
    }
}
