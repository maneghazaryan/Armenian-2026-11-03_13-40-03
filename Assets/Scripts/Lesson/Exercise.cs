using System;
using System.Collections.Generic;
using System.Linq;
using Aib.Data;

namespace Aib.Lesson
{
    public enum ExerciseKind
    {
        LetterIntro,         // card: new letter, its sound, association, optional deep explanation
        TraceLetter,         // trace the letter over a faint sample (practice, never a mistake)
        DrawLetter,          // write the letter from memory; the sample appears only after
        WordIntro,           // card: new word with meaning, before any quiz about it
        PickLetter,          // "which letter sounds like «с»?" (or by audio)
        MatchCase,           // capital -> small letter, or small -> capital
        ReadPickTranslation, // Armenian word -> choose Russian meaning
        MeaningPickWord,     // Russian meaning -> choose Armenian word
        ListenPickWord,      // audio -> choose Armenian word (only with audio)
        BuildWord,           // tap letter tiles in order to spell the word
        MixedText,           // card: Russian text with the known sounds in Armenian letters
        Phrase,              // card: tiny phrase to read, hear and repeat
        Explain,             // card: how something works (grammar, polite forms), Russian with Armenian examples
        FillGap,             // sentence with a gap -> choose the missing word
        BuildSentence,       // tap the words of a sentence in order
    }

    public class Exercise
    {
        public ExerciseKind Kind;
        public LetterData Letter;
        public WordData Word;
        public bool UseAudio;
        /// <summary>What is shown: MatchCase — the given glyph; TraceLetter/DrawLetter — the glyph to write; ReadPickTranslation — the word, maybe
        /// capitalised; MixedText — the mixed text (Answer holds the plain Russian).</summary>
        public string Shown;
        /// <summary>Answer choices (PickLetter: lowercase glyphs; others: text) or BuildWord tiles.</summary>
        public List<string> Options = new List<string>();
        public string Answer;
        /// <summary>WordIntro of a word taught on an earlier day, shown again in a topic.</summary>
        public bool Review;

        public bool IsQuiz => Kind != ExerciseKind.LetterIntro && Kind != ExerciseKind.TraceLetter && Kind != ExerciseKind.WordIntro &&
                              Kind != ExerciseKind.Phrase && Kind != ExerciseKind.MixedText && Kind != ExerciseKind.Explain;

        /// <summary>Progress key of the letter or word this exercise trains (null for texts and sentences).</summary>
        public string ItemKey => Kind == ExerciseKind.BuildSentence || Kind == ExerciseKind.FillGap ? null : Letter != null ? Letter.Key : Word?.Key;

        /// <summary>The letter or word it is about, to keep the same one from coming twice in a row.</summary>
        internal object Item => (object)Letter ?? (object)Word ?? Shown;
    }

    /// <summary>
    /// Turns a lesson part, or a set of due review items, into an ordered list of exercises.
    /// Each day has three parts:
    /// 1. new letters and words, first practice;
    /// 2. more practice with the same words;
    /// 3. reading: Russian texts written partly in Armenian letters, then Armenian phrases.
    /// Word days after the alphabet have no letters: part 1 meets the words, part 3 reads phrases
    /// with them and checks each word once more.
    /// </summary>
    public static class ExerciseBuilder
    {
        public const int Parts = 3;
        const int OptionCount = 4;
        const int BuildWordDistractors = 2;
        const int OlderCapitals = 2;

