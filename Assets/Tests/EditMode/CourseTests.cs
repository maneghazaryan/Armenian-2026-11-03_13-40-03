using System;
using System.Collections.Generic;
using System.Linq;
using Aib.Data;
using Aib.Lesson;
using Aib.Progress;
using NUnit.Framework;
using UnityEngine;

namespace Aib.Tests
{
    public class CourseTests
    {
        static Course LoadCourse() => Course.FromJson(
            Resources.Load<TextAsset>("Data/letters").text,
            Resources.Load<TextAsset>("Data/lessons").text);

        [Test]
        public void Tokenize_TreatsUAndEvAsSingleLetters()
        {
            CollectionAssert.AreEqual(new[] { "ե", "ր", "և", "ա", "ն", "ու", "մ" }, ArmenianText.Tokenize("Երևանում"));
            CollectionAssert.AreEqual(new[] { "ու", "ս" }, ArmenianText.Tokenize("Ուս։"));
            CollectionAssert.AreEqual(new[] { "ս", "ա" }, ArmenianText.Tokenize("Սա!"));
        }

        [Test]
        public void Course_LoadsAllLettersAndLessons()
        {
            var course = LoadCourse();
            Assert.AreEqual(39, course.Letters.Count); // primer alphabet: ու and և are taught as letters
            Assert.GreaterOrEqual(course.Lessons.Count, 7);
            // The whole alphabet is taught: every letter belongs to some day.
            foreach (var letter in course.Letters)
                Assert.Greater(course.DayOf(letter), 0, letter.lower);
            CollectionAssert.AreEqual(new[] { "ս", "ա", "ր" }, course.LettersUpTo(1).Select(l => l.lower));
        }

        [Test]
        public void EveryLessonUsesOnlyLearnedLetters()
        {
            var course = LoadCourse();
            foreach (var lesson in course.Lessons)
            {
                var known = course.LettersUpTo(lesson.day).Select(l => l.lower).ToHashSet();
                foreach (var item in lesson.words.Concat(lesson.phrases).Concat(lesson.verses))
                foreach (var t in ArmenianText.Tokenize(item.hy))
                    Assert.IsTrue(known.Contains(t), $"day {lesson.day}: '{item.hy}' uses unlearned '{t}'");
            }
        }

        [Test]
        public void Builder_OptionsContainAnswer_AndTilesSpellWord([Values(1, 2, 3)] int seed)
        {
            var course = LoadCourse();
            foreach (var lesson in course.Lessons)
            foreach (var hasAudio in new[] { false, true })
            for (int part = 1; part <= ExerciseBuilder.Parts; part++)
            {
                var list = ExerciseBuilder.Build(course, lesson, part, _ => hasAudio, seed);
                Assert.IsNotEmpty(list, $"day {lesson.day} part {part}");
                Assert.AreEqual(part == 1 ? lesson.newLetters.Count : 0, list.Count(e => e.Kind == ExerciseKind.LetterIntro));
                Assert.AreEqual(hasAudio && part == 2, list.Any(e => e.Kind == ExerciseKind.ListenPickWord));
                foreach (var ex in list.Where(e => e.IsQuiz && e.Kind != ExerciseKind.DrawLetter))
                {
                    if (ex.Kind == ExerciseKind.BuildWord)
                    {
                        var tiles = new List<string>(ex.Options);
                        foreach (var t in ArmenianText.Tokenize(ex.Word.hy))
                            Assert.IsTrue(tiles.Remove(t), $"missing tile '{t}' for {ex.Word.hy}");
                    }
                    else
                    {
                        Assert.Contains(ex.Answer, ex.Options);
                        Assert.GreaterOrEqual(ex.Options.Count, 2);
                        Assert.AreEqual(ex.Options.Count, ex.Options.Distinct().Count());
                    }
                }
            }
        }

        [Test]
        public void Session_RequeuesQuizWithMistake()
        {
            var quiz = new Exercise { Kind = ExerciseKind.PickLetter, Letter = new LetterData { id = "a" } };
            var card = new Exercise { Kind = ExerciseKind.LetterIntro };
            var s = new LessonSession(new List<Exercise> { card, quiz });
            s.Complete(true);  // cards never requeue
            s.Complete(true);  // quiz wrong -> back at the end
            Assert.IsFalse(s.IsFinished);
            Assert.AreSame(quiz, s.Current);
            s.Complete(false);
            Assert.IsTrue(s.IsFinished);
            Assert.AreEqual(1, s.Mistakes);
            CollectionAssert.AreEqual(new[] { "l:a" }, s.Missed);
        }

