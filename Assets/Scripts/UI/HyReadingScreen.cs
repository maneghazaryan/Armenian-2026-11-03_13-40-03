using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Aib.Data;
using UnityEngine.UIElements;

namespace Aib.UI
{
    /// <summary>
    /// Armenian texts for those who know the alphabet: Tumanyan's tales (retold simply and in the
    /// original), poems and songs. Without a text: the list, easiest first — by how many words of
    /// each text the learner doesn't know yet. With one: the text, where any word can be tapped to
    /// see its meaning and added to training. Words not known yet are red.
    /// </summary>
    public class HyReadingScreen : VisualElement, IBackHandler
    {
        /// <summary>Groups of the list by the number of words the learner doesn't know yet.</summary>
        public const int EasyMax = 25;
        public const int MediumMax = 60;

        readonly AppRoot _app;
        readonly HyText _text;
        readonly HashSet<string> _known;
        readonly List<(Label label, List<DictEntry> entries)> _words = new List<(Label, List<DictEntry>)>();
        VisualElement _popup;

        public HyReadingScreen(AppRoot app, HyText text = null)
        {
            _app = app;
            _text = text;
            _known = app.KnownWords();
            AddToClassList("screen");
            if (text == null) BuildList();
            else BuildReader();
        }

        public void OnBack()
        {
            if (_popup != null) ClosePopup();
            else if (_text == null) _app.ShowReading();
            else _app.ShowHyTexts();
        }

        /// <summary>Group title by the number of unknown words.</summary>
        public static string Level(int unknown) =>
            unknown <= EasyMax ? $"Мало новых слов (до {EasyMax})"
            : unknown <= MediumMax ? $"Больше новых слов (до {MediumMax})"
            : $"Много новых слов (больше {MediumMax})";

        void BuildList()
        {
            Add(Ui.TopBar("Армянские тексты", _app.ShowReading));
            var scroll = Ui.Scroll("grow");
            scroll.Add(Ui.Label(
                "Сказки Туманяна — в простом пересказе и в оригинале, стихи и песни. Нажмите на любое слово, чтобы увидеть перевод. " +
                "Сначала идут тексты, где меньше всего незнакомых вам слов.", "muted", "center"));

            var ranked = _app.HyTexts
                .Select(t => (text: t, unknown: _app.UnknownWords(t, _known)))
                .OrderBy(x => x.unknown)
                .ToList();
            var content = Ui.Box("reading-list");
            foreach (var group in ranked.GroupBy(x => Level(x.unknown)))
            {
                content.Add(Ui.Label(group.Key, "section-title", "full-row"));
                foreach (var (t, unknown) in group)
                {
                    bool read = _app.Progress.IsRead(t.ReadKey);
                    var item = Ui.Button("", () => _app.ShowHyText(t), "reading-item", "hy-item");
                    if (read) item.AddToClassList("read");
                    item.Add(Ui.Label($"{t.emoji}  {t.title}" + (read ? "  ✓" : ""), "hy-item-title"));
                    item.Add(Ui.Label($"{t.kind} · {t.author}\n{t.note}\nНезнакомых слов: {unknown}", "muted", "wrap"));
                    content.Add(item);
                }
            }
            scroll.Add(content);
            Add(scroll);
        }

