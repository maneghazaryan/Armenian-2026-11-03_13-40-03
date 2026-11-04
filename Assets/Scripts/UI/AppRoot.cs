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
                Resources.Load<TextAsset>("Data/lessons").text);
            Progress = ProgressStore.Load();
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
            ShowHome();
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

        public void ShowLesson(int day)
        {
            var lesson = Course.Lesson(day);
            Show(new LessonScreen(this, new LessonPlan
            {
                Heading = $"День {day}",
                Title = lesson.title,
                Intro = lesson.intro,
                Exercises = ExerciseBuilder.Build(Course, lesson, Audio.Has, Environment.TickCount),
                Finish = session =>
                {
                    var now = DateTime.Now;
                    Progress.Complete(day, DayUnlock.Stars(session.Mistakes), now);
                    Progress.Learn(lesson.newLetters.Select(id => Course.Letter(id).Key)
                        .Concat(lesson.words.Select(w => w.Key)), now);
                    SaveProgress();
                    return new[]
                    {
                        "Новые буквы: " + string.Join("  ", lesson.newLetters.Select(id => Course.Letter(id).lower)),
                        "Новые слова: " + string.Join(", ", lesson.words.Select(w => w.hy)),
                        "Завтра повторим их, чтобы они запомнились.",
                    };
                },
            }));
        }

        /// <summary>Today's spaced review: only items that are due, at most Leitner.MaxPerSession.</summary>
        public void ShowReview()
        {
            var keys = Progress.Due(DateTime.Now).Take(Leitner.MaxPerSession).Select(i => i.key).ToList();
            Show(new LessonScreen(this, new LessonPlan
            {
                Heading = "Повторение",
                Title = "Вспомним!",
                Intro = "Буквы и слова, которые пора повторить. Повторение через 1, 3, 7, 14 и 30 дней помогает запомнить их надолго.",
                Exercises = ExerciseBuilder.BuildReview(Course, keys, Progress.LastCompletedDay, Audio.Has, Environment.TickCount),
                Finish = session =>
                {
                    var now = DateTime.Now;
                    foreach (var key in keys)
                        Progress.Reviewed(key, !session.Missed.Contains(key), now);
                    SaveProgress();
                    int remembered = keys.Count(k => !session.Missed.Contains(k));
                    return new[]
                    {
                        $"Вспомнили сразу: {remembered} из {keys.Count}.",
                        session.Missed.Count > 0 ? "Ошибки повторим снова завтра." : "Отлично! Следующее повторение — через несколько дней.",
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
                var state = DayUnlock.State(l.day, Progress, now);
                return state != DayState.Available && state != DayState.ReviewFirst;
            });
        }

        public void SaveProgress()
        {
            ProgressStore.Save(Progress);
            Reminders.Schedule(Progress, DoneToday());
        }
    }
}