        [Test]
        public void Session_BackUndoesStepAndItsMistake()
        {
            var quiz = new Exercise { Kind = ExerciseKind.PickLetter, Letter = new LetterData { id = "a" } };
            var card = new Exercise { Kind = ExerciseKind.LetterIntro };
            var s = new LessonSession(new List<Exercise> { card, quiz });
            Assert.IsFalse(s.CanGoBack);
            s.Complete(false);
            s.Complete(true); // wrong: requeued
            Assert.AreEqual(3, s.Total);
            s.Back();
            Assert.AreSame(quiz, s.Current);
            Assert.AreEqual(2, s.Total);
            Assert.AreEqual(0, s.Mistakes);
            Assert.IsEmpty(s.Missed);
            s.Back();
            Assert.AreSame(card, s.Current);
            Assert.IsFalse(s.CanGoBack);
        }

        [Test]
        public void EveryLetterHasShapeAssociation()
        {
            foreach (var letter in LoadCourse().Letters)
                Assert.IsFalse(string.IsNullOrEmpty(letter.look), letter.id);
        }

        [Test]
        public void Builder_DrillsCapitalsOfNewLetters()
        {
            var course = LoadCourse();
            foreach (var lesson in course.Lessons)
            {
                var list = ExerciseBuilder.Build(course, lesson, 1, _ => false, 5);
                foreach (var id in lesson.newLetters)
                    Assert.IsTrue(list.Any(e => e.Kind == ExerciseKind.MatchCase && e.Letter.id == id), $"day {lesson.day}: {id}");
                foreach (var ex in list.Where(e => e.Kind == ExerciseKind.MatchCase))
                    Assert.IsTrue(ex.Shown == ex.Letter.upper ? ex.Answer == ex.Letter.lower : ex.Answer == ex.Letter.upper);
            }
            Assert.AreEqual("Ասա", ArmenianText.Capitalize("ասա"));
            Assert.AreEqual("Ուս", ArmenianText.Capitalize("ուս"));
            Assert.AreEqual("Եվա", ArmenianText.Capitalize("և" + "ա"));
        }

        [Test]
        public void Builder_NeverRepeatsTheSameWordOrLetterInARow([Values(1, 2, 3, 4, 5)] int seed)
        {
            var course = LoadCourse();
            foreach (var lesson in course.Lessons)
            for (int part = 1; part <= 2; part++)
            {
                var list = ExerciseBuilder.Build(course, lesson, part, _ => true, seed);
                int sameKind = 0;
                for (int i = 1; i < list.Count; i++)
                {
                    if (!list[i].IsQuiz || !list[i - 1].IsQuiz) continue;
                    Assert.AreNotEqual(list[i - 1].ItemKey, list[i].ItemKey, $"day {lesson.day} part {part} #{i}");
                    if (list[i - 1].Kind == list[i].Kind) sameKind++;
                }
                Assert.LessOrEqual(sameKind, 1, $"day {lesson.day} part {part}: same exercise type twice in a row");
            }
        }

