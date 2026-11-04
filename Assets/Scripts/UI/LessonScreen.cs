using System;
using System.Collections.Generic;
using System.Linq;
using Aib.Data;
using Aib.Lesson;
using UnityEngine.UIElements;

namespace Aib.UI
{
    /// <summary>What a lesson screen plays: a day's lesson or a review.</summary>
    public class LessonPlan
    {
        public string Heading;
        public string Title;
        public string Intro;
        public List<Exercise> Exercises;
        /// <summary>Saves the outcome and returns summary lines for the result screen.</summary>
        public Func<LessonSession, IEnumerable<string>> Finish;
    }

    /// <summary>Plays a lesson or review exercise by exercise, then shows the result.</summary>
    public class LessonScreen : VisualElement, IBackHandler
    {
        readonly AppRoot _app;
        readonly LessonPlan _plan;
        readonly LessonSession _session;
        readonly VisualElement _progressFill;
        readonly ScrollView _body;
        readonly VisualElement _footer;
        readonly Button _back;
        bool _hadMistake;

        public LessonScreen(AppRoot app, LessonPlan plan)
        {
            _app = app;
            _plan = plan;
            AddToClassList("screen");
            _session = new LessonSession(plan.Exercises);

            var top = Ui.Box("topbar");
            _back = Ui.Button("‹", OnBack, "btn-back", "btn-back-round");
            top.Add(_back);
            var bar = Ui.Box("progress", "grow");
            _progressFill = Ui.Box("progress-fill");
            bar.Add(_progressFill);
            top.Add(bar);
            top.Add(Ui.Button("✕", app.ShowHome, "btn-icon", "gap-left"));
            Add(top);

            // Scrolls when a card grows (hints, deep explanations), so nothing hides under the footer.
            // Short content stays vertically centred: the content is at least as tall as the viewport.
            _body = Ui.Scroll("grow");
            _body.contentContainer.AddToClassList("lesson-body");
            _body.contentViewport.RegisterCallback<GeometryChangedEvent>(e =>
                _body.contentContainer.style.minHeight = e.newRect.height);
            Add(_body);
            _footer = Ui.Box("lesson-footer");
            Add(_footer);

            ShowIntro();
        }

        /// <summary>One step back: previous exercise, or the intro from the first one.</summary>
        public void OnBack()
        {
            if (_session.IsFinished) return; // already saved; the result screen only leads home
            if (_session.CanGoBack)
            {
                _session.Back();
                ShowCurrent();
            }
            else if (_back.visible) ShowIntro();
            else _app.ShowHome();
        }

        void ShowIntro()
        {
            _back.visible = false;
            ClearBody();
            _body.Add(Ui.Label(_plan.Heading, "muted", "center"));
            _body.Add(Ui.Label(_plan.Title, "title", "center"));
            _body.Add(Ui.Label(_plan.Intro, "body", "center"));
            SetFooter(null, "Поехали!", ShowCurrent);
        }

        void ShowCurrent()
        {
            _progressFill.style.width = Length.Percent(100f * _session.Index / _session.Total);
            if (_session.IsFinished)
            {
                ShowResult();
                return;
            }

            _back.visible = true;
            _hadMistake = false;
            ClearBody();
            _footer.Clear();
            _footer.RemoveFromClassList("feedback");
            var ex = _session.Current;
            switch (ex.Kind)
            {
                case ExerciseKind.LetterIntro: ShowLetterIntro(ex); break;
                case ExerciseKind.WordIntro: ShowWordIntro(ex); break;
                case ExerciseKind.PickLetter: ShowPickLetter(ex); break;
                case ExerciseKind.MatchCase: ShowMatchCase(ex); break;
                case ExerciseKind.ReadPickTranslation: ShowReadPick(ex); break;
                case ExerciseKind.ListenPickWord: ShowListenPick(ex); break;
                case ExerciseKind.BuildWord: ShowBuildWord(ex); break;
                case ExerciseKind.Phrase: ShowPhrase(ex); break;
            }
        }

        void Next()
        {
            _session.Complete(_hadMistake);
            ShowCurrent();
        }

