using System;
using System.Collections.Generic;
using System.Linq;
using Aib.Data;

namespace Aib.Lesson
{
    public enum ExerciseKind
    {
        LetterIntro,         // card: new letter, its sound, warnings, optional deep explanation
        WordIntro,           // card: new word with meaning, before any quiz about it
        PickLetter,          // "which letter sounds like «с»?" (or by audio)
        MatchCase,           // capital -> small letter, or small -> capital
        ReadPickTranslation, // Armenian word -> choose Russian meaning
        ListenPickWord,      // audio -> choose Armenian word (only with audio)
        BuildWord,           // tap letter tiles in order to spell the word
        Phrase,              // card: tiny phrase to read, hear and repeat
    }

    public class Exercise
    {
        public ExerciseKind Kind;
        public LetterData Letter;
        public WordData Word;
        public bool UseAudio;
        /// <summary>What is shown: MatchCase — the given glyph; ReadPickTranslation — the word, maybe capitalised.</summary>
        public string Shown;
        /// <summary>Answer choices (PickLetter: lowercase glyphs; others: text) or BuildWord tiles.</summary>
        public List<string> Options = new List<string>();
        public string Answer;

        public bool IsQuiz => Kind != ExerciseKind.LetterIntro && Kind != ExerciseKind.WordIntro && Kind != ExerciseKind.Phrase;

        /// <summary>Progress key of the letter or word this exercise trains.</summary>
        public string ItemKey => Letter != null ? Letter.Key : Word.Key;
    }

    /// <summary>Turns a lesson, or a set of due review items, into an ordered list of exercises.</summary>
    public static class ExerciseBuilder
    {
        const int OptionCount = 4;
        const int BuildWordDistractors = 2;
        const int OlderCapitals = 2;

        public static List<Exercise> Build(Course course, LessonData lesson, Func<string, bool> hasAudio, int seed)
        {
            var rng = new Random(seed);
            var known = course.LettersUpTo(lesson.day);
            var knownWords = course.WordsUpTo(lesson.day);
            var result = new List<Exercise>();

            // 1. Meet each new letter, then recognise it among the known ones, small and capital.
            //    Two older letters get their capitals refreshed too.
            foreach (var id in lesson.newLetters)
                result.Add(new Exercise { Kind = ExerciseKind.LetterIntro, Letter = course.Letter(id), UseAudio = hasAudio("l_" + id) });
            var letterQuizzes = new List<Exercise>();
            foreach (var id in lesson.newLetters)
            {
                letterQuizzes.Add(PickLetter(course.Letter(id), known, hasAudio, rng));
                letterQuizzes.Add(MatchCase(course.Letter(id), known, rng));
            }
            var older = known.Where(l => !lesson.newLetters.Contains(l.id));
            foreach (var letter in Shuffled(older, rng).Take(OlderCapitals))
                letterQuizzes.Add(MatchCase(letter, known, rng));
            result.AddRange(Shuffled(letterQuizzes, rng));

            // 2. Meet each new word with its meaning before being asked about it.
            foreach (var word in lesson.words)
                result.Add(new Exercise { Kind = ExerciseKind.WordIntro, Word = word, UseAudio = hasAudio(word.AudioId) });

            // 3. Practise the day's words, mixed.
            var practice = new List<Exercise>();
            foreach (var word in lesson.words)
            {
                practice.Add(ReadPick(word, knownWords, hasAudio, rng));
                practice.Add(BuildWord(word, known, hasAudio, rng));
                if (hasAudio(word.AudioId))
                    practice.Add(ListenPick(word, knownWords, rng));
            }
            result.AddRange(Shuffled(practice, rng));

            // 4. Finish with phrases to read aloud.
            foreach (var phrase in lesson.phrases)
                result.Add(new Exercise { Kind = ExerciseKind.Phrase, Word = phrase, UseAudio = hasAudio(phrase.AudioId) });

            return result;
        }

        /// <summary>One quiz per due item: letters by sound or capital/small pair, words by meaning, spelling or listening.</summary>
        public static List<Exercise> BuildReview(Course course, IEnumerable<string> dueKeys, int lastDay, Func<string, bool> hasAudio, int seed)
        {
            var rng = new Random(seed);
            var known = course.LettersUpTo(lastDay);
            var knownWords = course.WordsUpTo(lastDay);
            var result = new List<Exercise>();
            foreach (var key in dueKeys)
            {
                switch (course.ByKey(key))
                {
                    case LetterData letter:
                        result.Add(rng.Next(2) == 0 ? PickLetter(letter, known, hasAudio, rng) : MatchCase(letter, known, rng));
                        break;
                    case WordData word:
                        int kinds = hasAudio(word.AudioId) ? 3 : 2;
                        switch (rng.Next(kinds))
                        {
                            case 0: result.Add(ReadPick(word, knownWords, hasAudio, rng)); break;
                            case 1: result.Add(BuildWord(word, known, hasAudio, rng)); break;
                            default: result.Add(ListenPick(word, knownWords, rng)); break;
                        }
                        break;
                }
            }
            return Shuffled(result, rng);
        }