        [Test]
        public void Part3_ReadsRussianWithKnownLettersInArmenian()
        {
            var day1 = new HashSet<string> { "ս", "ա", "ր" };
            Assert.AreEqual("Սиրенա зաпелա, вսе влюбилиսь.", ArmenianText.MixRussian("Сирена запела, все влюбились.", day1));
            var course = LoadCourse();
            Assert.AreEqual("Ոտ նոвый մոստ.", ArmenianText.MixRussian("Вот новый мост.", course.GlyphsUpTo(4)));
            Assert.AreEqual("Երևան!", ArmenianText.MixRussian("Ереван!", course.GlyphsUpTo(7)));
            Assert.AreEqual("окно", ArmenianText.MixRussian("окно", new HashSet<string>()));
            Assert.AreEqual("Օլյա", ArmenianText.MixRussian("Оля", course.GlyphsUpTo(17)));

            // ր only for the soft «р» (before и е ё ю я ь); every hard one is ռ, Russian until ռ is known.
            Assert.AreEqual("Рամա", ArmenianText.MixRussian("Рама", course.GlyphsUpTo(2)));
            Assert.AreEqual("Ռամա", ArmenianText.MixRussian("Рама", course.GlyphsUpTo(16)));
            Assert.AreEqual("Սաрա", ArmenianText.MixRussian("Сара", course.GlyphsUpTo(15)));
            Assert.AreEqual("Սառա", ArmenianText.MixRussian("Сара", course.GlyphsUpTo(16)));
            Assert.AreEqual("Ռուսսկիյ միռ", ArmenianText.MixRussian("Русский мир", course.GlyphsUpTo(16)));
            Assert.AreEqual("ծեռասա", ArmenianText.MixRussian("терраса", course.GlyphsUpTo(16)));
            Assert.AreEqual("պրիմեռնո", ArmenianText.MixRussian("примерно", course.GlyphsUpTo(16)));
            Assert.AreEqual("ռազմեռ", ArmenianText.MixRussian("размер", course.GlyphsUpTo(16)));
            Assert.AreEqual("Առմենիյա", ArmenianText.MixRussian("Армения", course.GlyphsUpTo(16)));
            Assert.AreEqual("սախառ, Սախարա", ArmenianText.MixRussian("сахар, Сахара", course.GlyphsUpTo(16)));

            // я ё ю е as յ + vowel; soft т/д are ծ/ձ and then need no յ; ч is չ.
            var all = course.GlyphsUpTo(17);
            Assert.AreEqual("Յուռա, յոլկա, պոյոտ, սեմьյա, շոլ", ArmenianText.MixRussian("Юра, ёлка, поёт, семья, шёл", all));
            Assert.AreEqual("Երևան", ArmenianText.MixRussian("Ереван", all));
            Assert.AreEqual("Ձիմա, դոժձь, մածь, ծոծա", ArmenianText.MixRussian("Дима, дождь, мать, тётя", all));
            Assert.AreEqual("դом, տам", ArmenianText.MixRussian("дом, там", new HashSet<string> { "դ", "տ", "ձ", "ծ" }));
            Assert.AreEqual("չայ", ArmenianText.MixRussian("чай", all));
            Assert.AreEqual("Дима, мать", ArmenianText.MixRussian("Дима, мать", new HashSet<string>()));

            foreach (var lesson in course.Lessons)
            {
                var texts = ExerciseBuilder.Build(course, lesson, 3, _ => false, 1).Where(e => e.Kind == ExerciseKind.MixedText).ToList();
                Assert.AreEqual(lesson.texts.Count, texts.Count);
                Assert.IsTrue(texts.All(t => t.Shown.Any(ArmenianText.IsArmenian)), $"day {lesson.day}");
            }
        }

        [Test]
        public void LetterTextsMentionOnlyLettersLearnedBefore()
        {
            var course = LoadCourse();
            foreach (var lesson in course.Lessons)
            {
                var known = course.GlyphsUpTo(lesson.day - 1);
                foreach (var t in ArmenianText.Tokenize(lesson.intro))
                    Assert.IsTrue(course.GlyphsUpTo(lesson.day).Contains(t), $"day {lesson.day} intro: '{t}'");
                foreach (var id in lesson.newLetters)
                {
                    var letter = course.Letter(id);
                    known.Add(letter.lower);
                    if (letter.lower == "ու") known.Add("ւ");
                    foreach (var text in new[] { letter.hint, letter.look, letter.warn, letter.more })
                    foreach (var t in ArmenianText.Tokenize(text ?? ""))
                        Assert.IsTrue(known.Contains(t), $"letter {id} mentions unlearned '{t}'");
                }
            }
            foreach (var letter in course.Letters)
                Assert.IsFalse(string.IsNullOrEmpty(letter.nameRu), letter.id);
        }