        void BuildReader()
        {
            Add(Ui.TopBar(_text.kind, _app.ShowHyTexts));
            var scroll = Ui.Scroll("grow");
            var page = Ui.Box("reading-page");
            page.Add(Ui.Label(_text.emoji, "emoji-big"));
            page.Add(Ui.Label(_text.title, "reading-title"));
            page.Add(Ui.Label(_text.author, "muted", "center"));
            page.Add(Ui.Label(_text.note, "muted", "center", "wrap"));
            page.Add(Ui.Label("Нажмите на слово — появится перевод. Красные — слова, которых вы ещё не знаете.", "hy-legend", "wrap"));

            // Paragraphs and stanzas are separated by an empty line; inside them every line is kept.
            foreach (var block in _text.text.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                var para = Ui.Box("hy-block");
                foreach (var line in block.Split('\n'))
                {
                    var row = Ui.Box("hy-line");
                    foreach (var chunk in ArmenianDictionary.Chunks(line))
                        row.Add(WordLabel(chunk));
                    para.Add(row);
                }
                page.Add(para);
            }
            scroll.Add(page);
            Add(scroll);

            var next = _app.HyTexts
                .Select(t => (text: t, unknown: _app.UnknownWords(t, _known)))
                .Where(x => x.text != _text && !_app.Progress.IsRead(x.text.ReadKey))
                .OrderBy(x => x.unknown)
                .Select(x => x.text)
                .FirstOrDefault();
            var footer = Ui.Box("lesson-footer");
            footer.Add(Ui.Button(next != null ? "Прочитано · следующий текст ›" : "Прочитано ✓", () =>
            {
                _app.Progress.MarkRead(_text.ReadKey);
                _app.SaveProgress();
                if (next != null) _app.ShowHyText(next);
                else _app.ShowHyTexts();
            }, "btn-primary", "wide"));
            Add(footer);
        }

        Label WordLabel(string chunk)
        {
            var label = new Label(chunk);
            label.AddToClassList("hy-word");
            var entries = _app.Course.Dictionary.Lookup(chunk, _text.id)
                .Where(x => x.form != null)
                .Select(x => x.form.Entry)
                .ToList();
            if (entries.Count > 0)
            {
                _words.Add((label, entries));
                Mark(label, entries);
                label.RegisterCallback<ClickEvent>(_ => ShowWord(chunk));
            }
            return label;
        }

        bool IsKnown(DictEntry e) => e.IsName || _known.Contains(e.hy.ToLowerInvariant());

        void Mark(Label label, List<DictEntry> entries) =>
            label.EnableInClassList("unknown", entries.Any(e => !IsKnown(e)));

        /// <summary>Panel at the bottom: the word, how it reads, what it means, and «Учить это слово».</summary>
        void ShowWord(string chunk)
        {
            ClosePopup();
            _popup = Ui.Box("word-popup");
            var close = Ui.Button("✕", ClosePopup, "btn-ghost", "popup-close");
            _popup.Add(close);

            foreach (var (word, form) in _app.Course.Dictionary.Lookup(chunk, _text.id))
            {
                var shown = Letters(chunk).Contains('-') ? word : Letters(chunk);
                _popup.Add(Ui.Label($"{shown}  [{ArmenianText.Transcribe(word)}]", "popup-word"));
                if (form == null)
                {
                    _popup.Add(Ui.Label("Этого слова нет в словаре.", "muted"));
                    continue;
                }
                var entry = form.Entry;
                _popup.Add(Ui.Label(form.Note ?? entry.ru, "popup-meaning", "wrap"));
                if (form.Note != null || Normalize(entry.hy) != word)
                    _popup.Add(Ui.Label($"Словарная форма: {entry.hy} — {entry.ru}", "muted", "wrap"));

                if (entry.IsName) continue;
                if (IsKnown(entry))
                {
                    _popup.Add(Ui.Label("✓ Вы учите это слово", "popup-known"));
                    continue;
                }
                Button learn = null;
                learn = Ui.Button($"Учить «{entry.hy}»", () =>
                {
                    _app.Progress.Learn(new[] { entry.Key }, DateTime.Now);
                    _app.SaveProgress();
                    _known.Add(entry.hy.ToLowerInvariant());
                    foreach (var (label, entries) in _words) Mark(label, entries);
                    learn.SetEnabled(false);
                    learn.text = "✓ Добавлено в повторение";
                }, "btn-secondary", "small");
                _popup.Add(learn);
            }
            Add(_popup);
        }

        void ClosePopup()
        {
            _popup?.RemoveFromHierarchy();
            _popup = null;
        }

        static string Normalize(string s) => ArmenianDictionary.Normalize(s);

        /// <summary>The word as written, without punctuation and the marks inside it.</summary>
        static string Letters(string chunk)
        {
            var sb = new StringBuilder();
            foreach (char c in chunk)
                if (char.IsLetter(c) || c == '-') sb.Append(c);
            return sb.ToString().Trim('-');
        }
    }
}
