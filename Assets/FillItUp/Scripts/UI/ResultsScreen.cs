using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.ProceduralImage;

namespace FillItUp
{
    public class ResultsScreen : UIScreen
    {
        public Button next, replay, map;
        public TMP_Text header, score, droplets, unlocked, nextLabel;
        public ProceduralImage headerFace;
        public Image[] stars = new Image[3];
        public RectTransform newBest, champion;

        public IEnumerator Play(int level, int score, int outOf, int starCount, bool isNewBest, bool unlockedNext, bool hasNext)
        {
            header.text = starCount == 3 ? "Perfect!" : "Level Complete!";
            header.fontSize = 84;
            header.rectTransform.offsetMin = Vector2.zero;
            headerFace.color = Palette.LevelColors[level];
            this.score.text = score + " / " + outOf + " right";
            droplets.text = "+" + score;
            unlocked.gameObject.SetActive(unlockedNext);
            if (unlockedNext) unlocked.text = "Level " + (level + 2) + " is open!";
            newBest.gameObject.SetActive(false);
            champion.gameObject.SetActive(false);
            nextLabel.text = hasNext ? "NEXT" : "MENU";
            foreach (var s in stars) { s.transform.localScale = Vector3.zero; s.transform.localRotation = Quaternion.identity; }
            SetButtons(false);
            Show();
            yield return new WaitForSeconds(0.55f);

            for (int i = 0; i < starCount; i++)
            {
                var st = stars[i].transform;
                st.localScale = Vector3.one * 2.4f;
                st.localRotation = Quaternion.Euler(0, 0, -40f);
                var seq = DOTween.Sequence();
                seq.Append(st.DOScale(1f, 0.3f).SetEase(Ease.InQuad));
                seq.Join(st.DORotate(Vector3.zero, 0.3f));
                yield return seq.WaitForCompletion();
                AudioManager.I?.Play(Sfx.StarStamp, 1f + i * 0.12f);
                UIBurst.Play((RectTransform)st, Palette.Gold, 12, 170f);
                st.DOPunchScale(Vector3.one * 0.25f, 0.3f, 6, 0.6f);
                yield return new WaitForSeconds(0.18f);
            }
            if (isNewBest)
            {
                newBest.gameObject.SetActive(true);
                newBest.localScale = Vector3.zero;
                newBest.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
                AudioManager.I?.Play(Sfx.Sparkle);
            }
            yield return new WaitForSeconds(0.25f);
            SetButtons(true);
        }

        /// <summary>Gold "Capacity Champion" ribbon over the header (first time all three levels are done).</summary>
        public void ShowChampion()
        {
            // the header turns into a gold champion banner with a trophy
            var head = (RectTransform)headerFace.transform.parent;
            headerFace.color = Palette.Gold;
            header.text = "Capacity Champion!";
            header.fontSize = 66;
            header.rectTransform.offsetMin = new Vector2(110, 0);
            champion.gameObject.SetActive(true);
            head.DOKill(true);
            head.localScale = Vector3.one * 1.6f;
            head.DOScale(1f, 0.45f).SetEase(Ease.OutBack);
            head.DOPunchRotation(new Vector3(0, 0, 8f), 0.6f, 8).SetDelay(0.45f);
            UIBurst.Play(head, Palette.Gold, 16, 280f);
        }

        void SetButtons(bool on)
        {
            foreach (var b in new[] { next, replay, map })
            {
                b.interactable = on;
                b.transform.DOKill();
                b.transform.DOScale(on ? 1f : 0f, on ? 0.35f : 0f).SetEase(Ease.OutBack);
            }
        }

        public static ResultsScreen Build(Transform canvas, GameConfig cfg)
        {
            var root = UIFactory.Stretch(canvas, "ResultsScreen");
            root.gameObject.AddComponent<CanvasGroup>();
            var s = root.gameObject.AddComponent<ResultsScreen>();
            UIFactory.Dim(root, 0.15f);

            var panel = UIFactory.Panel(root, "Panel", new Vector2(0.5f, 0.5f), new Vector2(330, -40), new Vector2(900, 820), Palette.Paper, 60);
            s.content = (RectTransform)panel.transform.parent;
            var p = panel.transform;

            s.headerFace = UIFactory.Panel(p, "Header", new Vector2(0.5f, 1f), new Vector2(0, 10), new Vector2(700, 140), Palette.LevelColors[0], 70);
            s.header = UIFactory.Label(s.headerFace.transform, "T", "Level Complete!", 84, Color.white, TextAlignmentOptions.Center, true);

            var starRow = UIFactory.Rect(p, "Stars", new Vector2(0.5f, 1f), new Vector2(0, -250), new Vector2(700, 230));
            for (int i = 0; i < 3; i++)
            {
                float size = i == 1 ? 230 : 190;
                var pos = new Vector2((i - 1) * 230f, i == 1 ? 25 : -10);
                UIFactory.Star(starRow, "Slot" + i, new Vector2(0.5f, 0.5f), pos, size, false);
                s.stars[i] = UIFactory.Star(starRow, "Star" + i, new Vector2(0.5f, 0.5f), pos, size, true);
            }

            var sc = UIFactory.Rect(p, "Score", new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(800, 100));
            s.score = UIFactory.Label(sc, "T", "5 / 5 right", 82, Palette.Ink);

            var drop = UIFactory.Panel(p, "Droplets", new Vector2(0.5f, 0.5f), new Vector2(0, -88), new Vector2(240, 84), Palette.PaperBlue, -1, false);
            UIFactory.Icon(drop.transform, "Drop", cfg.droplet, new Vector2(0f, 0.5f), new Vector2(50, 2), 64);
            s.droplets = UIFactory.Label(drop.transform, "T", "+5", 56, Palette.Hex(0x1C76C2));
            s.droplets.rectTransform.offsetMin = new Vector2(70, 0);

            var un = UIFactory.Rect(p, "Unlocked", new Vector2(0.5f, 0.5f), new Vector2(0, -160), new Vector2(800, 60));
            s.unlocked = UIFactory.Label(un, "T", "Level 2 is open!", 50, Palette.Purple);

            var nb = UIFactory.Panel(p, "NewBest", new Vector2(1f, 1f), new Vector2(-40, -70), new Vector2(230, 80), Palette.Pink, -1);
            s.newBest = (RectTransform)nb.transform.parent;
            s.newBest.localRotation = Quaternion.Euler(0, 0, -12f);
            UIFactory.Label(nb.transform, "T", "New best!", 46, Color.white, TextAlignmentOptions.Center, true);

            // trophy shown inside the header when the student becomes Capacity Champion
            s.champion = UIFactory.Icon(s.headerFace.transform, "Trophy", cfg.trophy, new Vector2(0f, 0.5f), new Vector2(80, 4), 100).rectTransform;
            s.champion.gameObject.SetActive(false);

            s.replay = UIFactory.RoundButton(p, "Replay", cfg.replay, new Vector2(0.5f, 0f), new Vector2(-300, 95), 136, Palette.Orange, Palette.OrangeLip);
            s.map = UIFactory.RoundButton(p, "Map", cfg.home, new Vector2(0.5f, 0f), new Vector2(-140, 95), 136, Palette.Blue, Palette.BlueLip);
            s.next = UIFactory.Button(p, "Next", "NEXT", new Vector2(0.5f, 0f), new Vector2(160, 95), new Vector2(380, 140), Palette.Green, Palette.GreenLip, 72, cfg.next);
            s.next.GetComponent<UIButtonFX>().idlePulse = true;
            s.nextLabel = s.next.transform.Find("Face/Label").GetComponent<TMP_Text>();
            return s;
        }
    }
}
