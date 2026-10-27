using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FillItUp
{
    public class MapScreen : UIScreen
    {
        [System.Serializable]
        public class Card
        {
            public Button button;
            public Image[] stars = new Image[3];
            public GameObject lockOverlay, title, subtitle;
            public RectTransform root;
        }

        public Button back;
        public Card[] cards = new Card[3];
        public GameObject champion;

        public void Refresh()
        {
            var d = SaveSystem.Data;
            for (int i = 0; i < cards.Length; i++)
            {
                bool unlocked = d.IsUnlocked(i);
                cards[i].lockOverlay.SetActive(!unlocked); // locked cards stay tappable: they shake
                cards[i].title.SetActive(unlocked);
                cards[i].subtitle.SetActive(unlocked);
                for (int s = 0; s < 3; s++)
                    cards[i].stars[s].color = s < d.levels[i].bestStars ? Palette.Gold : new Color(0.1f, 0.15f, 0.3f, 0.25f);
            }
            champion.SetActive(d.champion);
        }

        public override void Show()
        {
            base.Show();
            for (int i = 0; i < cards.Length; i++)
            {
                var rt = cards[i].root;
                rt.DOKill();
                rt.localScale = Vector3.zero;
                rt.DOScale(1f, 0.45f).SetEase(Ease.OutBack).SetDelay(0.08f + i * 0.09f).SetUpdate(true);
            }
        }

        /// <summary>Lock shakes, then pops open (first unlock of a level).</summary>
        public void PlayUnlock(int level)
        {
            var c = cards[level];
            c.lockOverlay.SetActive(true);
            c.title.SetActive(false);
            c.subtitle.SetActive(false);
            var lockRt = (RectTransform)c.lockOverlay.transform;
            var s = DOTween.Sequence().SetUpdate(true).SetLink(c.lockOverlay);
            s.AppendInterval(0.6f);
            s.Append(lockRt.DOShakeRotation(0.5f, new Vector3(0, 0, 20f), 14));
            s.AppendCallback(() => AudioManager.I?.Play(Sfx.Unlock));
            s.Append(lockRt.DOScale(1.3f, 0.12f));
            s.Append(lockRt.DOScale(0f, 0.2f).SetEase(Ease.InBack));
            s.AppendCallback(() =>
            {
                c.lockOverlay.SetActive(false);
                c.title.SetActive(true);
                c.subtitle.SetActive(true);
                lockRt.localScale = Vector3.one;
                UIBurst.Play(c.root, Palette.Gold, 14, 220f);
                c.root.DOPunchScale(Vector3.one * 0.12f, 0.4f, 8, 0.6f).SetUpdate(true);
            });
        }

        public static MapScreen Build(Transform canvas, GameConfig cfg)
        {
            var root = UIFactory.Stretch(canvas, "MapScreen");
            root.gameObject.AddComponent<CanvasGroup>();
            var s = root.gameObject.AddComponent<MapScreen>();

            var title = UIFactory.Rect(root, "Title", new Vector2(0.5f, 1f), new Vector2(0, -105), new Vector2(1000, 150));
            UIFactory.Label(title, "Text", "Pick a Level!", 116, Color.white, TextAlignmentOptions.Center, true).gameObject.AddComponent<TextWobble>().amplitude = 6f;

            s.back = UIFactory.RoundButton(root, "BackButton", cfg.back, new Vector2(0f, 1f), new Vector2(105, -100), 128, Palette.Blue, Palette.BlueLip);

            var icons = new[] { cfg.level1Icon, cfg.level2Icon, cfg.level3Icon };
            for (int i = 0; i < 3; i++)
            {
                var c = new Card();
                var b = UIFactory.Button(root, "Level" + (i + 1), null, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 500f - 150f, -40f),
                    new Vector2(450, 600), Palette.LevelColors[i], Palette.LevelLips[i]);
                b.GetComponent<UIButtonFX>().burst = true;
                c.button = b;
                c.root = (RectTransform)b.transform;
                var face = b.transform.Find("Face");

                var badge = UIFactory.Rect(face, "Number", new Vector2(0.5f, 1f), new Vector2(0, -78), new Vector2(116, 116));
                UIFactory.Rounded(badge, Color.white, -1).raycastTarget = false;
                UIFactory.Label(badge, "N", (i + 1).ToString(), 80, Palette.LevelLips[i]);

                var iconBack = UIFactory.Rect(face, "IconBack", new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(330, 230));
                UIFactory.Rounded(iconBack, new Color(1, 1, 1, 0.22f), 40).raycastTarget = false;
                UIFactory.Icon(iconBack, "Icon", icons[i], new Vector2(0.5f, 0.5f), Vector2.zero, 210);

                var tl = UIFactory.Rect(face, "Title", new Vector2(0.5f, 0f), new Vector2(0, 205), new Vector2(440, 80));
                UIFactory.Label(tl, "T", Levels.Defs[i].title, 62, Color.white, TextAlignmentOptions.Center, true);
                var st = UIFactory.Rect(face, "Subtitle", new Vector2(0.5f, 0f), new Vector2(0, 148), new Vector2(440, 50));
                UIFactory.Label(st, "T", Levels.Defs[i].subtitle, 40, new Color(1, 1, 1, 0.92f));
                c.title = tl.gameObject;
                c.subtitle = st.gameObject;

                var starsRow = UIFactory.Rect(face, "Stars", new Vector2(0.5f, 0f), new Vector2(0, 70), new Vector2(330, 90));
                UIFactory.Rounded(starsRow, new Color(1, 1, 1, 0.9f), -1).raycastTarget = false;
                for (int k = 0; k < 3; k++)
                    c.stars[k] = UIFactory.Star(starsRow, "Star" + k, new Vector2(0.5f, 0.5f), new Vector2((k - 1) * 92f, k == 1 ? 6 : 0), k == 1 ? 80 : 70, false);

                var lockRt = UIFactory.Stretch(b.transform, "Lock");
                UIFactory.Rounded(lockRt, new Color(0.1f, 0.14f, 0.28f, 0.78f), 450 * 0.34f).raycastTarget = false;
                UIFactory.Icon(lockRt, "LockIcon", cfg.lockIcon, new Vector2(0.5f, 0.5f), new Vector2(0, 70), 170);
                var lockText = UIFactory.Rect(lockRt, "Hint", new Vector2(0.5f, 0f), new Vector2(0, 220), new Vector2(420, 70));
                UIFactory.Label(lockText, "T", "Finish level " + i + "!", 50, Color.white, TextAlignmentOptions.Center, true);
                c.lockOverlay = lockRt.gameObject;
                s.cards[i] = c;
            }

            var champ = UIFactory.Panel(root, "Champion", new Vector2(0.5f, 0f), new Vector2(0, 70), new Vector2(720, 96), Palette.Gold, -1);
            UIFactory.Icon(champ.transform, "Trophy", cfg.trophy, new Vector2(0f, 0.5f), new Vector2(64, 4), 80);
            var ct = UIFactory.Label(champ.transform, "T", "Capacity Champion!", 58, Color.white, TextAlignmentOptions.Center, true);
            ct.rectTransform.offsetMin = new Vector2(90, 0);
            s.champion = champ.transform.parent.gameObject;
            return s;
        }
    }
}