        [Test]
        public void Day_IsDoneAfterAllPartsInOrder()
        {
            var day = new DateTime(2026, 11, 5);
            var p = new ProgressData();
            Assert.IsFalse(p.CompletePart(1, 2, 3, 0, day)); // out of order: ignored
            Assert.AreEqual(0, p.PartsDone(1, 3));
            Assert.IsFalse(p.CompletePart(1, 1, 3, 1, day));
            Assert.IsFalse(p.CompletePart(1, 2, 3, 2, day));
            Assert.AreEqual(2, p.PartsDone(1, 3));
            Assert.IsNull(p.Result(1));
            Assert.IsTrue(p.CompletePart(1, 3, 3, 0, day));
            Assert.AreEqual(2, p.Result(1).stars); // 3 mistakes over the day
            Assert.IsFalse(p.CompletePart(1, 1, 3, 9, day)); // replay changes nothing
            Assert.AreEqual(2, p.Result(1).stars);

            p.Missed(new[] { "w:sar" });
            Assert.IsTrue(p.IsHard("w:sar"));
            p.Learn(new[] { "w:sar" }, day);
            p.Reviewed("w:sar", true, day.AddDays(1));
            p.Reviewed("w:sar", true, day.AddDays(2));
            Assert.IsFalse(p.IsHard("w:sar"));
        }

        [Test]
        public void DayUnlock_NextDayOpensTomorrow_UnlessLockOff()
        {
            var today = new DateTime(2026, 11, 3);
            var p = new ProgressData();
            Assert.AreEqual(DayState.Available, DayUnlock.State(1, p, today));
            Assert.AreEqual(DayState.Locked, DayUnlock.State(2, p, today));

            p.Complete(1, 3, today);
            Assert.AreEqual(DayState.Done, DayUnlock.State(1, p, today));
            Assert.AreEqual(DayState.LockedUntilTomorrow, DayUnlock.State(2, p, today));
            Assert.AreEqual(DayState.Available, DayUnlock.State(2, p, today.AddDays(1)));
            Assert.AreEqual(DayState.Locked, DayUnlock.State(3, p, today.AddDays(1)));

            p.dayLock = false;
            Assert.AreEqual(DayState.Available, DayUnlock.State(2, p, today));
        }

        [Test]
        public void Progress_KeepsBestStars()
        {
            var p = new ProgressData();
            p.Complete(1, 2, DateTime.Today);
            p.Complete(1, 1, DateTime.Today);
            Assert.AreEqual(2, p.Result(1).stars);
            p.Complete(1, 3, DateTime.Today);
            Assert.AreEqual(3, p.Result(1).stars);
        }

        [Test]
        public void Builder_IntroducesEveryWordBeforeAnyQuizAboutIt()
        {
            var course = LoadCourse();
            foreach (var lesson in course.Lessons)
            {
                var list = ExerciseBuilder.Build(course, lesson, 1, _ => true, 7);
                foreach (var word in lesson.words)
                {
                    int intro = list.FindIndex(e => e.Kind == ExerciseKind.WordIntro && e.Word == word);
                    int firstQuiz = list.FindIndex(e => e.IsQuiz && e.Word == word);
                    Assert.GreaterOrEqual(intro, 0, word.hy);
                    Assert.Less(intro, firstQuiz, word.hy);
                }
            }
        }

        [Test]
        public void Review_MakesOneQuizPerDueItem()
        {
            var course = LoadCourse();
            var keys = new[] { "l:a", "l:s", "w:sar", "w:masis" };
            var list = ExerciseBuilder.BuildReview(course, keys, 2, _ => false, 3);
            Assert.AreEqual(keys.Length, list.Count);
            Assert.IsTrue(list.All(e => e.IsQuiz));
            CollectionAssert.AreEquivalent(keys, list.Select(e => e.ItemKey));
        }

        [Test]
        public void Leitner_PromotesRememberedAndResetsForgotten()
        {
            var day = new DateTime(2026, 11, 3);
            var p = new ProgressData();
            p.Learn(new[] { "l:a", "w:sar" }, day);
            Assert.AreEqual(0, p.Due(day).Count);
            Assert.AreEqual(2, p.Due(day.AddDays(1)).Count);

            p.Reviewed("l:a", true, day.AddDays(1));   // box 2 -> next in 3 days
            p.Reviewed("w:sar", false, day.AddDays(1)); // box 1 -> tomorrow
            Assert.AreEqual(new[] { "w:sar" }, p.Due(day.AddDays(2)).Select(i => i.key).ToArray());
            Assert.AreEqual(2, p.Due(day.AddDays(4)).Count);

            p.Learn(new[] { "l:a" }, day.AddDays(4)); // relearning does not reset
            Assert.AreEqual(2, p.items.First(i => i.key == "l:a").box);
        }

