using System;

namespace WuwaModModifier.Common.Helpers
{
    /// <summary>
    /// Shared text normalisation utilities that replace duplicated NormalizeLineEndings
    /// implementations in ModConfigParser and MainViewModel.
    /// </summary>
    public static class TextHelper
    {
        /// <summary>
        /// Normalizes all line endings in <paramref name="text"/> to the given target.
        /// When <paramref name="targetLineEnding"/> is null or empty the result uses <c>\n</c> only.
        /// </summary>
        public static string NormalizeLineEndings(string? text, string? targetLineEnding = null)
        {
            var normalized = (text ?? string.Empty)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace("\r", "\n", StringComparison.Ordinal);

            if (string.IsNullOrEmpty(targetLineEnding))
            {
                return normalized;
            }

            return normalized.Replace("\n", targetLineEnding, StringComparison.Ordinal);
        }

        /// <summary>
        /// Splits <paramref name="text"/> into lines, removing the trailing empty entry
        /// that a final line ending produces.
        /// </summary>
        public static string[] SplitLines(string text)
        {
            var lines = text.Split('\n');
            if (lines.Length > 0 && lines[^1].Length == 0)
            {
                return lines[..^1];
            }

            return lines;
        }
    }
}
