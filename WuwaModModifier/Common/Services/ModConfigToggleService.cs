using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WuwaModModifier.Model;

namespace WuwaModModifier.Common
{
    /// <summary>
    /// Toggle-related config operations: key-binding application, standardisation
    /// slot matching, and toggle creation.
    /// </summary>
    public class ModConfigToggleService : IModConfigToggleService
    {
        internal readonly IFileSystemService _fileSystem;
        internal readonly IModConfigParser _parser;
        internal readonly IModConfigAnalysisService _analysisService;

        public ModConfigToggleService(
            IFileSystemService fileSystem,
            IModConfigParser parser,
            IModConfigAnalysisService analysisService)
        {
            _fileSystem = fileSystem;
            _parser = parser;
            _analysisService = analysisService;
        }

        public ModConfigStandardizationResult StandardizeToggleSlots(ModConfigEditBuffer buffer, string templatePath)
        {
            if (string.IsNullOrWhiteSpace(templatePath) || !_fileSystem.FileExists(templatePath))
            {
                throw new FileNotFoundException("标准模板文件不存在。", templatePath);
            }

            var slots = ModConfigUpdateService.LoadStandardToggleSlots(_fileSystem, _parser, _analysisService, templatePath);
            if (slots.Count == 0)
            {
                throw new InvalidOperationException("标准模板中未找到可用的 toggle 槽位。");
            }

            var document = ParseBuffer(buffer);
            var orderedKeySections = document.Sections
                .Where(section => ModConfigUpdateService.IsKeySection(section.Name))
                .ToList();
            var analysis = _analysisService.Analyze(document);
            var fullCount = 0;
            var partialCount = 0;
            var skippedCount = 0;
            var nextSlotIndex = 0;
            var usedSlotIndices = new HashSet<int>();
            var items = new List<ModConfigStandardizationItemResult>();

            // Build lookup from standard variable name to slot index for stable matching.
            var slotIndexByVariable = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < slots.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(slots[i].VariableName))
                {
                    slotIndexByVariable[slots[i].VariableName] = i;
                }
            }

            foreach (var section in orderedKeySections)
            {
                var toggle = analysis.Toggles.FirstOrDefault(item =>
                    item.SectionName.Equals(section.Name, StringComparison.OrdinalIgnoreCase));
                var currentVariableName = toggle?.Targets.FirstOrDefault()?.VariableName ?? string.Empty;

                // Resolve slot: stable match by variable name first, then sequential.
                int slotIndex;
                if (!string.IsNullOrWhiteSpace(currentVariableName) &&
                    slotIndexByVariable.TryGetValue(currentVariableName, out var matchedIndex) &&
                    !usedSlotIndices.Contains(matchedIndex))
                {
                    slotIndex = matchedIndex;
                }
                else
                {
                    while (nextSlotIndex < slots.Count && usedSlotIndices.Contains(nextSlotIndex))
                    {
                        nextSlotIndex++;
                    }

                    if (nextSlotIndex >= slots.Count)
                    {
                        skippedCount++;
                        items.Add(new ModConfigStandardizationItemResult
                        {
                            OriginalSectionName = section.Name,
                            FinalSectionName = section.Name,
                            OriginalVariableName = currentVariableName,
                            FinalVariableName = currentVariableName,
                            Status = ModConfigStandardizationStatus.Skipped,
                            Reason = "标准模板槽位不足。"
                        });
                        continue;
                    }

                    slotIndex = nextSlotIndex++;
                }

                usedSlotIndices.Add(slotIndex);
                var slot = slots[slotIndex];
                var originalSectionName = section.Name;
                ModConfigUpdateService.ApplyKeyBindingsToSection(section, slot.KeyBindings);

                analysis = _analysisService.Analyze(document);
                partialCount++;
                items.Add(new ModConfigStandardizationItemResult
                {
                    OriginalSectionName = originalSectionName,
                    FinalSectionName = section.Name,
                    OriginalVariableName = currentVariableName,
                    FinalVariableName = currentVariableName,
                    TargetKeyBindings = slot.KeyBindings.ToList(),
                    Status = ModConfigStandardizationStatus.PartiallyStandardized,
                    Reason = "快捷键已强制对齐模板。"
                });
            }

