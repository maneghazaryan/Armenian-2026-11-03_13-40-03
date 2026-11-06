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
            // Stress, question and exclamation marks sit inside words (Ինչո՞ւ): ignore them.
            text = text.ToLowerInvariant().Replace("՛", "").Replace("՜", "").Replace("՞", "");
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

        // Russian sound -> Armenian letter. Russian т/п/к are unaspirated, like տ/պ/կ; «ч» is exactly չ.
        // я ё ю (and е after a vowel) are յ + vowel, handled in MixRussian.
        static readonly Dictionary<char, string> RussianToArmenian = new Dictionary<char, string>
        {
            ['а'] = "ա", ['б'] = "բ", ['в'] = "վ", ['г'] = "գ", ['д'] = "դ", ['е'] = "ե", ['ж'] = "ժ",
            ['з'] = "զ", ['и'] = "ի", ['й'] = "յ", ['к'] = "կ", ['л'] = "լ", ['м'] = "մ", ['н'] = "ն",
            ['о'] = "ո", ['п'] = "պ", ['с'] = "ս", ['т'] = "տ", ['у'] = "ու", ['ф'] = "ֆ",
            ['х'] = "խ", ['ц'] = "ծ", ['ч'] = "չ", ['ш'] = "շ", ['э'] = "է",
        };

        /// <summary>Letters after which Russian «р», «т», «д» are soft.</summary>
        const string SoftVowels = "иеёюяь";

        const string Vowels = "аеёиоуыэюя";

        /// <summary>я ё ю е as й + vowel: յա յո յու յե.</summary>
        static readonly Dictionary<char, string> Iotated = new Dictionary<char, string>
        {
            ['я'] = "ա", ['ё'] = "ո", ['ю'] = "ու", ['е'] = "ե",
        };

        /// <summary>Words whose «р» is ր although it is hard in Russian: names written with ր in Armenian (Սահարա).</summary>
        static readonly HashSet<string> SoftRWords = new HashSet<string>
        {
            "Сахара", "Сахары", "Сахаре", "Сахару", "Сахарой",
        };

        /// <summary>The whole word around position i, as written (case kept).</summary>
        static string WordAt(string text, int i)
        {
            int start = i, end = i;
            while (start > 0 && char.IsLetter(text[start - 1])) start--;
            while (end < text.Length && char.IsLetter(text[end])) end++;
            return text.Substring(start, end - start);
        }

        /// <summary>
        /// Writes the sounds of a Russian text that the learner can already read with Armenian letters:
        /// «Сирена запела» with ս ա ր known → «Սиրенա запелա». ո follows its reading rule
        /// («во» at the start of a word, «о» inside), and «ев» inside a word becomes և.
        /// я ё ю are written յա յո յու, and «е» after a vowel յե (at the start of a word ե already
        /// reads «йе»). A soft «т» is ծ and a soft «д» is ձ («мать» — Մածь, «Дима» — Ձիմա); the
        /// vowel after them then drops the յ, since the softness is already written («тётя» — ծոծա).
        /// Russian «р» is ր only where it is soft, before и е ё ю я ь («сирена», «пример»). The hard «р»
        /// everywhere else («рама», «красиво», «размер», «терраса») is the rolled ռ and stays Russian
        /// until ռ is learned. A few names spelled with ր in Armenian keep it («Сахара», but «сахар» — ռ).
        /// </summary>
        public static string MixRussian(string text, ISet<string> known)
        {
            var sb = new StringBuilder();
            bool softened = false; // the previous letter was a soft т/д written as ծ/ձ
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                char lower = char.ToLowerInvariant(c);
                bool upper = c != lower;
                bool wordStart = i == 0 || !char.IsLetter(text[i - 1]);
                string hy = null;
                int used = 1;
                bool wasSoftened = softened;
                softened = false;
                char before = wordStart ? ' ' : char.ToLowerInvariant(text[i - 1]);

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
                    char after = i + 1 < text.Length ? char.ToLowerInvariant(text[i + 1]) : ' ';
                    bool soft = !doubled && (SoftVowels.IndexOf(after) >= 0 || SoftRWords.Contains(WordAt(text, i)));
                    string letter = soft ? "ր" : "ռ";
                    if (known.Contains(letter))
                    {
                        hy = letter;
                        used = doubled ? 2 : 1;
                    }
                }
                else if ((lower == 'т' || lower == 'д') && i + 1 < text.Length
                         && SoftVowels.IndexOf(char.ToLowerInvariant(text[i + 1])) >= 0
                         && known.Contains(lower == 'т' ? "ծ" : "ձ"))
                {
                    hy = lower == 'т' ? "ծ" : "ձ";
                    softened = true;
                }
                else if (Iotated.TryGetValue(lower, out var vowel) && !(lower == 'е' && (wordStart || !IsVowelOrSign(before))))
                {
                    // After ծ/ձ (and ё after ж ш ч щ) the softness is already heard: just the vowel.
                    bool plain = wasSoftened || (lower == 'ё' && "жшчщ".IndexOf(before) >= 0);
                    if (known.Contains(vowel) && (plain || known.Contains("յ")))
                        hy = plain ? vowel : "յ" + vowel;
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

        static bool IsVowelOrSign(char c) => Vowels.IndexOf(c) >= 0 || c == 'ь' || c == 'ъ';

        static readonly Dictionary<char, string> Sounds = new Dictionary<char, string>
        {
            ['ա'] = "а", ['բ'] = "б", ['գ'] = "г", ['դ'] = "д", ['զ'] = "з", ['է'] = "э", ['ը'] = "ə",
            ['թ'] = "тʰ", ['ժ'] = "ж", ['ի'] = "и", ['լ'] = "л", ['խ'] = "х", ['ծ'] = "ц", ['կ'] = "к",
            ['հ'] = "h", ['ձ'] = "дз", ['ղ'] = "гх", ['ճ'] = "ч", ['մ'] = "м", ['յ'] = "й", ['ն'] = "н",
            ['շ'] = "ш", ['չ'] = "чʰ", ['պ'] = "п", ['ջ'] = "дж", ['ռ'] = "рр", ['ս'] = "с", ['վ'] = "в",
            ['տ'] = "т", ['ր'] = "р", ['ց'] = "цʰ", ['ւ'] = "в", ['փ'] = "пʰ", ['ք'] = "кʰ", ['օ'] = "о",
            ['ֆ'] = "ф",
        };

        /// <summary>
        /// Reading of an Armenian word in Cyrillic, in the course's notation (ʰ — with a breath,
        /// «гх» — ղ, ə — ը): ե is «е» at the start and after a vowel, «э» after a consonant;
        /// ո at the start is «во»; յա/յու are «я/ю». The ը heard but not written is not shown.
        /// </summary>
        public static string Transcribe(string hy)
        {
            var sb = new StringBuilder();
            string w = hy.ToLowerInvariant();
            bool prevVowel = false, start = true;
            for (int i = 0; i < w.Length; i++)
            {
                char c = w[i];
                string next2 = i + 2 < w.Length ? w.Substring(i + 1, 2) : (i + 1 < w.Length ? w.Substring(i + 1, 1) : "");
                string sound;
                bool vowel;
                if (c == 'ո' && i + 1 < w.Length && w[i + 1] == 'ւ') { sound = "у"; vowel = true; i++; }
                else if (c == 'յ' && next2.StartsWith("ու")) { sound = "ю"; vowel = true; i += 2; }
                else if (c == 'յ' && next2.StartsWith("ա")) { sound = "я"; vowel = true; i++; }
                else if (c == 'ե') { sound = start || prevVowel ? "е" : "э"; vowel = true; }
                else if (c == 'ո') { sound = start ? "во" : "о"; vowel = true; }
                else if (c == 'և') { sound = start ? "ев" : "эв"; vowel = false; }
                else if (Sounds.TryGetValue(c, out var s)) { sound = s; vowel = "աէըիօյ".IndexOf(c) >= 0; } // ե after յ reads «е»
                else { sb.Append(c); start = !char.IsLetter(c); prevVowel = false; continue; }
                sb.Append(sound);
                prevVowel = vowel;
                start = false;
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
