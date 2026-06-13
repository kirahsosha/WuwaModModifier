using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WuwaModModifier.Common.Helpers;
using WuwaModModifier.Model;

namespace WuwaModModifier.Common
{
    public class ModConfigUpdateService : IModConfigUpdateService
    {
        internal static readonly Regex VariableTokenRegex =
            new Regex(@"(?<var>\$[^\s=,]+)", RegexOptions.Compiled);

        internal static readonly Regex VariableNameRegex =
            new Regex(@"^\$[^\s=,\[\];]+$", RegexOptions.Compiled);

        internal static readonly Regex EqualityClauseRegex =
            new Regex(@"^\$[^\s=,]+\s*==\s*(?<value>[^\s\)]+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        internal static readonly Regex InequalityClauseRegex =
            new Regex(@"^\$[^\s=,]+\s*!=\s*(?<value>[^\s\)]+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        internal readonly IFileSystemService _fileSystem;
        internal readonly IModConfigParser _parser;
        internal readonly IModConfigAnalysisService _analysisService;

        public ModConfigUpdateService(
            IFileSystemService fileSystem,
            IModConfigParser parser,
            IModConfigAnalysisService analysisService)
        {
            _fileSystem = fileSystem;
            _parser = parser;
            _analysisService = analysisService;
        }

        public ModConfigEditBuffer LoadBuffer(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !_fileSystem.FileExists(filePath))
            {
                throw new FileNotFoundException("配置文件不存在。", filePath);
            }

            var content = _fileSystem.ReadAllText(filePath);
            return new ModConfigEditBuffer
            {
                SourcePath = filePath,
                Content = content,
                LineEnding = DetectLineEnding(content)
            };
        }

        public ModConfigTargetPreview PreviewSaveTarget(
            string configPath,
            ModConfigSaveTarget saveTarget,
            string modRootPath,
            string wwmiRootPath)
        {
            var targetPath = ResolveTargetPath(configPath, saveTarget, modRootPath, wwmiRootPath);
            return new ModConfigTargetPreview
            {
                SourcePath = configPath,
                TargetPath = targetPath,
                TargetExists = _fileSystem.FileExists(targetPath)
            };
        }

        public ModConfigTargetPreview PreviewSync(
            string configPath,
            ModConfigSyncDirection direction,
            string modRootPath,
            string wwmiRootPath)
        {
            var sourcePath = direction == ModConfigSyncDirection.ModToWwmi
                ? ResolveTargetPath(configPath, ModConfigSaveTarget.ModDirectory, modRootPath, wwmiRootPath)
                : ResolveTargetPath(configPath, ModConfigSaveTarget.WwmiDirectory, modRootPath, wwmiRootPath);
            var targetPath = direction == ModConfigSyncDirection.ModToWwmi
                ? ResolveTargetPath(configPath, ModConfigSaveTarget.WwmiDirectory, modRootPath, wwmiRootPath)
                : ResolveTargetPath(configPath, ModConfigSaveTarget.ModDirectory, modRootPath, wwmiRootPath);

            return new ModConfigTargetPreview
            {
                SourcePath = sourcePath,
                TargetPath = targetPath,
                TargetExists = _fileSystem.FileExists(targetPath)
            };
        }

        public ModConfigStandardizationResult StandardizeToggleSlots(ModConfigEditBuffer buffer, string templatePath)
        {
            if (string.IsNullOrWhiteSpace(templatePath) || !_fileSystem.FileExists(templatePath))
            {
                throw new FileNotFoundException("标准模板文件不存在。", templatePath);
            }

            var slots = LoadStandardToggleSlots(_fileSystem, _parser, _analysisService, templatePath);
            if (slots.Count == 0)
            {
                throw new InvalidOperationException("标准模板中未找到可用的 toggle 槽位。");
            }

            var document = ParseBuffer(_parser, buffer);
            var orderedKeySections = document.Sections
                .Where(section => IsKeySection(section.Name))
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
                ApplyKeyBindingsToSection(section, slot.KeyBindings);

                // Always partially standardized: only key bindings are updated,
                // section names and variable names/values are preserved.
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
                Buffer = CreateUpdatedBuffer(buffer, document, $"按 {Path.GetFileName(templatePath)} 批量标准化"),
                FullyStandardizedCount = fullCount,
                PartiallyStandardizedCount = partialCount,
                SkippedCount = skippedCount,
                Items = items
            };
        }

        public ModConfigEditBuffer CreateParameter(ModConfigEditBuffer buffer, string variableName)
        {
            if (string.IsNullOrWhiteSpace(variableName))
            {
                throw new ArgumentException("参数名不能为空。", nameof(variableName));
            }

            ValidateVariableName(variableName, nameof(variableName));

            var document = ParseBuffer(_parser, buffer);
            InsertParameterDeclaration(document, variableName);
            return CreateUpdatedBuffer(buffer, document, $"新增参数 {variableName}");
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

            ValidateVariableName(variableName, nameof(variableName));

            var document = ParseBuffer(_parser, buffer);
            var declaration = FindVariableDeclaration(document, variableName);
            if (declaration == null)
            {
                throw new InvalidOperationException($"未找到参数 {variableName} 的声明语句。");
            }

            declaration.RawText = ReplaceAssignmentValue(declaration.RawText, newDefaultValue);
            declaration.Value = newDefaultValue;
            return CreateUpdatedBuffer(buffer, document, $"更新参数 {variableName} 的默认值");
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

            ValidateVariableName(variableName, nameof(variableName));
            var normalizedBindings = NormalizeKeyBindings(keyBindings);

            var document = ParseBuffer(_parser, buffer);
            EnsureParameterDeclarationExists(document, variableName);
            InsertVisibilityBindingKeySection(document, variableName, normalizedBindings);
            return CreateUpdatedBuffer(buffer, document, $"为参数 {variableName} 新增按键绑定");
        }

        public ModConfigEditBuffer UpdateKeyBindings(
            ModConfigEditBuffer buffer,
            string keySectionName,
            IReadOnlyList<string> newKeyBindings)
        {
            var normalizedBindings = NormalizeKeyBindings(newKeyBindings);
            var document = ParseBuffer(_parser, buffer);
            var section = FindSection(document, keySectionName);
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

            ApplyKeyBindingsToSection(section, normalizedBindings);

            return CreateUpdatedBuffer(buffer, document, $"更新节 {section.Name} 的快捷键");
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

            ValidateVariableName(variableName, nameof(variableName));
            var normalizedValues = NormalizeToggleTargetValues(newValues);

            var document = ParseBuffer(_parser, buffer);
            var section = FindSection(document, keySectionName);
            var targetStatement = section.Statements.FirstOrDefault(statement =>
                statement.Kind == ModConfigStatementKind.VariableAssignment &&
                statement.VariableName.Equals(variableName, StringComparison.OrdinalIgnoreCase));

            if (targetStatement == null)
            {
                throw new InvalidOperationException($"节 {keySectionName} 中不存在 {variableName} 的赋值行。");
            }

            var mergedValueText = string.Join(", ", normalizedValues);
            targetStatement.RawText = ReplaceAssignmentValue(targetStatement.RawText, mergedValueText);
            targetStatement.Value = mergedValueText;
            return CreateUpdatedBuffer(buffer, document, $"更新节 {section.Name} 中 {variableName} 的切换值");
        }

