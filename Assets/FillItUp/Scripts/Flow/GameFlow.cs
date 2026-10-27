using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace FillItUp
{
    /// <summary>
    /// Runs the whole game inside one scene: menu, level map, the three levels, results and progress.
    /// Screens are UI panels over a single 3D stage; the camera glides between framed shots.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        [Header("World")]
        public Stage stage;
        public Mascot mascot;
        public CameraRig rig;
        public AudioManager audioManager;
        public Transform mascotMenuSpot, mascotMapSpot, mascotPlaySpot, mascotResultsSpot;

        [Header("Screens")]
        public MenuScreen menu;
        public MapScreen map;
        public HudScreen hud;
        public ResultsScreen results;
        public ProgressScreen progress;
        public PauseScreen pause;
        public CaptionBar captions;

        // subtitle height above the bottom edge
        const float CaptionLow = 24f;

        const float HintAfter = 9f;
        static readonly string[] PraiseWords = { "Yes!", "Amazing!", "Super!", "Wow!", "Splash-tastic!", "Great job!", "You got it!" };

        GameConfig cfg;
        Camera cam;
        int level;
        bool welcomed;
        float saveTimer;
        Coroutine levelRoutine;

        // per-question state
        Question current;
        int questionIndex;
        bool feedbackRunning;
        bool resolved;
        int wrongTries;
        float idleTimer;
        int pourAnswer = -1;

        public Question CurrentQuestion => current;
        public int QuestionIndex => questionIndex;
        public bool InLevel => levelRoutine != null;

        // per-level state
        int score, streak;
        float levelStart;
        int lastRight = -1, lastWrong = -1;

        void Awake()
        {
            cfg = GameConfig.Load();
            cam = Camera.main;
            Application.runInBackground = true;
            SaveSystem.Load();
            audioManager.Init(cfg);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            DynamicGI.UpdateEnvironment();
        }

        void Start()
        {
            Wire();
            foreach (var s in new UIScreen[] { menu, map, hud, results, progress, pause }) s.Hide(true);
            rig.Snap(Shot.Menu);
            Place(mascot.transform, mascotMenuSpot);
            mascot.Landed = p => { FXPool.I.Play(Fx.Splash, p + Vector3.up * 0.1f, 1.3f); FXPool.I.Play(Fx.Puff, p, 1f); };
            mascot.DropIn(mascotMenuSpot.position, 0.5f);
            ShowMenu();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            SaveSystem.AddPlayTime(dt);
            saveTimer += dt;
            if (saveTimer > 20f) { saveTimer = 0f; SaveSystem.Save(); }
            idleTimer += Time.deltaTime;
        }

        void OnApplicationPause(bool paused) { if (paused) SaveSystem.Save(); }
        void OnApplicationQuit() => SaveSystem.Save();

        static void Place(Transform t, Transform spot)
        {
            t.position = spot.position;
            t.rotation = spot.rotation;
            t.localScale = spot.localScale;
        }

        void MoveMascot(Transform spot, float duration = 0.7f)
        {
            mascot.MoveTo(spot.position, duration);
            mascot.transform.DOScale(spot.localScale, duration);
        }

        // ------------------------------------------------------------------ wiring

        void Wire()
        {
            menu.play.onClick.AddListener(() => { mascot.Nudge(); ShowMap(); });
            menu.sound.onClick.AddListener(ToggleSound);
            menu.progress.onClick.AddListener(ShowProgress);

            map.back.onClick.AddListener(ShowMenu);
            for (int i = 0; i < map.cards.Length; i++)
            {
                int idx = i;
                map.cards[i].button.onClick.AddListener(() => OnLevelCard(idx));
            }

            hud.pause.onClick.AddListener(Pause);
            hud.speaker.onClick.AddListener(() => { if (current != null) { audioManager.Speak(current.Voice); mascot.Nudge(); } });
            hud.fits.onClick.AddListener(() => pourAnswer = 0);
            hud.spills.onClick.AddListener(() => pourAnswer = 1);

            results.next.onClick.AddListener(() =>
            {
                if (level < Levels.Count - 1 && SaveSystem.Data.IsUnlocked(level + 1)) StartLevel(level + 1);
                else ShowMenu();
            });
            results.replay.onClick.AddListener(() => StartLevel(level));
            results.map.onClick.AddListener(ShowMap);

            progress.close.onClick.AddListener(() => { progress.Hide(); menu.Refresh(cfg); });
            progress.reset.onClick.AddListener(progress.AskReset);
            progress.confirmNo.onClick.AddListener(() => progress.confirm.SetActive(false));
            progress.confirmYes.onClick.AddListener(() =>
            {
                SaveSystem.Reset();
                progress.Refresh();
                menu.Refresh(cfg);
                mascot.Surprised();
            });

            pause.resume.onClick.AddListener(Resume);
            pause.sound.onClick.AddListener(() => { ToggleSound(); pause.Refresh(cfg); });
            pause.restart.onClick.AddListener(() => { Resume(); StartLevel(level); });
            pause.home.onClick.AddListener(() => { Resume(); ShowMenu(); });
        }

        void ToggleSound()
        {
            audioManager.SoundOn = !audioManager.SoundOn;
            menu.Refresh(cfg);
            if (audioManager.SoundOn) mascot.Cheer(); else mascot.Surprised();
        }

        // ------------------------------------------------------------------ screens

        void StopLevel()
        {
            if (levelRoutine != null) StopCoroutine(levelRoutine);
            levelRoutine = null;
            current = null;
            captions.Suppress(false);
            stage.HideAll();
            audioManager.StopLoop();
            audioManager.StopVoice();
            hud.Hide();
            results.Hide();
        }

        void ShowMenu()
        {
            StopLevel();
            map.Hide();
            menu.Refresh(cfg);
            menu.Show();
            captions.SetLift(CaptionLow);
            rig.MoveTo(Shot.Menu);
            mascot.SleepAllowed = true;
            if (Vector3.Distance(mascot.transform.position, mascotMenuSpot.position) > 0.1f) MoveMascot(mascotMenuSpot);
            mascot.LookAtCamera();
            DOVirtual.DelayedCall(0.6f, mascot.Wave).SetLink(gameObject);
        }

        void ShowMap()
        {
            StopLevel();
            menu.Hide();
            progress.Hide();
            map.Refresh();
            map.Show();
            captions.SetLift(CaptionLow);
            rig.MoveTo(Shot.Map);
            mascot.SleepAllowed = true;
            MoveMascot(mascotMapSpot);
            mascot.LookAtCamera();
            AudioManager.I.Play(Sfx.Whoosh, 1f, 0.6f);
            if (pendingUnlock >= 0)
            {
                // a level was just opened: the lock shakes and pops on its card
                map.PlayUnlock(pendingUnlock);
                pendingUnlock = -1;
                audioManager.Speak("unlocked");
                DOVirtual.DelayedCall(1.2f, mascot.Cheer).SetLink(gameObject);
                return;
            }
            audioManager.Speak(welcomed ? "pick_level" : "welcome");
            welcomed = true;
            DOVirtual.DelayedCall(0.75f, mascot.Hop).SetLink(gameObject);
        }

        int pendingUnlock = -1;

        void ShowProgress()
        {
            progress.Refresh();
            progress.Show();
            audioManager.Speak("progress");
            mascot.Cheer();
        }

        void OnLevelCard(int i)
        {
            if (!SaveSystem.Data.IsUnlocked(i))
            {
                var rt = map.cards[i].root;
                rt.DOKill(true);
                rt.DOShakeAnchorPos(0.4f, new Vector2(18f, 0), 18);
                audioManager.Play(Sfx.Wrong, 1f, 0.6f);
                mascot.ShakeHead();
                return;
            }
            StartLevel(i);
        }

        void Pause()
        {
            if (levelRoutine == null) return;
            Time.timeScale = 0f;
            audioManager.StopVoice();
            pause.Refresh(cfg);
            pause.Show();
        }

        void Resume()
        {
            Time.timeScale = 1f;
            pause.Hide();
        }

        // ------------------------------------------------------------------ level

        void StartLevel(int i)
        {
            StopLevel();
            pendingUnlock = -1;
            level = i;
            menu.Hide();
            map.Hide();
            results.Hide();
            levelRoutine = StartCoroutine(RunLevel());
        }

        IEnumerator RunLevel()
        {
            score = 0;
            streak = 0;
            levelStart = Time.time;
            SaveSystem.LevelStarted(level);
            var questions = QuestionFactory.Make(level);

            rig.MoveTo(Shot.Play);
            mascot.SleepAllowed = false;
            MoveMascot(mascotPlaySpot);
            hud.ResetLevel(level, questions.Count);
            hud.Show();
            captions.SetLift(CaptionLow);
            audioManager.Play(Sfx.Whoosh, 1f, 0.6f);
            yield return new WaitForSeconds(0.5f);

            // intro ribbon + Splashy explains the level
            hud.ShowIntro(level);
            audioManager.Play(Sfx.Sparkle, 1f, 0.6f);
            float voice = audioManager.Speak(Levels.Defs[level].introVoice);
            mascot.Wave();
            yield return new WaitForSeconds(Mathf.Clamp(voice, 1.6f, 6f) + 0.2f);
            yield return hud.HideIntro().WaitForCompletion();

            for (int q = 0; q < questions.Count; q++)
            {
                current = questions[q];
                questionIndex = q;
                hud.SetCurrent(q);
                if (current.type == QuestionType.Pour) yield return PourQuestion(q);
                else yield return ChoiceQuestion(q);
                hud.HideQuestion();
                yield return stage.Clear();
            }
            current = null;
            yield return LevelComplete(questions.Count);
            levelRoutine = null;
        }

        // ---------------------------------------------------- more / less / most / least

        IEnumerator ChoiceQuestion(int index)
        {
            resolved = false;
            wrongTries = 0;
            var views = stage.Show(current);
            yield return new WaitForSeconds(0.65f);
            hud.SetQuestion(current.Prompt);
            audioManager.Speak(current.Voice);
            mascot.Think(1.8f);
            mascot.LookAt(stage.transform.position + Vector3.up * 0.8f);

            foreach (var v in views)
            {
                v.Interactive = true;
                v.Clicked = OnPick;
                v.Hovered = OnHover;
            }
            idleTimer = 0f;
            while (!resolved)
            {
                if (idleTimer > HintAfter)
                {
                    idleTimer = 0f;
                    audioManager.Speak("tap_one");
                    mascot.Hop();
                    foreach (var v in views) if (v.Interactive) v.Wobble(0.6f);
                }
                yield return null;
            }
            // the pick handlers run the feedback; wait for it to finish
            while (feedbackRunning) yield return null;
        }

        void OnHover(ContainerView v, bool on)
        {
            var p = stage.PedestalOf(v);
            if (p == null || !v.Interactive && on) return;
            if (v.Interactive) p.SetState(on ? PedestalState.Hover : PedestalState.Idle);
            if (on)
            {
                v.Wobble(0.45f);
                audioManager.Play(Sfx.UiHover, 0.9f + v.Slot * 0.1f, 0.5f);
                mascot.LookAt(v.RimWorld());
            }
        }

        void OnPick(ContainerView v)
        {
            if (resolved || feedbackRunning) return;
            idleTimer = 0f;
            mascot.Nudge();
            if (v.Slot == current.answer)
            {
                resolved = true;
                foreach (var a in stage.Active) a.Interactive = false;
                StartCoroutine(Run(RightAnswer(v, wrongTries == 0)));
            }
            else
            {
                StartCoroutine(Run(WrongAnswer(v)));
            }
        }

        IEnumerator Run(IEnumerator routine)
        {
            feedbackRunning = true;
            yield return routine;
            feedbackRunning = false;
        }

        IEnumerator RightAnswer(ContainerView v, bool firstTry)
        {
            var p = stage.PedestalOf(v);
            p?.SetState(PedestalState.Right);
            p?.Punch();
            audioManager.Play(Sfx.Select);
            v.Bounce();
            foreach (var other in stage.Active) if (other != v) stage.PedestalOf(other)?.SetState(PedestalState.Off);

            mascot.LookAt(v.RimWorld());
            yield return stage.MagicFill(v);
            Celebrate(v, firstTry);
            yield return new WaitForSeconds(1.5f);
        }

        IEnumerator WrongAnswer(ContainerView v)
        {
            wrongTries++;
            streak = 0;
            hud.SetStreak(0);
            v.Interactive = false;
            var p = stage.PedestalOf(v);
            p?.SetState(PedestalState.Wrong);
            v.Shake();
            rig.Shake(0.05f);
            audioManager.Play(Sfx.Wrong);
            mascot.Sad();

            int left = 0;
            foreach (var a in stage.Active) if (a.Interactive) left++;
            bool reveal = left <= 1 || wrongTries >= 2;
            if (!reveal)
            {
                audioManager.Speak(PickVoice("wrong_", 3, ref lastWrong));
                yield return new WaitForSeconds(0.5f);
                p?.SetState(PedestalState.Off);
                yield break;
            }

            // show the answer, gently, and say why
            resolved = true;
            foreach (var a in stage.Active) a.Interactive = false;
            yield return new WaitForSeconds(0.7f);
            p?.SetState(PedestalState.Off);
            var right = stage.Active[current.answer];
            float len = audioManager.Speak("reveal");
            mascot.Point(right.RimWorld(), 1.6f);
            stage.PedestalOf(right)?.SetState(PedestalState.Right);
            right.Bounce();
            yield return new WaitForSeconds(Mathf.Max(0.8f, len));
            yield return stage.MagicFill(right);
            FXPool.I.Play(Fx.Sparkle, right.RimWorld(), 1f);
            len = audioManager.Speak(current.WhyVoice);
            SaveSystem.QuestionAnswered(level, false);
            hud.MarkPip(questionIndex, false);
            yield return new WaitForSeconds(Mathf.Max(1.2f, len + 0.2f));
        }

        /// <summary>Sparkles, praise, Splashy cheers, and on a first try a droplet flies to the jar.</summary>
        void Celebrate(ContainerView v, bool firstTry, bool speak = true)
        {
            var rim = v.RimWorld();
            FXPool.I.Play(Fx.Sparkle, rim + Vector3.up * 0.15f, 1.2f);
            FXPool.I.Play(Fx.StarBurst, rim + Vector3.up * 0.2f, 1f);
            audioManager.Play(Sfx.Correct);
            audioManager.Play(Sfx.Sparkle, 1f, 0.5f);
            hud.Praise(PraiseWords[Random.Range(0, PraiseWords.Length)], Palette.Confetti[Random.Range(0, Palette.Confetti.Length)]);

            switch (Random.Range(0, 3))
            {
                case 0: mascot.Cheer(); break;
                case 1: mascot.Spin(); break;
                default: mascot.Flip(); break;
            }

            if (firstTry)
            {
                score++;
                streak++;
                hud.FlyDroplet(cam.WorldToScreenPoint(rim), score, current == null ? 5 : Levels.Defs[level].questions);
                hud.SetStreak(streak);
                if (streak == 3 || streak == 5)
                {
                    audioManager.Play(Sfx.Streak);
                    FXPool.I.Play(Fx.Ring, mascot.transform.position + Vector3.up * 0.4f, 2f);
                    FXPool.I.Play(Fx.Hearts, mascot.transform.position + Vector3.up * 1.2f, 1f);
                    if (speak) audioManager.Speak(streak == 3 ? "streak_3" : "streak_5");
                }
                else if (speak) audioManager.Speak(PickVoice("right_", 6, ref lastRight));
            }
            else if (speak)
            {
                audioManager.Speak(PickVoice("right_", 6, ref lastRight));
            }
            SaveSystem.QuestionAnswered(level, firstTry);
            hud.MarkPip(questionIndex, firstTry);
        }

        static string PickVoice(string prefix, int count, ref int last)
        {
            int n;
            do n = Random.Range(1, count + 1); while (n == last && count > 1);
            last = n;
            return prefix + n;
        }

        // ---------------------------------------------------- level 3: fit or spill?

        IEnumerator PourQuestion(int index)
        {
            pourAnswer = -1;
            var views = stage.Show(current);
            var src = views[0];
            var tgt = views[1];
            yield return new WaitForSeconds(0.7f);
            hud.SetQuestion(current.Prompt);
            audioManager.Speak(current.Voice);
            mascot.Think(2f);
            mascot.LookAt(tgt.RimWorld());
            hud.ShowPourButtons(true);
            captions.Suppress(true);

            idleTimer = 0f;
            while (pourAnswer < 0)
            {
                if (idleTimer > HintAfter)
                {
                    idleTimer = 0f;
                    audioManager.Speak(current.Voice);
                    mascot.Hop();
                    src.Wobble(0.6f);
                    tgt.Wobble(0.6f);
                }
                yield return null;
            }
            bool correct = pourAnswer == current.answer;
            hud.ShowPourButtons(false);
            captions.Suppress(false);
            mascot.Nudge();

            // suspense, then the real pour proves it
            audioManager.Play(Sfx.Drumroll);
            mascot.Surprised();
            mascot.LookAt(src.RimWorld());
            yield return new WaitForSeconds(0.5f);
            yield return stage.Pour(src, tgt);
            mascot.LookAt(tgt.RimWorld());

            // the explanation ("It all fits!" / "It spilled over!") is the voice line here
            float len = audioManager.Speak(current.WhyVoice);
            if (correct)
            {
                stage.PedestalOf(tgt)?.SetState(PedestalState.Right);
                Celebrate(tgt, true, false);
            }
            else
            {
                streak = 0;
                hud.SetStreak(0);
                audioManager.Play(Sfx.Aww, 1f, 0.7f);
                mascot.Sad();
                SaveSystem.QuestionAnswered(level, false);
                hud.MarkPip(questionIndex, false);
            }
            yield return new WaitForSeconds(Mathf.Max(1.8f, len + 0.4f));
        }

        // ------------------------------------------------------------------ results

        IEnumerator LevelComplete(int outOf)
        {
            hud.Hide();
            int stars = Levels.StarsFor(score, outOf);
            bool unlockedNext = SaveSystem.LevelFinished(level, score, outOf, stars, Time.time - levelStart, out bool newBest);
            pendingUnlock = unlockedNext ? level + 1 : -1;
            var d = SaveSystem.Data;
            bool newChampion = !d.champion && d.levels[0].completions > 0 && d.levels[1].completions > 0 && d.levels[2].completions > 0;
            if (newChampion) { d.champion = true; SaveSystem.Save(); }

            rig.MoveTo(Shot.Results);
            MoveMascot(mascotResultsSpot);
            stage.HideAll();
            yield return new WaitForSeconds(0.6f);

            audioManager.Play(Sfx.LevelComplete);
            audioManager.Play(Sfx.Confetti);
            var c = mascotResultsSpot.position;
            FXPool.I.Play(Fx.Confetti, c + new Vector3(-1.2f, 0, 0.5f), 1f);
            FXPool.I.Play(Fx.Confetti, c + new Vector3(1.4f, 0, 0.5f), 1f);
            mascot.LookAtCamera();
            mascot.Dance(3.2f);
            audioManager.Speak(stars == 3 ? "perfect" : score <= 2 ? "good_try" : "level_done");

            bool hasNext = level < Levels.Count - 1;
            yield return results.Play(level, score, outOf, stars, newBest, unlockedNext, hasNext);

            if (newChampion)
            {
                yield return new WaitForSeconds(0.6f);
                audioManager.Speak("champion");
                audioManager.Play(Sfx.Streak);
                mascot.Flip();
                FXPool.I.Play(Fx.Confetti, c + Vector3.up * 2f, 1.4f);
                FXPool.I.Play(Fx.StarBurst, c + Vector3.up * 1.2f, 1.5f);
                results.ShowChampion();
            }
            else if (unlockedNext)
            {
                // the map plays the unlock (lock pops, "A new level is open!"); here just a happy spin
                yield return new WaitForSeconds(0.9f);
                audioManager.Play(Sfx.Unlock, 1f, 0.7f);
                mascot.Spin();
            }
        }

    }
}
