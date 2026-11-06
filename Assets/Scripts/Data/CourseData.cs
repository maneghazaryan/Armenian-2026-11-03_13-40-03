using System;
using System.Collections.Generic;
using System.Linq;

namespace Aib.Data
{
    [Serializable]
    public class LetterData
    {
        public string id;
        public string upper;
        public string lower;
        /// <summary>Armenian letter name (used for audio); learners see <see cref="nameRu"/>.</summary>
        public string name;
        /// <summary>Letter name written in Russian, e.g. «айб».</summary>
        public string nameRu;
        public string ru;
        /// <summary>Shape association linking the glyph (small and capital) to its sound.</summary>
        public string look;
        public string hint;
        public string warn;
        /// <summary>Optional deeper explanation (Russian with English comparisons).</summary>
        public string more;

        public string AudioId => "l_" + id;
        public string Key => "l:" + id;
    }

    [Serializable]
    public class WordData
    {
        public string id;
        public string hy;
        public string tr;
        public string ru;
        public string emoji;
        /// <summary>Optional remark shown with the meaning, e.g. a second meaning.</summary>
        public string note;

        public string AudioId => "w_" + id;
        public string Key => "w:" + id;
    }

    [Serializable]
    public class LessonData
    {
        public int day;
        public string title;
        public string intro;
        public List<string> newLetters = new List<string>();
        public List<WordData> words = new List<WordData>();
        public List<WordData> phrases = new List<WordData>();
        /// <summary>Short rhymes/verses (hy may contain line breaks).</summary>
        public List<WordData> verses = new List<WordData>();
        /// <summary>Russian sentences shown with the known sounds written in Armenian letters.</summary>
        public List<string> texts = new List<string>();
    }

    /// <summary>A longer Russian text (an interesting fact) read with the learned sounds in Armenian letters.</summary>
    [Serializable]
    public class ReadingText
    {
        public string id;
        public string topic;
        public string emoji;
        public string title;
        public string text;

        public static List<ReadingText> ListFromJson(string json) =>
            UnityEngine.JsonUtility.FromJson<ReadingList>(json).texts;
    }

    [Serializable]
    class ReadingList { public List<ReadingText> texts; }

    [Serializable]
    class LetterList { public List<LetterData> letters; }

    [Serializable]
    class LessonList { public List<LessonData> lessons; }

    /// <summary>The whole course: alphabet plus day-by-day lessons.</summary>
    public class Course
    {
        public readonly List<LetterData> Letters;
        public readonly List<LessonData> Lessons;
        readonly Dictionary<string, LetterData> _byId;

        public Course(List<LetterData> letters, List<LessonData> lessons)
        {
            Letters = letters;
            Lessons = lessons.OrderBy(l => l.day).ToList();
            _byId = letters.ToDictionary(l => l.id);
        }

        public static Course FromJson(string lettersJson, string lessonsJson)
        {
            var letters = UnityEngine.JsonUtility.FromJson<LetterList>(lettersJson).letters;
            var lessons = UnityEngine.JsonUtility.FromJson<LessonList>(lessonsJson).lessons;
            return new Course(letters, lessons);
        }

        public LetterData Letter(string id) => _byId[id];

        public LessonData Lesson(int day) => Lessons.FirstOrDefault(l => l.day == day);

        /// <summary>Day on which a letter is taught, or 0 if no lesson teaches it yet.</summary>
        public int DayOf(LetterData letter) =>
            Lessons.FirstOrDefault(l => l.newLetters.Contains(letter.id))?.day ?? 0;

        /// <summary>Letter or word by progress key ("l:a", "w:sar"); null if it no longer exists.</summary>
        public object ByKey(string key)
        {
            if (key.StartsWith("l:")) return _byId.TryGetValue(key.Substring(2), out var l) ? l : null;
            var id = key.Substring(2);
            return Lessons.SelectMany(x => x.words).FirstOrDefault(w => w.id == id);
        }

        /// <summary>Letters introduced on days 1..day, in teaching order.</summary>
        public List<LetterData> LettersUpTo(int day) =>
            Lessons.Where(l => l.day <= day).SelectMany(l => l.newLetters).Select(Letter).ToList();

        /// <summary>Small letters readable after days 1..day; ու also makes its hook ւ known.</summary>
        public HashSet<string> GlyphsUpTo(int day)
        {
            var set = LettersUpTo(day).Select(l => l.lower).ToHashSet();
            if (set.Contains("ու")) set.Add("ւ");
            return set;
        }

        /// <summary>Words (not phrases) taught on days 1..day.</summary>
        public List<WordData> WordsUpTo(int day) =>
            Lessons.Where(l => l.day <= day).SelectMany(l => l.words).ToList();
    }
}
