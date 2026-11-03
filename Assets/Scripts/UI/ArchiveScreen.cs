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
                var row = Ui.Box("archive-row");
                var text = Ui.Box("grow");
                text.Add(Ui.Label(item.hy, "archive-hy"));
                text.Add(Ui.Label($"[{item.tr}]  {item.ru}", "muted", "wrap"));
                if (!string.IsNullOrEmpty(item.emoji)) row.Add(Ui.Label(item.emoji, "archive-emoji"));
                row.Add(text);
                if (app.Audio.Has(item.AudioId))
                    row.Add(Ui.Button("🔊", () => app.Audio.Play(item.AudioId), "btn-ghost"));
                parent.Add(row);
            }
        }
    }
}