        [Test]
        public void DayUnlock_ReviewComesBeforeNextLesson()
        {
            var day = new DateTime(2026, 11, 3);
            var p = new ProgressData();
            p.Complete(1, 3, day);
            p.Learn(new[] { "l:a" }, day);
            Assert.AreEqual(DayState.ReviewFirst, DayUnlock.State(2, p, day.AddDays(1)));
            p.Reviewed("l:a", true, day.AddDays(1));
            Assert.AreEqual(DayState.Available, DayUnlock.State(2, p, day.AddDays(1)));
        }

        [Test]
        public void Reminders_SkipPastTimesAndFinishedToday()
        {
            var p = new ProgressData { reminderHour = 19 };
            var morning = new DateTime(2026, 11, 3, 8, 0, 0);
            Assert.AreEqual(morning.Date.AddHours(19), Reminders.Plan(p, morning, doneToday: false)[0].time);
            Assert.AreEqual(morning.Date.AddDays(1).AddHours(19), Reminders.Plan(p, morning, doneToday: true)[0].time);
            Assert.AreEqual(morning.Date.AddDays(1).AddHours(19), Reminders.Plan(p, morning.AddHours(12), doneToday: false)[0].time);
            p.reminderHour = -1;
            Assert.IsEmpty(Reminders.Plan(p, morning, doneToday: false));
            Assert.AreEqual(9, Reminders.NextHour(-1));
            Assert.AreEqual(-1, Reminders.NextHour(21));
        }

        // ---------- drawing letters ----------

        /// <summary>The letter's own mask cells, as if drawn dot by dot, moved and scaled.</summary>
        static List<List<Vector2>> DrawnFrom(bool[] mask, float scale = 1, Vector2 shift = default)
        {
            var strokes = new List<List<Vector2>>();
            for (int i = 0; i < mask.Length; i++)
                if (mask[i])
                {
                    var p = new Vector2(i % DrawingCheck.N + 0.5f, i / DrawingCheck.N + 0.5f) / DrawingCheck.N;
                    strokes.Add(new List<Vector2> { (p - new Vector2(.5f, .5f)) * scale + new Vector2(.5f, .5f) + shift });
                }
            return strokes;
        }

        [Test]
        public void EveryLetterHasAShapeToTraceAndCheck()
        {
            foreach (var letter in LoadCourse().Letters)
            foreach (var glyph in new[] { letter.lower, letter.upper })
            {
                var key = GlyphStore.Key(letter, glyph);
                var mask = GlyphStore.Mask(key);
                Assert.IsNotNull(mask, key);
                Assert.Greater(mask.Count(c => c), 20, key);
                var guide = GlyphStore.Guide(key);
                Assert.IsNotNull(guide, key);
                Assert.AreEqual(256, guide.width, key);
                Assert.IsTrue(DrawingCheck.Passes(DrawnFrom(mask), mask, fromMemory: false), key);
            }
        }

        [Test]
        public void Drawing_FromMemoryMayBeAnywhereButMustBeTheLetter()
        {
            var course = LoadCourse();
            var s = GlyphStore.Mask(GlyphStore.Key(course.Letter("s"), "ս"));
            var l = GlyphStore.Mask(GlyphStore.Key(course.Letter("l"), "լ"));
            var small = DrawnFrom(s, 0.5f, new Vector2(-0.2f, 0.15f));
            Assert.IsFalse(DrawingCheck.Passes(small, s, fromMemory: false), "tracing must follow the sample");
            Assert.IsTrue(DrawingCheck.Passes(small, s, fromMemory: true));
            Assert.IsFalse(DrawingCheck.Passes(DrawnFrom(l), s, fromMemory: true), "լ is not ս");

            var scribble = new List<List<Vector2>> { new List<Vector2> { new Vector2(.1f, .1f), new Vector2(.9f, .9f), new Vector2(.1f, .9f), new Vector2(.9f, .1f) } };
            Assert.IsFalse(DrawingCheck.Passes(scribble, s, fromMemory: true));
            Assert.IsFalse(DrawingCheck.Passes(new List<List<Vector2>>(), s, fromMemory: true));
        }

