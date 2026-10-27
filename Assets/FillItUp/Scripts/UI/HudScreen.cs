using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.ProceduralImage;

namespace FillItUp
{
    /// <summary>In-level HUD: question banner (with replay voice), progress pips, the droplet jar, streak badge,
    /// fit/spill answer buttons, praise words and the level intro banner.</summary>
    public class HudScreen : UIScreen
    {
        public Button pause, speaker, fits, spills;
        public RectTransform banner, pourButtons, jar, jarWater, streak, intro, praise;
        public TMP_Text question, jarCount, streakLabel, praiseLabel, introNumber, introTitle;
        public ProceduralImage introFace;
        public ProceduralImage[] pips = new ProceduralImage[5];
        public Image dropletIcon;

        const float JarWaterMax = 150f;
        int dropletsShown;

        public void ResetLevel(int level, int questions)
        {
            for (int i = 0; i < pips.Length; i++)
            {
                pips[i].gameObject.SetActive(i < questions);
                pips[i].color = new Color(1, 1, 1, 0.45f);
                pips[i].transform.localScale = Vector3.one;
            }
            dropletsShown = 0;
            jarCount.text = "0";
            jarWater.sizeDelta = new Vector2(jarWater.sizeDelta.x, 0f);
            streak.gameObject.SetActive(false);
            pourButtons.gameObject.SetActive(false);
            praise.gameObject.SetActive(false);
            intro.gameObject.SetActive(false);
            banner.localScale = Vector3.zero;
        }

        public void SetQuestion(string text)
        {
            question.text = text;
            banner.DOKill();
            banner.localScale = Vector3.one * 0.6f;
            banner.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
        }

        public void HideQuestion() { banner.DOKill(); banner.DOScale(0f, 0.2f).SetEase(Ease.InBack); }