            return new ModConfigStandardizationResult
            {
                Buffer = ModConfigUpdateService.CreateUpdatedBuffer(buffer, document, $"按 {Path.GetFileName(templatePath)} 批量标准化"),
                FullyStandardizedCount = fullCount,
                PartiallyStandardizedCount = partialCount,
                SkippedCount = skippedCount,
                Items = items
            };
        }

        public ModConfigEditBuffer CreateToggleBinding(
            ModConfigEditBuffer buffer,
            string variableName,
            IReadOnlyList<string> keyBindings)
        {
            if (string.IsNullOrWhiteSpace(variableName))
            {
                throw new ArgumentException("参数名不能为空。", nameof(variableName));
            }

            ModConfigUpdateService.ValidateVariableName(variableName, nameof(variableName));
            var normalizedBindings = ModConfigUpdateService.NormalizeKeyBindings(keyBindings);

            var document = ParseBuffer(buffer);
            ModConfigUpdateService.EnsureParameterDeclarationExists(document, variableName);
            ModConfigUpdateService.InsertVisibilityBindingKeySection(document, variableName, normalizedBindings);
            return ModConfigUpdateService.CreateUpdatedBuffer(buffer, document, $"为参数 {variableName} 新增按键绑定");
        }

        public ModConfigEditBuffer UpdateKeyBindings(
            ModConfigEditBuffer buffer,
            string keySectionName,
            IReadOnlyList<string> newKeyBindings)
        {
            var normalizedBindings = ModConfigUpdateService.NormalizeKeyBindings(newKeyBindings);
            var document = ParseBuffer(buffer);
            var section = ModConfigUpdateService.FindSection(document, keySectionName);
            var keyStatementIndices = section.Statements
                .Select((statement, index) => new { statement, index })
                .Where(item => item.statement.Kind == ModConfigStatementKind.Assignment &&
                    item.statement.Name.Equals("key", StringComparison.OrdinalIgnoreCase))
                .Select(item => item.index)
                .ToList();

            if (keyStatementIndices.Count == 0)
            {
                throw new InvalidOperationException($"节 {keySectionName} 中不存在 key = 行。");
            }

            ModConfigUpdateService.ApplyKeyBindingsToSection(section, normalizedBindings);

            return ModConfigUpdateService.CreateUpdatedBuffer(buffer, document, $"更新节 {section.Name} 的快捷键");
        }

        public ModConfigEditBuffer UpdateToggleTargetValues(
            ModConfigEditBuffer buffer,
            string keySectionName,
            string variableName,
            IReadOnlyList<string> newValues)
        {
            if (string.IsNullOrWhiteSpace(keySectionName))
            {
                throw new ArgumentException("节名不能为空。", nameof(keySectionName));
            }

            if (string.IsNullOrWhiteSpace(variableName))
            {
                throw new ArgumentException("参数名不能为空。", nameof(variableName));
            }

            ModConfigUpdateService.ValidateVariableName(variableName, nameof(variableName));
            var normalizedValues = ModConfigUpdateService.NormalizeToggleTargetValues(newValues);

            var document = ParseBuffer(buffer);
            var section = ModConfigUpdateService.FindSection(document, keySectionName);
            var targetStatement = section.Statements.FirstOrDefault(statement =>
                statement.Kind == ModConfigStatementKind.VariableAssignment &&
                statement.VariableName.Equals(variableName, StringComparison.OrdinalIgnoreCase));

            if (targetStatement == null)
            {
                throw new InvalidOperationException($"节 {keySectionName} 中不存在 {variableName} 的赋值行。");
            }

            var mergedValueText = string.Join(", ", normalizedValues);
            targetStatement.RawText = ModConfigUpdateService.ReplaceAssignmentValue(targetStatement.RawText, mergedValueText);
            targetStatement.Value = mergedValueText;
            return ModConfigUpdateService.CreateUpdatedBuffer(buffer, document, $"更新节 {section.Name} 中 {variableName} 的切换值");
        }

        internal ModConfigDocument ParseBuffer(ModConfigEditBuffer buffer)
        {
            return ModConfigUpdateService.ParseBuffer(_parser, buffer);
        }
    }
}
