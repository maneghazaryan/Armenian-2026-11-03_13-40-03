using System.Collections.Generic;
using System.Linq;
using Aib.Data;
using UnityEngine.UIElements;

namespace Aib.UI
{
    /// <summary>Dictionary of everything learned so far: words, phrases and verses from completed days.</summary>
    public class ArchiveScreen : VisualElement, IBackHandler
    {
        readonly AppRoot _app;

        public ArchiveScreen(AppRoot app)
        {
            _app = app;
            AddToClassList("screen");
            Add(Ui.TopBar("Словарь", app.ShowHome));

            var done = app.Course.Lessons.Where(l => app.Progress.Result(l.day) != null).ToList();
            var scroll = Ui.Scroll("grow");
            if (done.Count == 0)
                scroll.Add(Ui.Label("Здесь появятся слова, фразы и стихи из пройденных уроков.", "muted", "center"));
            else
                scroll.Add(Ui.Label("Сначала прочитайте слово сами, потом нажмите, чтобы проверить себя.", "muted", "center"));

            AddSection(app, scroll, "Слова", done.SelectMany(l => l.words));
            AddSection(app, scroll, "Фразы", done.SelectMany(l => l.phrases));
            AddSection(app, scroll, "Стихи", done.SelectMany(l => l.verses));
            Add(scroll);
        }

        public void OnBack() => _app.ShowHome();

        static void AddSection(AppRoot app, VisualElement parent, string title, IEnumerable<WordData> items)
        {
            var list = items.ToList();
            if (list.Count == 0) return;
            parent.Add(Ui.Label($"{title} ({list.Count})", "section-title"));
            foreach (var item in list)
            {
                // Read first: the meaning and pronunciation stay hidden until the row is tapped.
                var row = Ui.Box("archive-row");
                bool hard = app.Progress.IsHard(item.Key);
                if (hard) row.AddToClassList("hard");
                var text = Ui.Box("grow");
                text.Add(Ui.Label(item.hy, "archive-hy"));
                var hidden = hard ? "трудное слово · нажмите, чтобы проверить себя" : "нажмите, чтобы увидеть перевод";
                var meaning = $"[{item.tr}]  {item.ru}" + (string.IsNullOrEmpty(item.note) ? "" : "\n" + item.note);
                var answer = Ui.Label(hidden, "muted", "wrap");
                text.Add(answer);
                if (!string.IsNullOrEmpty(item.emoji)) row.Add(Ui.Label(item.emoji, "archive-emoji"));
                row.Add(text);
                if (app.Audio.Has(item.AudioId))
                {
                    var play = Ui.Button("🔊", () => app.Audio.Play(item.AudioId), "btn-ghost");
                    play.RegisterCallback<ClickEvent>(e => e.StopPropagation());
                    row.Add(play);
                }
                bool shown = false;
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    shown = !shown;
                    answer.text = Ui.Mark(shown ? meaning : hidden);
                });
                parent.Add(row);
            }
        }
    }
}
