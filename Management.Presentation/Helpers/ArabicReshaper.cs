using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Management.Presentation.Helpers
{
    /// <summary>
    /// A lightweight Arabic Reshaper to handle character joining and RTL reordering for non-shaping engines (like SkiaSharp in LiveCharts2).
    /// </summary>
    public static class ArabicReshaper
    {
        private static readonly Dictionary<char, char[]> GlyphForms = new Dictionary<char, char[]>
        {
            // Isolated, Initial, Medial, Final
            { '\u0627', new[] { '\uFE8D', '\uFE8D', '\uFE8E', '\uFE8E' } }, // Alef
            { '\u0628', new[] { '\uFE8F', '\uFE91', '\uFE92', '\uFE90' } }, // Beh
            { '\u062A', new[] { '\uFE95', '\uFE97', '\uFE98', '\uFE96' } }, // Teh
            { '\u062B', new[] { '\uFE99', '\uFE9B', '\uFE9C', '\uFE9A' } }, // Theh
            { '\u062C', new[] { '\uFE9D', '\uFE9F', '\uFEA0', '\uFE9E' } }, // Jeem
            { '\u062D', new[] { '\uFEA1', '\uFEA3', '\uFEA4', '\uFEA2' } }, // Hah
            { '\u062E', new[] { '\uFEA5', '\uFEA7', '\uFEA8', '\uFEA6' } }, // Khah
            { '\u062F', new[] { '\uFEA9', '\uFEA9', '\uFEAA', '\uFEAA' } }, // Dal
            { '\u0630', new[] { '\uFEAB', '\uFEAB', '\uFEAC', '\uFEAC' } }, // Thal
            { '\u0631', new[] { '\uFEAD', '\uFEAD', '\uFEAE', '\uFEAE' } }, // Reh
            { '\u0632', new[] { '\uFEAF', '\uFEAF', '\uFEB0', '\uFEB0' } }, // ZAIN
            { '\u0633', new[] { '\uFEB1', '\uFEB3', '\uFEB4', '\uFEB2' } }, // Seen
            { '\u0634', new[] { '\uFEB5', '\uFEB7', '\uFEB8', '\uFEB6' } }, // Sheen
            { '\u0635', new[] { '\uFEB9', '\uFEBB', '\uFEBC', '\uFEBA' } }, // Sad
            { '\u0636', new[] { '\uFEBD', '\uFEBF', '\uFEC0', '\uFEBE' } }, // Dad
            { '\u0637', new[] { '\uFEC1', '\uFEC3', '\uFEC4', '\uFEC2' } }, // Tah
            { '\u0638', new[] { '\uFEC5', '\uFEC7', '\uFEC8', '\uFEC6' } }, // Zah
            { '\u0639', new[] { '\uFEC9', '\uFECB', '\uFECC', '\uFECA' } }, // Ain
            { '\u063A', new[] { '\uFECD', '\uFECF', '\uFED0', '\uFECE' } }, // Ghain
            { '\u0641', new[] { '\uFED1', '\uFED3', '\uFED4', '\uFED2' } }, // Feh
            { '\u0642', new[] { '\uFED5', '\uFED7', '\uFED8', '\uFED6' } }, // Qaf
            { '\u0643', new[] { '\uFED9', '\uFEDB', '\uFEDC', '\uFEDA' } }, // Kaf
            { '\u0644', new[] { '\uFEDD', '\uFEDF', '\uFEE0', '\uFEDE' } }, // Lam
            { '\u0645', new[] { '\uFEE1', '\uFEE3', '\uFEE4', '\uFEE2' } }, // Meem
            { '\u0646', new[] { '\uFEE5', '\uFEE7', '\uFEE8', '\uFEE6' } }, // Noon
            { '\u0647', new[] { '\uFEE9', '\uFEEB', '\uFEEC', '\uFEEA' } }, // Heh
            { '\u0648', new[] { '\uFEED', '\uFEED', '\uFEEE', '\uFEEE' } }, // Waw
            { '\u064A', new[] { '\uFEF1', '\uFEF3', '\uFEF4', '\uFEF2' } }, // Yeh
            { '\u0622', new[] { '\uFE81', '\uFE81', '\uFE82', '\uFE82' } }, // Alef Mad
            { '\u0623', new[] { '\uFE83', '\uFE83', '\uFE84', '\uFE84' } }, // Alef Hamza Above
            { '\u0625', new[] { '\uFE87', '\uFE87', '\uFE88', '\uFE88' } }, // Alef Hamza Below
            { '\u0624', new[] { '\uFE85', '\uFE85', '\uFE86', '\uFE86' } }, // Waw Hamza Above
            { '\u0626', new[] { '\uFE89', '\uFE8B', '\uFE8C', '\uFE8A' } }, // Yeh Hamza Above
            { '\u0629', new[] { '\uFE93', '\uFE93', '\uFE94', '\uFE94' } }, // Teh Marbuta
            { '\u0649', new[] { '\uFEEF', '\uFEEF', '\uFEF0', '\uFEF0' } }, // Alef Maksura
            { '\u0640', new[] { '\u0640', '\u0640', '\u0640', '\u0640' } }, // Tatweel
        };

        private static readonly HashSet<char> NonJoiningToRight = new HashSet<char>
        {
            '\u0627', '\u0622', '\u0623', '\u0625', '\u062F', '\u0630', '\u0631', '\u0632', '\u0648', '\u0624', '\u0629'
        };

        public static string Reshape(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            if (!input.Any(c => c >= '\u0600' && c <= '\u06FF')) return input;

            var result = new StringBuilder();
            var words = input.Split(' ');
            var reshapedWords = new List<string>();

            foreach (var word in words)
            {
                if (string.IsNullOrEmpty(word))
                {
                    reshapedWords.Add("");
                    continue;
                }

                // If word contains non-arabic, don't reshape or handle carefully
                // For simplicity, we only reshape words that are primarily Arabic
                if (!word.Any(c => c >= '\u0600' && c <= '\u06FF'))
                {
                    reshapedWords.Add(word);
                    continue;
                }

                char[] chars = word.ToCharArray();
                char[] reshaped = new char[chars.Length];

                for (int i = 0; i < chars.Length; i++)
                {
                    char current = chars[i];
                    if (!GlyphForms.ContainsKey(current))
                    {
                        reshaped[i] = current;
                        continue;
                    }

                    bool linkBefore = i > 0 && GlyphForms.ContainsKey(chars[i - 1]) && !NonJoiningToRight.Contains(chars[i - 1]);
                    bool linkAfter = i < chars.Length - 1 && GlyphForms.ContainsKey(chars[i + 1]);

                    var forms = GlyphForms[current];
                    if (linkBefore && linkAfter) reshaped[i] = forms[2]; // Medial
                    else if (linkBefore) reshaped[i] = forms[3];        // Final
                    else if (linkAfter) reshaped[i] = forms[1];         // Initial
                    else reshaped[i] = forms[0];                        // Isolated
                }

                // Reverse for RTL
                Array.Reverse(reshaped);
                reshapedWords.Add(new string(reshaped));
            }

            // Reverse word order as well for full RTL
            reshapedWords.Reverse();
            return string.Join(" ", reshapedWords);
        }
    }
}
