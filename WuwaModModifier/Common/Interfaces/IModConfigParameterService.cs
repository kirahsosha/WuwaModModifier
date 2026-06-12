using WuwaModModifier.Model;

namespace WuwaModModifier.Common
{
    /// <summary>
    /// Parameter-related config operations: creation, default-value updates,
    /// and safe renaming.
    /// </summary>
    public interface IModConfigParameterService
    {
        ModConfigEditBuffer CreateParameter(ModConfigEditBuffer buffer, string variableName);
        ModConfigEditBuffer UpdateParameterDefaultValue(ModConfigEditBuffer buffer, string variableName, string newDefaultValue);
        ModConfigEditBuffer RenameParameter(ModConfigEditBuffer buffer, string oldVariableName, string newVariableName);
    }
}
