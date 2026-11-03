using System;
using UnityEngine.UIElements;

namespace Aib.UI
{
    /// <summary>A screen that reacts to the Android back button (Escape in the editor).</summary>
    public interface IBackHandler
    {
        void OnBack();
    }

    /// <summary>Small helpers to build UI Toolkit elements in code.</summary>
    public static class Ui
    {
        public static Label Label(string text, params string[] classes)
        {
            var l = new Label(text);
            foreach (var c in classes) l.AddToClassList(c);
            return l;
        }

        public static Button Button(string text, Action onClick, params string[] classes)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList("btn");
            foreach (var c in classes) b.AddToClassList(c);
            return b;
        }

        public static VisualElement Box(params string[] classes)
        {
            var v = new VisualElement();
            foreach (var c in classes) v.AddToClassList(c);
            return v;
        }

        public static ScrollView Scroll(params string[] classes)
        {
            var s = new ScrollView(ScrollViewMode.Vertical)
            {
                verticalScrollerVisibility = ScrollerVisibility.Hidden,
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
            };
            foreach (var c in classes) s.AddToClassList(c);
            return s;
        }

        /// <summary>Top bar with a "back" button and a title.</summary>
        public static VisualElement TopBar(string title, Action onBack)
        {
            var top = Box("topbar");
            top.Add(Button("‹ Назад", onBack, "btn-back"));
            top.Add(Label(title, "title", "grow", "gap-left"));
            return top;
        }

        public static string Stars(int n) => new string('★', n) + new string('☆', 3 - n);
    }
}
