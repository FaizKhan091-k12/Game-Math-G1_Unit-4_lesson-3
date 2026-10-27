using UnityEngine;

namespace FillItUp
{
    /// <summary>Keeps its RectTransform inside Screen.safeArea (phone notches, rounded corners, home bars).</summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        Rect applied;
        Vector2Int size;

        void Awake() => Apply();

        void Update()
        {
            if (Screen.safeArea != applied || Screen.width != size.x || Screen.height != size.y) Apply();
        }

        void Apply()
        {
            applied = Screen.safeArea;
            size = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(applied.xMin / Screen.width, applied.yMin / Screen.height);
            rt.anchorMax = new Vector2(applied.xMax / Screen.width, applied.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
