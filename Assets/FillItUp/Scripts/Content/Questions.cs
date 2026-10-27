using System.Collections.Generic;
using UnityEngine;

namespace FillItUp
{
    public enum QuestionType { More, Less, Most, Least, Pour }

    public struct Option
    {
        public ContainerKind kind;
        public float size; // extra scale for same-kind questions (capacity grows with size cubed)
        public float Capacity => ContainerCatalog.Millilitres(kind) * size * size * size;
    }

    public class Question
    {
        public QuestionType type;
        public readonly List<Option> options = new List<Option>();
        public int answer;          // option index; for Pour: 0 = fits, 1 = spills
        public bool sameKind;

        public string Prompt
        {
            get
            {
                switch (type)
                {
                    case QuestionType.More: return "Which holds <color=#FF8A00>more</color>?";
                    case QuestionType.Less: return "Which holds <color=#FF8A00>less</color>?";
                    case QuestionType.Most: return "Which holds <color=#FF8A00>the most</color>?";
                    case QuestionType.Least: return "Which holds <color=#FF8A00>the least</color>?";
                    default: return "Will the water <color=#1C9BEF>fit</color> or <color=#FF8A00>spill</color>?";
                }
            }
        }

        public string Voice
        {
            get
            {
                switch (type)
                {
                    case QuestionType.More: return "q_more";
                    case QuestionType.Less: return "q_less";
                    case QuestionType.Most: return "q_most";
                    case QuestionType.Least: return "q_least";
                    default: return "q_pour";
                }
            }
        }

        public string WhyVoice
        {
            get
            {
                switch (type)
                {
                    case QuestionType.More: return "why_more";
                    case QuestionType.Less: return "why_less";
                    case QuestionType.Most: return "why_most";
                    case QuestionType.Least: return "why_least";
                    default: return answer == 0 ? "fits" : "spills";
                }
            }
        }
    }

    public class LevelDef
    {
        public string title, subtitle, introVoice;
        public int questions;
    }

    public static class Levels
    {
        public const int Count = 3;
        public const int QuestionsPerLevel = 5;

        public static readonly LevelDef[] Defs =
        {
            new LevelDef { title = "More or Less", subtitle = "Pick from two!", introVoice = "l1_intro", questions = QuestionsPerLevel },
            new LevelDef { title = "Most or Least", subtitle = "Pick from three!", introVoice = "l2_intro", questions = QuestionsPerLevel },
            new LevelDef { title = "Magic Water", subtitle = "Fit or spill?", introVoice = "l3_intro", questions = QuestionsPerLevel },
        };

        public static int StarsFor(int score, int outOf)
        {
            if (score >= outOf) return 3;
            if (score >= outOf - 2) return 2;
            return 1;
        }
    }

    /// <summary>Builds a fresh, varied set of questions for a level. Every comparison is clear-cut for a 6-year-old.</summary>
    public static class QuestionFactory
    {
        const float DifferentKindRatio = 6f;  // level 1: very different containers
        const float ThreeWayRatio = 3f;       // level 2: neighbours differ at least 3x
        const float PourRatio = 3f;

        static readonly List<ContainerKind> pool = new List<ContainerKind>();

        public static List<Question> Make(int level)
        {
            pool.Clear();
            var list = new List<Question>();
            int n = Levels.Defs[level].questions;
            bool flip = Random.value < 0.5f;
            for (int i = 0; i < n; i++)
            {
                Question q;
                if (level == 0)
                {
                    var type = (i % 2 == 0) ^ flip ? QuestionType.More : QuestionType.Less;
                    q = i < 3 ? DifferentKinds(type, 2, DifferentKindRatio) : SameKind(type, new[] { 0.66f, 1f });
                }
                else if (level == 1)
                {
                    var type = (i % 2 == 0) ^ flip ? QuestionType.Most : QuestionType.Least;
                    q = i == 2 || i == 4 ? SameKind(type, new[] { 0.6f, 0.8f, 1.04f }) : DifferentKinds(type, 3, ThreeWayRatio);
                }
                else
                {
                    q = Pour(i, flip);
                }
                list.Add(q);
            }
            return list;
        }

