using System.Collections.Generic;
using WuwaModModifier.Model;

namespace WuwaModModifier.Common
{
    /// <summary>
    /// Visibility and draw-target operations: toggling visibility, binding to
    /// parameters, creating / removing visibility bindings.
    /// </summary>
    public interface IModConfigVisibilityService
    {
        ModConfigEditBuffer ToggleVisibility(ModConfigEditBuffer buffer, string componentSectionName, string drawLabel, bool isVisible);
        ModConfigEditBuffer BindVisibilityToParameter(ModConfigEditBuffer buffer, string componentSectionName, string drawLabel, string variableName);
        ModConfigEditBuffer CreateVisibilityBinding(ModConfigEditBuffer buffer, string componentSectionName, string drawLabel, string variableName, IReadOnlyList<string> keyBindings);
        ModConfigEditBuffer RemoveVisibilityBinding(ModConfigEditBuffer buffer, string componentSectionName, string drawLabel, string variableName);
    }
}
