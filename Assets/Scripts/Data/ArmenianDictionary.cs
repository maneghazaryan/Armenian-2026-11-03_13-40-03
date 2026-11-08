using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Aib.Data
{
    /// <summary>A dictionary word: its usual form and meaning. Names start with a capital letter.</summary>
    public class DictEntry
    {
        public string hy;
        public string ru;

        public bool IsName => hy.Length > 0 && char.IsUpper(hy[0]);
        /// <summary>Progress key once the learner adds the word to training.</summary>
        public string Key => "d:" + hy;
    }

    /// <summary>A form met in a text («մարդու»), the dictionary word it belongs to, and what this form means.</summary>
    public class DictForm
    {
        public DictEntry Entry;
        /// <summary>Meaning of this exact form, e.g. «человека (род. п.)»; null for the dictionary form itself.</summary>
        public string Note;
    }

    /// <summary>
    /// Word forms of the Armenian texts, from Resources/Data/dictionary.txt. One line per word:
    /// <code>մարդ = человек | մարդը = (этот) человек; մարդու = человека</code>
    /// The dictionary form and its meaning, then «|» and the forms met in texts, separated by «;»,
    /// each with its own meaning if it differs (a note may itself contain «;»). Lines under «[text-id]» apply only to that text
    /// (for dialect words like «մեր» = «мать») and win over the common ones. «#» starts a comment.
    /// </summary>
    public class ArmenianDictionary
    {
        const string Common = "*";

        /// <summary>«;» before an Armenian word separates forms; a «;» inside a Russian note does not.</summary>
        static readonly Regex FormSeparator = new Regex(@";\s*(?=[\u0531-\u0587])");

        readonly Dictionary<string, Dictionary<string, DictForm>> _forms = new Dictionary<string, Dictionary<string, DictForm>>();
        readonly Dictionary<string, DictEntry> _entries = new Dictionary<string, DictEntry>();

        /// <summary>Problems found while reading: malformed lines, a form defined twice in one section.</summary>
        public readonly List<string> Problems = new List<string>();

        public IEnumerable<DictEntry> Entries => _entries.Values;

        public static ArmenianDictionary Parse(string text)
        {
            var dict = new ArmenianDictionary();
            string section = Common;
            int lineNo = 0;
            foreach (var raw in text.Split('\n'))
            {
                lineNo++;
                var line = raw.Trim().TrimStart('﻿');
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2).Trim();
                    continue;
                }
                var halves = line.Split('|');
                var head = halves[0].Split(new[] { " = " }, 2, StringSplitOptions.None);
                if (head.Length != 2 || halves.Length > 2)
                {
                    dict.Problems.Add($"line {lineNo}: expected «слово = значение | формы»: {line}");
                    continue;
                }
                var hy = head[0].Trim();
                // The same dictionary word may be listed again under a text section with more forms.
                if (!dict._entries.TryGetValue(hy, out var entry))
                    dict._entries[hy] = entry = new DictEntry { hy = hy, ru = head[1].Trim() };
                dict.AddForm(section, hy, entry, null, lineNo);
                if (halves.Length == 2)
                    foreach (var part in FormSeparator.Split(halves[1]))
                    {
                        if (part.Trim().Length == 0) continue;
                        var f = part.Split(new[] { " = " }, 2, StringSplitOptions.None);
                        dict.AddForm(section, f[0].Trim(), entry, f.Length == 2 ? f[1].Trim() : null, lineNo);
                    }
            }
            return dict;
        }

        void AddForm(string section, string form, DictEntry entry, string note, int lineNo)
        {
            if (!_forms.TryGetValue(section, out var forms)) _forms[section] = forms = new Dictionary<string, DictForm>();
            var key = Normalize(form);
            if (forms.TryGetValue(key, out var old))
            {
                if (old.Entry != entry || old.Note != note)
                    Problems.Add($"line {lineNo}: form «{form}» already means «{old.Entry.hy}»" + (section == Common ? "" : $" in [{section}]"));
                return;
            }
            forms[key] = new DictForm { Entry = entry, Note = note };
        }

        public DictEntry Entry(string hy) => _entries.TryGetValue(hy, out var e) ? e : null;

        /// <summary>The form in a text; the text's own section first. Null when it is not in the dictionary.</summary>
        public DictForm Find(string word, string textId)
        {
            var key = Normalize(word);
            if (key.Length == 0) return null;
            var exact = FindExact(key, textId);
            if (exact != null) return exact;
            // Not listed: an inflected form of a listed word (Ձիավորին → ձիավոր, կսպանի → սպանել).
            foreach (var guess in Guesses(key))
            {
                var g = FindExact(guess, textId);
                if (g != null) return new DictForm { Entry = g.Entry, Note = null };
            }
            return null;
        }

        DictForm FindExact(string key, string textId)
        {
            if (textId != null && _forms.TryGetValue(textId, out var own) && own.TryGetValue(key, out var f)) return f;
            return _forms.TryGetValue(Common, out var common) && common.TryGetValue(key, out f) ? f : null;
        }

        static readonly string[] NounEndings =
        {
            "ներից", "երից", "ներին", "երին", "ներով", "երով", "ներում", "երում", "ներին", "ները", "երը", "ների", "երի",
            "ներն", "երն", "ներս", "երս", "ներդ", "երդ", "ներ", "եր", "ումս", "ումդ", "ից", "ով", "ում", "ին", "իս", "իդ",
            "ն", "ը", "ի", "ս", "դ", "ու",
        };

        /// <summary>Verb endings and the infinitive endings they may come from.</summary>
        static readonly (string end, string[] inf)[] VerbEndings =
        {
            ("ացրեցին", new[] { "ացնել" }), ("ացրեց", new[] { "ացնել" }), ("ացրել", new[] { "ացնել" }), ("ացրին", new[] { "ացնել" }),
            ("եցինք", new[] { "ել", "նել" }), ("եցին", new[] { "ել", "նել" }), ("եցիք", new[] { "ել" }), ("եցեք", new[] { "ել" }),
            ("եցիր", new[] { "ել" }), ("եցի", new[] { "ել", "նել" }), ("եցավ", new[] { "ենալ", "անալ" }), ("եցան", new[] { "ենալ", "անալ" }),
            ("եցել", new[] { "ենալ", "անալ" }), ("եցած", new[] { "ենալ", "անալ", "ել" }), ("եց", new[] { "ել", "նել" }),
            ("ացավ", new[] { "անալ", "ենալ" }), ("ացան", new[] { "անալ", "ենալ" }), ("ացար", new[] { "անալ" }), ("ացա", new[] { "անալ" }),
            ("ացել", new[] { "անալ", "ալ" }), ("ացած", new[] { "անալ", "ալ" }), ("ացին", new[] { "ալ", "անալ" }), ("ացի", new[] { "ալ" }), ("աց", new[] { "ալ", "անալ" }),
            ("ելու", new[] { "ել" }), ("ելիս", new[] { "ել" }), ("ելով", new[] { "ել" }), ("ելուց", new[] { "ել" }), ("ելը", new[] { "ել" }),
            ("ալու", new[] { "ալ" }), ("ալիս", new[] { "ալ" }), ("ալով", new[] { "ալ" }), ("ալուց", new[] { "ալ" }),
            ("ում", new[] { "ել", "ալ", "նել" }), ("ված", new[] { "վել" }), ("ած", new[] { "ել", "ալ", "նել" }), ("ող", new[] { "ել", "ալ" }),
            ("ավ", new[] { "նել", "ալ", "ել" }), ("ան", new[] { "նել", "ալ" }), ("ար", new[] { "նել", "ալ" }), ("ա", new[] { "նել", "ալ" }),
            ("ենք", new[] { "ել", "նել" }), ("եք", new[] { "ել", "նել" }), ("են", new[] { "ել", "նել" }), ("եմ", new[] { "ել", "նել" }), ("ես", new[] { "ել", "նել" }),
            ("իր", new[] { "ել", "նել" }), ("ի", new[] { "ել", "նել" }), ("ել", new[] { "ել" }), ("ալ", new[] { "ալ" }),
        };

        /// <summary>Possible dictionary words for a form not listed itself: endings and the կ-/չ- prefixes taken off.</summary>
        public static IEnumerable<string> Guesses(string key)
        {
            foreach (var prefix in new[] { "", "կ", "չ" })
            {
                if (!key.StartsWith(prefix) || key.Length - prefix.Length < 2) continue;
                var rest = key.Substring(prefix.Length);
                if (prefix.Length > 0) yield return rest;
                foreach (var end in NounEndings)
                    if (rest.Length - end.Length >= 2 && rest.EndsWith(end))
                        yield return rest.Substring(0, rest.Length - end.Length);
                foreach (var (end, infs) in VerbEndings)
                    if (rest.Length - end.Length >= 2 && rest.EndsWith(end))
                        foreach (var inf in infs)
                            yield return rest.Substring(0, rest.Length - end.Length) + inf;
            }
        }

        /// <summary>
        /// Dictionary words of one piece of text between spaces («մարդ-ախպեր,»): the whole piece if it
        /// is listed, otherwise each part between hyphens. A part not in the dictionary comes with null.
        /// </summary>
        public List<(string word, DictForm form)> Lookup(string chunk, string textId)
        {
            var whole = Normalize(chunk);
            var result = new List<(string, DictForm)>();
            if (whole.Length == 0) return result;
            var f = Find(whole, textId);
            if (f != null || !whole.Contains('-'))
            {
                result.Add((whole, f));
                return result;
            }
            foreach (var part in whole.Split('-').Where(p => p.Length > 0))
                result.Add((part, Find(part, textId)));
            return result;
        }

        /// <summary>Distinct dictionary words of a whole text (names included), in order of appearance.</summary>
        public List<DictEntry> WordsOf(string text, string textId)
        {
            var seen = new HashSet<DictEntry>();
            var result = new List<DictEntry>();
            foreach (var chunk in Chunks(text))
                foreach (var (_, form) in Lookup(chunk, textId))
                    if (form != null && seen.Add(form.Entry))
                        result.Add(form.Entry);
            return result;
        }

        /// <summary>Pieces of text between spaces and line breaks, punctuation kept.</summary>
        public static IEnumerable<string> Chunks(string text) =>
            text.Split(new[] { ' ', '\n', '\t', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        /// <summary>
        /// Lowercase, without punctuation and the stress, question and exclamation marks written
        /// inside words (ո՞ւր → ուր, լա՛վ → լավ). Inner hyphens stay.
        /// </summary>
        public static string Normalize(string word)
        {
            var sb = new StringBuilder(word.Length);
            foreach (char ch in word.ToLowerInvariant())
                if (char.IsLetter(ch) || ch == '-')
                    sb.Append(ch);
            return sb.ToString().Trim('-');
        }
    }
}
