using System;
using System.Collections.Generic;
using System.Linq;
using Aib.Audio;
using Aib.Data;
using Aib.Lesson;
using Aib.Progress;
using UnityEngine;
using UnityEngine.InputSystem;
using FontAsset = UnityEngine.TextCore.Text.FontAsset;
using UnityEngine.UIElements;

namespace Aib.UI
{
    /// <summary>
    /// Creates the whole app at startup: UI document, fonts, styles, audio and screens.
    /// Nothing needs to be set up in the scene.
    /// </summary>
    public class AppRoot : MonoBehaviour
    {
        public Course Course { get; private set; }
        public ProgressData Progress { get; private set; }
        public AudioService Audio { get; private set; }
        /// <summary>Interesting facts for the reading section.</summary>
        public List<ReadingText> Reading { get; private set; }
        /// <summary>Stories, poems and songs in Armenian, for after the alphabet.</summary>
        public List<HyText> HyTexts { get; private set; }

        /// <summary>Layout is designed for 1280 units of height and 720 of width (portrait).</summary>
        static readonly Vector2Int Reference = new Vector2Int(720, 1280);

        PanelSettings _panel;
        VisualElement _root;
        VisualElement _screenHost;
        Vector2Int _appliedScreen;
        Rect _appliedSafeArea;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<AppRoot>() != null) return;
            var go = new GameObject("Aib");
            DontDestroyOnLoad(go);
            go.AddComponent<AppRoot>();
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            Course = Course.FromJson(
                Resources.Load<TextAsset>("Data/letters").text,
                Resources.Load<TextAsset>("Data/lessons").text,
                Resources.Load<TextAsset>("Data/words").text);
            Course.Dictionary = ArmenianDictionary.Parse(Resources.Load<TextAsset>("Data/dictionary").text);
            Reading = ReadingText.ListFromJson(Resources.Load<TextAsset>("Data/reading").text);
            HyTexts = HyText.ListFromJson(Resources.Load<TextAsset>("Data/texts_hy").text);
            Progress = ProgressStore.Load();
            Progress.Prune(key => Course.ByKey(key) != null);
            Audio = gameObject.AddComponent<AudioService>();

            _panel = ScriptableObject.CreateInstance<PanelSettings>();
            _panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/AppTheme");
            _panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _panel.referenceResolution = Reference;
            _panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;

            var doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = _panel;

            _root = doc.rootVisualElement;
            _root.AddToClassList("app");
            _root.style.unityFontDefinition = FontDefinition.FromSDFFont(CreateFont());
            _screenHost = new VisualElement();
            _screenHost.AddToClassList("screen-host");
            _root.Add(_screenHost);
            ApplyLayout(new Vector2Int(Screen.width, Screen.height), Screen.safeArea);

