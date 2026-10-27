using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FillItUp
{
    /// <summary>
    /// Splashy's animation set, all procedural (DOTween + a little maths), so there is no Animator cost:
    /// idle breathing and arm sway, blinking, eye tracking, lip-flap while talking, sleepy after a while,
    /// and one-shot actions: Wave, Hop, Cheer, Spin, Flip, Dance, Sad, ShakeHead, Think, Point,
    /// Surprised, DropIn and a random reaction when poked.
    /// </summary>
    public class Mascot : MonoBehaviour, IPointerClickHandler
    {
        enum Mood { Smile, Open, Sad }

        public Transform act, breath, eyeL, eyeR, pupilL, pupilR, mouthSmile, mouthOpen, mouthSad, armL, armR, feet;

        const float ArmRest = 22f;
        const float SleepAfter = 24f;

        public bool SleepAllowed { get; set; } = true;
        public System.Action<Vector3> Landed; // world position, for splash effects

        float armLA = -ArmRest, armRA = ArmRest;
        bool armsSwing = true;
        Mood mood = Mood.Smile;
        Sequence seq;
        float blinkTimer = 2f, idleTimer;
        bool sleepy, blinking;
        float eyeOpen = 1f, eyeScale = 1f;
        Vector3? lookTarget;
        Vector2 pupilOffset, lookUp;
        Camera cam;

        void Start()
        {
            cam = Camera.main;
        }

        // ------------------------------------------------------------------ per-frame procedural layer

        void Update()
        {
            float t = Time.time;
            float dt = Time.deltaTime;

            // breathing (slower when sleepy)
            float speed = sleepy ? 1.2f : 2.4f;
            float b = Mathf.Sin(t * speed);
            breath.localScale = new Vector3(1f - 0.018f * b, 1f + 0.03f * b, 1f - 0.018f * b);
            breath.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 1.3f) * (sleepy ? 4f : 2f));

            float swing = armsSwing ? Mathf.Sin(t * 2.1f) * 7f : 0f;
            armL.localRotation = Quaternion.Euler(0, 0, armLA - swing);
            armR.localRotation = Quaternion.Euler(0, 0, armRA - swing);

            // sleepy after a long idle
            idleTimer += dt;
            if (!sleepy && SleepAllowed && idleTimer > SleepAfter && (seq == null || !seq.IsActive()))
            {
                sleepy = true;
                AudioManager.I?.Play(Sfx.Yawn);
                AudioManager.I?.Speak("sleepy");
            }

            // blinking
            blinkTimer -= dt;
            if (blinkTimer <= 0f && !blinking)
            {
                blinkTimer = Random.Range(1.8f, 4.5f);
                blinking = true;
                DOTween.To(() => eyeOpen, v => eyeOpen = v, 0.08f, 0.07f).SetLoops(2, LoopType.Yoyo).SetLink(gameObject)
                    .OnComplete(() => { eyeOpen = 1f; blinking = false; });
            }
            float lid = sleepy ? 0.3f : eyeOpen;
            var es = new Vector3(eyeScale, eyeScale * lid, eyeScale);
            eyeL.localScale = es;
            eyeR.localScale = es;

            // eyes follow the look target (or the camera)
            Vector2 want = lookUp;
            if (lookTarget.HasValue)
            {
                var local = transform.InverseTransformPoint(lookTarget.Value) - new Vector3(0, 0.6f, 0);
                var d = new Vector2(local.x, local.y);
                if (d.sqrMagnitude > 0.0001f) want += Vector2.ClampMagnitude(d * 0.03f, 0.032f);
            }
            pupilOffset = Vector2.Lerp(pupilOffset, want, dt * 10f);
            pupilL.localPosition = new Vector3(pupilOffset.x, pupilOffset.y - 0.005f, -0.045f);
            pupilR.localPosition = new Vector3(pupilOffset.x, pupilOffset.y - 0.005f, -0.045f);

            // mouth: talking beats mood
            float voice = AudioManager.I != null ? AudioManager.I.VoiceLevel() : 0f;
            bool talking = voice > 0.02f;
            bool open = talking || mood == Mood.Open || sleepy && Mathf.Sin(t * 0.7f) > 0.95f;
            mouthOpen.gameObject.SetActive(open);
            mouthSmile.gameObject.SetActive(!open && mood == Mood.Smile);
            mouthSad.gameObject.SetActive(!open && mood == Mood.Sad);
            if (open) mouthOpen.localScale = talking ? new Vector3(0.9f + voice * 0.2f, 0.35f + voice * 0.85f, 1f) : Vector3.one;
        }

        // ------------------------------------------------------------------ helpers

        public void LookAt(Vector3? world) => lookTarget = world;
        public void LookAtCamera() => lookTarget = cam != null ? cam.transform.position : (Vector3?)null;

        void Wake()
        {
            idleTimer = 0f;
            if (sleepy) { sleepy = false; AudioManager.I?.StopVoice(); }
        }

        Sequence Begin()
        {
            Wake();
            seq?.Kill();
            act.DOKill();
            mood = Mood.Smile;
            lookUp = Vector2.zero;
            eyeScale = 1f;
            var s = DOTween.Sequence().SetLink(gameObject);
            // blend back to rest first, so actions can interrupt each other smoothly
            s.Append(act.DOLocalMove(Vector3.zero, 0.1f));
            s.Join(act.DOLocalRotateQuaternion(Quaternion.identity, 0.1f));
            s.Join(act.DOScale(1f, 0.1f));
            seq = s;
            return s;
        }

        Tween Arms(float l, float r, float dur, Ease ease = Ease.OutBack)
        {
            var s = DOTween.Sequence();
            s.Join(DOTween.To(() => armLA, v => armLA = v, l, dur).SetEase(ease));
            s.Join(DOTween.To(() => armRA, v => armRA = v, r, dur).SetEase(ease));
            return s;
        }

        void Rest(Sequence s, float dur = 0.25f)
        {
            s.Append(Arms(-ArmRest, ArmRest, dur, Ease.OutQuad));
            s.Join(act.DOLocalRotateQuaternion(Quaternion.identity, dur));
            s.AppendCallback(() => { armsSwing = true; mood = Mood.Smile; eyeScale = 1f; lookUp = Vector2.zero; });
        }

        void AppendHop(Sequence s, float height, float air, bool sound = true)
        {
            s.Append(act.DOScale(new Vector3(1.18f, 0.8f, 1.18f), 0.09f).SetEase(Ease.OutQuad));
            if (sound) s.AppendCallback(() => AudioManager.I?.Play(Sfx.Boing, Random.Range(0.95f, 1.15f), 0.7f));
            s.Append(act.DOLocalMoveY(height, air * 0.5f).SetEase(Ease.OutQuad));
            s.Join(act.DOScale(new Vector3(0.88f, 1.15f, 0.88f), air * 0.25f));
            s.Insert(s.Duration() - air * 0.25f, act.DOScale(1f, air * 0.25f));
            s.Append(act.DOLocalMoveY(0f, air * 0.5f).SetEase(Ease.InQuad));
            s.Append(act.DOScale(new Vector3(1.2f, 0.78f, 1.2f), 0.07f));
            s.Append(act.DOScale(1f, 0.35f).SetEase(Ease.OutElastic));
        }

        // ------------------------------------------------------------------ actions

        public void Idle()
        {
            var s = Begin();
            Rest(s, 0.2f);
        }

        public void Wave()
        {
            var s = Begin();
            armsSwing = false;
            s.AppendCallback(() => AudioManager.I?.Play(Sfx.Giggle, 1.05f, 0.6f));
            s.Append(Arms(-ArmRest, 150f, 0.25f));
            s.Join(act.DOLocalRotate(new Vector3(0, 0, 6f), 0.25f));
            for (int i = 0; i < 3; i++)
            {
                s.Append(DOTween.To(() => armRA, v => armRA = v, 118f, 0.16f).SetEase(Ease.InOutSine));
                s.Append(DOTween.To(() => armRA, v => armRA = v, 152f, 0.16f).SetEase(Ease.InOutSine));
            }
            Rest(s);
        }

        public void Hop()
        {
            var s = Begin();
            AppendHop(s, 0.3f, 0.45f);
        }

        public void Cheer()
        {
            var s = Begin();
            armsSwing = false;
            s.AppendCallback(() => { mood = Mood.Open; eyeScale = 1.12f; AudioManager.I?.Play(Sfx.Giggle, 1.1f, 0.7f); });
            s.Join(Arms(-150f, 150f, 0.2f));
            AppendHop(s, 0.55f, 0.55f);
            s.Append(DOTween.To(() => armRA, v => armRA = v, 125f, 0.12f).SetLoops(2, LoopType.Yoyo));
            Rest(s);
        }

        public void Spin()
        {
            var s = Begin();
            armsSwing = false;
            s.Join(Arms(-95f, 95f, 0.15f));
            s.AppendCallback(() => mood = Mood.Open);
            float start = s.Duration();
            AppendHop(s, 0.6f, 0.6f);
            s.Insert(start + 0.1f, act.DOLocalRotate(new Vector3(0, 360f, 0), 0.55f, RotateMode.LocalAxisAdd).SetEase(Ease.InOutQuad));
            Rest(s);
        }

        public void Flip()
        {
            var s = Begin();
            armsSwing = false;
            s.Join(Arms(-160f, 160f, 0.15f));
            s.AppendCallback(() => { mood = Mood.Open; AudioManager.I?.Play(Sfx.Giggle, 1.15f, 0.7f); });
            float start = s.Duration();
            AppendHop(s, 0.95f, 0.75f);
            s.Insert(start + 0.12f, act.DOLocalRotate(new Vector3(-360f, 0, 0), 0.62f, RotateMode.LocalAxisAdd).SetEase(Ease.InOutSine));
            Rest(s);
        }

        public void Dance(float seconds)
        {
            var s = Begin();
            armsSwing = false;
            s.AppendCallback(() => mood = Mood.Open);
            int beats = Mathf.Max(2, Mathf.RoundToInt(seconds / 0.42f));
            for (int i = 0; i < beats; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                s.Append(act.DOLocalMove(new Vector3(0.12f * side, 0.16f, 0), 0.21f).SetEase(Ease.OutQuad));
                s.Join(act.DOLocalRotate(new Vector3(0, -15f * side, 12f * side), 0.21f));
                s.Join(Arms(side > 0 ? -40f : -150f, side > 0 ? 150f : 40f, 0.21f, Ease.OutQuad));
                s.Append(act.DOLocalMove(Vector3.zero, 0.21f).SetEase(Ease.InQuad));
                s.Append(act.DOScale(new Vector3(1.1f, 0.9f, 1.1f), 0.06f).SetLoops(2, LoopType.Yoyo));
            }
            Rest(s);
        }

        public void Sad()
        {
            var s = Begin();
            armsSwing = false;
            s.AppendCallback(() => { mood = Mood.Sad; AudioManager.I?.Play(Sfx.Aww, 1f, 0.6f); });
            s.Join(Arms(-4f, 4f, 0.25f, Ease.OutQuad));
            s.Join(act.DOScale(new Vector3(1.07f, 0.9f, 1.07f), 0.3f));
            s.Append(act.DOLocalRotate(new Vector3(0, 18f, 0), 0.14f));
            s.Append(act.DOLocalRotate(new Vector3(0, -18f, 0), 0.2f));
            s.Append(act.DOLocalRotate(new Vector3(0, 12f, 0), 0.18f));
            s.Append(act.DOLocalRotate(Vector3.zero, 0.15f));
            s.AppendInterval(0.5f);
            s.Append(act.DOScale(1f, 0.3f).SetEase(Ease.OutBack));
            Rest(s);
        }

        public void ShakeHead()
        {
            var s = Begin();
            s.AppendCallback(() => mood = Mood.Sad);
            for (int i = 0; i < 2; i++)
            {
                s.Append(act.DOLocalRotate(new Vector3(0, 20f, 0), 0.12f));
                s.Append(act.DOLocalRotate(new Vector3(0, -20f, 0), 0.16f));
            }
            Rest(s, 0.2f);
        }

        public void Think(float hold = 1.6f)
        {
            var s = Begin();
            armsSwing = false;
            s.AppendCallback(() => lookUp = new Vector2(0.018f, 0.03f));
            s.Join(Arms(-ArmRest, 128f, 0.3f));
            s.Join(act.DOLocalRotate(new Vector3(0, 0, -9f), 0.3f));
            s.Append(act.DOLocalMoveY(0.03f, 0.35f).SetLoops(Mathf.Max(1, Mathf.RoundToInt(hold / 0.35f)), LoopType.Yoyo));
            Rest(s);
        }

        public void Point(Vector3 world, float hold = 1.4f)
        {
            var s = Begin();
            armsSwing = false;
            var shoulder = transform.position + Vector3.up * 0.44f * transform.lossyScale.y;
            float dx = world.x - shoulder.x, dy = world.y - shoulder.y;
            float angle = Mathf.Atan2(dx, -dy) * Mathf.Rad2Deg;
            bool right = dx >= 0f;
            s.AppendCallback(() => { LookAt(world); mood = Mood.Open; });
            s.Join(Arms(right ? -ArmRest : Mathf.Min(angle, -30f), right ? Mathf.Max(angle, 30f) : ArmRest, 0.3f));
            s.Join(act.DOLocalRotate(new Vector3(0, 0, right ? -8f : 8f), 0.3f));
            s.Append(DOTween.To(() => right ? armRA : armLA, v => { if (right) armRA = v; else armLA = v; },
                (right ? Mathf.Max(angle, 30f) : Mathf.Min(angle, -30f)) + (right ? 10f : -10f), 0.15f).SetLoops(4, LoopType.Yoyo));
            s.AppendInterval(Mathf.Max(0f, hold - 0.6f));
            s.AppendCallback(() => mood = Mood.Smile);
            Rest(s);
        }

        public void Surprised()
        {
            var s = Begin();
            armsSwing = false;
            s.AppendCallback(() => { mood = Mood.Open; eyeScale = 1.3f; });
            s.Join(Arms(-110f, 110f, 0.15f));
            AppendHop(s, 0.15f, 0.3f, false);
            s.AppendInterval(0.4f);
            Rest(s);
        }

        /// <summary>Falls from the sky and lands with a squishy splash.</summary>
        public void DropIn(Vector3 landWorld, float delay = 0f)
        {
            var s = Begin();
            transform.position = landWorld + Vector3.up * 7f;
            armsSwing = false;
            s.PrependInterval(delay);
            s.AppendCallback(() => { mood = Mood.Open; armLA = -160f; armRA = 160f; });
            s.Append(transform.DOMove(landWorld, 0.55f).SetEase(Ease.InQuad));
            s.Join(act.DOScale(new Vector3(0.85f, 1.25f, 0.85f), 0.55f));
            s.AppendCallback(() => { AudioManager.I?.Play(Sfx.Land); AudioManager.I?.Play(Sfx.Splash, 1.1f, 0.6f); Landed?.Invoke(landWorld); });
            s.Append(act.DOScale(new Vector3(1.35f, 0.6f, 1.35f), 0.08f));
            s.Append(act.DOScale(1f, 0.6f).SetEase(Ease.OutElastic));
            s.Join(Arms(-ArmRest, ArmRest, 0.4f));
            Rest(s, 0.1f);
        }

        /// <summary>Walks (hops) to a new spot.</summary>
        public Tween MoveTo(Vector3 world, float duration = 0.6f)
        {
            Wake();
            transform.DOKill();
            var s = DOTween.Sequence().SetLink(gameObject);
            s.Append(transform.DOJump(world, 0.4f, Mathf.Max(1, Mathf.RoundToInt(duration / 0.3f)), duration).SetEase(Ease.Linear));
            return s;
        }

        public void OnPointerClick(PointerEventData e) => Poke();

        public void Poke()
        {
            if (seq != null && seq.IsActive() && seq.IsPlaying() && !sleepy) return;
            switch (Random.Range(0, 4))
            {
                case 0: Spin(); break;
                case 1: Cheer(); break;
                case 2: Flip(); break;
                default: Wave(); break;
            }
        }

        /// <summary>Any player activity: stay awake.</summary>
        public void Nudge() => Wake();
    }
}
