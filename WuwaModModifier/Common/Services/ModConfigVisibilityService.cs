using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using WuwaModModifier.Model;

namespace WuwaModModifier.Common
{
    /// <summary>
    /// Visibility and draw-target operations: toggling visibility, binding to
    /// parameters, creating / removing visibility bindings.
    /// </summary>
    public class ModConfigVisibilityService : IModConfigVisibilityService
    {
        internal readonly IFileSystemService _fileSystem;
        internal readonly IModConfigParser _parser;
        internal readonly IModConfigAnalysisService _analysisService;

        public ModConfigVisibilityService(
            IFileSystemService fileSystem,
            IModConfigParser parser,
            IModConfigAnalysisService analysisService)
        {
            _fileSystem = fileSystem;
            _parser = parser;
            _analysisService = analysisService;
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

            var document = ParseBuffer(buffer);
            var analysis = _analysisService.Analyze(document);
            var visibilityItem = ModConfigUpdateService.FindVisibilityItem(analysis, componentSectionName, drawLabel);
            var drawTarget = ModConfigUpdateService.FindDrawTargetInfo(document, componentSectionName, drawLabel);
            if (drawTarget == null)
            {
                throw new InvalidOperationException("未找到模型显示项的控制表达式。");
            }

            if (!isVisible && ModConfigUpdateService.CanToggleVisibilityDirectly(visibilityItem))
            {
                ModConfigUpdateService.WrapDrawTargetWithConstantCondition(drawTarget, false);
                return ModConfigUpdateService.CreateUpdatedBuffer(
                    buffer,
                    document,
                    $"切换 {componentSectionName} / {drawLabel} 为隐藏");
            }

            if (isVisible && ModConfigUpdateService.IsVisibilityDirectlyHidden(visibilityItem))
            {
                ModConfigUpdateService.UnwrapDrawTargetFromConstantCondition(drawTarget, false);
                return ModConfigUpdateService.CreateUpdatedBuffer(
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

            var availableValues = ModConfigUpdateService.GetAvailableValues(parameter);
            if (availableValues.Count < 2)
            {
                throw new InvalidOperationException($"参数 {variableName} 的值域不足以安全切换显示状态。");
            }

            var controlExpressions = drawTarget.ControlExpressions
                .Where(expression => ModConfigUpdateService.VariableTokenRegex.Matches(expression)
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
                if (!ModConfigUpdateService.TryGetAllowedValues(controlExpression, variableName, availableValues, out var allowedValues))
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

            var declaration = ModConfigUpdateService.FindVariableDeclaration(document, variableName);
            if (declaration == null)
            {
                throw new InvalidOperationException($"未找到参数 {variableName} 的声明语句。");
            }

            declaration.RawText = ModConfigUpdateService.ReplaceAssignmentValue(declaration.RawText, targetValue);
            return ModConfigUpdateService.CreateUpdatedBuffer(
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

            ModConfigUpdateService.ValidateVariableName(variableName, nameof(variableName));

            var document = ParseBuffer(buffer);
            var analysis = _analysisService.Analyze(document);
            var visibilityItem = ModConfigUpdateService.FindVisibilityItem(analysis, componentSectionName, drawLabel);

            ModConfigUpdateService.EnsureVisibilityCanBind(visibilityItem);
            ModConfigUpdateService.EnsureBindableVisibilityParameter(analysis, variableName);

            var drawTarget = ModConfigUpdateService.FindDrawTargetInfo(document, componentSectionName, drawLabel);
            if (drawTarget == null)
            {
                throw new InvalidOperationException("未找到模型显示项的可插入位置。");
            }

            ModConfigUpdateService.WrapDrawTargetWithVisibilityCondition(drawTarget, variableName);
            return ModConfigUpdateService.CreateUpdatedBuffer(buffer, document, $"将 {componentSectionName} / {drawLabel} 绑定到参数 {variableName}");
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

            ModConfigUpdateService.ValidateVariableName(variableName, nameof(variableName));
            var normalizedBindings = ModConfigUpdateService.NormalizeKeyBindings(keyBindings);

            var document = ParseBuffer(buffer);
            var analysis = _analysisService.Analyze(document);
            var visibilityItem = ModConfigUpdateService.FindVisibilityItem(analysis, componentSectionName, drawLabel);

            ModConfigUpdateService.EnsureVisibilityCanCreateNewBinding(visibilityItem);

            if (analysis.Parameters.Any(item => item.Name.Equals(variableName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"参数 {variableName} 已存在，不能重复创建。");
            }

            ModConfigUpdateService.InsertParameterDeclaration(document, variableName);
            ModConfigUpdateService.InsertVisibilityBindingKeySection(document, variableName, normalizedBindings);

            var drawTarget = ModConfigUpdateService.FindDrawTargetInfo(document, componentSectionName, drawLabel);
            if (drawTarget == null)
            {
                throw new InvalidOperationException("未找到模型显示项的可插入位置。");
            }

            if (visibilityItem.ControllingParameters.Count == 1)
            {
                ModConfigUpdateService.ReplaceVisibilityControlVariable(
                    drawTarget,
                    visibilityItem.ControllingParameters[0],
                    variableName);
            }
            else
            {
                ModConfigUpdateService.WrapDrawTargetWithVisibilityCondition(drawTarget, variableName);
            }

            return ModConfigUpdateService.CreateUpdatedBuffer(buffer, document, $"为 {componentSectionName} / {drawLabel} 新建参数 {variableName} 与快捷键绑定");
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

            ModConfigUpdateService.ValidateVariableName(variableName, nameof(variableName));

            var document = ParseBuffer(buffer);
            var analysis = _analysisService.Analyze(document);
            var visibilityItem = ModConfigUpdateService.FindVisibilityItem(analysis, componentSectionName, drawLabel);

            if (!visibilityItem.ControllingParameters.Contains(variableName, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"参数 {variableName} 未绑定到 {componentSectionName} / {drawLabel}。");
            }

            var drawTarget = ModConfigUpdateService.FindDrawTargetInfo(document, componentSectionName, drawLabel);
            if (drawTarget == null)
            {
                throw new InvalidOperationException("未找到模型显示项的控制表达式。");
            }

            ModConfigUpdateService.UnwrapVisibilityBinding(drawTarget, variableName);

            return ModConfigUpdateService.CreateUpdatedBuffer(buffer, document, $"移除 {componentSectionName} / {drawLabel} 的参数 {variableName} 绑定");
        }

        internal ModConfigDocument ParseBuffer(ModConfigEditBuffer buffer)
        {
            return ModConfigUpdateService.ParseBuffer(_parser, buffer);
        }
    }
}
