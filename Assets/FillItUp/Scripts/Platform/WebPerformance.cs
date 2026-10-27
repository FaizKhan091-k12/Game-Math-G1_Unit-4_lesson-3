using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FillItUp
{
    /// <summary>
    /// Frame pacing and adaptive quality for the web player (created automatically in web builds).
    ///  - Ticks on the browser's requestAnimationFrame (vsync), capped at 60 fps by FrameCap.jslib,
    ///    so 100/120/144 Hz screens don't spend CPU and battery on extra frames.
    ///  - When frames are missed while the CPU still has headroom (weak integrated graphics, high-DPI
    ///    screens), MSAA is switched off and then the 3D render scale is lowered one step at a time.
    ///    The UI keeps full resolution. If a raised level drops frames again, it settles one lower.
    /// Add ?fps=1 to the page URL for a small stats overlay.
    /// </summary>
    [DefaultExecutionOrder(-32000)]
    public class WebPerformance : MonoBehaviour
    {
        struct Level
        {
            public int msaa;
            public float scale;
        }

        const int FrameCap = 60;
        const float WindowSeconds = 2f;
        const int IntervalSkipFrames = 30;
        const int IntervalSamples = 90;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void FIU_SetFrameCap(int fps);
#endif

        // Runs once per app start (not on scene reloads); the object survives reloads.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var go = new GameObject("WebPerformance");
            DontDestroyOnLoad(go);
            go.AddComponent<WebPerformance>();
#endif
        }

        UniversalRenderPipelineAsset urp;
        readonly List<Level> levels = new List<Level>();
        int level;
        int floor;          // best level we may still raise to
        bool justRaised;
        int goodWindows;

        // frame-interval detection
        readonly List<float> intervalSamples = new List<float>();
        int frameIndex;
        float targetInterval; // 0 until detected

        // current window
        double frameStart;
        float windowTime;
        int windowFrames, slowFrames;
        float cpuSum;

        // overlay
        bool overlay;
        string overlayText = "";
        float overlayTimer;
        int overlayFrames;
        float overlayCpu;
        float overlayWorst;
        GUIStyle overlayStyle;

        void Awake()
        {
            // -1 keeps Unity on requestAnimationFrame; a targetFrameRate would switch it to setTimeout.
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 1;
#if UNITY_WEBGL && !UNITY_EDITOR
            FIU_SetFrameCap(FrameCap);
#endif

            urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
            {
                AddLevel(urp.msaaSampleCount, 1f);
                AddLevel(1, 1f);
                AddLevel(1, 0.85f);
                AddLevel(1, 0.72f);
            }
            overlay = Application.absoluteURL.Contains("fps=1");
            StartCoroutine(EndOfFrameLoop());
        }

        void AddLevel(int msaa, float scale)
        {
            if (levels.Count > 0 && levels[levels.Count - 1].msaa == msaa && Mathf.Approximately(levels[levels.Count - 1].scale, scale)) return;
            levels.Add(new Level { msaa = msaa, scale = scale });
        }

        void Update()
        {
            frameStart = Time.realtimeSinceStartupAsDouble;
            float dt = Time.unscaledDeltaTime;
            frameIndex++;

            if (targetInterval <= 0f)
            {
                if (frameIndex > IntervalSkipFrames) intervalSamples.Add(dt);
                if (intervalSamples.Count >= IntervalSamples) DetectFrameInterval();
                return;
            }

            windowTime += dt;
            windowFrames++;
            if (dt > targetInterval * 1.4f) slowFrames++;
            if (windowTime >= WindowSeconds && windowFrames >= 30) EvaluateWindow();
        }

        /// <summary>
        /// The frame budget: 1/60 s, or one refresh on screens slower than 60 Hz. vsync-paced frames are never
        /// shorter than one refresh, so a low percentile of dt is the refresh interval.
        /// </summary>
        void DetectFrameInterval()
        {
            intervalSamples.Sort();
            float refresh = intervalSamples[intervalSamples.Count / 4];
            targetInterval = Mathf.Max(refresh, 1f / FrameCap);
            intervalSamples.Clear();
        }

        IEnumerator EndOfFrameLoop()
        {
            var wait = new WaitForEndOfFrame();
            while (true)
            {
                yield return wait;
                float cpu = (float)(Time.realtimeSinceStartupAsDouble - frameStart);
                if (targetInterval > 0f) cpuSum += cpu;
                if (overlay)
                {
                    overlayFrames++;
                    overlayCpu += cpu;
                }
            }
        }

        void EvaluateWindow()
        {
            float slowFraction = (float)slowFrames / windowFrames;
            float cpuAvg = cpuSum / windowFrames;
            windowTime = 0f;
            windowFrames = slowFrames = 0;
            cpuSum = 0f;
            if (levels.Count == 0) return;

            if (slowFraction > 0.2f)
            {
                goodWindows = 0;
                if (justRaised)
                {
                    // The better level could not hold 60 fps: go back and stay there.
                    floor = Mathf.Min(level + 1, levels.Count - 1);
                    SetLevel(level + 1);
                }
                // Lower resolution only helps when the GPU is the bottleneck; MSAA off is always worth a try.
                else if (level == 0 || cpuAvg < targetInterval * 0.75f)
                {
                    SetLevel(level + 1);
                }
                justRaised = false;
                return;
            }

            justRaised = false;
            if (slowFraction < 0.03f && level > floor && cpuAvg < targetInterval * 0.5f)
            {
                if (++goodWindows >= 4)
                {
                    goodWindows = 0;
                    SetLevel(level - 1);
                    justRaised = true;
                }
            }
            else
            {
                goodWindows = 0;
            }
        }

        void SetLevel(int index)
        {
            index = Mathf.Clamp(index, 0, levels.Count - 1);
            if (index == level) return;
            level = index;
            urp.msaaSampleCount = levels[level].msaa;
            urp.renderScale = levels[level].scale;
        }

        void LateUpdate()
        {
            if (!overlay) return;
            float dt = Time.unscaledDeltaTime;
            overlayTimer += dt;
            overlayWorst = Mathf.Max(overlayWorst, dt);
            if (overlayTimer < 0.5f || overlayFrames == 0) return;
            float fps = overlayFrames / overlayTimer;
            float cpuMs = overlayCpu / overlayFrames * 1000f;
            string q = levels.Count > 0 ? $"Q{level} MSAA {levels[level].msaa}x scale {levels[level].scale:0.00}" : "no URP";
            overlayText = $"{fps:0} FPS (cap {FrameCap})   worst {overlayWorst * 1000f:0} ms   CPU {cpuMs:0.0} ms   {q}";
            overlayTimer = 0f;
            overlayFrames = 0;
            overlayCpu = 0f;
            overlayWorst = 0f;
        }

        void OnGUI()
        {
            if (!overlay) return;
            if (overlayStyle == null)
            {
                overlayStyle = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
                overlayStyle.normal.textColor = Color.white;
            }
            // Bottom centre, clear of the HUD.
            const float w = 720f, h = 30f;
            GUI.Box(new Rect((Screen.width - w) * 0.5f, Screen.height - h - 6f, w, h), overlayText, overlayStyle);
        }
    }
}