        static ContainerKind Draw(System.Predicate<ContainerKind> ok)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var candidates = new List<ContainerKind>();
                foreach (var k in ContainerCatalog.All) if (!pool.Contains(k) && ok(k)) candidates.Add(k);
                if (candidates.Count > 0)
                {
                    var pick = candidates[Random.Range(0, candidates.Count)];
                    pool.Add(pick);
                    return pick;
                }
                pool.Clear(); // everything used: allow repeats
            }
            var all = new List<ContainerKind>();
            foreach (var k in ContainerCatalog.All) if (ok(k)) all.Add(k);
            return all.Count > 0 ? all[Random.Range(0, all.Count)] : ContainerKind.Bucket;
        }

        static Question DifferentKinds(QuestionType type, int count, float ratio)
        {
            for (int tries = 0; tries < 60; tries++)
            {
                var picked = new List<ContainerKind>();
                var savedPool = new List<ContainerKind>(pool);
                for (int i = 0; i < count; i++)
                {
                    var k = Draw(c =>
                    {
                        foreach (var p in picked)
                        {
                            float a = ContainerCatalog.Millilitres(c), b = ContainerCatalog.Millilitres(p);
                            if (Mathf.Max(a, b) / Mathf.Min(a, b) < ratio) return false;
                        }
                        return true;
                    });
                    picked.Add(k);
                }
                if (Valid(picked, ratio))
                {
                    var q = new Question { type = type };
                    foreach (var k in picked) q.options.Add(new Option { kind = k, size = 1f });
                    Shuffle(q.options);
                    q.answer = AnswerIndex(q);
                    return q;
                }
                pool.Clear();
                pool.AddRange(savedPool);
            }
            // fallback that always works
            var f = new Question { type = type };
            f.options.Add(new Option { kind = ContainerKind.Spoon, size = 1f });
            f.options.Add(new Option { kind = ContainerKind.Bucket, size = 1f });
            if (count == 3) f.options.Add(new Option { kind = ContainerKind.Bathtub, size = 1f });
            Shuffle(f.options);
            f.answer = AnswerIndex(f);
            return f;
        }

        static bool Valid(List<ContainerKind> picked, float ratio)
        {
            for (int i = 0; i < picked.Count; i++)
                for (int j = i + 1; j < picked.Count; j++)
                {
                    float a = ContainerCatalog.Millilitres(picked[i]), b = ContainerCatalog.Millilitres(picked[j]);
                    if (Mathf.Max(a, b) / Mathf.Min(a, b) < ratio) return false;
                }
            return true;
        }

        static Question SameKind(QuestionType type, float[] sizes)
        {
            // the pool, tub and tank are too wide to show three of at once
            var k = Draw(c => c != ContainerKind.Pool && c != ContainerKind.Bathtub && c != ContainerKind.FishTank && c != ContainerKind.Spoon);
            var q = new Question { type = type, sameKind = true };
            foreach (var s in sizes) q.options.Add(new Option { kind = k, size = s });
            Shuffle(q.options);
            q.answer = AnswerIndex(q);
            return q;
        }

        static Question Pour(int index, bool flip)
        {
            // alternate fits / spills, starting at random, so both outcomes appear
            bool spills = (index % 2 == 0) ^ flip;
            for (int tries = 0; tries < 80; tries++)
            {
                var a = Draw(ContainerCatalog.CanPour);
                var b = Draw(c => ContainerCatalog.CanPour(c) && c != a);
                float ca = ContainerCatalog.Millilitres(a), cb = ContainerCatalog.Millilitres(b);
                if (Mathf.Max(ca, cb) / Mathf.Min(ca, cb) < PourRatio) { pool.Remove(a); pool.Remove(b); if (tries > 40) pool.Clear(); continue; }
                // source spills when it holds more than the target
                var source = spills == (ca > cb) ? a : b;
                var target = source == a ? b : a;
                var q = new Question { type = QuestionType.Pour, answer = spills ? 1 : 0 };
                q.options.Add(new Option { kind = source, size = 1f });
                q.options.Add(new Option { kind = target, size = 1f });
                return q;
            }
            var f = new Question { type = QuestionType.Pour, answer = spills ? 1 : 0 };
            f.options.Add(new Option { kind = spills ? ContainerKind.Bucket : ContainerKind.Teacup, size = 1f });
            f.options.Add(new Option { kind = spills ? ContainerKind.Mug : ContainerKind.Pot, size = 1f });
            return f;
        }

        static int AnswerIndex(Question q)
        {
            bool wantMax = q.type == QuestionType.More || q.type == QuestionType.Most;
            int best = 0;
            for (int i = 1; i < q.options.Count; i++)
            {
                float c = q.options[i].Capacity, b = q.options[best].Capacity;
                if (wantMax ? c > b : c < b) best = i;
            }
            return best;
        }

        static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
