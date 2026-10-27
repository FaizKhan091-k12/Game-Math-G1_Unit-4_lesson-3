using System.Runtime.InteropServices;
using UnityEngine;

namespace FillItUp
{
    /// <summary>
    /// Hands each finished level to the hosting web page (window.postMessage to the parent frame, plus a
    /// "fillitup-progress" DOM event), so an LMS wrapper can record it. Does nothing outside WebGL.
    /// </summary>
    public static class WebBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void FIU_Report(string json);
#endif

        [System.Serializable]
        class LevelReport
        {
            public string game = "FillItUp";
            public string eventName = "levelComplete";
            public int level, score, outOf, stars, seconds, totalStars, droplets, accuracyPercent;
            public bool champion;
        }

        public static void ReportLevel(int level, int score, int outOf, int stars, float seconds, SaveData data)
        {
            var r = new LevelReport
            {
                level = level, score = score, outOf = outOf, stars = stars, seconds = Mathf.RoundToInt(seconds),
                totalStars = data.TotalStars, droplets = data.droplets, accuracyPercent = SaveSystem.AccuracyPercent,
                champion = data.champion
            };
            string json = JsonUtility.ToJson(r);
#if UNITY_WEBGL && !UNITY_EDITOR
            try { FIU_Report(json); } catch (System.Exception e) { Debug.LogWarning("[FillItUp] report failed: " + e.Message); }
#else
            Debug.Log("[FillItUp] progress " + json);
#endif
        }
    }
}
