using System;
using System.Linq;
using WuwaModModifier.Model;

namespace WuwaModModifier.Common
{
    /// <summary>
    /// Parameter-related config operations: creation, default-value updates,
    /// and safe renaming.
    /// </summary>
    public class ModConfigParameterService : IModConfigParameterService
    {
        internal readonly IFileSystemService _fileSystem;
        internal readonly IModConfigParser _parser;
        internal readonly IModConfigAnalysisService _analysisService;

        public ModConfigParameterService(
            IFileSystemService fileSystem,
            IModConfigParser parser,
            IModConfigAnalysisService analysisService)
        {
            _fileSystem = fileSystem;
            _parser = parser;
            _analysisService = analysisService;
        }

        public ModConfigEditBuffer CreateParameter(ModConfigEditBuffer buffer, string variableName)
        {
            if (string.IsNullOrWhiteSpace(variableName))
            {
                throw new ArgumentException("参数名不能为空。", nameof(variableName));
            }

            ModConfigUpdateService.ValidateVariableName(variableName, nameof(variableName));

            var document = ParseBuffer(buffer);
            ModConfigUpdateService.InsertParameterDeclaration(document, variableName);
            return ModConfigUpdateService.CreateUpdatedBuffer(buffer, document, $"新增参数 {variableName}");
        }

        public ModConfigEditBuffer UpdateParameterDefaultValue(
            ModConfigEditBuffer buffer,
            string variableName,
            string newDefaultValue)
        {
            if (string.IsNullOrWhiteSpace(variableName))
            {
                throw new ArgumentException("参数名不能为空。", nameof(variableName));
            }

            if (string.IsNullOrWhiteSpace(newDefaultValue))
            {
                throw new ArgumentException("参数默认值不能为空。", nameof(newDefaultValue));
            }

            ModConfigUpdateService.ValidateVariableName(variableName, nameof(variableName));

            var document = ParseBuffer(buffer);
            var declaration = ModConfigUpdateService.FindVariableDeclaration(document, variableName);
            if (declaration == null)
            {
                throw new InvalidOperationException($"未找到参数 {variableName} 的声明语句。");
            }

            declaration.RawText = ModConfigUpdateService.ReplaceAssignmentValue(declaration.RawText, newDefaultValue);
            declaration.Value = newDefaultValue;
            return ModConfigUpdateService.CreateUpdatedBuffer(buffer, document, $"更新参数 {variableName} 的默认值");
        }

        public ModConfigEditBuffer RenameParameter(
            ModConfigEditBuffer buffer,
            string oldVariableName,
            string newVariableName)
        {
            ModConfigUpdateService.ValidateVariableName(oldVariableName, nameof(oldVariableName));
            ModConfigUpdateService.ValidateVariableName(newVariableName, nameof(newVariableName));

            if (oldVariableName.Equals(newVariableName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("新旧参数名不能相同。");
            }

            var document = ParseBuffer(buffer);
            var analysis = _analysisService.Analyze(document);
            var parameter = analysis.Parameters.SingleOrDefault(item =>
                item.Name.Equals(oldVariableName, StringComparison.OrdinalIgnoreCase));

            if (parameter == null)
            {
                throw new InvalidOperationException($"未找到参数 {oldVariableName}。");
            }

            if (!parameter.CanRename || parameter.Kind == ModConfigParameterKind.System)
            {
                throw new InvalidOperationException($"参数 {oldVariableName} 不允许安全重命名。");
            }

            if (analysis.Parameters.Any(item => item.Name.Equals(newVariableName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"参数 {newVariableName} 已存在。");
            }

            var changed = ModConfigUpdateService.ReplaceVariableTokensInDocument(document, oldVariableName, newVariableName);

            if (!changed)
            {
                throw new InvalidOperationException($"参数 {oldVariableName} 没有可替换的安全引用。");
            }

            return ModConfigUpdateService.CreateUpdatedBuffer(buffer, document, $"重命名参数 {oldVariableName} -> {newVariableName}");
        }

        internal ModConfigDocument ParseBuffer(ModConfigEditBuffer buffer)
        {
            return ModConfigUpdateService.ParseBuffer(_parser, buffer);
        }
    }
}
