using System.Collections.Generic;
using WuwaModModifier.Model;

namespace WuwaModModifier.Common
{
    /// <summary>
    /// Toggle-related config operations: key-binding application, standardisation
    /// slot matching, and toggle creation.
    /// </summary>
    public interface IModConfigToggleService
    {
        ModConfigEditBuffer CreateToggleBinding(ModConfigEditBuffer buffer, string variableName, IReadOnlyList<string> keyBindings);
        ModConfigEditBuffer UpdateKeyBindings(ModConfigEditBuffer buffer, string keySectionName, IReadOnlyList<string> newKeyBindings);
        ModConfigEditBuffer UpdateToggleTargetValues(ModConfigEditBuffer buffer, string keySectionName, string variableName, IReadOnlyList<string> newValues);
        ModConfigStandardizationResult StandardizeToggleSlots(ModConfigEditBuffer buffer, string templatePath);
    }
}