        // ---------- exercises ----------

        void ShowLetterIntro(Exercise ex)
        {
            _body.Add(Ui.Label("Новая буква", "muted", "center"));
            _body.Add(LetterCard.Build(_app, ex.Letter));
            if (ex.UseAudio) _app.Audio.Play(ex.Letter.AudioId);
            SetFooter(null, "Дальше", Next);
        }

        void ShowWordIntro(Exercise ex)
        {
            _body.Add(Ui.Label("Новое слово", "muted", "center"));
            var card = Ui.Box("letter-card");
            card.Add(Ui.Label(ex.Word.emoji ?? "", "emoji-big"));
            card.Add(Ui.Label(ex.Word.hy, "word-big"));
            card.Add(Ui.Label($"[{ex.Word.tr}]", "hint"));
            card.Add(Ui.Label(ex.Word.ru, "title", "center"));
            if (ex.UseAudio)
            {
                card.Add(PlayButton(ex.Word.AudioId, autoplay: true));
            }
            _body.Add(card);
            _body.Add(Ui.Label("Прочитайте слово вслух по буквам, потом целиком.", "muted", "center"));
            SetFooter(null, "Дальше", Next);
        }

        void ShowPickLetter(Exercise ex)
        {
            if (ex.UseAudio)
            {
                _body.Add(Ui.Label("Какую букву вы слышите?", "prompt"));
                _body.Add(PlayButton(ex.Letter.AudioId, autoplay: true));
            }
            else
            {
                _body.Add(Ui.Label("Какая буква читается как", "prompt"));
                _body.Add(Ui.Label($"[{ex.Letter.ru}]", "glyph-md"));
            }
            AddOptions(ex, "option-glyph", o => o);
        }

        void ShowMatchCase(Exercise ex)
        {
            bool fromCapital = ex.Shown == ex.Letter.upper;
            _body.Add(Ui.Label(fromCapital ? "Найдите маленькую (строчную) пару" : "Найдите большую (заглавную) пару", "prompt"));
            _body.Add(Ui.Label(ex.Shown, "glyph-xl"));
            var pair = $"{ex.Letter.upper} {ex.Letter.lower} — «{ex.Letter.name}» [{ex.Letter.ru}]";
            AddOptions(ex, "option-glyph", o => o, extra: pair);
        }

        void ShowReadPick(Exercise ex)
        {
            _body.Add(Ui.Label("Прочитайте слово", "prompt"));
            _body.Add(Ui.Label(ex.Shown ?? ex.Word.hy, "word-big"));
            var hint = Ui.Label("", "hint");
            _body.Add(Ui.Button("Подсказка", () => hint.text = ex.Word.tr, "btn-ghost", "small"));
            _body.Add(hint);
            AddOptions(ex, "option-text", o => o, () =>
            {
                hint.text = ex.Word.tr;
                if (ex.UseAudio) _app.Audio.Play(ex.Word.AudioId);
            });
        }

        void ShowListenPick(Exercise ex)
        {
            _body.Add(Ui.Label("Что вы слышите?", "prompt"));
            _body.Add(PlayButton(ex.Word.AudioId, autoplay: true));
            AddOptions(ex, "option-word", o => o);
        }

        void ShowBuildWord(Exercise ex)
        {
            _body.Add(Ui.Label("Соберите слово", "prompt"));
            _body.Add(Ui.Label($"{ex.Word.emoji} {ex.Word.ru}", "title", "center"));
            if (ex.UseAudio) _body.Add(PlayButton(ex.Word.AudioId, autoplay: false));

            var target = ArmenianText.Tokenize(ex.Word.hy);
            var slots = Ui.Box("slots");
            var slotLabels = target.Select(_ =>
            {
                var s = Ui.Label("", "slot");
                slots.Add(s);
                return s;
            }).ToList();
            _body.Add(slots);

            var tiles = Ui.Box("tiles");
            int pos = 0;
            foreach (var glyph in ex.Options)
            {
                Button tile = null;
                tile = Ui.Button(glyph, () =>
                {
                    if (pos >= target.Count) return;
                    if (glyph == target[pos])
                    {
                        slotLabels[pos].text = pos == 0 ? Capitalize(ex.Word.hy, glyph) : glyph;
                        slotLabels[pos].AddToClassList("filled");
                        tile.SetEnabled(false);
                        pos++;
                        if (pos == target.Count)
                        {
                            if (ex.UseAudio) _app.Audio.Play(ex.Word.AudioId);
                            ShowFeedback(ex.Word.hy + " — " + ex.Word.tr);
                        }
                    }
                    else
                    {
                        _hadMistake = true;
                        Shake(tile);
                    }
                }, "tile");
                tiles.Add(tile);
            }
            _body.Add(tiles);
        }

