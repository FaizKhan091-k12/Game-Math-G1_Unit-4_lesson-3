using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FillItUp
{
    public class PauseScreen : UIScreen
    {
        public Button resume, sound, restart, home;
        public Image soundIcon;
        public TMP_Text soundLabel;

        public void Refresh(GameConfig cfg)
        {
            bool on = SaveSystem.Data.soundOn;
            soundIcon.sprite = on ? cfg.speakerOn : cfg.speakerOff;
            soundLabel.text = on ? "Sound: ON" : "Sound: OFF";
        }

        public static PauseScreen Build(Transform canvas, GameConfig cfg)
        {
            var root = UIFactory.Stretch(canvas, "PauseScreen");
            root.gameObject.AddComponent<CanvasGroup>();
            var s = root.gameObject.AddComponent<PauseScreen>();
            UIFactory.Dim(root, 0.5f);

            var panel = UIFactory.Panel(root, "Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 800), Palette.Paper, 60);
            s.content = (RectTransform)panel.transform.parent;
            var p = panel.transform;
            var head = UIFactory.Panel(p, "Header", new Vector2(0.5f, 1f), new Vector2(0, 10), new Vector2(460, 130), Palette.Purple, 65);
            UIFactory.Label(head.transform, "T", "Paused", 84, Color.white, TextAlignmentOptions.Center, true);

            var size = new Vector2(520, 130);
            s.resume = UIFactory.Button(p, "Resume", "Play on!", new Vector2(0.5f, 1f), new Vector2(0, -210), size, Palette.Green, Palette.GreenLip, 66, cfg.play);
            s.sound = UIFactory.Button(p, "Sound", "Sound: ON", new Vector2(0.5f, 1f), new Vector2(0, -370), size, Palette.Blue, Palette.BlueLip, 60, cfg.speakerOn);
            s.soundIcon = s.sound.transform.Find("Face/Icon").GetComponent<Image>();
            s.soundLabel = s.sound.transform.Find("Face/Label").GetComponent<TMP_Text>();
            s.restart = UIFactory.Button(p, "Restart", "Restart", new Vector2(0.5f, 1f), new Vector2(0, -530), size, Palette.Orange, Palette.OrangeLip, 66, cfg.replay);
            s.home = UIFactory.Button(p, "Home", "Menu", new Vector2(0.5f, 1f), new Vector2(0, -690), size, Palette.Pink, Palette.PinkLip, 66, cfg.home);
            return s;
        }
    }
}
