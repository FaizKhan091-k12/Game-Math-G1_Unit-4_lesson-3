using TMPro;
using UnityEngine;

namespace FillItUp
{
    /// <summary>Wavy, bouncing letters for titles (vertex animation, no extra objects).</summary>
    [RequireComponent(typeof(TMP_Text))]
    public class TextWobble : MonoBehaviour
    {
        public float amplitude = 10f;
        public float speed = 4f;
        public float wave = 0.55f;

        TMP_Text text;

        void Awake() => text = GetComponent<TMP_Text>();

        void LateUpdate()
        {
            text.ForceMeshUpdate();
            var info = text.textInfo;
            float t = Time.unscaledTime * speed;
            for (int i = 0; i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                if (!ch.isVisible) continue;
                var verts = info.meshInfo[ch.materialReferenceIndex].vertices;
                float y = Mathf.Sin(t + i * wave) * amplitude;
                float s = 1f + Mathf.Sin(t + i * wave + 1.2f) * 0.04f;
                var mid = (verts[ch.vertexIndex] + verts[ch.vertexIndex + 2]) * 0.5f;
                for (int v = 0; v < 4; v++)
                    verts[ch.vertexIndex + v] = mid + (verts[ch.vertexIndex + v] - mid) * s + new Vector3(0, y, 0);
            }
            for (int m = 0; m < info.meshInfo.Length; m++)
            {
                info.meshInfo[m].mesh.vertices = info.meshInfo[m].vertices;
                text.UpdateGeometry(info.meshInfo[m].mesh, m);
            }
        }
    }
}
