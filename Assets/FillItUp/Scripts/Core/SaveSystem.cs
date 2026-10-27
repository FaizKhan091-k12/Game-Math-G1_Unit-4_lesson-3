using System;
using System.Collections.Generic;
using UnityEngine;

namespace FillItUp
{
    [Serializable]
    public class LevelProgress
    {
        public int bestStars;
        public int bestScore;
        public int lastScore = -1;
        public int plays;          // started
        public int completions;    // finished all questions
        public int questions;      // answered in total
        public int firstTryCorrect;
    }

    [Serializable]
    public class AttemptRecord
    {
        public int level;
        public int score;
        public int outOf;
        public int stars;
        public int seconds;
        public string date;
    }

    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public bool soundOn = true;
        public LevelProgress[] levels = { new LevelProgress(), new LevelProgress(), new LevelProgress() };
        public int droplets;
        public float secondsPlayed;
        public int sessions;
        public bool champion;
        public string firstPlayed;
        public string lastPlayed;
        public List<AttemptRecord> history = new List<AttemptRecord>();

        public int TotalStars
        {
            get { int s = 0; foreach (var l in levels) s += l.bestStars; return s; }
        }

        public bool IsUnlocked(int level) => level == 0 || levels[level - 1].completions > 0;
    }

    /// <summary>
    /// Student progress, kept as JSON in PlayerPrefs (IndexedDB on WebGL, so it survives page reloads).
    /// Level results are also handed to the web page (see WebBridge) so an LMS wrapper can pick them up.
    /// </summary>
    public static class SaveSystem
    {
        const string Key = "FillItUp.Save.v1";
        const int MaxHistory = 30;

        public static SaveData Data { get; private set; }

        public static void Load()
        {
            Data = null;
            try
            {
                if (PlayerPrefs.HasKey(Key)) Data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(Key));
            }
            catch (Exception e) { Debug.LogWarning("[FillItUp] Save data unreadable, starting fresh: " + e.Message); }
            if (Data == null) Data = new SaveData();
            if (Data.levels == null || Data.levels.Length != 3)
                Data.levels = new[] { new LevelProgress(), new LevelProgress(), new LevelProgress() };
            if (Data.history == null) Data.history = new List<AttemptRecord>();
            if (string.IsNullOrEmpty(Data.firstPlayed)) Data.firstPlayed = Today();
            Data.sessions++;
            Data.lastPlayed = Today();
            Save();
        }

        public static void Save()
        {
            if (Data == null) return;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
        }

        public static void Reset()
        {
            bool sound = Data != null && Data.soundOn;
            Data = new SaveData { soundOn = sound, sessions = 1, firstPlayed = Today(), lastPlayed = Today() };
            Save();
        }

        public static void LevelStarted(int level)
        {
            Data.levels[level].plays++;
            Save();
        }

        public static void QuestionAnswered(int level, bool firstTry)
        {
            var l = Data.levels[level];
            l.questions++;
            if (firstTry) { l.firstTryCorrect++; Data.droplets++; }
            Save();
        }

        /// <summary>Records a finished level. Returns true when it unlocked the next level for the first time.</summary>
        public static bool LevelFinished(int level, int score, int outOf, int stars, float seconds, out bool newBest)
        {
            var l = Data.levels[level];
            bool firstCompletion = l.completions == 0;
            newBest = score > l.bestScore || stars > l.bestStars;
            l.completions++;
            l.lastScore = score;
            l.bestScore = Mathf.Max(l.bestScore, score);
            l.bestStars = Mathf.Max(l.bestStars, stars);
            Data.history.Add(new AttemptRecord
            {
                level = level + 1, score = score, outOf = outOf, stars = stars,
                seconds = Mathf.RoundToInt(seconds), date = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            });
            while (Data.history.Count > MaxHistory) Data.history.RemoveAt(0);
            Save();
            WebBridge.ReportLevel(level + 1, score, outOf, stars, seconds, Data);
            return firstCompletion && level < 2;
        }

        public static void AddPlayTime(float seconds)
        {
            Data.secondsPlayed += seconds;
        }

        public static int AccuracyPercent
        {
            get
            {
                int q = 0, c = 0;
                foreach (var l in Data.levels) { q += l.questions; c += l.firstTryCorrect; }
                return q == 0 ? 0 : Mathf.RoundToInt(100f * c / q);
            }
        }

        public static int QuestionsAnswered
        {
            get { int q = 0; foreach (var l in Data.levels) q += l.questions; return q; }
        }

        static string Today() => DateTime.Now.ToString("yyyy-MM-dd");
    }
}
