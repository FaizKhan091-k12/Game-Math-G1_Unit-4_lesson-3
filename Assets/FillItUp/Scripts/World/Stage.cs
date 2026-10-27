using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace FillItUp
{
    /// <summary>
    /// The play area: pedestals, pooled containers, the "magic water" fill used to prove an answer,
    /// and the tilt-and-pour scene for level 3.
    /// </summary>
    public class Stage : MonoBehaviour
    {
        public Pedestal[] pedestals = new Pedestal[3];
        public Transform pourStand;     // tall stand for the pouring container (top at standHeight)
        public float standHeight = 1.0f;
        public WaterStream stream;
        public Transform puddle;
        public Transform containersRoot;

        static readonly float[] Two = { -1.75f, 1.75f };
        static readonly float[] Three = { -2.35f, 0f, 2.35f };
        const float PourSourceX = -1.55f, PourTargetX = 1.45f;

        readonly Dictionary<ContainerKind, Stack<ContainerView>> pool = new Dictionary<ContainerKind, Stack<ContainerView>>();
        readonly List<ContainerView> active = new List<ContainerView>();
        GameConfig cfg;

        public IReadOnlyList<ContainerView> Active => active;

        void Awake()
        {
            cfg = GameConfig.Load();
            foreach (var p in pedestals) p.transform.localScale = Vector3.zero;
            pourStand.gameObject.SetActive(false);
            if (puddle != null) puddle.localScale = Vector3.zero;
        }

        /// <summary>The pour stand grows out of the deck; when hidden it is switched off (a flattened stand still draws).</summary>
        void SetStand(bool on)
        {
            pourStand.DOKill();
            if (on)
            {
                pourStand.gameObject.SetActive(true);
                pourStand.localScale = new Vector3(1f, 0.02f, 1f);
                pourStand.DOScale(Vector3.one, 0.45f).SetEase(Ease.OutBack);
            }
            else if (pourStand.gameObject.activeSelf)
            {
                pourStand.DOScale(new Vector3(1f, 0.02f, 1f), 0.25f).OnComplete(() => pourStand.gameObject.SetActive(false));
            }
        }

        // ------------------------------------------------------------------ spawning

        ContainerView Get(ContainerKind kind)
        {
            if (pool.TryGetValue(kind, out var stack) && stack.Count > 0)
            {
                var v = stack.Pop();
                v.gameObject.SetActive(true);
                return v;
            }
            var prefab = cfg.ContainerPrefab(kind);
            var go = Instantiate(prefab, containersRoot);
            return go.GetComponent<ContainerView>();
        }

        void Release(ContainerView v)
        {
            v.Interactive = false;
            v.Clicked = null;
            v.Hovered = null;
            v.ResetPose();
            v.gameObject.SetActive(false);
            if (!pool.TryGetValue(v.kind, out var stack)) pool[v.kind] = stack = new Stack<ContainerView>();
            stack.Push(v);
        }

        /// <summary>Puts the question's containers on their pedestals with a bouncy entrance.</summary>
        public List<ContainerView> Show(Question q)
        {
            active.Clear();
            if (q.type == QuestionType.Pour) return ShowPour(q);

            float[] xs = q.options.Count == 2 ? Two : Three;
            var scales = RelativeScales(q.options);
            for (int i = 0; i < pedestals.Length; i++)
            {
                var p = pedestals[i];
                p.transform.DOKill();
                p.Container = null;
                if (i >= q.options.Count)
                {
                    p.transform.DOScale(0f, 0.2f);
                    continue;
                }
                var o = q.options[i];
                var v = Get(o.kind);
                float scale = scales[i];
                v.SetDisplayScale(scale);
                v.Capacity = o.Capacity;
                v.Slot = i;
                float r = Mathf.Max(0.55f, v.width * scale / 1.2f);
                p.transform.localPosition = new Vector3(xs[i], 0, 0);
                p.transform.DOScale(new Vector3(r, 1f, r), 0.35f).SetEase(Ease.OutBack).SetDelay(i * 0.08f);
                p.SetState(PedestalState.Idle);
                p.Container = v;
                v.transform.position = transform.TransformPoint(new Vector3(xs[i], p.height, 0));
                v.transform.rotation = transform.rotation;
                v.PopIn(0.15f + i * 0.12f);
                int idx = i;
                DOVirtual.DelayedCall(0.2f + i * 0.12f, () => AudioManager.I?.Play(Sfx.Boing, 0.9f + idx * 0.12f, 0.55f)).SetLink(gameObject);
                active.Add(v);
            }
            SetStand(false);
            return new List<ContainerView>(active);
        }

        List<ContainerView> ShowPour(Question q)
        {
            var src = Get(q.options[0].kind);
            var tgt = Get(q.options[1].kind);
            var scales = RelativeScales(q.options);
            src.SetDisplayScale(scales[0]);
            tgt.SetDisplayScale(scales[1]);
            src.Capacity = q.options[0].Capacity;
            tgt.Capacity = q.options[1].Capacity;

            for (int i = 0; i < pedestals.Length; i++)
            {
                pedestals[i].Container = null;
                pedestals[i].transform.DOKill();
                if (i != 1) pedestals[i].transform.DOScale(0f, 0.2f);
            }
            var tp = pedestals[1];
            float r = Mathf.Max(0.55f, tgt.width * tgt.DisplayScale / 1.2f);
            tp.transform.localPosition = new Vector3(PourTargetX, 0, 0);
            tp.transform.DOScale(new Vector3(r, 1f, r), 0.35f).SetEase(Ease.OutBack);
            tp.SetState(PedestalState.Idle);

            pourStand.localPosition = new Vector3(PourSourceX, 0, 0);
            SetStand(true);

            src.transform.position = transform.TransformPoint(new Vector3(PourSourceX, standHeight, 0));
            tgt.transform.position = transform.TransformPoint(new Vector3(PourTargetX, tp.height, 0));
            src.transform.rotation = tgt.transform.rotation = transform.rotation;
            src.PopIn(0.25f);
            tgt.PopIn(0.4f);
            src.SetFill(1f);
            DOVirtual.DelayedCall(0.3f, () => AudioManager.I?.Play(Sfx.Boing, 0.95f, 0.55f)).SetLink(gameObject);
            DOVirtual.DelayedCall(0.45f, () => AudioManager.I?.Play(Sfx.Boing, 1.1f, 0.55f)).SetLink(gameObject);
            active.Add(src);
            active.Add(tgt);
            return new List<ContainerView>(active);
        }

        /// <summary>
        /// On-screen sizes for one question. Every model is normalised to the same on-screen area, so the
        /// containers are sized against each other here: the one that holds the most is shown biggest and
        /// each one that holds less is at least 28% smaller than the next, so size never contradicts the answer.
        /// </summary>
        static float[] RelativeScales(List<Option> options)
        {
            const float Largest = 1.4f, Smallest = 0.38f;
            const float MinStep = 0.45f, MaxStep = 0.72f;
            var order = new List<int>();
            for (int i = 0; i < options.Count; i++) order.Add(i);
            order.Sort((a, b) => options[b].Capacity.CompareTo(options[a].Capacity));
            var scales = new float[options.Count];
            float s = Largest;
            scales[order[0]] = s;
            for (int k = 1; k < order.Count; k++)
            {
                float ratio = options[order[k]].Capacity / options[order[k - 1]].Capacity;
                s = Mathf.Max(Smallest, s * Mathf.Clamp(Mathf.Pow(ratio, 1f / 3f), MinStep, MaxStep));
                scales[order[k]] = s;
            }
            return scales;
        }

        public Pedestal PedestalOf(ContainerView v)
        {
            foreach (var p in pedestals) if (p.Container == v) return p;
            return null;
        }

        public IEnumerator Clear()
        {
            for (int i = 0; i < active.Count; i++) active[i].PopOut(i * 0.05f);
            foreach (var p in pedestals) { p.Container = null; p.SetState(PedestalState.Idle); }
            if (puddle != null) puddle.DOScale(0f, 0.3f);
            yield return new WaitForSeconds(0.32f);
            foreach (var v in active) Release(v);
            active.Clear();
        }

        public void HideAll()
        {
            foreach (var v in active) Release(v);
            active.Clear();
            foreach (var p in pedestals) { p.transform.DOKill(); p.transform.DOScale(0f, 0.25f); p.Container = null; }
            SetStand(false);
            if (puddle != null) puddle.DOScale(0f, 0.25f);
            stream.Stop();
        }

        // ------------------------------------------------------------------ water effects

        /// <summary>Magic water pours from the sky and fills a container to the brim.</summary>
        public IEnumerator MagicFill(ContainerView v, float duration = 0.9f)
        {
            AudioManager.I?.Play(Sfx.Fill);
            var top = v.SurfaceWorld() + Vector3.up * 7f;
            float t = 0f;
            v.DOFill(1f, duration);
            while (t < duration)
            {
                t += Time.deltaTime;
                stream.Set(new Vector3(v.SurfaceWorld().x, top.y, v.SurfaceWorld().z), v.SurfaceWorld(), Mathf.Clamp01(Mathf.Min(t, duration - t) * 8f) * 0.9f + 0.1f);
                yield return null;
            }
            stream.Stop();
            AudioManager.I?.Play(Sfx.Splash, 1.1f, 0.8f);
            FXPool.I.Play(Fx.Splash, v.SurfaceWorld(), 0.9f);
            v.Bounce();
        }

        /// <summary>
        /// Lifts the source, tips it over the target and pours. When the source holds more than the
        /// target, the target fills to the brim and the rest spills over the side.
        /// </summary>
        public IEnumerator Pour(ContainerView src, ContainerView tgt)
        {
            bool spills = src.Capacity > tgt.Capacity;
            Vector3 home = src.transform.position;
            Quaternion homeRot = src.transform.rotation;
            var rim = tgt.RimWorld();
            Vector3 pourPoint = rim + new Vector3(-0.08f, 0.45f + 0.15f * tgt.DisplayScale, -0.05f);

            // lift and swing over
            AudioManager.I?.Play(Sfx.Whoosh, 1f, 0.6f);
            var lift = DOTween.Sequence().SetLink(gameObject);
            lift.Append(src.transform.DOMove(home + Vector3.up * 0.9f, 0.4f).SetEase(Ease.OutQuad));
            lift.Join(src.transform.DORotate(new Vector3(0, 0, -20f), 0.4f));
            yield return lift.WaitForCompletion();

            // tip while keeping the lip on the pour point
            float tilt = 0f;
            var tip = DOTween.To(() => tilt, x => tilt = x, 1f, 0.6f).SetEase(Ease.InOutSine).SetLink(gameObject);
            Vector3 start = src.transform.position;
            while (tip.IsActive() && tip.IsPlaying())
            {
                src.transform.rotation = homeRot * Quaternion.Euler(0, 0, Mathf.Lerp(-20f, -112f, tilt));
                var aim = Vector3.Lerp(start, pourPoint, tilt);
                src.transform.position += aim - src.LipWorld();
                yield return null;
            }
            src.transform.rotation = homeRot * Quaternion.Euler(0, 0, -112f);
            src.transform.position += pourPoint - src.LipWorld();

            // pour
            AudioManager.I?.PlayLoop(Sfx.Pour, 0.9f);
            float total = 2.0f;
            float targetFull = spills ? 1f : Mathf.Max(0.15f, src.Capacity / tgt.Capacity);
            float fillTime = spills ? total * tgt.Capacity / src.Capacity : total;
            fillTime = Mathf.Max(fillTime, 0.6f);
            float t = 0f, overflowTimer = 0f;
            bool overflowStarted = false;
            while (t < total)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / total);
                src.SetFill(1f - k);
                tgt.SetFill(Mathf.Lerp(0f, targetFull, Mathf.Clamp01(t / fillTime)));
                float thick = Mathf.Clamp01(Mathf.Min(t * 6f, (total - t) * 4f));
                stream.Set(src.LipWorld(), tgt.SurfaceWorld(), 0.25f + thick * 0.75f);

                if (spills && t >= fillTime)
                {
                    if (!overflowStarted)
                    {
                        overflowStarted = true;
                        AudioManager.I?.Play(Sfx.Overflow);
                        if (puddle != null)
                        {
                            puddle.position = new Vector3(tgt.transform.position.x, transform.position.y + 0.01f, tgt.transform.position.z - 0.2f);
                            float size = Mathf.Max(1.6f, tgt.width * tgt.DisplayScale + 1f);
                            puddle.DOScale(new Vector3(size, 1f, size * 0.7f), 1.2f).SetEase(Ease.OutCubic);
                        }
                        tgt.Wobble(1.4f);
                    }
                    overflowTimer -= Time.deltaTime;
                    if (overflowTimer <= 0f)
                    {
                        overflowTimer = 0.13f;
                        float side = Random.value < 0.5f ? -1f : 1f;
                        var edge = tgt.RimWorld() + new Vector3(side * tgt.width * tgt.DisplayScale * 0.45f, 0, -0.1f);
                        FXPool.I.Play(Fx.Splash, edge, 0.7f);
                    }
                }
                yield return null;
            }
            stream.Stop();
            AudioManager.I?.StopLoop();
            if (!spills) { FXPool.I.Play(Fx.Bubbles, tgt.SurfaceWorld(), 1f); tgt.Bounce(); }

            // put the empty source back
            var back = DOTween.Sequence().SetLink(gameObject);
            back.Append(src.transform.DORotateQuaternion(homeRot, 0.4f).SetEase(Ease.OutBack));
            back.Join(src.transform.DOMove(home, 0.45f).SetEase(Ease.InOutQuad));
            yield return back.WaitForCompletion();
        }
    }
}
