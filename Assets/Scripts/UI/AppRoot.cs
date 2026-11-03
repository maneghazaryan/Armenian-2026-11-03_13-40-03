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

        VisualElement _root;
        VisualElement _screenHost;
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

            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/AppTheme");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(720, 1280);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0f;

            var doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = panel;

            _root = doc.rootVisualElement;
            _root.AddToClassList("app");
            _root.style.unityFontDefinition = FontDefinition.FromSDFFont(CreateFont());
            _screenHost = new VisualElement();
            _screenHost.AddToClassList("screen-host");
            _root.Add(_screenHost);

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

            // Keep content clear of notches and home indicators.
            var safe = Screen.safeArea;
            if (safe == _appliedSafeArea || Screen.width == 0 || Screen.height == 0) return;
            _appliedSafeArea = safe;
            var size = _root.layout.size;
            if (float.IsNaN(size.x) || size.x <= 0) return;
            float sx = size.x / Screen.width, sy = size.y / Screen.height;
            _root.style.paddingLeft = safe.xMin * sx;
            _root.style.paddingRight = (Screen.width - safe.xMax) * sx;
            _root.style.paddingTop = (Screen.height - safe.yMax) * sy;
            _root.style.paddingBottom = safe.yMin * sy;
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
