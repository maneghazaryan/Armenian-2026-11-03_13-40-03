using System;
using System.Linq;
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
            header.Add(Ui.Label($"Изучено букв: {learned} из {app.Course.Letters.Count}", "muted"));
            Add(header);

            var list = Ui.Scroll("grow");

            int due = app.Progress.Due(today).Count;
            if (due > 0)
            {
                var review = Ui.Box("day-card", "review-card");
                var text = Ui.Box("grow");
                text.Add(Ui.Label("Повторение на сегодня", "day-title"));
                text.Add(Ui.Label($"Букв и слов: {Math.Min(due, Leitner.MaxPerSession)}", "muted"));
                review.Add(text);
                review.Add(Ui.Button("Повторить", app.ShowReview, "btn-primary"));
                list.Add(review);
            }

            foreach (var lesson in app.Course.Lessons)
            {
                var state = DayUnlock.State(lesson.day, app.Progress, today);
                var card = Ui.Box("day-card", "day-" + state.ToString().ToLowerInvariant());

                var letters = string.Join("  ", lesson.newLetters.Select(id =>
                {
                    var l = app.Course.Letter(id);
                    return l.upper + " " + l.lower;
                }));
                var text = Ui.Box("grow");
                text.Add(Ui.Label($"День {lesson.day} · {lesson.title}", "day-title"));
                text.Add(Ui.Label(letters, "day-letters"));
                card.Add(text);

                switch (state)
                {
                    case DayState.Done:
                        card.Add(Ui.Button(Ui.Stars(app.Progress.Result(lesson.day).stars),
                            () => app.ShowLesson(lesson.day), "btn-ghost", "stars"));
                        break;
                    case DayState.Available:
                        card.Add(Ui.Button("Начать", () => app.ShowLesson(lesson.day), "btn-primary"));
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
            list.Add(Ui.Label("Новые уроки скоро появятся!", "muted", "center"));
            Add(list);

            var footer = Ui.Box("footer");
            footer.Add(Ui.Button("Алфавит", app.ShowAlphabet, "btn-secondary", "grow"));
            footer.Add(Ui.Button("Словарь", app.ShowArchive, "btn-secondary", "grow", "gap-left"));
            Add(footer);

            var settings = Ui.Box("footer");
            settings.Add(Ui.Button("🔔 " + Reminders.Label(app.Progress.reminderHour), () =>
            {
                app.Progress.reminderHour = Reminders.NextHour(app.Progress.reminderHour);
                app.SaveProgress();
                app.ShowHome();
            }, "btn-ghost", "small"));

            // Testing aid only: release builds always keep one lesson per day.
            if (Debug.isDebugBuild)
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
            Add(settings);
        }
    }
}
