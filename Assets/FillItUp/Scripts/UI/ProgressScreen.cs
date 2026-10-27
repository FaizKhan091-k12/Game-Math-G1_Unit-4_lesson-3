using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FillItUp
{
    /// <summary>The student's progress: stars and best score per level, totals, recent attempts, champion badge.</summary>
    public class ProgressScreen : UIScreen
    {
        [System.Serializable]
        public class Row
        {
            public Image[] stars = new Image[3];
            public TMP_Text detail;
        }

        public Button close, reset, confirmYes, confirmNo;
        public Row[] rows = new Row[3];
        public TMP_Text droplets, accuracy, questions, time, recent, championText;
        public Image championIcon;
        public GameObject confirm;

        public void Refresh()
        {
            var d = SaveSystem.Data;
            for (int i = 0; i < 3; i++)
            {
                var l = d.levels[i];
                for (int s = 0; s < 3; s++)
                    rows[i].stars[s].color = s < l.bestStars ? Palette.Gold : new Color(0.12f, 0.2f, 0.35f, 0.18f);
                rows[i].detail.text = !d.IsUnlocked(i) ? "Locked" :
                    l.completions == 0 ? (l.plays > 0 ? "Not finished yet" : "Not played yet") :
                    "Best " + l.bestScore + "/" + Levels.QuestionsPerLevel + "   Played " + l.completions + "x";
            }
            droplets.text = d.droplets.ToString();
            accuracy.text = SaveSystem.AccuracyPercent + "%";
            questions.text = SaveSystem.QuestionsAnswered.ToString();
            time.text = UIFactory.FormatTime(d.secondsPlayed);
            championText.text = d.champion ? "Capacity Champion!" : "Finish all 3 levels!";
            championIcon.color = d.champion ? Color.white : new Color(1, 1, 1, 0.35f);

            var sb = new StringBuilder();
            int shown = 0;
            for (int i = d.history.Count - 1; i >= 0 && shown < 3; i--, shown++)
            {
                var h = d.history[i];
                if (shown > 0) sb.Append("     ");
                sb.Append("Lv").Append(h.level).Append(": ").Append(h.score).Append('/').Append(h.outOf);
            }
            recent.text = shown == 0 ? "Recent: play a level!" : "Recent:  " + sb;
            confirm.SetActive(false);
        }

        public void AskReset()
        {
            confirm.SetActive(true);
            var rt = (RectTransform)confirm.transform.Find("Box");
            rt.localScale = Vector3.zero;
            rt.DOScale(1f, 0.3f).SetEase(Ease.OutBack);
        }

        static TMP_Text Tile(Transform parent, string name, Sprite icon, string caption, Vector2 pos, Color color)
        {
            var face = UIFactory.Panel(parent, name, new Vector2(0.5f, 1f), pos, new Vector2(250, 190), Palette.PaperBlue, 36, false);
            if (icon != null) UIFactory.Icon(face.transform, "Icon", icon, new Vector2(0.5f, 1f), new Vector2(0, -42), 60);
            var v = UIFactory.Rect(face.transform, "Value", new Vector2(0.5f, 0.5f), new Vector2(0, -12), new Vector2(240, 70));
            var value = UIFactory.Label(v, "T", "0", 62, color);
            var c = UIFactory.Rect(face.transform, "Caption", new Vector2(0.5f, 0f), new Vector2(0, 26), new Vector2(240, 40));
            UIFactory.Label(c, "T", caption, 32, Palette.InkSoft);
            return value;
        }

        public static ProgressScreen Build(Transform canvas, GameConfig cfg)
        {
            var root = UIFactory.Stretch(canvas, "ProgressScreen");
            root.gameObject.AddComponent<CanvasGroup>();
            var s = root.gameObject.AddComponent<ProgressScreen>();
            UIFactory.Dim(root, 0.45f);

            var panel = UIFactory.Panel(root, "Panel", new Vector2(0.5f, 0.5f), new Vector2(-20, -10), new Vector2(1560, 900), Palette.Paper, 60);
            s.content = (RectTransform)panel.transform.parent;
            var p = panel.transform;

            var head = UIFactory.Panel(p, "Header", new Vector2(0.5f, 1f), new Vector2(0, 10), new Vector2(640, 130), Palette.Purple, 65);
            UIFactory.Label(head.transform, "T", "My Progress", 84, Color.white, TextAlignmentOptions.Center, true);
            s.close = UIFactory.RoundButton(p, "Close", cfg.close, new Vector2(1f, 1f), new Vector2(-20, -20), 120, Palette.Pink, Palette.PinkLip);

            // level rows
            var icons = new[] { cfg.level1Icon, cfg.level2Icon, cfg.level3Icon };
            for (int i = 0; i < 3; i++)
            {
                var row = new Row();
                var face = UIFactory.Panel(p, "Level" + (i + 1), new Vector2(0f, 1f), new Vector2(520, -200 - i * 200f), new Vector2(920, 170), Palette.PaperBlue, 40, false);
                var chip = UIFactory.Rect(face.transform, "Chip", new Vector2(0f, 0.5f), new Vector2(95, 0), new Vector2(140, 140));
                UIFactory.Rounded(chip, Palette.LevelColors[i], 36).raycastTarget = false;
                UIFactory.Icon(chip, "Icon", icons[i], new Vector2(0.5f, 0.5f), Vector2.zero, 110);
                var title = UIFactory.Rect(face.transform, "Title", new Vector2(0f, 1f), new Vector2(190, -48), new Vector2(420, 70), new Vector2(0f, 0.5f));
                UIFactory.Label(title, "T", (i + 1) + ". " + Levels.Defs[i].title, 54, Palette.Ink, TextAlignmentOptions.Left);
                var det = UIFactory.Rect(face.transform, "Detail", new Vector2(0f, 0f), new Vector2(190, 48), new Vector2(460, 56), new Vector2(0f, 0.5f));
                row.detail = UIFactory.Label(det, "T", "Not played yet", 38, Palette.InkSoft, TextAlignmentOptions.Left);
                for (int k = 0; k < 3; k++)
                    row.stars[k] = UIFactory.Star(face.transform, "Star" + k, new Vector2(1f, 0.5f), new Vector2(-250 + k * 92f, k == 1 ? 6 : 0), k == 1 ? 92 : 80, false);
                s.rows[i] = row;
            }

            // totals
            s.droplets = Tile(p, "Droplets", cfg.droplet, "droplets", new Vector2(370, -230), Palette.Hex(0x1C76C2));
            s.accuracy = Tile(p, "Accuracy", cfg.check, "right first try", new Vector2(640, -230), Palette.GreenLip);
            s.questions = Tile(p, "Questions", cfg.chart, "questions", new Vector2(370, -440), Palette.OrangeLip);
            s.time = Tile(p, "Time", cfg.play, "time played", new Vector2(640, -440), Palette.PurpleLip);

            var champ = UIFactory.Panel(p, "Champion", new Vector2(0.5f, 1f), new Vector2(505, -660), new Vector2(520, 110), Palette.Gold, -1, false);
            s.championIcon = UIFactory.Icon(champ.transform, "Trophy", cfg.trophy, new Vector2(0f, 0.5f), new Vector2(66, 2), 84);
            s.championText = UIFactory.Label(champ.transform, "T", "Finish all 3 levels!", 44, Color.white, TextAlignmentOptions.Center, true);
            s.championText.rectTransform.offsetMin = new Vector2(110, 0);

            var rec = UIFactory.Rect(p, "Recent", new Vector2(0f, 0f), new Vector2(70, 95), new Vector2(1000, 60), new Vector2(0f, 0.5f));
            s.recent = UIFactory.Label(rec, "T", "Recent:", 40, Palette.InkSoft, TextAlignmentOptions.Left);

            s.reset = UIFactory.Button(p, "Reset", "Reset", new Vector2(1f, 0f), new Vector2(-150, 95), new Vector2(220, 90), Palette.Grey, Palette.GreyLip, 46);
            s.reset.GetComponent<UIButtonFX>().burst = false;

            // confirm popup
            var conf = UIFactory.Stretch(p, "Confirm");
            var dim = conf.gameObject.AddComponent<Image>();
            dim.color = new Color(0.05f, 0.1f, 0.2f, 0.4f);
            var box = UIFactory.Panel(conf, "Box", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 420), Palette.Paper, 50);
            var q = UIFactory.Rect(box.transform, "Question", new Vector2(0.5f, 1f), new Vector2(0, -120), new Vector2(760, 150));
            var qt = UIFactory.Label(q, "T", "Start over?\n<size=40>All stars and droplets will be cleared.</size>", 70, Palette.Ink);
            qt.textWrappingMode = TextWrappingModes.Normal;
            s.confirmYes = UIFactory.Button(box.transform, "Yes", "Yes", new Vector2(0.5f, 0f), new Vector2(-170, 100), new Vector2(280, 120), Palette.Wrong, Palette.PinkLip, 60);
            s.confirmNo = UIFactory.Button(box.transform, "No", "No", new Vector2(0.5f, 0f), new Vector2(170, 100), new Vector2(280, 120), Palette.Green, Palette.GreenLip, 60);
            s.confirm = conf.gameObject;
            box.transform.parent.name = "Box";
            conf.gameObject.SetActive(false);
            return s;
        }
    }
}