        [Test]
        public void NewLettersAreTracedThenWrittenFromMemory()
        {
            var course = LoadCourse();
            foreach (var lesson in course.Lessons)
            {
                var part1 = ExerciseBuilder.Build(course, lesson, 1, _ => false, 3);
                var part2 = ExerciseBuilder.Build(course, lesson, 2, _ => false, 3);
                foreach (var id in lesson.newLetters)
                {
                    int intro = part1.FindIndex(e => e.Kind == ExerciseKind.LetterIntro && e.Letter.id == id);
                    Assert.AreEqual(ExerciseKind.TraceLetter, part1[intro + 1].Kind, $"day {lesson.day} {id}");
                    Assert.AreEqual(part1[intro].Letter.lower, part1[intro + 1].Shown);
                    Assert.IsTrue(part2.Any(e => e.Kind == ExerciseKind.DrawLetter && e.Letter.id == id), $"day {lesson.day} {id}");
                }
            }
        }

        // ---------- review and reading ----------

        [Test]
        public void ReadingTexts_LoadAndTurnArmenianAsLettersAreLearned()
        {
            var course = LoadCourse();
            var texts = ReadingText.ListFromJson(Resources.Load<TextAsset>("Data/reading").text);
            Assert.GreaterOrEqual(texts.Count, 20);
            Assert.AreEqual(texts.Count, texts.Select(t => t.id).Distinct().Count());
            foreach (var t in texts)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(t.title), t.id);
                Assert.Greater(t.text.Length, 200, t.id);
                Assert.AreEqual(t.text, ArmenianText.MixRussian(t.text, new HashSet<string>()), t.id);
                int day1 = ArmenianText.MixRussian(t.text, course.GlyphsUpTo(1)).Count(ArmenianText.IsArmenian);
                int all = ArmenianText.MixRussian(t.text, course.GlyphsUpTo(17)).Count(ArmenianText.IsArmenian);
                Assert.Greater(day1, 0, t.id);
                Assert.Greater(all, day1 * 2, t.id);
            }
        }

        [Test]
        public void Practice_PicksHardItemsFirst_AndReadTextsAreRemembered()
        {
            var p = new ProgressData();
            var today = new DateTime(2026, 1, 10);
            p.Learn(new[] { "l:s", "l:a", "l:r", "w:sar" }, today);
            p.Reviewed("l:s", true, today);
            p.Missed(new[] { "w:sar" });
            var keys = p.PracticeKeys(3, new System.Random(1));
            Assert.AreEqual(3, keys.Count);
            Assert.AreEqual("w:sar", keys[0]);
            CollectionAssert.DoesNotContain(keys, "l:s"); // the best remembered one is left out

            Assert.IsFalse(p.IsRead("octopus"));
            p.MarkRead("octopus");
            p.MarkRead("octopus");
            Assert.IsTrue(p.IsRead("octopus"));
            Assert.AreEqual(1, p.read.Count);
        }

        [Test]
        public void UnlockAll_OpensEveryDayAndKeepsEarnedStars()
        {
            var course = LoadCourse();
            var today = new DateTime(2026, 11, 6);
            var p = new ProgressData();
            p.Complete(1, 2, today);
            p.CompletePart(2, 1, ExerciseBuilder.Parts, 0, today);
            p.UnlockAll(course.Lessons.Select(l => (l.day,
                l.newLetters.Select(id => course.Letter(id).Key).Concat(l.words.Select(w => w.Key)))), today);

            Assert.IsTrue(course.Lessons.All(l => DayUnlock.State(l.day, p, today) == DayState.Done));
            Assert.AreEqual(2, p.Result(1).stars);
            Assert.AreEqual(0, p.partial.Count);
            Assert.AreEqual(course.Letters.Count + course.Lessons.Sum(l => l.words.Count), p.items.Count);
            Assert.AreEqual(0, p.Due(today).Count);
        }
    }
}
