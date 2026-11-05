using System;
using System.Linq;
using System.Text;
using Aib.Data;
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
        public const string ArmenianColor = "#B5332E";

        public static Label Label(string text, params string[] classes)
        {
            var l = new Label(Mark(text));
            TagScript(l, text);
            foreach (var c in classes) l.AddToClassList(c);
            return l;
        }

        public static Button Button(string text, Action onClick, params string[] classes)
        {
            var b = new Button(onClick) { text = Mark(text) };
            TagScript(b, text);
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

        /// <summary>
        /// Armenian and Russian must be told apart at a glance: text that is only Armenian gets the
        /// "hy" class (Armenian colour); in mixed text the Armenian letters become bold and coloured.
        /// </summary>
        public static string Mark(string text)
        {
            if (string.IsNullOrEmpty(text) || !text.Any(ArmenianText.IsArmenian) || !text.Any(IsOtherLetter))
                return text;
            var sb = new StringBuilder();
            bool inside = false;
            foreach (char c in text)
            {
                bool hy = ArmenianText.IsArmenian(c);
                if (hy && !inside) sb.Append("<b><color=").Append(ArmenianColor).Append('>');
                if (!hy && inside) sb.Append("</color></b>");
                inside = hy;
                sb.Append(c);
            }
            if (inside) sb.Append("</color></b>");
            return sb.ToString();
        }

        static bool IsOtherLetter(char c) => char.IsLetter(c) && !ArmenianText.IsArmenian(c);

        static void TagScript(VisualElement e, string text)
        {
            if (!string.IsNullOrEmpty(text) && text.Any(ArmenianText.IsArmenian) && !text.Any(IsOtherLetter))
                e.AddToClassList("hy");
        }

        public static string Stars(int n) => new string('★', n) + new string('☆', 3 - n);
    }
}