        static Exercise PickLetter(LetterData letter, List<LetterData> known, Func<string, bool> hasAudio, Random rng)
        {
            // Letters sharing the same Russian hint would make the question ambiguous.
            var others = known.Where(l => l.id != letter.id && l.ru != letter.ru).Select(l => l.lower);
            return new Exercise
            {
                Kind = ExerciseKind.PickLetter,
                Letter = letter,
                UseAudio = hasAudio(letter.AudioId),
                Answer = letter.lower,
                Options = WithDistractors(letter.lower, others, rng),
            };
        }

        /// <summary>Capital shown, pick the small letter — or the other way round.</summary>
        static Exercise MatchCase(LetterData letter, List<LetterData> known, Random rng)
        {
            bool fromCapital = rng.Next(2) == 0;
            Func<LetterData, string> form = fromCapital ? l => l.lower : l => l.upper;
            return new Exercise
            {
                Kind = ExerciseKind.MatchCase,
                Letter = letter,
                Shown = fromCapital ? letter.upper : letter.lower,
                Answer = form(letter),
                Options = WithDistractors(form(letter), known.Where(l => l.id != letter.id).Select(form), rng),
            };
        }

        /// <summary>Half the time the word starts with a capital, as at the start of a sentence.</summary>
        static Exercise ReadPick(WordData word, List<WordData> knownWords, Func<string, bool> hasAudio, Random rng) => new Exercise
        {
            Kind = ExerciseKind.ReadPickTranslation,
            Word = word,
            Shown = rng.Next(2) == 0 ? ArmenianText.Capitalize(word.hy) : word.hy,
            UseAudio = hasAudio(word.AudioId),
            Answer = word.ru,
            Options = WithDistractors(word.ru, knownWords.Where(w => w.id != word.id).Select(w => w.ru), rng),
        };

        static Exercise ListenPick(WordData word, List<WordData> knownWords, Random rng) => new Exercise
        {
            Kind = ExerciseKind.ListenPickWord,
            Word = word,
            UseAudio = true,
            Answer = word.hy,
            Options = WithDistractors(word.hy, knownWords.Where(w => w.id != word.id).Select(w => w.hy), rng),
        };

        static Exercise BuildWord(WordData word, List<LetterData> known, Func<string, bool> hasAudio, Random rng)
        {
            var tiles = ArmenianText.Tokenize(word.hy);
            var spare = known.Select(l => l.lower).Where(g => !tiles.Contains(g));
            return new Exercise
            {
                Kind = ExerciseKind.BuildWord,
                Word = word,
                UseAudio = hasAudio(word.AudioId),
                Answer = string.Concat(tiles),
                Options = Shuffled(tiles.Concat(Shuffled(spare, rng).Take(BuildWordDistractors)), rng),
            };
        }

        static List<string> WithDistractors(string answer, IEnumerable<string> pool, Random rng)
        {
            var distractors = Shuffled(pool.Distinct().Where(o => o != answer), rng).Take(OptionCount - 1);
            return Shuffled(distractors.Append(answer), rng);
        }

        static List<T> Shuffled<T>(IEnumerable<T> items, Random rng)
        {
            var list = items.ToList();
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            return list;
        }
    }

    /// <summary>Runs through exercises; a quiz answered with mistakes comes back at the end. Steps can be undone.</summary>
    public class LessonSession
    {
        readonly List<Exercise> _queue;
        readonly HashSet<string> _missed = new HashSet<string>();
        readonly Stack<(bool requeued, bool newlyMissed)> _history = new Stack<(bool, bool)>();
        int _index;

        public int Mistakes { get; private set; }
        public int Total => _queue.Count;
        public int Index => _index;
        public bool IsFinished => _index >= _queue.Count;
        public Exercise Current => IsFinished ? null : _queue[_index];
        /// <summary>Item keys answered wrongly at least once.</summary>
        public IReadOnlyCollection<string> Missed => _missed;
        public bool CanGoBack => _history.Count > 0;

        public LessonSession(List<Exercise> exercises) => _queue = new List<Exercise>(exercises);

        public void Complete(bool hadMistake)
        {
            var ex = Current;
            if (ex == null) return;
            bool requeued = hadMistake && ex.IsQuiz, newlyMissed = false;
            if (requeued)
            {
                Mistakes++;
                newlyMissed = _missed.Add(ex.ItemKey);
                _queue.Add(ex);
            }
            _history.Push((requeued, newlyMissed));
            _index++;
        }

        /// <summary>Returns to the previous step and undoes what completing it did.</summary>
        public void Back()
        {
            if (_history.Count == 0) return;
            var (requeued, newlyMissed) = _history.Pop();
            _index--;
            if (!requeued) return;
            _queue.RemoveAt(_queue.Count - 1);
            Mistakes--;
            if (newlyMissed) _missed.Remove(_queue[_index].ItemKey);
        }
    }
}