        public ModConfigEditBuffer RenameParameter(
            ModConfigEditBuffer buffer,
            string oldVariableName,
            string newVariableName)
        {
            ValidateVariableName(oldVariableName, nameof(oldVariableName));
            ValidateVariableName(newVariableName, nameof(newVariableName));

            if (oldVariableName.Equals(newVariableName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("新旧参数名不能相同。");
            }

            var document = ParseBuffer(_parser, buffer);
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

            var changed = ReplaceVariableTokensInDocument(document, oldVariableName, newVariableName);

            if (!changed)
            {
                throw new InvalidOperationException($"参数 {oldVariableName} 没有可替换的安全引用。");
            }

            return CreateUpdatedBuffer(buffer, document, $"重命名参数 {oldVariableName} -> {newVariableName}");
        }

        public ModConfigEditBuffer ToggleVisibility(
            ModConfigEditBuffer buffer,
            string componentSectionName,
            string drawLabel,
            bool isVisible)
        {
            if (string.IsNullOrWhiteSpace(componentSectionName))
            {
                throw new ArgumentException("组件节名不能为空。", nameof(componentSectionName));
            }

            if (string.IsNullOrWhiteSpace(drawLabel))
            {
                throw new ArgumentException("模型显示标签不能为空。", nameof(drawLabel));
            }

            var document = ParseBuffer(_parser, buffer);
            var analysis = _analysisService.Analyze(document);
            var visibilityItem = FindVisibilityItem(analysis, componentSectionName, drawLabel);
            var drawTarget = FindDrawTargetInfo(document, componentSectionName, drawLabel);
            if (drawTarget == null)
            {
                throw new InvalidOperationException("未找到模型显示项的控制表达式。");
            }

            if (!isVisible && CanToggleVisibilityDirectly(visibilityItem))
            {
                WrapDrawTargetWithConstantCondition(drawTarget, false);
                return CreateUpdatedBuffer(
                    buffer,
                    document,
                    $"切换 {componentSectionName} / {drawLabel} 为隐藏");
            }

            if (isVisible && IsVisibilityDirectlyHidden(visibilityItem))
            {
                UnwrapDrawTargetFromConstantCondition(drawTarget, false);
                return CreateUpdatedBuffer(
                    buffer,
                    document,
                    $"切换 {componentSectionName} / {drawLabel} 为显示");
            }

            if (visibilityItem.ControllingParameters.Count != 1)
            {
                throw new InvalidOperationException("模型显示项不满足单参数安全改写条件。");
            }

            var variableName = visibilityItem.ControllingParameters[0];
            var parameter = analysis.Parameters.SingleOrDefault(item =>
                item.Name.Equals(variableName, StringComparison.OrdinalIgnoreCase));

            if (parameter == null || !parameter.IsDeclaredInConstants)
            {
                throw new InvalidOperationException($"未找到参数 {variableName} 的可写默认值定义。");
            }

            var availableValues = GetAvailableValues(parameter);
            if (availableValues.Count < 2)
            {
                throw new InvalidOperationException($"参数 {variableName} 的值域不足以安全切换显示状态。");
            }

            var controlExpressions = drawTarget.ControlExpressions
                .Where(expression => VariableTokenRegex.Matches(expression)
                    .Cast<Match>()
                    .Any(match => match.Groups["var"].Value.Equals(variableName, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (controlExpressions.Count == 0)
            {
                throw new InvalidOperationException("模型显示项没有可用于安全改写的控制表达式。");
            }

            var visibleValues = new HashSet<string>(availableValues, StringComparer.OrdinalIgnoreCase);
            foreach (var controlExpression in controlExpressions)
            {
                if (!TryGetAllowedValues(controlExpression, variableName, availableValues, out var allowedValues))
                {
                    throw new InvalidOperationException("模型显示项的控制表达式过于复杂，无法安全改写。");
                }

                visibleValues.IntersectWith(allowedValues);
            }

            if (visibleValues.Count == 0)
            {
                throw new InvalidOperationException("无法为该模型显示项推导可见状态值域。");
            }

            var targetValue = isVisible
                ? (visibleValues.Contains(parameter.DefaultValue) ? parameter.DefaultValue : visibleValues.First())
                : availableValues.FirstOrDefault(value => !visibleValues.Contains(value));

            if (string.IsNullOrWhiteSpace(targetValue))
            {
                throw new InvalidOperationException("没有可用于隐藏该模型显示项的安全默认值。");
            }

            var declaration = FindVariableDeclaration(document, variableName);
            if (declaration == null)
            {
                throw new InvalidOperationException($"未找到参数 {variableName} 的声明语句。");
            }

            declaration.RawText = ReplaceAssignmentValue(declaration.RawText, targetValue);
            return CreateUpdatedBuffer(
                buffer,
                document,
                $"切换 {componentSectionName} / {drawLabel} 为{(isVisible ? "显示" : "隐藏")}");
        }

        public ModConfigEditBuffer BindVisibilityToParameter(
            ModConfigEditBuffer buffer,
            string componentSectionName,
            string drawLabel,
            string variableName)
        {
            if (string.IsNullOrWhiteSpace(componentSectionName))
            {
                throw new ArgumentException("组件节名不能为空。", nameof(componentSectionName));
            }

            if (string.IsNullOrWhiteSpace(drawLabel))
            {
                throw new ArgumentException("模型显示标签不能为空。", nameof(drawLabel));
            }

            ValidateVariableName(variableName, nameof(variableName));

            var document = ParseBuffer(_parser, buffer);
            var analysis = _analysisService.Analyze(document);
            var visibilityItem = FindVisibilityItem(analysis, componentSectionName, drawLabel);

            EnsureVisibilityCanBind(visibilityItem);
            EnsureBindableVisibilityParameter(analysis, variableName);

            var drawTarget = FindDrawTargetInfo(document, componentSectionName, drawLabel);
            if (drawTarget == null)
            {
                throw new InvalidOperationException("未找到模型显示项的可插入位置。");
            }

            WrapDrawTargetWithVisibilityCondition(drawTarget, variableName);
            return CreateUpdatedBuffer(buffer, document, $"将 {componentSectionName} / {drawLabel} 绑定到参数 {variableName}");
        }

        public ModConfigEditBuffer CreateVisibilityBinding(
            ModConfigEditBuffer buffer,
            string componentSectionName,
            string drawLabel,
            string variableName,
            IReadOnlyList<string> keyBindings)
        {
            if (string.IsNullOrWhiteSpace(componentSectionName))
            {
                throw new ArgumentException("组件节名不能为空。", nameof(componentSectionName));
            }

            if (string.IsNullOrWhiteSpace(drawLabel))
            {
                throw new ArgumentException("模型显示标签不能为空。", nameof(drawLabel));
            }

            if (string.IsNullOrWhiteSpace(variableName))
            {
                throw new ArgumentException("参数名不能为空。", nameof(variableName));
            }

            ValidateVariableName(variableName, nameof(variableName));
            var normalizedBindings = NormalizeKeyBindings(keyBindings);

            var document = ParseBuffer(_parser, buffer);
            var analysis = _analysisService.Analyze(document);
            var visibilityItem = FindVisibilityItem(analysis, componentSectionName, drawLabel);

            EnsureVisibilityCanCreateNewBinding(visibilityItem);

            if (analysis.Parameters.Any(item => item.Name.Equals(variableName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"参数 {variableName} 已存在，不能重复创建。");
            }

            InsertParameterDeclaration(document, variableName);
            InsertVisibilityBindingKeySection(document, variableName, normalizedBindings);

            var drawTarget = FindDrawTargetInfo(document, componentSectionName, drawLabel);
            if (drawTarget == null)
            {
                throw new InvalidOperationException("未找到模型显示项的可插入位置。");
            }

            if (visibilityItem.ControllingParameters.Count == 1)
            {
                ReplaceVisibilityControlVariable(
                    drawTarget,
                    visibilityItem.ControllingParameters[0],
                    variableName);
            }
            else
            {
                WrapDrawTargetWithVisibilityCondition(drawTarget, variableName);
            }

            return CreateUpdatedBuffer(buffer, document, $"为 {componentSectionName} / {drawLabel} 新建参数 {variableName} 与快捷键绑定");
        }

        public ModConfigEditBuffer RemoveVisibilityBinding(
            ModConfigEditBuffer buffer,
            string componentSectionName,
            string drawLabel,
            string variableName)
        {
            if (string.IsNullOrWhiteSpace(componentSectionName))
            {
                throw new ArgumentException("组件节名不能为空。", nameof(componentSectionName));
            }

            if (string.IsNullOrWhiteSpace(drawLabel))
            {
                throw new ArgumentException("模型显示标签不能为空。", nameof(drawLabel));
            }

            if (string.IsNullOrWhiteSpace(variableName))
            {
                throw new ArgumentException("参数名不能为空。", nameof(variableName));
            }

            var document = ParseBuffer(_parser, buffer);
            var analysis = _analysisService.Analyze(document);
            var visibilityItem = FindVisibilityItem(analysis, componentSectionName, drawLabel);

            if (!visibilityItem.ControllingParameters.Contains(variableName, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"参数 {variableName} 不直接控制该模型显示项。");
            }

            var drawTarget = FindDrawTargetInfo(document, componentSectionName, drawLabel);
            if (drawTarget == null)
            {
                throw new InvalidOperationException("未找到模型显示项的控制表达式。");
            }

            UnwrapVisibilityBinding(drawTarget, variableName);
            return CreateUpdatedBuffer(buffer, document, $"移除 {componentSectionName} / {drawLabel} 对 {variableName} 的控制绑定");
        }

        public ModConfigSaveResult SaveBuffer(ModConfigEditBuffer buffer, string targetPath)
        {
            ValidateBuffer(buffer);
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                throw new ArgumentException("目标路径不能为空。", nameof(targetPath));
            }

            _fileSystem.WriteAllText(targetPath, buffer.Content);
            return new ModConfigSaveResult
            {
                TargetPath = targetPath
            };
        }

        public ModConfigSaveResult SaveBufferToTarget(
            ModConfigEditBuffer buffer,
            ModConfigSaveTarget saveTarget,
            string modRootPath,
            string wwmiRootPath)
        {
            var targetPath = ResolveTargetPath(buffer.SourcePath, saveTarget, modRootPath, wwmiRootPath);
            return SaveBuffer(buffer, targetPath);
        }

        public ModConfigSyncResult SyncConfig(
            string configPath,
            ModConfigSyncDirection direction,
            string modRootPath,
            string wwmiRootPath)
        {
            if (string.IsNullOrWhiteSpace(configPath))
            {
                throw new ArgumentException("配置路径不能为空。", nameof(configPath));
            }

            var preview = PreviewSync(configPath, direction, modRootPath, wwmiRootPath);
            var sourcePath = preview.SourcePath;
            var targetPath = preview.TargetPath;

            if (!_fileSystem.FileExists(sourcePath))
            {
                throw new FileNotFoundException("同步源配置文件不存在。", sourcePath);
            }

            _fileSystem.WriteAllText(targetPath, _fileSystem.ReadAllText(sourcePath));
            return new ModConfigSyncResult
            {
                SourcePath = sourcePath,
                TargetPath = targetPath
            };
        }

        internal static string DetectLineEnding(string content)
        {
            if (content.Contains("\r\n", StringComparison.Ordinal))
            {
                return "\r\n";
            }

            if (content.Contains('\n'))
            {
                return "\n";
            }

            return Environment.NewLine;
        }

        internal static void ValidateBuffer(ModConfigEditBuffer? buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }
        }

        internal static void ValidateVariableName(string variableName, string parameterName)
        {
            if (!VariableNameRegex.IsMatch(variableName))
            {
                throw new ArgumentException("变量名格式非法。", parameterName);
            }
        }

        internal static IReadOnlyList<string> NormalizeKeyBindings(IReadOnlyList<string> newKeyBindings)
        {
            if (newKeyBindings == null)
            {
                throw new ArgumentNullException(nameof(newKeyBindings));
            }

            var result = newKeyBindings
                .Select(binding => binding?.Trim() ?? string.Empty)
                .ToList();

            if (result.Count == 0 || result.Any(binding => !IsValidKeyBinding(binding)))
            {
                throw new InvalidOperationException("快捷键文本非法。");
            }

            return result;
        }

        internal static IReadOnlyList<string> NormalizeToggleTargetValues(IReadOnlyList<string> newValues)
        {
            if (newValues == null)
            {
                throw new ArgumentNullException(nameof(newValues));
            }

            var result = newValues
                .Select(value => value?.Trim() ?? string.Empty)
                .ToList();

            if (result.Count == 0 || result.Any(value => string.IsNullOrWhiteSpace(value) || value.IndexOfAny(new[] { '\r', '\n', '=', ';', '[', ']' }) >= 0))
            {
                throw new InvalidOperationException("切换值文本非法。");
            }

            return result;
        }

        internal static bool IsValidKeyBinding(string binding)
        {
            return !string.IsNullOrWhiteSpace(binding) &&
                binding.IndexOfAny(new[] { '\r', '\n', '=', ';', '[', ']' }) < 0;
        }

        internal static ModConfigDocument ParseBuffer(IModConfigParser parser, ModConfigEditBuffer buffer)
        {
            ValidateBuffer(buffer);
            return parser.Parse(buffer.Content);
        }

        internal static ModConfigSection FindSection(ModConfigDocument document, string sectionName)
        {
            var section = document.Sections.FirstOrDefault(item =>
                item.Name.Equals(sectionName, StringComparison.OrdinalIgnoreCase));
            if (section == null)
            {
                throw new InvalidOperationException($"未找到节 {sectionName}。");
            }

            return section;
        }

        internal static string GetIndentation(string rawText)
        {
            var nonWhitespaceIndex = rawText.TakeWhile(char.IsWhiteSpace).Count();
            return rawText.Substring(0, nonWhitespaceIndex);
        }

        internal static void ApplyKeyBindingsToSection(ModConfigSection section, IReadOnlyList<string> normalizedBindings)
        {
            var keyStatementIndices = section.Statements
                .Select((statement, index) => new { statement, index })
                .Where(item => item.statement.Kind == ModConfigStatementKind.Assignment &&
                    item.statement.Name.Equals("key", StringComparison.OrdinalIgnoreCase))
                .Select(item => item.index)
                .ToList();

            if (keyStatementIndices.Count == 0)
            {
                throw new InvalidOperationException($"节 {section.Name} 中不存在 key = 行。");
            }

            var firstStatement = section.Statements[keyStatementIndices[0]];
            var indent = GetIndentation(firstStatement.RawText);
            var insertIndex = keyStatementIndices[0];
            var lineNumber = firstStatement.LineNumber;

            for (var index = keyStatementIndices.Count - 1; index >= 0; index--)
            {
                section.Statements.RemoveAt(keyStatementIndices[index]);
            }

            section.Statements.InsertRange(insertIndex, normalizedBindings.Select(binding => new ModConfigStatement
            {
                Kind = ModConfigStatementKind.Assignment,
                RawText = $"{indent}key = {binding}",
                Name = "key",
                Value = binding,
                LineNumber = lineNumber
            }));
        }

        internal static string ReplaceVariableTokens(string rawText, string oldVariableName, string newVariableName)
        {
            return VariableTokenRegex.Replace(rawText, match =>
            {
                var variableName = match.Groups["var"].Value;
                return variableName.Equals(oldVariableName, StringComparison.OrdinalIgnoreCase)
                    ? newVariableName
                    : variableName;
            });
        }

        internal static bool ReplaceVariableTokensInDocument(ModConfigDocument document, string oldVariableName, string newVariableName)
        {
            var changed = false;
            foreach (var statement in EnumerateStatements(document))
            {
                if (statement.Kind == ModConfigStatementKind.Comment || statement.Kind == ModConfigStatementKind.BlankLine)
                {
                    continue;
                }

                var replacedText = ReplaceVariableTokens(statement.RawText, oldVariableName, newVariableName);
                if (!replacedText.Equals(statement.RawText, StringComparison.Ordinal))
                {
                    statement.RawText = replacedText;
                    if (statement.VariableName.Equals(oldVariableName, StringComparison.OrdinalIgnoreCase))
                    {
                        statement.VariableName = newVariableName;
                    }

                    if (statement.Name.Equals(oldVariableName, StringComparison.OrdinalIgnoreCase))
                    {
                        statement.Name = newVariableName;
                    }

                    if (statement.Value.Equals(oldVariableName, StringComparison.OrdinalIgnoreCase))
                    {
                        statement.Value = newVariableName;
                    }

                    changed = true;
                }
            }

            return changed;
        }

        internal static void RenameSection(ModConfigSection section, string newSectionName)
        {
            if (section.Name.Equals(newSectionName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            section.Name = newSectionName;
            section.HeaderText = $"[{newSectionName}]";
        }

        internal static bool IsKeySection(string sectionName)
        {
            return ModConfigSectionHelpers.IsKeySection(sectionName);
        }

        internal static bool TryGetStandardizationTarget(
            ModToggleDefinition toggle,
            IReadOnlyCollection<ModParameterDefinition> parameters,
            out string currentVariableName,
            out bool canRename)
        {
            currentVariableName = string.Empty;
            canRename = false;

            if (!toggle.ToggleType.Equals("cycle", StringComparison.OrdinalIgnoreCase) || toggle.Targets.Count != 1)
            {
                return false;
            }

            var target = toggle.Targets[0];
            if (string.IsNullOrWhiteSpace(target.VariableName) || !IsBinaryToggleValues(target.Values))
            {
                return false;
            }

            var parameter = parameters.FirstOrDefault(item =>
                item.Name.Equals(target.VariableName, StringComparison.OrdinalIgnoreCase));
            if (parameter == null || parameter.Kind == ModConfigParameterKind.System)
            {
                return false;
            }

            currentVariableName = target.VariableName;
            canRename = parameter.CanRename;
            return true;
        }

        internal static bool IsBinaryToggleValues(IReadOnlyCollection<string> values)
        {
            return values.Count > 0 && values.All(value => value == "0" || value == "1");
        }

        internal static List<StandardToggleSlot> LoadStandardToggleSlots(
            IFileSystemService fileSystem,
            IModConfigParser parser,
            IModConfigAnalysisService analysisService,
            string templatePath)
        {
            var templateDocument = parser.ParseFile(templatePath);
            var templateAnalysis = analysisService.Analyze(templateDocument);

            return templateAnalysis.Toggles
                .Where(toggle => TryGetStandardizationTarget(toggle, templateAnalysis.Parameters, out _, out _))
                .Select(toggle => new StandardToggleSlot
                {
                    SectionName = toggle.SectionName,
                    VariableName = toggle.Targets[0].VariableName,
                    KeyBindings = toggle.KeyBindings.ToList()
                })
                .ToList();
        }

        internal static IEnumerable<ModConfigStatement> EnumerateStatements(ModConfigDocument document)
        {
            foreach (var statement in document.RootStatements)
            {
                yield return statement;
            }

            foreach (var section in document.Sections)
            {
                foreach (var statement in section.Statements)
                {
                    yield return statement;
                }
            }
        }

        internal static IReadOnlyList<string> GetAvailableValues(ModParameterDefinition parameter)
        {
            var values = parameter.ValueOptions
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (values.Count == 0 && (parameter.DefaultValue == "0" || parameter.DefaultValue == "1"))
            {
                values.Add("0");
                values.Add("1");
            }

            return values;
        }

        internal static ModVisibilityItem FindVisibilityItem(
            ModConfigAnalysisResult analysis,
            string componentSectionName,
            string drawLabel)
        {
            var normalizedRequestedLabels = SplitVisibilityLabels(drawLabel)
                .Select(NormalizeVisibilityLabel)
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .ToList();
            var visibilityItem = analysis.VisibilityItems.SingleOrDefault(item =>
                item.SectionName.Equals(componentSectionName, StringComparison.OrdinalIgnoreCase) &&
                item.DrawLabels
                    .Select(NormalizeVisibilityLabel)
                    .Any(label => normalizedRequestedLabels.Contains(label, StringComparer.OrdinalIgnoreCase)));

            if (visibilityItem == null)
            {
                throw new InvalidOperationException($"未找到 {componentSectionName} / {drawLabel} 对应的模型显示项。");
            }

            return visibilityItem;
        }

        internal static void EnsureVisibilityCanBind(ModVisibilityItem visibilityItem)
        {
            if (visibilityItem.DrawCallCount <= 0)
            {
                throw new InvalidOperationException("该模型显示项没有可精确改写的 draw 调用。");
            }

            if (visibilityItem.ControllingParameters.Count > 0 ||
                visibilityItem.ControllingKeySections.Count > 0 ||
                visibilityItem.ControllingKeyBindings.Count > 0)
            {
                throw new InvalidOperationException("该模型显示项已存在控制参数或按键绑定。");
            }
        }

        internal static void EnsureVisibilityCanCreateNewBinding(ModVisibilityItem visibilityItem)
        {
            if (visibilityItem.DrawCallCount <= 0)
            {
                throw new InvalidOperationException("该模型显示项没有可精确改写的 draw 调用。");
            }

            if (visibilityItem.ControllingParameters.Count > 1)
            {
                throw new InvalidOperationException("该模型显示项存在多个控制参数，当前无法自动改绑。");
            }

            if (visibilityItem.ControllingParameters.Count == 0 &&
                (visibilityItem.ControllingKeySections.Count > 0 || visibilityItem.ControllingKeyBindings.Count > 0))
            {
                throw new InvalidOperationException("该模型显示项存在无法识别参数的控制按键，当前无法自动改绑。");
            }
        }

        internal static void EnsureBindableVisibilityParameter(
            ModConfigAnalysisResult analysis,
            string variableName)
        {
            var parameter = analysis.Parameters.SingleOrDefault(item =>
                item.Name.Equals(variableName, StringComparison.OrdinalIgnoreCase));
            if (parameter == null)
            {
                throw new InvalidOperationException($"未找到参数 {variableName}。");
            }

            var availableValues = GetAvailableValues(parameter);
            var isBinaryToggle = availableValues.Count > 0 &&
                availableValues.All(value => value == "0" || value == "1") &&
                availableValues.Contains("0", StringComparer.OrdinalIgnoreCase) &&
                availableValues.Contains("1", StringComparer.OrdinalIgnoreCase);

            if (!parameter.IsDeclaredInConstants ||
                parameter.Kind == ModConfigParameterKind.System ||
                parameter.BoundKeySections.Count == 0 ||
                !isBinaryToggle)
            {
                throw new InvalidOperationException($"参数 {variableName} 不属于可复用的二值切换参数。");
            }
        }

        internal static ModConfigStatement? FindVariableDeclaration(ModConfigDocument document, string variableName)
        {
            return EnumerateStatements(document).FirstOrDefault(statement =>
                statement.Kind == ModConfigStatementKind.GlobalDeclaration &&
                statement.VariableName.Equals(variableName, StringComparison.OrdinalIgnoreCase));
        }

        internal static void EnsureParameterDeclarationExists(ModConfigDocument document, string variableName)
        {
            if (FindVariableDeclaration(document, variableName) == null)
            {
                throw new InvalidOperationException($"未找到参数 {variableName} 的声明语句。");
            }
        }

        internal static void InsertParameterDeclaration(ModConfigDocument document, string variableName)
        {
            if (FindVariableDeclaration(document, variableName) != null)
            {
                throw new InvalidOperationException($"参数 {variableName} 已存在。");
            }

            var constantsSection = GetOrCreateConstantsSection(document);
            var insertIndex = GetInsertIndexBeforeTrailingBlankLines(constantsSection.Statements);
            constantsSection.Statements.Insert(insertIndex, CreateGlobalDeclaration(variableName, "1"));
        }

        internal static void InsertVisibilityBindingKeySection(
            ModConfigDocument document,
            string variableName,
            IReadOnlyList<string> keyBindings)
        {
            var sectionName = CreateUniqueKeySectionName(document, variableName);
            var keySection = new ModConfigSection
            {
                Name = sectionName,
                HeaderText = $"[{sectionName}]"
            };

            if (TryGetPreferredKeyCondition(document, out var conditionValue))
            {
                keySection.Statements.Add(CreateAssignmentStatement("condition", conditionValue));
            }

            foreach (var binding in keyBindings)
            {
                keySection.Statements.Add(CreateAssignmentStatement("key", binding));
            }

            keySection.Statements.Add(CreateAssignmentStatement("type", "cycle"));
            keySection.Statements.Add(CreateVariableAssignment(variableName, "0,1"));
            keySection.Statements.Add(CreateBlankLineStatement());

            var insertIndex = GetVisibilityBindingKeySectionInsertIndex(document);
            if (insertIndex > 0)
            {
                EnsureTrailingBlankLine(document.Sections[insertIndex - 1]);
            }

            document.Sections.Insert(insertIndex, keySection);
        }

        internal static ModConfigSection GetOrCreateConstantsSection(ModConfigDocument document)
        {
            var section = document.Sections.FirstOrDefault(item =>
                item.Name.Equals("Constants", StringComparison.OrdinalIgnoreCase));
            if (section != null)
            {
                return section;
            }

            var constantsSection = new ModConfigSection
            {
                Name = "Constants",
                HeaderText = "[Constants]"
            };

            document.Sections.Insert(0, constantsSection);
            return constantsSection;
        }

        internal static int GetInsertIndexBeforeTrailingBlankLines(IList<ModConfigStatement> statements)
        {
            var insertIndex = statements.Count;
            while (insertIndex > 0 && statements[insertIndex - 1].Kind == ModConfigStatementKind.BlankLine)
            {
                insertIndex--;
            }

            return insertIndex;
        }

        internal static int GetVisibilityBindingKeySectionInsertIndex(ModConfigDocument document)
        {
            var lastKeySectionIndex = -1;
            for (var index = 0; index < document.Sections.Count; index++)
            {
                if (IsKeySection(document.Sections[index].Name))
                {
                    lastKeySectionIndex = index;
                }
            }

            if (lastKeySectionIndex >= 0)
            {
                return lastKeySectionIndex + 1;
            }

            var constantsSectionIndex = document.Sections.FindIndex(section =>
                section.Name.Equals("Constants", StringComparison.OrdinalIgnoreCase));
            return constantsSectionIndex >= 0 ? constantsSectionIndex + 1 : document.Sections.Count;
        }

        internal static string CreateUniqueKeySectionName(ModConfigDocument document, string variableName)
        {
            var baseName = variableName.TrimStart('$');
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = "binding";
            }

            var candidate = $"Key {baseName}";
            var suffix = 2;
            while (document.Sections.Any(section => section.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            {
                candidate = $"Key {baseName}_{suffix++}";
            }

            return candidate;
        }

        internal static bool TryGetPreferredKeyCondition(ModConfigDocument document, out string conditionValue)
        {
            conditionValue = string.Empty;

            foreach (var section in document.Sections.Where(section => IsKeySection(section.Name)))
            {
                var conditionStatement = section.Statements.FirstOrDefault(statement =>
                    statement.Kind == ModConfigStatementKind.Assignment &&
                    statement.Name.Equals("condition", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(statement.Value));
                if (conditionStatement == null)
                {
                    continue;
                }

                conditionValue = conditionStatement.Value;
                return true;
            }

            return false;
        }

        internal static void EnsureTrailingBlankLine(ModConfigSection section)
        {
            if (section.Statements.Count == 0 ||
                section.Statements[section.Statements.Count - 1].Kind != ModConfigStatementKind.BlankLine)
            {
                section.Statements.Add(CreateBlankLineStatement());
            }
        }

        internal static ModConfigStatement CreateGlobalDeclaration(string variableName, string value)
        {
            return new ModConfigStatement
            {
                Kind = ModConfigStatementKind.GlobalDeclaration,
                RawText = $"global persist {variableName} = {value}",
                Name = $"global persist {variableName}",
                Value = value,
                VariableName = variableName
            };
        }

        internal static ModConfigStatement CreateAssignmentStatement(string name, string value)
        {
            return new ModConfigStatement
            {
                Kind = ModConfigStatementKind.Assignment,
                RawText = $"{name} = {value}",
                Name = name,
                Value = value
            };
        }

        internal static ModConfigStatement CreateVariableAssignment(string variableName, string value)
        {
            return new ModConfigStatement
            {
                Kind = ModConfigStatementKind.VariableAssignment,
                RawText = $"{variableName} = {value}",
                Name = variableName,
                Value = value,
                VariableName = variableName
            };
        }

        internal static ModConfigStatement CreateBlankLineStatement()
        {
            return new ModConfigStatement
            {
                Kind = ModConfigStatementKind.BlankLine,
                RawText = string.Empty
            };
        }

        internal static ModConfigStatement CreateControlLineStatement(string rawText)
        {
            return new ModConfigStatement
            {
                Kind = ModConfigStatementKind.ControlLine,
                RawText = rawText,
                Name = GetLeadingToken(rawText),
                Value = rawText.Trim()
            };
        }

        internal static string GetLeadingToken(string rawText)
        {
            var trimmed = rawText.Trim();
            var spaceIndex = trimmed.IndexOf(' ');
            return spaceIndex < 0 ? trimmed : trimmed.Substring(0, spaceIndex);
        }

        internal static string JoinVisibilityLabels(IEnumerable<string> labels)
        {
            return string.Join(" | ", labels.Where(label => !string.IsNullOrWhiteSpace(label)));
        }

        internal static IEnumerable<string> SplitVisibilityLabels(string drawLabel)
        {
            return (drawLabel ?? string.Empty)
                .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(label => label.Trim())
                .Where(label => !string.IsNullOrWhiteSpace(label));
        }

        internal static string NormalizeVisibilityLabel(string drawLabel)
        {
            return (drawLabel ?? string.Empty)
                .Trim()
                .TrimStart(';')
                .Trim()
                .Replace("Draw", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("Component", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim();
        }

        internal static void WrapDrawTargetWithVisibilityCondition(DrawTargetInfo drawTarget, string variableName)
        {
            WrapDrawTargetWithCondition(drawTarget, $"{variableName} == 1");
        }

        internal static void WrapDrawTargetWithConstantCondition(DrawTargetInfo drawTarget, bool isVisible)
        {
            WrapDrawTargetWithCondition(drawTarget, isVisible ? "1" : "0");
        }

        internal static void WrapDrawTargetWithCondition(DrawTargetInfo drawTarget, string conditionExpression)
        {
            var section = drawTarget.Section;
            var drawStatement = section.Statements[drawTarget.DrawStatementIndex];
            var outerIndent = GetIndentation(drawStatement.RawText);
            var innerIndent = outerIndent + "    ";

            for (var index = drawTarget.BlockStartIndex; index <= drawTarget.DrawStatementIndex; index++)
            {
                var statement = section.Statements[index];
                if (statement.Kind == ModConfigStatementKind.BlankLine)
                {
                    continue;
                }

                statement.RawText = innerIndent + statement.RawText.TrimStart();
            }

            section.Statements.Insert(
                drawTarget.BlockStartIndex,
                CreateControlLineStatement($"{outerIndent}if {conditionExpression}"));
            section.Statements.Insert(
                drawTarget.DrawStatementIndex + 2,
                CreateControlLineStatement($"{outerIndent}endif"));
        }

        internal static void UnwrapDrawTargetFromConstantCondition(DrawTargetInfo drawTarget, bool isVisible)
        {
            var wrapper = FindConstantConditionWrapper(drawTarget, isVisible);
            if (wrapper == null)
            {
                throw new InvalidOperationException("未找到可恢复的常量模型显示控制块。");
            }

            var section = wrapper.Section;
            var startIndex = wrapper.StatementIndex;
            var endIndex = FindMatchingEndIfIndex(section, startIndex);
            UnwrapIndentedBlock(section, startIndex, endIndex);
        }

        internal static void UnwrapVisibilityBinding(DrawTargetInfo drawTarget, string variableName)
        {
            var wrapper = FindVisibilityConditionWrapper(drawTarget, variableName);
            if (wrapper == null)
            {
                throw new InvalidOperationException($"未找到参数 {variableName} 的控制块。");
            }

            var section = wrapper.Section;
            var startIndex = wrapper.StatementIndex;
            var endIndex = FindMatchingEndIfIndex(section, startIndex);
            UnwrapIndentedBlock(section, startIndex, endIndex);
        }

        internal static void UnwrapIndentedBlock(ModConfigSection section, int startIndex, int endIndex)
        {
            var outerIndent = GetIndentation(section.Statements[startIndex].RawText);
            var innerIndent = outerIndent + "    ";

            for (var index = startIndex + 1; index < endIndex; index++)
            {
                var statement = section.Statements[index];
                if (statement.Kind == ModConfigStatementKind.BlankLine)
                {
                    continue;
                }

                if (statement.RawText.StartsWith(innerIndent, StringComparison.Ordinal))
                {
                    statement.RawText = outerIndent + statement.RawText.Substring(innerIndent.Length);
                }
            }

            section.Statements.RemoveAt(endIndex);
            section.Statements.RemoveAt(startIndex);
        }

        internal static ControlLineTarget? FindVisibilityConditionWrapper(DrawTargetInfo drawTarget, string variableName)
        {
            var count = Math.Min(drawTarget.ControlExpressions.Count, drawTarget.ControlLineTargets.Count);
            for (var index = count - 1; index >= 0; index--)
            {
                var controlLineTarget = drawTarget.ControlLineTargets[index];
                if (!ReferenceEquals(controlLineTarget.Section, drawTarget.Section))
                {
                    continue;
                }

                var expression = drawTarget.ControlExpressions[index];
                if (IsVisibilityConditionExpression(expression, variableName))
                {
                    return controlLineTarget;
                }
            }

            return null;
        }

        internal static bool IsVisibilityConditionExpression(string expression, string variableName)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return false;
            }

            var trimmed = expression.Trim();
            return trimmed.Equals($"{variableName} == 1", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.Equals($"{variableName}==1", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.Equals(variableName, StringComparison.OrdinalIgnoreCase) ||
                   (trimmed.StartsWith(variableName, StringComparison.OrdinalIgnoreCase) &&
                    trimmed.EndsWith("== 1", StringComparison.OrdinalIgnoreCase));
        }

        internal static bool CanToggleVisibilityDirectly(ModVisibilityItem visibilityItem)
        {
            return visibilityItem.DrawCallCount > 0 &&
                visibilityItem.ControllingParameters.Count == 0 &&
                visibilityItem.ControllingKeySections.Count == 0 &&
                visibilityItem.ControllingKeyBindings.Count == 0;
        }

        internal static bool IsVisibilityDirectlyHidden(ModVisibilityItem visibilityItem)
        {
            return CanToggleVisibilityDirectly(visibilityItem) &&
                visibilityItem.ControlExpressions.Any(expression => IsConstantConditionExpression(expression, false));
        }

        internal static ControlLineTarget? FindConstantConditionWrapper(DrawTargetInfo drawTarget, bool isVisible)
        {
            var count = Math.Min(drawTarget.ControlExpressions.Count, drawTarget.ControlLineTargets.Count);
            for (var index = count - 1; index >= 0; index--)
            {
                var controlLineTarget = drawTarget.ControlLineTargets[index];
                if (!ReferenceEquals(controlLineTarget.Section, drawTarget.Section))
                {
                    continue;
                }

                if (IsConstantConditionExpression(drawTarget.ControlExpressions[index], isVisible))
                {
                    return controlLineTarget;
                }
            }

            return null;
        }

        internal static int FindMatchingEndIfIndex(ModConfigSection section, int startIndex)
        {
            var depth = 0;
            for (var index = startIndex; index < section.Statements.Count; index++)
            {
                var statement = section.Statements[index];
                if (statement.Kind != ModConfigStatementKind.ControlLine)
                {
                    continue;
                }

                var trimmed = statement.RawText.Trim();
                if (trimmed.StartsWith("if ", StringComparison.OrdinalIgnoreCase))
                {
                    depth++;
                    continue;
                }

                if (trimmed.Equals("endif", StringComparison.OrdinalIgnoreCase))
                {
                    depth--;
                    if (depth == 0)
                    {
                        return index;
                    }
                }
            }

            throw new InvalidOperationException("未找到模型显示控制块对应的 endif。");
        }

        internal static bool IsConstantConditionExpression(string expression, bool isVisible)
        {
            return expression.Trim().Equals(isVisible ? "1" : "0", StringComparison.OrdinalIgnoreCase);
        }

        internal static void ReplaceVisibilityControlVariable(
            DrawTargetInfo drawTarget,
            string oldVariableName,
            string newVariableName)
        {
            var changed = false;

            foreach (var controlLineTarget in drawTarget.ControlLineTargets)
            {
                var statement = controlLineTarget.Section.Statements[controlLineTarget.StatementIndex];
                var replacedText = ReplaceVariableTokens(statement.RawText, oldVariableName, newVariableName);
                if (replacedText.Equals(statement.RawText, StringComparison.Ordinal))
                {
                    continue;
                }

                statement.RawText = replacedText;
                statement.Value = replacedText.Trim();
                changed = true;
            }

            if (!changed)
            {
                throw new InvalidOperationException("未找到模型显示项中可替换的控制参数引用。");
            }
        }

        internal static string ReplaceAssignmentValue(string rawText, string newValue)
        {
            var equalsIndex = rawText.IndexOf('=');
            if (equalsIndex < 0)
            {
                throw new InvalidOperationException("目标语句不包含可改写的默认值。");
            }

            return $"{rawText.Substring(0, equalsIndex + 1)} {newValue}";
        }

        internal static bool TryGetAllowedValues(
            string expression,
            string variableName,
            IReadOnlyList<string> availableValues,
            out HashSet<string> allowedValues)
        {
            allowedValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var normalizedExpression = TrimOuterParentheses(expression.Trim());
            if (normalizedExpression.Length == 0 || normalizedExpression.Equals(ModConfigSectionHelpers.ElseSentinel, StringComparison.Ordinal))
            {
                return false;
            }

            if (normalizedExpression.Equals(variableName, StringComparison.OrdinalIgnoreCase))
            {
                if (!availableValues.Contains("1", StringComparer.OrdinalIgnoreCase))
                {
                    return false;
                }

                allowedValues.Add("1");
                return true;
            }

            if (normalizedExpression.Equals($"!{variableName}", StringComparison.OrdinalIgnoreCase))
            {
                if (!availableValues.Contains("0", StringComparer.OrdinalIgnoreCase))
                {
                    return false;
                }

                allowedValues.Add("0");
                return true;
            }

            if (normalizedExpression.Contains("||", StringComparison.Ordinal))
            {
                foreach (var clause in normalizedExpression.Split(new[] { "||" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!TryGetEqualityClauseValue(clause, variableName, out var clauseValue))
                    {
                        return false;
                    }

                    allowedValues.Add(clauseValue);
                }

                return allowedValues.Count > 0;
            }

            if (normalizedExpression.Contains("&&", StringComparison.Ordinal))
            {
                var disallowedValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var clause in normalizedExpression.Split(new[] { "&&" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!TryGetInequalityClauseValue(clause, variableName, out var clauseValue))
                    {
                        return false;
                    }

                    disallowedValues.Add(clauseValue);
                }

                foreach (var value in availableValues.Where(value => !disallowedValues.Contains(value)))
                {
                    allowedValues.Add(value);
                }

                return allowedValues.Count > 0;
            }

            if (TryGetEqualityClauseValue(normalizedExpression, variableName, out var equalityValue))
            {
                allowedValues.Add(equalityValue);
                return true;
            }

            if (TryGetInequalityClauseValue(normalizedExpression, variableName, out var inequalityValue))
            {
                foreach (var value in availableValues.Where(value => !value.Equals(inequalityValue, StringComparison.OrdinalIgnoreCase)))
                {
                    allowedValues.Add(value);
                }

                return allowedValues.Count > 0;
            }

            return false;
        }

        internal static bool TryGetEqualityClauseValue(string clause, string variableName, out string value)
        {
            value = string.Empty;
            var normalizedClause = TrimOuterParentheses(clause.Trim());
            var match = EqualityClauseRegex.Match(normalizedClause);
            if (!match.Success)
            {
                return false;
            }

            var leftSide = normalizedClause.Substring(0, normalizedClause.IndexOf("==", StringComparison.Ordinal)).Trim();
            if (!leftSide.Equals(variableName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            value = match.Groups["value"].Value.Trim();
            return value.Length > 0;
        }

        internal static bool TryGetInequalityClauseValue(string clause, string variableName, out string value)
        {
            value = string.Empty;
            var normalizedClause = TrimOuterParentheses(clause.Trim());
            var match = InequalityClauseRegex.Match(normalizedClause);
            if (!match.Success)
            {
                return false;
            }

            var leftSide = normalizedClause.Substring(0, normalizedClause.IndexOf("!=", StringComparison.Ordinal)).Trim();
            if (!leftSide.Equals(variableName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            value = match.Groups["value"].Value.Trim();
            return value.Length > 0;
        }

        internal static string TrimOuterParentheses(string text)
        {
            var result = text;
            while (result.Length >= 2 && result.StartsWith("(", StringComparison.Ordinal) && result.EndsWith(")", StringComparison.Ordinal))
            {
                result = result.Substring(1, result.Length - 2).Trim();
            }

            return result;
        }

        internal static DrawTargetInfo? FindDrawTargetInfo(ModConfigDocument document, string componentSectionName, string drawLabel)
        {
            var sectionLookup = document.Sections
                .GroupBy(section => section.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            if (!sectionLookup.TryGetValue(componentSectionName, out var componentSection))
            {
                return null;
            }

            return FindDrawTargetInfo(
                componentSection,
                sectionLookup,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                Array.Empty<string>(),
                Array.Empty<ControlLineTarget>(),
                drawLabel);
        }

        internal static DrawTargetInfo? FindDrawTargetInfo(
            ModConfigSection currentSection,
            IReadOnlyDictionary<string, ModConfigSection> sectionLookup,
            HashSet<string> activePathSectionNames,
            IReadOnlyCollection<string> inheritedExpressions,
            IReadOnlyCollection<ControlLineTarget> inheritedControlLineTargets,
            string drawLabel)
        {
            if (!activePathSectionNames.Add(currentSection.Name))
            {
                return null;
            }

            try
            {
                var controlStack = new Stack<string>();
                var controlLineTargetStack = new Stack<ControlLineTarget>();
                var pendingDrawLabel = string.Empty;
                var pendingDrawLabelIndex = -1;

                for (var statementIndex = 0; statementIndex < currentSection.Statements.Count; statementIndex++)
                {
                    var statement = currentSection.Statements[statementIndex];
                    switch (statement.Kind)
                    {
                        case ModConfigStatementKind.Comment:
                            if (TryGetDrawLabel(statement.RawText, out var candidateLabel))
                            {
                                pendingDrawLabel = candidateLabel;
                                pendingDrawLabelIndex = statementIndex;
                            }
                            break;

                        case ModConfigStatementKind.BlankLine:
                            pendingDrawLabel = string.Empty;
                            pendingDrawLabelIndex = -1;
                            break;

                        case ModConfigStatementKind.ControlLine:
                            UpdateControlStack(controlStack, statement.RawText.Trim());
                            UpdateControlLineTargetStack(controlLineTargetStack, currentSection, statementIndex, statement.RawText.Trim());
                            break;

                        case ModConfigStatementKind.Assignment when statement.Name.Equals("drawindexed", StringComparison.OrdinalIgnoreCase):
                            if (NormalizeVisibilityLabel(pendingDrawLabel).Equals(
                                NormalizeVisibilityLabel(drawLabel),
                                StringComparison.OrdinalIgnoreCase))
                            {
                                return new DrawTargetInfo
                                {
                                    Section = currentSection,
                                    BlockStartIndex = pendingDrawLabelIndex >= 0 ? pendingDrawLabelIndex : statementIndex,
                                    DrawStatementIndex = statementIndex,
                                    ControlLineTargets = inheritedControlLineTargets
                                        .Concat(controlLineTargetStack.Reverse())
                                        .ToList(),
                                    ControlExpressions = inheritedExpressions
                                        .Concat(controlStack.Reverse())
                                        .ToList()
                                };
                            }

                            pendingDrawLabel = string.Empty;
                            pendingDrawLabelIndex = -1;
                            break;

                        case ModConfigStatementKind.Assignment when statement.Name.Equals("run", StringComparison.OrdinalIgnoreCase):
                            if (!sectionLookup.TryGetValue(statement.Value, out var targetSection))
                            {
                                break;
                            }

                            var foundInChild = FindDrawTargetInfo(
                                targetSection,
                                sectionLookup,
                                activePathSectionNames,
                                inheritedExpressions
                                    .Concat(controlStack.Reverse())
                                    .ToList(),
                                inheritedControlLineTargets
                                    .Concat(controlLineTargetStack.Reverse())
                                    .ToList(),
                                drawLabel);
                            if (foundInChild != null)
                            {
                                return foundInChild;
                            }
                            break;
                    }
                }
            }
            finally
            {
                activePathSectionNames.Remove(currentSection.Name);
            }

            return null;
        }

        internal static void UpdateControlStack(Stack<string> controlStack, string rawText)
        {
            ModConfigSectionHelpers.UpdateControlStack(controlStack, rawText);
        }

        internal static void UpdateControlLineTargetStack(
            Stack<ControlLineTarget> controlLineTargetStack,
            ModConfigSection currentSection,
            int statementIndex,
            string rawText)
        {
            if (rawText.StartsWith("if ", StringComparison.OrdinalIgnoreCase))
            {
                controlLineTargetStack.Push(new ControlLineTarget
                {
                    Section = currentSection,
                    StatementIndex = statementIndex
                });
                return;
            }

            if (rawText.StartsWith("elif ", StringComparison.OrdinalIgnoreCase) ||
                rawText.Equals("else", StringComparison.OrdinalIgnoreCase))
            {
                if (controlLineTargetStack.Count > 0)
                {
                    controlLineTargetStack.Pop();
                }

                controlLineTargetStack.Push(new ControlLineTarget
                {
                    Section = currentSection,
                    StatementIndex = statementIndex
                });
                return;
            }

            if (rawText.Equals("endif", StringComparison.OrdinalIgnoreCase) && controlLineTargetStack.Count > 0)
            {
                controlLineTargetStack.Pop();
            }
        }

        internal static bool TryGetDrawLabel(string rawText, out string drawLabel)
        {
            return ModConfigSectionHelpers.TryGetDrawLabel(rawText, out drawLabel);
        }

        internal static string RenderDocument(ModConfigDocument document, string lineEnding)
        {
            var lines = new List<string>();
            lines.AddRange(document.RootStatements.Select(statement => statement.RawText));

            foreach (var section in document.Sections)
            {
                lines.Add(section.HeaderText);
                lines.AddRange(section.Statements.Select(statement => statement.RawText));
            }

            return string.Join(lineEnding, lines);
        }

        internal static ModConfigEditBuffer CreateUpdatedBuffer(ModConfigEditBuffer sourceBuffer, ModConfigDocument document, string changeDescription)
        {
            var renderedContent = RenderDocument(document, sourceBuffer.LineEnding);
            var nextAppliedChanges = renderedContent.Equals(sourceBuffer.Content, StringComparison.Ordinal)
                ? sourceBuffer.AppliedChanges.ToList()
                : sourceBuffer.AppliedChanges.Concat(new[] { changeDescription }).ToList();

            return new ModConfigEditBuffer
            {
                SourcePath = sourceBuffer.SourcePath,
                LineEnding = sourceBuffer.LineEnding,
                Content = renderedContent,
                AppliedChanges = nextAppliedChanges
            };
        }

        internal static string ResolveTargetPath(
            string configPath,
            ModConfigSaveTarget saveTarget,
            string modRootPath,
            string wwmiRootPath)
        {
            if (TryResolveTargetPathByRootReplacement(configPath, saveTarget, modRootPath, wwmiRootPath, out var targetPath))
            {
                return targetPath;
            }

            var locationInfo = ParseConfigLocation(configPath);
            return BuildTargetPath(locationInfo, saveTarget, modRootPath, wwmiRootPath);
        }

        internal static bool TryResolveTargetPathByRootReplacement(
            string configPath,
            ModConfigSaveTarget saveTarget,
            string modRootPath,
            string wwmiRootPath,
            out string targetPath)
        {
            targetPath = string.Empty;

            if (TryGetRelativePathUnderRoot(configPath, modRootPath, out var relativeFromMod))
            {
                if (saveTarget == ModConfigSaveTarget.ModDirectory)
                {
                    targetPath = Path.GetFullPath(configPath);
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(wwmiRootPath))
                {
                    if (TryParseModRelativePath(relativeFromMod, out var locationInfo))
                    {
                        targetPath = BuildTargetPath(locationInfo, ModConfigSaveTarget.WwmiDirectory, modRootPath, wwmiRootPath);
                        return true;
                    }

                    targetPath = Path.GetFullPath(Path.Combine(wwmiRootPath, relativeFromMod));
                    return true;
                }
            }

            if (TryGetRelativePathUnderRoot(configPath, wwmiRootPath, out var relativeFromWwmi))
            {
                if (saveTarget == ModConfigSaveTarget.WwmiDirectory)
                {
                    targetPath = Path.GetFullPath(configPath);
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(modRootPath))
                {
                    if (TryParseWwmiRelativePath(relativeFromWwmi, out var locationInfo))
                    {
                        targetPath = BuildTargetPath(locationInfo, ModConfigSaveTarget.ModDirectory, modRootPath, wwmiRootPath);
                        return true;
                    }

                    targetPath = Path.GetFullPath(Path.Combine(modRootPath, relativeFromWwmi));
                    return true;
                }
            }

            return false;
        }

        internal static bool TryGetRelativePathUnderRoot(string filePath, string rootPath, out string relativePath)
        {
            relativePath = string.Empty;
            if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(rootPath))
            {
                return false;
            }

            var normalizedRoot = EnsureTrailingSeparator(Path.GetFullPath(rootPath));
            var normalizedPath = Path.GetFullPath(filePath);
            if (!normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            relativePath = normalizedPath.Substring(normalizedRoot.Length);
            return !string.IsNullOrWhiteSpace(relativePath);
        }

        internal static string EnsureTrailingSeparator(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        internal static ConfigLocationInfo ParseConfigLocation(string configPath)
        {
            var configDirectory = Path.GetDirectoryName(configPath);
            if (string.IsNullOrWhiteSpace(configDirectory))
            {
                throw new InvalidOperationException("无法解析配置文件所在目录。");
            }

            var fileName = Path.GetFileName(configPath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new InvalidOperationException("无法解析配置文件名。");
            }

            if (TryParseConfigLocationFromHierarchy(configDirectory, fileName, out var locationInfo))
            {
                return locationInfo;
            }

            throw new InvalidOperationException("无法从当前配置路径解析 Mod/WWMI 目标位置。");
        }

        internal static string BuildTargetPath(
            ConfigLocationInfo locationInfo,
            ModConfigSaveTarget saveTarget,
            string modRootPath,
            string wwmiRootPath)
        {
            var modFolderName = $"[{locationInfo.Id}]{locationInfo.ModName}";
            var wwmiFolderName = $"[{locationInfo.CharacterName}][{locationInfo.Id}]{locationInfo.ModName}";
            var baseDirectory = saveTarget == ModConfigSaveTarget.ModDirectory
                ? Path.Combine(modRootPath, locationInfo.CharacterName, modFolderName)
                : Path.Combine(wwmiRootPath, wwmiFolderName);

            var targetPath = string.IsNullOrWhiteSpace(locationInfo.RelativeSubdirectory)
                ? Path.Combine(baseDirectory, locationInfo.FileName)
                : Path.Combine(baseDirectory, locationInfo.RelativeSubdirectory, locationInfo.FileName);

            return Path.GetFullPath(targetPath);
        }

        internal static bool TryParseModRelativePath(string relativePath, out ConfigLocationInfo locationInfo)
        {
            locationInfo = new ConfigLocationInfo();

            var segments = SplitPathSegments(relativePath);
            if (segments.Length < 3)
            {
                return false;
            }

            var characterName = segments[0];
            var (id, modName) = ModPathHelper.ParseModFolderName(segments[1]);
            return TryCreateLocationInfo(characterName, id, modName, segments.Skip(2).ToArray(), out locationInfo);
        }

        internal static bool TryParseWwmiRelativePath(string relativePath, out ConfigLocationInfo locationInfo)
        {
            locationInfo = new ConfigLocationInfo();

            var segments = SplitPathSegments(relativePath);
            if (segments.Length < 2)
            {
                return false;
            }

            var (characterName, id, modName) = ModPathHelper.ParseWwmiFolderName(segments[0]);
            return TryCreateLocationInfo(characterName, id, modName, segments.Skip(1).ToArray(), out locationInfo);
        }

        internal static bool TryParseConfigLocationFromHierarchy(
            string configDirectory,
            string fileName,
            out ConfigLocationInfo locationInfo)
        {
            locationInfo = new ConfigLocationInfo();

            var originalDirectory = Path.GetFullPath(configDirectory);
            var currentDirectory = originalDirectory;

            while (!string.IsNullOrWhiteSpace(currentDirectory))
            {
                var folderName = Path.GetFileName(currentDirectory);
                var relativeSubdirectory = GetRelativeSubdirectory(currentDirectory, originalDirectory);
                var (wwmiCharacter, wwmiId, wwmiModName) = ModPathHelper.ParseWwmiFolderName(folderName);
                if (TryCreateLocationInfo(wwmiCharacter, wwmiId, wwmiModName, relativeSubdirectory, fileName, out locationInfo))
                {
                    return true;
                }

                var (modId, modName) = ModPathHelper.ParseModFolderName(folderName);
                var parentDirectory = Path.GetDirectoryName(currentDirectory);
                var characterName = ModPathHelper.GetCharacterNameFromFolder(parentDirectory ?? string.Empty);
                if (TryCreateLocationInfo(characterName, modId, modName, relativeSubdirectory, fileName, out locationInfo))
                {
                    return true;
                }

                if (string.IsNullOrWhiteSpace(parentDirectory) ||
                    parentDirectory.Equals(currentDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                currentDirectory = parentDirectory;
            }

            return false;
        }

        internal static bool TryCreateLocationInfo(
            string characterName,
            string id,
            string modName,
            IReadOnlyList<string> pathSegments,
            out ConfigLocationInfo locationInfo)
        {
            locationInfo = new ConfigLocationInfo();
            if (pathSegments.Count == 0)
            {
                return false;
            }

            var fileName = pathSegments[pathSegments.Count - 1];
            var relativeSubdirectory = pathSegments.Count > 1
                ? CombinePathSegments(pathSegments.Take(pathSegments.Count - 1))
                : string.Empty;

            return TryCreateLocationInfo(characterName, id, modName, relativeSubdirectory, fileName, out locationInfo);
        }

        internal static bool TryCreateLocationInfo(
            string characterName,
            string id,
            string modName,
            string relativeSubdirectory,
            string fileName,
            out ConfigLocationInfo locationInfo)
        {
            locationInfo = new ConfigLocationInfo();
            if (string.IsNullOrWhiteSpace(characterName) ||
                string.IsNullOrWhiteSpace(id) ||
                string.IsNullOrWhiteSpace(modName) ||
                string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            locationInfo = new ConfigLocationInfo
            {
                CharacterName = characterName,
                Id = id,
                ModName = modName,
                RelativeSubdirectory = relativeSubdirectory,
                FileName = fileName
            };
            return true;
        }

        internal static string GetRelativeSubdirectory(string baseDirectory, string targetDirectory)
        {
            var relativePath = Path.GetRelativePath(baseDirectory, targetDirectory);
            return relativePath.Equals(".", StringComparison.Ordinal)
                ? string.Empty
                : relativePath;
        }

        internal static string[] SplitPathSegments(string path)
        {
            return path.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);
        }

        internal static string CombinePathSegments(IEnumerable<string> segments)
        {
            var normalizedSegments = segments
                .Where(segment => !string.IsNullOrWhiteSpace(segment))
                .ToArray();

            return normalizedSegments.Length == 0
                ? string.Empty
                : Path.Combine(normalizedSegments);
        }

        internal sealed class ConfigLocationInfo
        {
            public string CharacterName { get; set; } = string.Empty;
            public string Id { get; set; } = string.Empty;
            public string ModName { get; set; } = string.Empty;
            public string RelativeSubdirectory { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;
        }

        internal sealed class DrawTargetInfo
        {
            public ModConfigSection Section { get; set; } = null!;
            public int BlockStartIndex { get; set; }
            public int DrawStatementIndex { get; set; }
            public List<ControlLineTarget> ControlLineTargets { get; set; } = new List<ControlLineTarget>();
            public List<string> ControlExpressions { get; set; } = new List<string>();
        }

        internal sealed class ControlLineTarget
        {
            public ModConfigSection Section { get; set; } = null!;
            public int StatementIndex { get; set; }
        }

        internal sealed class StandardToggleSlot
        {
            public string SectionName { get; set; } = string.Empty;
            public string VariableName { get; set; } = string.Empty;
            public List<string> KeyBindings { get; set; } = new List<string>();
        }
    }
}