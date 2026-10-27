using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FillItUp
{
    public class MenuScreen : UIScreen
    {
        public Button play, sound, progress;
        public Image soundIcon;
        public TMP_Text starsLabel;

        public void Refresh(GameConfig cfg)
        {
            starsLabel.text = SaveSystem.Data.TotalStars + "/9";
            soundIcon.sprite = SaveSystem.Data.soundOn ? cfg.speakerOn : cfg.speakerOff;
        }

        public static MenuScreen Build(Transform canvas, GameConfig cfg)
        {
            var root = UIFactory.Stretch(canvas, "MenuScreen");
            root.gameObject.AddComponent<CanvasGroup>();
            var s = root.gameObject.AddComponent<MenuScreen>();

            var title = UIFactory.Rect(root, "Title", new Vector2(0.5f, 1f), new Vector2(0, -175), new Vector2(1300, 230));
            var t = UIFactory.Label(title, "Text", "Fill It Up!", 196, Color.white, TextAlignmentOptions.Center, true);
            t.enableVertexGradient = true;
            t.colorGradient = new VertexGradient(Color.white, Color.white, Palette.Hex(0xB8ECFF), Palette.Hex(0xB8ECFF));
            t.characterSpacing = 6f;
            t.wordSpacing = 12f;
            t.gameObject.AddComponent<TextWobble>();

            var tag = UIFactory.Panel(root, "Tagline", new Vector2(0.5f, 1f), new Vector2(0, -330), new Vector2(640, 86), Palette.Paper, -1);
            UIFactory.Label(tag.transform, "Text", "Which one holds more?", 46, Palette.Ink);

            s.play = UIFactory.Button(root, "PlayButton", "PLAY", new Vector2(0.5f, 0f), new Vector2(0, 250), new Vector2(480, 165),
                Palette.Green, Palette.GreenLip, 92, cfg.play);
            s.play.GetComponent<UIButtonFX>().idlePulse = true;

            s.sound = UIFactory.RoundButton(root, "SoundButton", cfg.speakerOn, new Vector2(1f, 1f), new Vector2(-105, -100), 132, Palette.Blue, Palette.BlueLip);
            s.soundIcon = s.sound.transform.Find("Face/Icon").GetComponent<Image>();
            s.progress = UIFactory.RoundButton(root, "ProgressButton", cfg.trophy, new Vector2(1f, 1f), new Vector2(-260, -100), 132, Palette.Purple, Palette.PurpleLip);

            var chip = UIFactory.Panel(root, "StarsChip", new Vector2(0f, 1f), new Vector2(175, -100), new Vector2(250, 104), Palette.Paper, -1);
            UIFactory.Icon(chip.transform, "Star", cfg.star, new Vector2(0f, 0.5f), new Vector2(62, 2), 76, Palette.Gold);
            s.starsLabel = UIFactory.Label(chip.transform, "Count", "0/9", 58, Palette.Ink, TextAlignmentOptions.Center);
            s.starsLabel.rectTransform.offsetMin = new Vector2(100, 0);
            return s;
        }
    }
}