            Reminders.Schedule(Progress, DoneToday());
            if (Progress.ShouldOfferRecall(DateTime.Now)) Show(new RecallOfferScreen(this));
            else ShowHome();
        }

        /// <summary>Words, letters and phrases in the recall offered at app start.</summary>
        public const int RecallItems = 6;
        public const int RecallSentences = 1;

        /// <summary>The offer was answered (taken or skipped): not again today.</summary>
        public void RecallOffered()
        {
            Progress.lastRecall = DayUnlock.Format(DateTime.Now);
            SaveProgress();
        }

        /// <summary>
        /// Short recall of what is known already: due items, then a random choice weighted to mistakes
        /// and weakly known items, and a phrase of a finished day to build from its words. Due items move
        /// in the review; a mistake on any item brings it back tomorrow.
        /// </summary>
        public void ShowRecall()
        {
            RecallOffered();
            var now = DateTime.Now;
            var rng = new System.Random(Environment.TickCount);
            var keys = Progress.RecallKeys(RecallItems, rng, now);
            var due = Progress.Due(now).Select(i => i.key).ToHashSet();
            var phrases = Course.Lessons
                .Where(l => l.IsWordsDay && Progress.Result(l.day) != null)
                .SelectMany(l => l.phrases)
                .Where(ExerciseBuilder.CanBuild)
                .OrderBy(_ => rng.Next())
                .Take(RecallSentences)
                .Select(p => ExerciseBuilder.BuildSentence(p, rng));
            var exercises = ExerciseBuilder.BuildReview(Course, keys, Progress.LastCompletedDay, Audio.Has, rng.Next());
            exercises.AddRange(phrases);
            Show(new LessonScreen(this, new LessonPlan
            {
                Heading = "Вспомним",
                Title = "Что вы уже знаете",
                Intro = "Несколько слов, которые вы уже учили, и одна фраза из них. Чаще всего — те, в которых были ошибки.",
                Exercises = exercises,
                Finish = session =>
                {
                    foreach (var key in keys)
                        if (due.Contains(key) || session.Missed.Contains(key))
                            Progress.Reviewed(key, !session.Missed.Contains(key), now);
                    Progress.Missed(session.Missed);
                    if (keys.Any(due.Contains)) Progress.MarkReviewed(now);
                    SaveProgress();
                    int remembered = keys.Count(k => !session.Missed.Contains(k));
                    return new[]
                    {
                        $"Вспомнили сразу: {remembered} из {keys.Count}.",
                        session.Missed.Count > 0 ? "Ошибки повторим завтра." : "Отлично, всё помните!",
                    };
                },
            }));
        }

        /// <summary>Noto Sans (Latin + Cyrillic) with Armenian, symbol and emoji fallbacks.</summary>
        static FontAsset CreateFont()
        {
            static FontAsset Load(string name) =>
                FontAsset.CreateFontAsset(Resources.Load<Font>("Fonts/" + name));

            var main = Load("NotoSans-Regular");
            main.fallbackFontAssetTable = new List<FontAsset>
            {
                Load("NotoSansArmenian-Regular"),
                Load("NotoSansSymbols2-Regular"),
                Load("NotoEmoji-Regular"),
            };
            return main;
        }

        void Update()
        {
            // Android back button arrives as Escape.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame &&
                _screenHost.childCount > 0 && _screenHost[0] is IBackHandler screen)
                screen.OnBack();

            ApplyLayout(new Vector2Int(Screen.width, Screen.height), Screen.safeArea);
        }

        /// <summary>
        /// Portrait (phones) scales by width, landscape (PC, web, tablets) by height, so the UI
        /// always has 1280 units of height to work with. The root gets a "portrait" or
        /// "landscape" class for layout rules in App.uss, and padding that keeps content clear
        /// of notches, rounded corners and the home indicator.
        /// </summary>
        void ApplyLayout(Vector2Int screen, Rect safe)
        {
            if (screen.x <= 0 || screen.y <= 0 || (screen == _appliedScreen && safe == _appliedSafeArea)) return;
            _appliedScreen = screen;
            _appliedSafeArea = safe;

            bool landscape = screen.x > screen.y;
            _panel.match = landscape ? 1f : 0f;
            _root.EnableInClassList("landscape", landscape);
            _root.EnableInClassList("portrait", !landscape);

            // Screen pixels per UI unit, as ScaleWithScreenSize computes it for match 0 or 1.
            float scale = landscape ? (float)screen.y / Reference.y : (float)screen.x / Reference.x;
            _root.style.paddingLeft = safe.xMin / scale;
            _root.style.paddingRight = (screen.x - safe.xMax) / scale;
            _root.style.paddingTop = (screen.y - safe.yMax) / scale;
            _root.style.paddingBottom = safe.yMin / scale;
        }

        void Show(VisualElement screen)
        {
            _screenHost.Clear();
            _screenHost.Add(screen);
        }

        public void ShowHome() => Show(new HomeScreen(this));

        public void ShowAlphabet() => Show(new AlphabetScreen(this));

        public void ShowArchive() => Show(new ArchiveScreen(this));

        public void ShowReading() => Show(new ReadingScreen(this));

        public void ShowReadingText(ReadingText text) => Show(new ReadingScreen(this, text));

        public void ShowHyTexts() => Show(new HyReadingScreen(this));

        public void ShowHyText(HyText text) => Show(new HyReadingScreen(this, text));

        /// <summary>All letter days passed: the Armenian texts are open.</summary>
        public bool AlphabetDone => Course.Lessons.Where(l => !l.IsWordsDay).All(l => Progress.Result(l.day) != null);

        /// <summary>
        /// Dictionary words the learner knows: words of passed days and words added to training.
        /// Course words count by their written form, so «մայր» learned on day 11 is known in texts.
        /// </summary>
        public HashSet<string> KnownWords()
        {
            var known = new HashSet<string>();
            foreach (var item in Progress.items)
                if (Course.ByKey(item.key) is WordData w)
                    known.Add(w.hy.ToLowerInvariant());
            return known;
        }

        /// <summary>Words of a text the learner does not know yet (names don't count).</summary>
        public int UnknownWords(HyText text, HashSet<string> known) =>
            Course.Dictionary.WordsOf(text.text, text.id).Count(e => !e.IsName && !known.Contains(e.hy.ToLowerInvariant()));

        /// <summary>Letters met so far: those of finished days and of a day whose first part is done.</summary>
        public HashSet<string> KnownGlyphs()
        {
            int day = Course.Lessons
                .Where(l => Progress.PartsDone(l.day, ExerciseBuilder.Parts) > 0)
                .Select(l => l.day)
                .DefaultIfEmpty(0)
                .Max();
            return Course.GlyphsUpTo(day);
        }

        /// <summary>
        /// One part of a day: 1 — new letters and words, 2 — more practice, 3 — reading.
        /// Without a part, continues where the learner stopped (part 1 when replaying a finished day).
        /// </summary>
        public void ShowLesson(int day, int part = 0)
        {
            // Words the learner chose not to learn are left out.
            var lesson = Course.Lesson(day).Keeping(key => !Progress.IsRemoved(key));
            bool topic = Course.IsTopic(day);
            const int parts = ExerciseBuilder.Parts;
            if (part == 0)
                part = Progress.Result(day) != null ? 1 : Progress.PartsDone(day, parts) + 1;

            string title = part == 1 ? lesson.title : part == 2 ? "Ещё практика" : "Читаем";
            string intro = part == 1
                ? lesson.intro
                : part == 2
                    ? "Те же слова — новые упражнения. Если слово забылось, нажмите «Подсказка»."
                    : lesson.IsWordsDay
                        ? "Фразы с новыми словами: прочитайте вслух, потом проверьте себя. В конце — каждое слово ещё раз."
                        : "Знакомые русские слова, в которых звуки, уже известные вам, записаны армянскими буквами. Армянские буквы — красные и жирные. Прочитайте вслух, потом проверьте себя.";

            Show(new LessonScreen(this, new LessonPlan
            {
                Heading = topic ? $"Тема «{lesson.title}» · часть {part} из {parts}" : $"День {day} · часть {part} из {parts}",
                Title = title,
                Intro = intro,
                Letters = part == 1 ? lesson.newLetters.Select(Course.Letter).ToList() : null,
                Exercises = ExerciseBuilder.Build(Course, lesson, part, Audio.Has, Environment.TickCount),
                NextLabel = part < parts ? $"Часть {part + 1} ›" : null,
                Next = part < parts ? () => ShowLesson(day, part + 1) : null,
                Finish = session =>
                {
                    var now = DateTime.Now;
                    Progress.Missed(session.Missed);
                    bool dayDone = Progress.CompletePart(day, part, parts, session.Mistakes, now);
                    if (dayDone)
                        Progress.Learn(lesson.newLetters.Select(id => Course.Letter(id).Key)
                            .Concat(lesson.words.Select(w => w.Key))
                            .Concat(Course.ReviewWords(lesson).Select(w => w.Key)), now);
                    SaveProgress();
                    if (!dayDone)
                        return new[] { part < parts ? $"Часть {part} из {parts} пройдена." : "Повторили ещё раз." };
                    return new[]
                    {
                        topic ? $"Тема «{lesson.title}» пройдена!" : $"День {day} пройден!",
                        lesson.IsWordsDay ? null : "Новые буквы: " + string.Join("  ", lesson.newLetters.Select(id => Course.Letter(id).lower)),
                        lesson.words.Count > 0 ? "Новые слова: " + string.Join(", ", lesson.words.Select(w => w.hy)) : null,
                        lesson.words.Count > 0 ? "Завтра повторим их, чтобы они запомнились." : null,
                        topic ? "Теперь откроется следующая тема." : null,
                    }.Where(line => line != null).ToArray();
                },
            }));
        }

        /// <summary>
        /// Review from the home screen. When items are due, today's spaced review (at most
        /// Leitner.MaxPerSession). Otherwise free practice: hard and least remembered items first.
        /// </summary>
        public void ShowReview()
        {
            if (Progress.Due(DateTime.Now).Count == 0)
            {
                ShowPractice();
                return;
            }
            var keys = Progress.Due(DateTime.Now).Take(Leitner.MaxPerSession).Select(i => i.key).ToList();
            Show(new LessonScreen(this, new LessonPlan
            {
                Heading = "Повторение",
                Title = "Вспомним!",
                Intro = "Буквы и слова, которые пора повторить. Повторение через 1, 2, 4, 7, 14 и 30 дней помогает запомнить их надолго.",
                Exercises = ExerciseBuilder.BuildReview(Course, keys, Progress.LastCompletedDay, Audio.Has, Environment.TickCount),
                Finish = session =>
                {
                    var now = DateTime.Now;
                    foreach (var key in keys)
                        Progress.Reviewed(key, !session.Missed.Contains(key), now);
                    Progress.Missed(session.Missed);
                    Progress.MarkReviewed(now);
                    SaveProgress();
                    int remembered = keys.Count(k => !session.Missed.Contains(k));
                    int left = Progress.Due(now).Count;
                    return new[]
                    {
                        $"Вспомнили сразу: {remembered} из {keys.Count}.",
                        session.Missed.Count > 0 ? "Ошибки повторим снова завтра." : "Отлично! Следующее повторение — через несколько дней.",
                        left > 0 ? $"Ещё можно повторить: {left}. Это можно сделать позже — уроки уже открыты." : null,
                    }.Where(line => line != null).ToArray();
                },
            }));
        }

        /// <summary>
        /// Extra practice when nothing is due. Correct answers don't move review dates (the item is not
        /// due yet); a mistake marks the item hard and brings it back tomorrow.
        /// </summary>
        void ShowPractice()
        {
            var keys = Progress.PracticeKeys(Leitner.MaxPerSession, new System.Random(Environment.TickCount));
            Show(new LessonScreen(this, new LessonPlan
            {
                Heading = "Повторение",
                Title = "Тренировка",
                Intro = "На сегодня всё повторено. Потренируемся ещё: сначала трудные буквы и слова, потом те, что вы знаете хуже всего.",
                Exercises = ExerciseBuilder.BuildReview(Course, keys, Progress.LastCompletedDay, Audio.Has, Environment.TickCount),
                Finish = session =>
                {
                    var now = DateTime.Now;
                    foreach (var key in session.Missed)
                        Progress.Reviewed(key, false, now);
                    Progress.Missed(session.Missed);
                    SaveProgress();
                    int remembered = keys.Count(k => !session.Missed.Contains(k));
                    return new[]
                    {
                        $"Вспомнили сразу: {remembered} из {keys.Count}.",
                        session.Missed.Count > 0 ? "Ошибки повторим завтра." : "Отлично, всё помните!",
                    };
                },
            }));
        }

        /// <summary>True when nothing is left to do today: no review due and no lesson open.</summary>
        public bool DoneToday()
        {
            var now = DateTime.Now;
            return Progress.Due(now).Count == 0 && Course.Lessons.All(l =>
            {
                var state = StateOf(l.day, now);
                return state != DayState.Available && state != DayState.ReviewFirst;
            });
        }

        /// <summary>State of a letter day or a topic.</summary>
        public DayState StateOf(int day, DateTime now) => DayUnlock.State(Course, day, Progress, now);

        /// <summary>The learner doesn't want this word: out of lessons and review until restored.</summary>
        public void RemoveWord(string key)
        {
            Progress.RemoveWord(key);
            SaveProgress();
        }

        /// <summary>Back to learning a removed word: into review if its day is done (or it came from a text).</summary>
        public void RestoreWord(string key)
        {
            Progress.RestoreWord(key);
            bool learned = key.StartsWith("d:") || Course.Lessons.Any(l => Progress.Result(l.day) != null &&
                (l.words.Any(w => w.Key == key) || l.review.Any(id => "w:" + id == key)));
            if (learned) Progress.Learn(new[] { key }, DateTime.Now);
            SaveProgress();
        }

        public void SkipTopic(int day)
        {
            if (Course.Lesson(day)?.optional != true) return;
            Progress.Skip(day);
            SaveProgress();
            ShowHome();
        }

        public void UnskipTopic(int day)
        {
            Progress.Unskip(day);
            SaveProgress();
            ShowHome();
        }

        /// <summary>For testers: every day counts as passed, all letters and words are known.</summary>
        public void UnlockAll()
        {
            Progress.UnlockAll(Course.Lessons.Select(l => (l.day,
                l.newLetters.Select(id => Course.Letter(id).Key).Concat(l.words.Select(w => w.Key)))), DateTime.Now);
            SaveProgress();
            ShowHome();
        }

        /// <summary>Starts the course over. The settings (one lesson a day, reminders) stay.</summary>
        public void ResetProgress()
        {
            Progress.Reset();
            SaveProgress();
            ShowHome();
        }

        public void SaveProgress()
        {
            ProgressStore.Save(Progress);
            Reminders.Schedule(Progress, DoneToday());
        }
    }
}
