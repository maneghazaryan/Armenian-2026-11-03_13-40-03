using System.Collections.Generic;
using System.Linq;
using Aib.Data;
using UnityEngine.UIElements;

namespace Aib.UI
{
    /// <summary>
    /// All letters. Learned ones open their full explanation at any time;
    /// the rest only say when they will be learned.
    /// </summary>
    public class AlphabetScreen : VisualElement, IBackHandler
    {
        readonly AppRoot _app;
        readonly ScrollView _scroll;
        readonly VisualElement _detail;
        readonly HashSet<string> _learned;

        public AlphabetScreen(AppRoot app)
        {
            _app = app;
            AddToClassList("screen");

            Add(Ui.TopBar("Алфавит", app.ShowHome));

            _learned = app.Course.Lessons
                .Where(l => app.Progress.Result(l.day) != null)
                .SelectMany(l => l.newLetters)
                .ToHashSet();

            _scroll = Ui.Scroll("grow");
            var content = Ui.Box("alphabet-content"); // detail beside the grid in landscape
            _scroll.Add(content);
            _detail = Ui.Box("letter-detail");
            _detail.Add(Ui.Label($"Изучено букв: {_learned.Count}. Нажмите на букву, чтобы перечитать объяснение.", "muted", "center"));
            if (app.Course.Letters.Any(l => app.Progress.IsHard(l.Key)))
                _detail.Add(Ui.Label("Жёлтым отмечены буквы, в которых были ошибки.", "muted", "center"));
            content.Add(_detail);

            var grid = Ui.Box("letter-grid");
            foreach (var letter in app.Course.Letters)
            {
                var cell = Ui.Button(letter.upper + letter.lower, () => ShowLetter(letter), "letter-cell");
                if (_learned.Contains(letter.id)) cell.AddToClassList("learned");
                if (app.Progress.IsHard(letter.Key)) cell.AddToClassList("hard");
                grid.Add(cell);
            }
            content.Add(grid);
            Add(_scroll);
        }

        public void OnBack() => _app.ShowHome();

        void ShowLetter(LetterData letter)
        {
            _detail.Clear();
            if (_learned.Contains(letter.id))
            {
                _detail.Add(LetterCard.Build(_app, letter, compact: true, expanded: true));
            }
            else
            {
                var card = Ui.Box("letter-card");
                card.Add(Ui.Label(letter.upper + " " + letter.lower, "glyph-md"));
                int day = _app.Course.DayOf(letter);
                card.Add(Ui.Label(day > 0 ? $"Эту букву вы узнаете в день {day}." : "Эта буква будет в следующих уроках.", "muted", "center"));
                _detail.Add(card);
            }
            _scroll.scrollOffset = UnityEngine.Vector2.zero;
        }
    }

    /// <summary>Letter glyphs, name, Russian sound, hint, shape association, warning and optional deep explanation.</summary>
    public static class LetterCard
    {
        public static VisualElement Build(AppRoot app, LetterData letter, bool compact = false, bool expanded = false)
        {
            var card = Ui.Box("letter-card");
            // Like a primer page: the letter, a picture, and a keyword starting with its sound.
            var top = Ui.Box("letter-top");
            top.Add(Ui.Label(letter.upper + " " + letter.lower, compact ? "glyph-md" : "glyph-xl"));
            if (!string.IsNullOrEmpty(letter.pic)) top.Add(Ui.Label(letter.pic, compact ? "letter-pic-md" : "letter-pic"));
            card.Add(top);
            card.Add(Ui.Label(KeyLine(letter), "letter-name"));
            card.Add(Ui.Label($"буква «{letter.nameRu}» · большая и маленькая", "muted", "center"));
            card.Add(Ui.Label(letter.hint, "body", "center"));
            if (!string.IsNullOrEmpty(letter.look))
                card.Add(Ui.Label("Как запомнить: " + letter.look, "look"));
            if (!string.IsNullOrEmpty(letter.warn))
                card.Add(Ui.Label("⚠ " + letter.warn, "warn"));

            if (!string.IsNullOrEmpty(letter.more))
            {
                var more = Ui.Label(letter.more, "more");
                more.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
                if (!expanded)
                {
                    Button toggle = null;
                    toggle = Ui.Button("Подробнее о звуке ▾", () =>
                    {
                        bool show = more.style.display == DisplayStyle.None;
                        more.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                        toggle.text = show ? "Скрыть ▴" : "Подробнее о звуке ▾";
                        // Bring the opened text into view once it has been laid out.
                        var scroll = more.GetFirstAncestorOfType<ScrollView>();
                        if (show && scroll != null) scroll.schedule.Execute(() => scroll.ScrollTo(more)).StartingIn(50);
                    }, "btn-ghost", "small");
                    card.Add(toggle);
                }
                card.Add(more);
            }

            if (app.Audio.Has(letter.AudioId))
                card.Add(Ui.Button("🔊 Послушать", () => app.Audio.Play(letter.AudioId), "btn-secondary"));
            return card;
        }

        /// <summary>«[а]  арка» with the sound part of the keyword in red, or just the sound.</summary>
        public static string KeyLine(LetterData letter)
        {
            if (string.IsNullOrEmpty(letter.key)) return $"Звук [{letter.ru}]";
            var key = letter.key.Replace("{", $"<b><color={Ui.ArmenianColor}>").Replace("}", "</color></b>");
            return $"[{letter.ru}]  {key}";
        }
    }
}
