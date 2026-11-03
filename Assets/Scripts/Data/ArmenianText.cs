using System.Collections.Generic;

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

        /// <summary>First letter capital, as at the start of a sentence: ասա → Ասա, ու → Ու, և → Եվ.</summary>
        public static string Capitalize(string word)
        {
            if (string.IsNullOrEmpty(word)) return word;
            if (word[0] == 'և') return "Եվ" + word.Substring(1);
            return char.ToUpperInvariant(word[0]) + word.Substring(1);
        }
    }
}
