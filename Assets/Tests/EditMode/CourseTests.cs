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
            CollectionAssert.AreEqual(new[] { "ա", "ս", "ր" }, course.LettersUpTo(1).Select(l => l.lower));
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
            {
                var list = ExerciseBuilder.Build(course, lesson, _ => hasAudio, seed);
                Assert.AreEqual(lesson.newLetters.Count, list.Count(e => e.Kind == ExerciseKind.LetterIntro));
                Assert.AreEqual(hasAudio, list.Any(e => e.Kind == ExerciseKind.ListenPickWord));
                foreach (var ex in list.Where(e => e.IsQuiz))
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
                var list = ExerciseBuilder.Build(course, lesson, _ => false, 5);
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
                var list = ExerciseBuilder.Build(course, lesson, _ => true, 7);
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
    }
}
