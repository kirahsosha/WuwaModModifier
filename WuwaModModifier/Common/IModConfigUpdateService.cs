using System.Collections.Generic;
using WuwaModModifier.Model;

namespace WuwaModModifier.Common
{
    public interface IModConfigUpdateService
    {
        ModConfigEditBuffer LoadBuffer(string filePath);
        ModConfigTargetPreview PreviewSaveTarget(string configPath, ModConfigSaveTarget saveTarget, string modRootPath, string wwmiRootPath);
        ModConfigTargetPreview PreviewSync(string configPath, ModConfigSyncDirection direction, string modRootPath, string wwmiRootPath);
        ModConfigStandardizationResult StandardizeToggleSlots(ModConfigEditBuffer buffer, string templatePath);
        ModConfigEditBuffer CreateParameter(ModConfigEditBuffer buffer, string variableName);
        ModConfigEditBuffer UpdateParameterDefaultValue(ModConfigEditBuffer buffer, string variableName, string newDefaultValue);
        ModConfigEditBuffer CreateToggleBinding(ModConfigEditBuffer buffer, string variableName, IReadOnlyList<string> keyBindings);
        ModConfigEditBuffer UpdateKeyBindings(ModConfigEditBuffer buffer, string keySectionName, IReadOnlyList<string> newKeyBindings);
        ModConfigEditBuffer UpdateToggleTargetValues(ModConfigEditBuffer buffer, string keySectionName, string variableName, IReadOnlyList<string> newValues);
        ModConfigEditBuffer RenameParameter(ModConfigEditBuffer buffer, string oldVariableName, string newVariableName);
        ModConfigEditBuffer ToggleVisibility(ModConfigEditBuffer buffer, string componentSectionName, string drawLabel, bool isVisible);
        ModConfigEditBuffer BindVisibilityToParameter(ModConfigEditBuffer buffer, string componentSectionName, string drawLabel, string variableName);
        ModConfigEditBuffer CreateVisibilityBinding(ModConfigEditBuffer buffer, string componentSectionName, string drawLabel, string variableName, IReadOnlyList<string> keyBindings);
        ModConfigSaveResult SaveBuffer(ModConfigEditBuffer buffer, string targetPath);
        ModConfigSaveResult SaveBufferToTarget(ModConfigEditBuffer buffer, ModConfigSaveTarget saveTarget, string modRootPath, string wwmiRootPath);
        ModConfigSyncResult SyncConfig(string configPath, ModConfigSyncDirection direction, string modRootPath, string wwmiRootPath);
    }
}