        void ShowPhrase(Exercise ex)
        {
            _body.Add(Ui.Label("Прочитайте вслух", "prompt"));
            _body.Add(Ui.Label(ex.Word.hy, "word-big"));
            var reveal = Ui.Box("phrase-reveal");
            reveal.Add(Ui.Label(ex.Word.tr, "hint"));
            reveal.Add(Ui.Label(ex.Word.ru, "body", "center"));
            reveal.style.display = DisplayStyle.None;
            _body.Add(Ui.Button("Показать перевод", () => reveal.style.display = DisplayStyle.Flex, "btn-ghost", "small"));
            _body.Add(reveal);
            if (ex.UseAudio) _body.Add(PlayButton(ex.Word.AudioId, autoplay: false));
            SetFooter(null, "Дальше", Next);
        }

        // ---------- shared pieces ----------

        void AddOptions(Exercise ex, string optionClass, Func<string, string> label, Action onAnswered = null, string extra = null)
        {
            var box = Ui.Box("options");
            foreach (var option in ex.Options)
            {
                Button b = null;
                b = Ui.Button(label(option), () =>
                {
                    bool correct = option == ex.Answer;
                    if (!correct)
                    {
                        _hadMistake = true;
                        b.AddToClassList("wrong");
                        b.SetEnabled(false);
                        return;
                    }
                    b.AddToClassList("right");
                    box.SetEnabled(false);
                    onAnswered?.Invoke();
                    ShowFeedback(extra);
                }, "option", optionClass);
                box.Add(b);
            }
            _body.Add(box);
        }

        Button PlayButton(string audioId, bool autoplay)
        {
            if (autoplay) _app.Audio.Play(audioId);
            return Ui.Button("🔊", () => _app.Audio.Play(audioId), "btn-play");
        }

        void ShowFeedback(string extra)
        {
            var text = _hadMistake ? "Получилось! Повторим ещё раз в конце." : "Верно!";
            if (!string.IsNullOrEmpty(extra)) text += "\n" + extra;
            SetFooter(text, "Дальше", Next);
        }

        void SetFooter(string message, string buttonText, Action onClick)
        {
            _footer.Clear();
            _footer.EnableInClassList("feedback", message != null);
            if (message != null) _footer.Add(Ui.Label(message, "feedback-text"));
            _footer.Add(Ui.Button(buttonText, onClick, "btn-primary", "wide"));
        }

        void ShowResult()
        {
            var lines = _plan.Finish(_session).ToList();
            _back.visible = false;
            ClearBody();
            _body.Add(Ui.Label("Готово!", "title", "center"));
            _body.Add(Ui.Label(Ui.Stars(Progress.DayUnlock.Stars(_session.Mistakes)), "result-stars"));
            foreach (var line in lines)
                _body.Add(Ui.Label(line, "body", "center"));
            SetFooter(null, "На главную", _app.ShowHome);
        }

        void ClearBody()
        {
            _body.Clear();
            _body.scrollOffset = UnityEngine.Vector2.zero;
        }

        static string Capitalize(string word, string glyph) =>
            char.IsUpper(word[0]) ? word.Substring(0, glyph.Length) : glyph;

        void Shake(VisualElement e)
        {
            e.AddToClassList("shake");
            schedule.Execute(() => e.RemoveFromClassList("shake")).StartingIn(250);
        }
    }
}