        public void SetCurrent(int index)
        {
            var p = pips[index];
            p.color = Color.white;
            p.transform.DOKill();
            p.transform.localScale = Vector3.one;
            p.transform.DOScale(1.3f, 0.45f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
        }

        public void MarkPip(int index, bool firstTry)
        {
            var p = pips[index];
            p.transform.DOKill();
            p.transform.localScale = Vector3.one * 1.5f;
            p.transform.DOScale(1f, 0.35f).SetEase(Ease.OutBack);
            p.color = firstTry ? Palette.Correct : Palette.Orange;
        }

        public void ShowPourButtons(bool on)
        {
            pourButtons.gameObject.SetActive(true);
            pourButtons.DOKill();
            if (on)
            {
                fits.interactable = spills.interactable = true;
                pourButtons.anchoredPosition = new Vector2(0, -260);
                pourButtons.DOAnchorPosY(0, 0.45f).SetEase(Ease.OutBack);
            }
            else
            {
                fits.interactable = spills.interactable = false;
                pourButtons.DOAnchorPosY(-260, 0.3f).SetEase(Ease.InBack).OnComplete(() => pourButtons.gameObject.SetActive(false));
            }
        }

        /// <summary>A droplet icon flies from a screen point into the jar, which then fills a notch.</summary>
        public void FlyDroplet(Vector2 screenPoint, int total, int max)
        {
            var canvas = GetComponentInParent<Canvas>().rootCanvas;
            var cr = (RectTransform)canvas.transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(cr, screenPoint, null, out var local);
            var drop = UIFactory.Rect(cr, "FlyingDrop", new Vector2(0.5f, 0.5f), local, new Vector2(90, 90));
            var img = drop.gameObject.AddComponent<Image>();
            img.sprite = dropletIcon.sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            drop.localScale = Vector3.zero;
            var s = DOTween.Sequence().SetLink(drop.gameObject);
            s.Append(drop.DOScale(1.3f, 0.2f).SetEase(Ease.OutBack));
            s.Append(drop.DOMove(jar.position, 0.55f).SetEase(Ease.InCubic));
            s.Join(drop.DOScale(0.6f, 0.55f));
            s.OnComplete(() =>
            {
                Destroy(drop.gameObject);
                dropletsShown = total;
                jarCount.text = total.ToString();
                AudioManager.I?.Play(Sfx.Droplet, 1f + total * 0.06f);
                jar.DOKill(true);
                jar.DOPunchScale(new Vector3(0.15f, -0.12f, 0), 0.4f, 7, 0.6f);
                jarWater.DOSizeDelta(new Vector2(jarWater.sizeDelta.x, JarWaterMax * Mathf.Clamp01((float)total / max)), 0.4f).SetEase(Ease.OutBack);
                UIBurst.Play(jar, Palette.Hex(0x7FD3FF), 8, 90f);
            });
        }

        public void SetStreak(int count)
        {
            if (count < 2) { if (streak.gameObject.activeSelf) streak.DOScale(0f, 0.2f).OnComplete(() => streak.gameObject.SetActive(false)); return; }
            streak.gameObject.SetActive(true);
            streakLabel.text = "x" + count;
            streak.DOKill();
            streak.localScale = Vector3.one * 1.6f;
            streak.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
            streak.DOPunchRotation(new Vector3(0, 0, 12f), 0.5f, 8).SetDelay(0.1f);
        }

        public void Praise(string word, Color color)
        {
            praise.gameObject.SetActive(true);
            praiseLabel.text = word;
            praiseLabel.color = color;
            praise.DOKill();
            praise.anchoredPosition = new Vector2(0, 40);
            praise.localScale = Vector3.zero;
            praise.localRotation = Quaternion.Euler(0, 0, Random.Range(-8f, 8f));
            var s = DOTween.Sequence().SetTarget(praise);
            s.Append(praise.DOScale(1f, 0.35f).SetEase(Ease.OutBack, 2.5f));
            s.AppendInterval(0.55f);
            s.Append(praise.DOAnchorPosY(150, 0.35f).SetEase(Ease.InQuad));
            s.Join(praise.DOScale(0.5f, 0.35f));
            s.OnComplete(() => praise.gameObject.SetActive(false));
        }

        public Tween ShowIntro(int level)
        {
            intro.gameObject.SetActive(true);
            introNumber.text = "Level " + (level + 1);
            introTitle.text = Levels.Defs[level].title;
            introFace.color = Palette.LevelColors[level];
            intro.DOKill();
            intro.anchoredPosition = new Vector2(-1500, 0);
            intro.localRotation = Quaternion.Euler(0, 0, -4f);
            return intro.DOAnchorPosX(0, 0.55f).SetEase(Ease.OutBack);
        }

        public Tween HideIntro()
        {
            intro.DOKill();
            return intro.DOAnchorPosX(1500, 0.35f).SetEase(Ease.InBack).OnComplete(() => intro.gameObject.SetActive(false));
        }

        public static HudScreen Build(Transform canvas, GameConfig cfg)
        {
            var root = UIFactory.Stretch(canvas, "HudScreen");
            root.gameObject.AddComponent<CanvasGroup>();
            var s = root.gameObject.AddComponent<HudScreen>();
            s.content = UIFactory.Stretch(root, "Content");
            var c = s.content;

            s.pause = UIFactory.RoundButton(c, "PauseButton", cfg.pause, new Vector2(0f, 1f), new Vector2(100, -95), 120, Palette.Purple, Palette.PurpleLip);

            // question banner
            var bannerFace = UIFactory.Panel(c, "Banner", new Vector2(0.5f, 1f), new Vector2(0, -98), new Vector2(1080, 150), Palette.Paper, 75);
            s.banner = (RectTransform)bannerFace.transform.parent;
            s.speaker = UIFactory.RoundButton(bannerFace.transform, "Speaker", cfg.speakerOn, new Vector2(0f, 0.5f), new Vector2(85, 6), 112, Palette.Blue, Palette.BlueLip);
            s.speaker.GetComponent<UIButtonFX>().burst = false;
            s.question = UIFactory.Label(bannerFace.transform, "Question", "Which holds more?", 78, Palette.Ink);
            s.question.rectTransform.offsetMin = new Vector2(150, 4);
            s.question.rectTransform.offsetMax = new Vector2(-30, 0);

            // progress pips
            var pipRow = UIFactory.Rect(c, "Pips", new Vector2(0.5f, 1f), new Vector2(0, -212), new Vector2(420, 50));
            for (int i = 0; i < 5; i++)
            {
                var p = UIFactory.Rect(pipRow, "Pip" + i, new Vector2(0.5f, 0.5f), new Vector2((i - 2) * 72f, 0), new Vector2(40, 40));
                var outline = UIFactory.Rect(p, "Ring", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(50, 50));
                UIFactory.Rounded(outline, Palette.UiShadow, -1).raycastTarget = false;
                outline.SetAsFirstSibling();
                var img = UIFactory.Rounded(p, Color.white, -1);
                img.raycastTarget = false;
                s.pips[i] = img;
            }

            // droplet jar
            s.jar = UIFactory.Rect(c, "Jar", new Vector2(1f, 1f), new Vector2(-110, -125), new Vector2(150, 180));
            var jarBack = UIFactory.Stretch(s.jar, "Glass");
            UIFactory.Rounded(jarBack, new Color(1f, 1f, 1f, 0.75f), 40).raycastTarget = false;
            var water = UIFactory.Rect(s.jar, "Water", new Vector2(0.5f, 0f), new Vector2(0, 14), new Vector2(122, 0), new Vector2(0.5f, 0f));
            UIFactory.Rounded(water, Palette.Hex(0x3FB6FF), 30).raycastTarget = false;
            s.jarWater = water;
            var jarRim = UIFactory.Rect(s.jar, "Rim", new Vector2(0.5f, 1f), new Vector2(0, 8), new Vector2(170, 34));
            UIFactory.Rounded(jarRim, Palette.Hex(0x1C76C2), 17).raycastTarget = false;
            var border = UIFactory.Stretch(s.jar, "Border");
            UIFactory.Rounded(border, Palette.Hex(0x1C76C2), 40, 7).raycastTarget = false;
            s.dropletIcon = UIFactory.Icon(s.jar, "Drop", cfg.droplet, new Vector2(0.5f, 0.5f), new Vector2(0, 10), 76);
            var countBack = UIFactory.Rect(s.jar, "CountBack", new Vector2(0.5f, 0f), new Vector2(0, -44), new Vector2(110, 70));
            UIFactory.Rounded(countBack, Palette.Hex(0x1C76C2), -1).raycastTarget = false;
            s.jarCount = UIFactory.Label(countBack, "Count", "0", 54, Color.white, TextAlignmentOptions.Center, true);

            // streak badge
            var streakFace = UIFactory.Panel(c, "Streak", new Vector2(1f, 1f), new Vector2(-300, -110), new Vector2(170, 90), Palette.Orange, -1);
            s.streak = (RectTransform)streakFace.transform.parent;
            UIFactory.Icon(streakFace.transform, "Flame", cfg.flame, new Vector2(0f, 0.5f), new Vector2(46, 4), 70);
            s.streakLabel = UIFactory.Label(streakFace.transform, "Label", "x3", 56, Color.white, TextAlignmentOptions.Center, true);
            s.streakLabel.rectTransform.offsetMin = new Vector2(70, 0);

            // fit / spill answers
            s.pourButtons = UIFactory.Rect(c, "PourButtons", new Vector2(0.5f, 0f), Vector2.zero, new Vector2(1100, 260), new Vector2(0.5f, 0f));
            s.fits = UIFactory.Button(s.pourButtons, "Fits", "It fits!", new Vector2(0.5f, 0.5f), new Vector2(-290, 0), new Vector2(500, 160), Palette.Green, Palette.GreenLip, 70, cfg.fitsIcon);
            s.spills = UIFactory.Button(s.pourButtons, "Spills", "It spills!", new Vector2(0.5f, 0.5f), new Vector2(290, 0), new Vector2(500, 160), Palette.Orange, Palette.OrangeLip, 70, cfg.spillsIcon);

            // praise word
            s.praise = UIFactory.Rect(c, "Praise", new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1200, 220));
            s.praiseLabel = UIFactory.Label(s.praise, "Word", "Amazing!", 170, Palette.Gold, TextAlignmentOptions.Center, true);

            // level intro ribbon
            var introFace = UIFactory.Panel(c, "Intro", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980, 300), Palette.LevelColors[0], 60);
            s.intro = (RectTransform)introFace.transform.parent;
            s.introFace = introFace;
            var num = UIFactory.Rect(introFace.transform, "Number", new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(900, 80));
            s.introNumber = UIFactory.Label(num, "T", "Level 1", 64, new Color(1, 1, 1, 0.9f));
            var tt = UIFactory.Rect(introFace.transform, "Title", new Vector2(0.5f, 0f), new Vector2(0, 95), new Vector2(900, 130));
            s.introTitle = UIFactory.Label(tt, "T", "More or Less", 120, Color.white, TextAlignmentOptions.Center, true);
            return s;
        }
    }
}
