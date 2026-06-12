using System;
using System.Collections.Generic;
using WuwaModModifier.Model;

namespace WuwaModModifier.Common.Helpers
{
    /// <summary>
    /// Shared helpers for INI config section processing that were previously duplicated
    /// across ModConfigUpdateService and ModConfigAnalysisService.
    /// </summary>
    public static class ModConfigSectionHelpers
    {
        /// <summary>
        /// Sentinel pushed onto the control stack when an <c>else</c> branch is entered.
        /// </summary>
        public const string ElseSentinel = "__ELSE_BRANCH__";

        /// <summary>
        /// Returns <c>true</c> when the section name represents a Key-binding section
        /// (e.g. "Key", "Key_1", "KeySwapToggle").
        /// </summary>
        public static bool IsKeySection(string sectionName)
        {
            return sectionName.StartsWith("Key", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Attempts to extract a draw label from a Draw comment line.
        /// Lines starting with "; Draw skipped" are intentionally excluded.
        /// </summary>
        public static bool TryGetDrawLabel(string rawText, out string drawLabel)
        {
            drawLabel = string.Empty;
            var trimmed = rawText.Trim();
            if (!trimmed.StartsWith("; Draw ", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("; Draw skipped", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            drawLabel = trimmed.TrimStart(';').Trim();
            return !string.IsNullOrWhiteSpace(drawLabel);
        }

        /// <summary>
        /// Updates a generic control-flow stack (<c>if</c> / <c>elif</c> / <c>else</c> / <c>endif</c>).
        /// This is the superset version that handles all four branch types.
        /// </summary>
        public static void UpdateControlStack(Stack<string> controlStack, string rawText)
        {
            if (rawText.StartsWith("if ", StringComparison.OrdinalIgnoreCase))
            {
                controlStack.Push(rawText.Substring(2).Trim());
                return;
            }

            if (rawText.StartsWith("elif ", StringComparison.OrdinalIgnoreCase))
            {
                if (controlStack.Count > 0)
                {
                    controlStack.Pop();
                }

                controlStack.Push(rawText.Substring(4).Trim());
                return;
            }

            if (rawText.Equals("else", StringComparison.OrdinalIgnoreCase))
            {
                if (controlStack.Count > 0)
                {
                    controlStack.Pop();
                }

                controlStack.Push(ElseSentinel);
                return;
            }

            if (rawText.Equals("endif", StringComparison.OrdinalIgnoreCase) && controlStack.Count > 0)
            {
                controlStack.Pop();
            }
        }
    }
}