        public static List<Exercise> Build(Course course, LessonData lesson, int part, Func<string, bool> hasAudio, int seed)
        {
            var rng = new Random(seed);
            var known = course.LettersUpTo(lesson.day);
            var knownWords = course.WordsUpTo(lesson.day);
            var result = new List<Exercise>();
            // A topic shows some words of earlier days again; they are practised with the new ones.
            var review = course.ReviewWords(lesson);
            var words = lesson.words.Concat(review).ToList();

            switch (part)
            {
                case 1:
                    foreach (var text in lesson.explain)
                        result.Add(new Exercise { Kind = ExerciseKind.Explain, Shown = text });
                    // Meet each new letter and trace it, small then capital; then recognise it by sound
                    // and as capital/small. Two older letters get their capitals refreshed too.
                    foreach (var id in lesson.newLetters)
                    {
                        var letter = course.Letter(id);
                        result.Add(new Exercise { Kind = ExerciseKind.LetterIntro, Letter = letter, UseAudio = hasAudio("l_" + id) });
                        result.Add(Draw(ExerciseKind.TraceLetter, letter, letter.lower));
                        if (HasOwnCapital(letter))
                            result.Add(Draw(ExerciseKind.TraceLetter, letter, letter.upper));
                    }
                    var letterQuizzes = new List<Exercise>();
                    foreach (var id in lesson.newLetters)
                    {
                        letterQuizzes.Add(PickLetter(course.Letter(id), known, hasAudio, rng));
                        letterQuizzes.Add(MatchCase(course.Letter(id), known, rng));
                    }
                    // Word days after the alphabet are only about words.
                    var older = lesson.IsWordsDay ? Enumerable.Empty<LetterData>() : known.Where(l => !lesson.newLetters.Contains(l.id));
                    foreach (var letter in Shuffled(older, rng).Take(OlderCapitals))
                        letterQuizzes.Add(MatchCase(letter, known, rng));
                    result.AddRange(Spread(letterQuizzes, rng));

                    // Meet each new word with its meaning before being asked about it, then practise.
                    foreach (var word in words)
                        result.Add(new Exercise { Kind = ExerciseKind.WordIntro, Word = word, UseAudio = hasAudio(word.AudioId), Review = review.Contains(word) });
                    var first = new List<Exercise>();
                    // New words are read and spelled; words already known are recalled from their meaning.
                    foreach (var word in lesson.words)
                    {
                        first.Add(ReadPick(word, knownWords, hasAudio, rng));
                        first.Add(BuildWord(word, known, hasAudio, rng));
                    }
                    foreach (var word in review)
                        first.Add(MeaningPick(word, knownWords, hasAudio, rng));
                    result.AddRange(Spread(first.Concat(Gaps(lesson, rng)), rng, after: result.LastOrDefault()));
                    break;

                case 2:
                    // Same words, other angles: meaning -> word, listening, spelling, reading.
                    var more = new List<Exercise>();
                    foreach (var word in words)
                    {
                        more.Add(MeaningPick(word, knownWords, hasAudio, rng));
                        more.Add(ReadPick(word, knownWords, hasAudio, rng));
                        more.Add(BuildWord(word, known, hasAudio, rng));
                        if (hasAudio(word.AudioId))
                            more.Add(ListenPick(word, knownWords, rng));
                    }
                    foreach (var id in lesson.newLetters)
                    {
                        more.Add(MatchCase(course.Letter(id), known, rng));
                        more.Add(Draw(ExerciseKind.DrawLetter, course.Letter(id), course.Letter(id).lower));
                    }
                    more.AddRange(lesson.order.Select(o => BuildSentence(o, rng)));
                    result.AddRange(Spread(more, rng));
                    break;

                default:
                    // Read: familiar Russian with the known sounds in Armenian letters, then real phrases.
                    var glyphs = course.GlyphsUpTo(lesson.day);
                    foreach (var text in lesson.texts)
                        result.Add(new Exercise { Kind = ExerciseKind.MixedText, Shown = ArmenianText.MixRussian(text, glyphs), Answer = text });
                    foreach (var phrase in lesson.phrases.Concat(lesson.verses))
                        result.Add(new Exercise { Kind = ExerciseKind.Phrase, Word = phrase, UseAudio = hasAudio(phrase.AudioId) });
                    // A day of new words ends with a check: its phrases built from their words (the words
                    // learned so far put together), the gap sentences, every word of the day from its meaning.
                    if (lesson.IsWordsDay)
                        result.AddRange(Spread(Gaps(lesson, rng)
                            .Concat(lesson.phrases.Where(CanBuild).Select(p => BuildSentence(p, rng)))
                            .Concat(words.Select(w => MeaningPick(w, knownWords, hasAudio, rng)))
                            .Concat(lesson.words.Select(w => ReadPick(w, knownWords, hasAudio, rng))),
                            rng, after: result.LastOrDefault()));
                    break;
            }
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
                        switch (rng.Next(3))
                        {
                            case 0: result.Add(PickLetter(letter, known, hasAudio, rng)); break;
                            case 1: result.Add(MatchCase(letter, known, rng)); break;
                            default: result.Add(Draw(ExerciseKind.DrawLetter, letter, letter.lower)); break;
                        }
                        break;
                    case WordData word:
                        int kinds = hasAudio(word.AudioId) ? 4 : 3;
                        switch (rng.Next(kinds))
                        {
                            case 0: result.Add(ReadPick(word, knownWords, hasAudio, rng)); break;
                            case 1: result.Add(MeaningPick(word, knownWords, hasAudio, rng)); break;
                            case 2: result.Add(BuildWord(word, known, hasAudio, rng)); break;
                            default: result.Add(ListenPick(word, knownWords, rng)); break;
                        }
                        break;
                }
            }
            return Spread(result, rng);
        }

        /// <summary>
        /// Shuffles, then orders so that neither the same kind of exercise nor the same letter/word
        /// comes twice in a row, as far as the mix allows.
        /// </summary>
        public static List<Exercise> Spread(IEnumerable<Exercise> items, Random rng, Exercise after = null)
        {
            var left = Shuffled(items, rng);
            var result = new List<Exercise>();
            var prev = after;
            while (left.Count > 0)
            {
                // Among the allowed ones, place the letter/word (then kind) with the most exercises
                // still waiting; leaving those for the end is what forces repeats.
                int Score(Exercise e) =>
                    left.Count(x => x.Item == e.Item) * 100 + left.Count(x => x.Kind == e.Kind);
                Exercise pick =
                    Best(left.Where(e => prev == null || (e.Item != prev.Item && e.Kind != prev.Kind)), Score) ??
                    Best(left.Where(e => e.Item != prev.Item), Score) ??
                    Best(left, Score);
                result.Add(pick);
                left.Remove(pick);
                prev = pick;
            }
            return result;
        }

        /// <summary>Highest-scoring item; ties keep the shuffled order. Null when there is none.</summary>
        static Exercise Best(IEnumerable<Exercise> items, Func<Exercise, int> score)
        {
            Exercise best = null;
            int bestScore = int.MinValue;
            foreach (var e in items)
            {
                int sc = score(e);
                if (sc > bestScore) (best, bestScore) = (e, sc);
            }
            return best;
        }

        /// <summary>«Մենք լավ [ենք]։» -> «Մենք լավ …։», choose among the lesson's bracketed words.</summary>
        public static List<Exercise> Gaps(LessonData lesson, Random rng)
        {
            var pool = lesson.gaps.Select(g => GapAnswer(g.hy)).Where(a => a != null).Concat(lesson.gapOptions).Distinct().ToList();
            return lesson.gaps.Where(g => GapAnswer(g.hy) != null).Select(g =>
            {
                var answer = GapAnswer(g.hy);
                return new Exercise
                {
                    Kind = ExerciseKind.FillGap,
                    Word = g,
                    Shown = System.Text.RegularExpressions.Regex.Replace(g.hy, @"\[[^\]]*\]", "…"),
                    Answer = answer,
                    Options = WithDistractors(answer, pool, rng),
                };
            }).ToList();
        }

        /// <summary>The bracketed word of a gap sentence, or null.</summary>
        public static string GapAnswer(string hy)
        {
            int a = hy.IndexOf('['), b = hy.IndexOf(']');
            return a >= 0 && b > a + 1 ? hy.Substring(a + 1, b - a - 1) : null;
        }

        /// <summary>A gap sentence written out in full.</summary>
        public static string GapFull(string hy) => hy.Replace("[", "").Replace("]", "");

        /// <summary>The words of a sentence, end punctuation dropped; «՞» stays inside its word.</summary>
        public static List<string> SentenceWords(string hy) =>
            hy.Split(' ', StringSplitOptions.RemoveEmptyEntries)
              .Select(w => w.TrimEnd('։', '.', ',', '!', '՝'))
              .Where(w => w.Length > 0)
              .ToList();

        /// <summary>A short phrase without a dialogue dash can be built from its words.</summary>
        public static bool CanBuild(WordData phrase)
        {
            int n = SentenceWords(phrase.hy).Count;
            return n >= 2 && n <= 6 && !phrase.hy.Contains("—");
        }

        public static Exercise BuildSentence(WordData sentence, Random rng)
        {
            var parts = SentenceWords(sentence.hy);
            var options = Shuffled(parts, rng);
            // Never offer the words already in order.
            if (parts.Count > 1 && options.SequenceEqual(parts)) options.Reverse();
            return new Exercise { Kind = ExerciseKind.BuildSentence, Word = sentence, Answer = string.Join(" ", parts), Options = options };
        }

        /// <summary>Trace or write one form of a letter.</summary>
        static Exercise Draw(ExerciseKind kind, LetterData letter, string glyph) =>
            new Exercise { Kind = kind, Letter = letter, Shown = glyph, Answer = glyph };

        /// <summary>և has no capital of its own: at the start of a word it is written Եվ, two known letters.</summary>
        static bool HasOwnCapital(LetterData letter) => letter.id != "ev";

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

        static Exercise MeaningPick(WordData word, List<WordData> knownWords, Func<string, bool> hasAudio, Random rng) => new Exercise
        {
            Kind = ExerciseKind.MeaningPickWord,
            Word = word,
            UseAudio = hasAudio(word.AudioId),
            Answer = word.hy,
            Options = WithDistractors(word.hy, knownWords.Where(w => w.id != word.id).Select(w => w.hy), rng),
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

        /// <summary>The learner doesn't want this word: its exercises from here on are dropped. Earlier steps can't be undone after it.</summary>
        public void Drop(WordData word)
        {
            for (int i = _queue.Count - 1; i >= _index; i--)
                if (_queue[i].Word == word) _queue.RemoveAt(i);
            _history.Clear();
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
