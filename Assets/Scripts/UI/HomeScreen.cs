using System;
using System.Linq;
using Aib.Lesson;
using Aib.Progress;
using UnityEngine;
using UnityEngine.UIElements;

namespace Aib.UI
{
    /// <summary>Today's review, the path of daily lessons, and links to the alphabet, dictionary and reminders.</summary>
    public class HomeScreen : VisualElement
    {
        public HomeScreen(AppRoot app)
        {
            AddToClassList("screen");
            var today = DateTime.Now;

            var header = Ui.Box("header");
            header.Add(Ui.Label("Այբ", "logo"));
            header.Add(Ui.Label("Армянский с нуля", "subtitle"));
            int learned = app.Course.Lessons
                .Where(l => app.Progress.Result(l.day) != null)
                .Sum(l => l.newLetters.Count);
            int words = app.Progress.items.Count(i => !i.key.StartsWith("l:"));
            header.Add(Ui.Label($"Изучено букв: {learned} из {app.Course.Letters.Count}" + (words > 0 ? $" · слов: {words}" : ""), "muted"));
            Add(header);

            var scroll = Ui.Scroll("grow");
            var list = Ui.Box("day-list"); // two columns in landscape
            scroll.Add(list);

            // Review is always open once something is learned: today's due items, or free practice.
            int due = app.Progress.Due(today).Count;
            if (app.Progress.items.Count > 0)
            {
                var review = Ui.Box("day-card", "review-card");
                var text = Ui.Box("grow");
                text.Add(Ui.Label(due > 0 ? "Повторение на сегодня" : "Повторение", "day-title"));
                text.Add(Ui.Label(due > 0
                    ? $"Букв и слов: {Math.Min(due, Leitner.MaxPerSession)}"
                    : "На сегодня всё повторено — можно потренироваться ещё.", "muted", "wrap"));
                review.Add(text);
                review.Add(Ui.Button(due > 0 ? "Повторить" : "Тренировка", app.ShowReview, due > 0 ? "btn-primary" : "btn-secondary"));
                list.Add(review);
            }

            foreach (var lesson in app.Course.Lessons)
            {
                // After the alphabet: a new set of words every day.
                if (lesson.day == app.Course.AlphabetDays + 1)
                {
                    list.Add(Ui.Label("Слова на каждый день", "section-title", "full-row"));
                    list.Add(Ui.Label("Алфавит выучен — дальше каждый день новые слова и их повторение.", "muted", "full-row"));
                }
                var state = DayUnlock.State(lesson.day, app.Progress, today);
                var card = Ui.Box("day-card", "day-" + state.ToString().ToLowerInvariant());

                var letters = lesson.IsWordsDay
                    ? string.Join(", ", lesson.words.Take(3).Select(w => w.hy)) + "…"
                    : string.Join("  ", lesson.newLetters.Select(id =>
                    {
                        var l = app.Course.Letter(id);
                        return l.upper + " " + l.lower;
                    }));
                var text = Ui.Box("grow");
                text.Add(Ui.Label($"День {lesson.day} · {lesson.title}", "day-title"));
                text.Add(Ui.Label(letters, "day-letters"));
                int parts = app.Progress.PartsDone(lesson.day, ExerciseBuilder.Parts);
                if (state == DayState.Available || state == DayState.ReviewFirst)
                    text.Add(Ui.Label(PartDots(parts), "parts"));
                card.Add(text);

                switch (state)
                {
                    case DayState.Done:
                        card.Add(Ui.Button(Ui.Stars(app.Progress.Result(lesson.day).stars),
                            () => app.ShowLesson(lesson.day), "btn-ghost", "stars"));
                        break;
                    case DayState.Available:
                        card.Add(Ui.Button(parts == 0 ? "Начать" : $"Часть {parts + 1}", () => app.ShowLesson(lesson.day), "btn-primary"));
                        break;
                    case DayState.ReviewFirst:
                        card.Add(Ui.Label("После\nповторения", "lock", "center"));
                        break;
                    case DayState.LockedUntilTomorrow:
                        card.Add(Ui.Label("Завтра", "lock"));
                        break;
                    default:
                        card.Add(Ui.Label("🔒", "lock"));
                        break;
                }
                list.Add(card);
            }
            list.Add(Ui.Label("Новые уроки скоро появятся!", "muted", "center", "full-row"));
            Add(scroll);

            var footer = Ui.Box("footer");
            footer.Add(Ui.Button("Алфавит", app.ShowAlphabet, "btn-secondary", "grow"));
            footer.Add(Ui.Button("Словарь", app.ShowArchive, "btn-secondary", "grow", "gap-left"));
            footer.Add(Ui.Button("Чтение", app.ShowReading, "btn-secondary", "grow", "gap-left"));
            Add(footer);

            var settings = Ui.Box("footer", "settings");
            if (Reminders.Supported)
                settings.Add(Ui.Button("🔔 " + Reminders.Label(app.Progress.reminderHour), () =>
            {
                app.Progress.reminderHour = Reminders.NextHour(app.Progress.reminderHour);
                app.SaveProgress();
                app.ShowHome();
            }, "btn-ghost", "small"));

            // On: the next day opens tomorrow. Off: the learner may take several lessons in one day.
            {
                var dayLock = new Toggle("Один урок в день") { value = app.Progress.dayLock };
                dayLock.AddToClassList("toggle");
                dayLock.RegisterValueChangedCallback(e =>
                {
                    app.Progress.dayLock = e.newValue;
                    app.SaveProgress();
                    app.ShowHome();
                });
                settings.Add(dayLock);
            }
            // Start over. Asks once more before doing it.
            if (app.Progress.results.Count > 0 || app.Progress.items.Count > 0 || app.Progress.partial.Count > 0)
            {
                Button reset = null;
                bool asked = false;
                reset = Ui.Button("Сбросить прогресс", () =>
                {
                    if (asked) { app.ResetProgress(); return; }
                    asked = true;
                    reset.text = "Начать заново? Всё пройденное забудется. Нажмите ещё раз";
                }, "btn-ghost", "small");
                settings.Add(reset);
            }

            // For testers: open everything at once. Asks once more before doing it.
            if (app.Course.Lessons.Any(l => app.Progress.Result(l.day) == null))
            {
                Button unlock = null;
                bool asked = false;
                unlock = Ui.Button("Открыть все уроки", () =>
                {
                    if (asked) { app.UnlockAll(); return; }
                    asked = true;
                    unlock.text = "Отметить все дни пройденными? Нажмите ещё раз";
                }, "btn-ghost", "small");
                settings.Add(unlock);
            }
            Add(settings);
        }

        /// <summary>"● ● ○  часть 3 из 3" style progress through the parts of a day.</summary>
        static string PartDots(int done)
        {
            var dots = string.Join(" ", Enumerable.Range(0, ExerciseBuilder.Parts).Select(i => i < done ? "●" : "○"));
            return done == 0 ? $"{dots}  урок, практика, чтение" : $"{dots}  часть {done + 1} из {ExerciseBuilder.Parts}";
        }
    }
}
