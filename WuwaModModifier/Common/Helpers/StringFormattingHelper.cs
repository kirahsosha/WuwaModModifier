using System.Collections.Generic;
using System.Linq;

namespace WuwaModModifier.Common.Helpers
{
    /// <summary>
    /// Shared string formatting utilities that replace duplicated Join / JoinSemicolon
    /// implementations across ConfigAnalysisSummaryItem and service classes.
    /// </summary>
    public static class StringFormattingHelper
    {
        /// <summary>
        /// Joins non-empty values with the default pipe separator used throughout the INI config UI.
        /// </summary>
        public static string JoinNonEmpty(IEnumerable<string> values, string separator = " | ")
        {
            return string.Join(separator, values.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        /// <summary>
        /// Joins non-empty values with a semicolon separator (e.g. linked parameter names).
        /// </summary>
        public static string JoinSemicolon(IEnumerable<string> values)
        {
            return string.Join("; ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
        }
    }
}
