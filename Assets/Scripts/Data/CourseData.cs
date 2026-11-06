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

        /// <summary>Set for words added from the Armenian texts: their key is "d:" + the word.</summary>
        [NonSerialized] public string KeyOverride;

        public string AudioId => "w_" + id;
        public string Key => KeyOverride ?? "w:" + id;
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

        /// <summary>A day of new words after the alphabet (Resources/Data/words.json): no new letters.</summary>
        public bool IsWordsDay => newLetters.Count == 0;
    }

    /// <summary>
    /// A story, poem or song in Armenian for those who know the alphabet. Every word can be tapped
    /// for its meaning (see <see cref="ArmenianDictionary"/>). Lines break at «\n», paragraphs and
    /// stanzas at an empty line.
    /// </summary>
    [Serializable]
    public class HyText
    {
        public string id;
        /// <summary>«Сказка», «Рассказ», «Стихи», «Песня».</summary>
        public string kind;
        public string title;
        public string author;
        /// <summary>Russian title and remark: retold or original, shortened, old spelling modernised.</summary>
        public string note;
        public string emoji;
        public string text;

        public string ReadKey => "hy:" + id;

        public static List<HyText> ListFromJson(string json) =>
            UnityEngine.JsonUtility.FromJson<HyTextList>(json).texts;
    }

    [Serializable]
    class HyTextList { public List<HyText> texts; }

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
        readonly Dictionary<string, WordData> _dictWords = new Dictionary<string, WordData>();

        /// <summary>Words of the Armenian texts; words added from them to training resolve through it.</summary>
        public ArmenianDictionary Dictionary;

        public Course(List<LetterData> letters, List<LessonData> lessons)
        {
            Letters = letters;
            Lessons = lessons.OrderBy(l => l.day).ToList();
            _byId = letters.ToDictionary(l => l.id);
        }

        /// <summary>Alphabet, its lessons, and optionally the word days that follow (words.json).</summary>
        public static Course FromJson(string lettersJson, string lessonsJson, string wordsJson = null)
        {
            var letters = UnityEngine.JsonUtility.FromJson<LetterList>(lettersJson).letters;
            var lessons = UnityEngine.JsonUtility.FromJson<LessonList>(lessonsJson).lessons;
            if (wordsJson != null)
                lessons.AddRange(UnityEngine.JsonUtility.FromJson<LessonList>(wordsJson).lessons);
            return new Course(letters, lessons);
        }

        /// <summary>Last day that teaches letters: after it the whole alphabet is known.</summary>
        public int AlphabetDays => Lessons.Where(l => !l.IsWordsDay).Select(l => l.day).DefaultIfEmpty(0).Max();

        public LetterData Letter(string id) => _byId[id];

        public LessonData Lesson(int day) => Lessons.FirstOrDefault(l => l.day == day);

        /// <summary>Day on which a letter is taught, or 0 if no lesson teaches it yet.</summary>
        public int DayOf(LetterData letter) =>
            Lessons.FirstOrDefault(l => l.newLetters.Contains(letter.id))?.day ?? 0;

        /// <summary>Letter or word by progress key ("l:a", "w:sar"); null if it no longer exists.</summary>
        public object ByKey(string key)
        {
            if (key.StartsWith("l:")) return _byId.TryGetValue(key.Substring(2), out var l) ? l : null;
            if (key.StartsWith("d:")) return DictWord(key.Substring(2));
            var id = key.Substring(2);
            return Lessons.SelectMany(x => x.words).FirstOrDefault(w => w.id == id);
        }

        /// <summary>A dictionary word as a word to train (one instance per word); null if unknown.</summary>
        public WordData DictWord(string hy)
        {
            if (_dictWords.TryGetValue(hy, out var w)) return w;
            var entry = Dictionary?.Entry(hy);
            if (entry == null) return null;
            w = new WordData { id = "d_" + hy, hy = hy, ru = entry.ru, tr = ArmenianText.Transcribe(hy), KeyOverride = entry.Key };
            _dictWords[hy] = w;
            return w;
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
