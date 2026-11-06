using System.Collections.Generic;
using System.Linq;
using Aib.Data;
using UnityEngine.UIElements;

namespace Aib.UI
{
    /// <summary>
    /// Reading section: interesting facts in Russian, with every sound the learner already knows
    /// written in Armenian letters. The more letters learned, the more Armenian each text shows.
    /// Without a text: the list of texts by topic. With one: the text itself.
    /// </summary>
    public class ReadingScreen : VisualElement, IBackHandler
    {
        readonly AppRoot _app;
        readonly ReadingText _text;

        public ReadingScreen(AppRoot app, ReadingText text = null)
        {
            _app = app;
            _text = text;
            AddToClassList("screen");
            if (text == null) BuildList();
            else BuildReader();
        }

        public void OnBack()
        {
            if (_text == null) _app.ShowHome();
            else _app.ShowReading();
        }

        void BuildList()
        {
            Add(Ui.TopBar("Чтение", _app.ShowHome));
            var scroll = Ui.Scroll("grow");
            int known = _app.KnownGlyphs().Count(g => g != "ւ");
            scroll.Add(Ui.Label(known == 0
                ? "Интересные факты о мире. Пройдите первую часть первого дня — и в текстах появятся армянские буквы."
                : $"Интересные факты о мире. Звуки, которые вы уже знаете (букв: {known}), записаны армянскими буквами. Чем больше букв вы выучите, тем больше армянского будет в текстах.",
                "muted", "center"));

            // Real Armenian texts: open once the whole alphabet is learned.
            if (_app.AlphabetDone)
                scroll.Add(Ui.Button("📖  Армянские тексты: сказки, стихи и песни ›", _app.ShowHyTexts, "btn-primary", "wide", "hy-entry"));
            else
                scroll.Add(Ui.Label("🔒  Армянские сказки, стихи и песни откроются, когда вы выучите все буквы.", "locked-note", "wrap"));

            var content = Ui.Box("reading-list"); // two columns in landscape
            foreach (var topic in _app.Reading.GroupBy(t => t.topic))
            {
                content.Add(Ui.Label(topic.Key, "section-title", "full-row"));
                foreach (var item in topic)
                {
                    var t = item;
                    bool read = _app.Progress.IsRead(t.id);
                    var b = Ui.Button($"{t.emoji}  {t.title}" + (read ? "  ✓" : ""), () => _app.ShowReadingText(t), "reading-item");
                    if (read) b.AddToClassList("read");
                    content.Add(b);
                }
            }
            scroll.Add(content);
            Add(scroll);
        }

        void BuildReader()
        {
            Add(Ui.TopBar("Чтение", _app.ShowReading));
            var glyphs = _app.KnownGlyphs();
            var scroll = Ui.Scroll("grow");
            var page = Ui.Box("reading-page");
            page.Add(Ui.Label(_text.emoji, "emoji-big"));
            var title = Ui.Label(ArmenianText.MixRussian(_text.title, glyphs), "reading-title");
            page.Add(title);
            page.Add(Ui.Label(glyphs.Count == 0
                ? "Пока вы не знаете армянских букв, текст по-русски."
                : "Армянские буквы — красные и жирные, русские — обычные. Читайте вслух, не спешите.", "muted", "center"));

            var paragraphs = _text.text.Split('\n');
            var labels = new List<Label>();
            foreach (var p in paragraphs)
            {
                var label = Ui.Label(ArmenianText.MixRussian(p, glyphs), "reading-text");
                labels.Add(label);
                page.Add(label);
            }

            // Check yourself: switch the whole text between the mixed and the plain Russian version.
            bool plain = false;
            Button toggle = null;
            toggle = Ui.Button("Показать по-русски", () =>
            {
                plain = !plain;
                title.text = Ui.Mark(plain ? _text.title : ArmenianText.MixRussian(_text.title, glyphs));
                for (int i = 0; i < labels.Count; i++)
                {
                    labels[i].text = Ui.Mark(plain ? paragraphs[i] : ArmenianText.MixRussian(paragraphs[i], glyphs));
                    labels[i].EnableInClassList("plain-text", plain);
                }
                toggle.text = plain ? "Показать с армянскими буквами" : "Показать по-русски";
            }, "btn-ghost", "small");
            if (glyphs.Count > 0) page.Add(toggle);
            scroll.Add(page);
            Add(scroll);

            // Done: mark as read and go on to the next unread text, or back to the list.
            var next = _app.Reading.SkipWhile(t => t != _text).Skip(1).FirstOrDefault(t => !_app.Progress.IsRead(t.id) && t != _text);
            var footer = Ui.Box("lesson-footer");
            footer.Add(Ui.Button(next != null ? "Прочитано · следующий текст ›" : "Прочитано ✓", () =>
            {
                _app.Progress.MarkRead(_text.id);
                _app.SaveProgress();
                if (next != null) _app.ShowReadingText(next);
                else _app.ShowReading();
            }, "btn-primary", "wide"));
            Add(footer);
        }
    }
}
