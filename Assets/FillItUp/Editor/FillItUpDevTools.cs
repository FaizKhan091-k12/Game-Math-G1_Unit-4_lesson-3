using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FillItUp.EditorTools
{
    /// <summary>Editor-only helpers for testing: set the Game view to an exact resolution (phone, tablet, desktop).</summary>
    public static class FillItUpDevTools
    {
        /// <summary>Selects (adding if needed) a fixed Game view resolution. Returns false when Unity's internals changed.</summary>
        public static bool SetGameViewSize(int width, int height)
        {
            try
            {
                var asm = typeof(Editor).Assembly;
                var sizesType = asm.GetType("UnityEditor.GameViewSizes");
                var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                var sizes = singleton.GetProperty("instance").GetValue(null);
                var groupType = (GameViewSizeGroupType)sizesType.GetProperty("currentGroupType").GetValue(sizes);
                var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new object[] { (int)groupType });
                var gType = group.GetType();
                string label = "FIU " + width + "x" + height;

                int index = -1;
                int total = (int)gType.GetMethod("GetTotalCount").Invoke(group, null);
                var getSize = gType.GetMethod("GetGameViewSize");
                for (int i = 0; i < total; i++)
                {
                    var s = getSize.Invoke(group, new object[] { i });
                    var st = s.GetType();
                    if ((int)st.GetProperty("width").GetValue(s) == width && (int)st.GetProperty("height").GetValue(s) == height &&
                        st.GetProperty("sizeType").GetValue(s).ToString() == "FixedResolution") { index = i; break; }
                }
                if (index < 0)
                {
                    var sizeType = asm.GetType("UnityEditor.GameViewSize");
                    var sizeEnum = asm.GetType("UnityEditor.GameViewSizeType");
                    var ctor = sizeType.GetConstructor(new[] { sizeEnum, typeof(int), typeof(int), typeof(string) });
                    var newSize = ctor.Invoke(new object[] { Enum.Parse(sizeEnum, "FixedResolution"), width, height, label });
                    gType.GetMethod("AddCustomSize").Invoke(group, new[] { newSize });
                    index = (int)gType.GetMethod("GetTotalCount").Invoke(group, null) - 1;
                }

                var gameViewType = asm.GetType("UnityEditor.GameView");
                foreach (var w in Resources.FindObjectsOfTypeAll(gameViewType))
                {
                    var prop = gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (prop != null) prop.SetValue(w, index);
                    else gameViewType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        ?.Invoke(w, new object[] { index, null });
                    ((EditorWindow)w).Repaint();
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[FillItUp] Could not set Game view size: " + e.Message);
                return false;
            }
        }
    }
}
