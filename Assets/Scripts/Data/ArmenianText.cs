using System.Collections.Generic;
using System.Text;

namespace Aib.Data
{
    public static class ArmenianText
    {
        /// <summary>
        /// Splits text into lowercase Armenian letters, skipping spaces and punctuation.
        /// "ու" and "և" are single letters, as in the primer.
        /// Must match tokenize() in Tools/validate_course.py.
        /// </summary>
        public static List<string> Tokenize(string text)
        {
            var tokens = new List<string>();
            text = text.ToLowerInvariant();
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == 'ո' && i + 1 < text.Length && text[i + 1] == 'ւ')
                {
                    tokens.Add("ու");
                    i++;
                }
                else if (text[i] >= 'ա' && text[i] <= 'և')
                {
                    tokens.Add(text[i].ToString());
                }
            }
            return tokens;
        }

        public static bool IsArmenian(char c) => c >= '\u0531' && c <= '\u058A';

        // Russian sound -> Armenian letter. Russian т/п/к/ч are unaspirated, like տ/պ/կ/ճ.
        static readonly Dictionary<char, string> RussianToArmenian = new Dictionary<char, string>
        {
            ['а'] = "ա", ['б'] = "բ", ['в'] = "վ", ['г'] = "գ", ['д'] = "դ", ['е'] = "ե", ['ж'] = "ժ",
            ['з'] = "զ", ['и'] = "ի", ['й'] = "յ", ['к'] = "կ", ['л'] = "լ", ['м'] = "մ", ['н'] = "ն",
            ['о'] = "ո", ['п'] = "պ", ['с'] = "ս", ['т'] = "տ", ['у'] = "ու", ['ф'] = "ֆ",
            ['х'] = "խ", ['ц'] = "ծ", ['ч'] = "ճ", ['ш'] = "շ", ['э'] = "է",
        };

        /// <summary>
        /// Writes the sounds of a Russian text that the learner can already read with Armenian letters:
        /// «Сирена запела» with ս ա ր known → «Սиրенա запелա». ո follows its reading rule
        /// («во» at the start of a word, «о» inside), and «ев» inside a word becomes և.
        /// Russian «р» is ր only where it is a light tap («сирена», «мир»); the strong «р» at the start
        /// of a word («рама») and a double «рр» are ռ, and stay Russian until ռ is learned.
        /// </summary>
        public static string MixRussian(string text, ISet<string> known)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                char lower = char.ToLowerInvariant(c);
                bool upper = c != lower;
                bool wordStart = i == 0 || !char.IsLetter(text[i - 1]);
                string hy = null;
                int used = 1;

                if (wordStart && lower == 'в' && i + 1 < text.Length && char.ToLowerInvariant(text[i + 1]) == 'о' && known.Contains("ո"))
                {
                    hy = "ո";
                    used = 2;
                }
                else if (!wordStart && lower == 'е' && i + 1 < text.Length && text[i + 1] == 'в' && known.Contains("և"))
                {
                    hy = "և";
                    used = 2;
                }
                else if (lower == 'р')
                {
                    bool doubled = i + 1 < text.Length && char.ToLowerInvariant(text[i + 1]) == 'р';
                    string letter = wordStart || doubled ? "ռ" : "ր";
                    if (known.Contains(letter))
                    {
                        hy = letter;
                        used = doubled ? 2 : 1;
                    }
                }
                else if (lower == 'о' && wordStart)
                {
                    // ո at the start of a word reads «во»; a plain «о» there is օ (or stays Russian until օ is known).
                    hy = known.Contains("օ") ? "օ" : null;
                }
                else if (RussianToArmenian.TryGetValue(lower, out var mapped) && known.Contains(mapped))
                {
                    hy = mapped;
                }

                if (hy == null)
                {
                    sb.Append(c);
                    continue;
                }
                sb.Append(upper ? Capitalize(hy) : hy);
                i += used - 1;
            }
            return sb.ToString();
        }

        /// <summary>First letter capital, as at the start of a sentence: ասա → Ասա, ու → Ու, և → Եվ.</summary>
        public static string Capitalize(string word)
        {
            if (string.IsNullOrEmpty(word)) return word;
            if (word[0] == 'և') return "Եվ" + word.Substring(1);
            return char.ToUpperInvariant(word[0]) + word.Substring(1);
        }
    }
}
