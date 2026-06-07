using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using WuwaModModifier.Model;

namespace WuwaModModifier.Common
{
    /// <summary>
    /// 在宽容型 AST 之上提取第一阶段语义信息：toggle、参数和模型显示项。
    /// </summary>
    public class ModConfigAnalysisService : IModConfigAnalysisService
    {
        private const string RootSectionName = "<root>";

        private static readonly Regex VariableReferenceRegex =
            new Regex(@"(?<var>\$[^\s=,]+)", RegexOptions.Compiled);

        private static readonly Regex VariableComparisonRegex =
            new Regex(@"(?<var>\$[^\s=,()!<>]+)\s*(==|=)\s*(?<value>[-+]?\d+(?:\.\d+)?)", RegexOptions.Compiled);

        private readonly IModConfigParser _parser;

        public ModConfigAnalysisService(IModConfigParser parser)
        {
            _parser = parser;
        }

        public ModConfigAnalysisResult Analyze(ModConfigDocument document)
        {
            var result = new ModConfigAnalysisResult();
            var parameters = new Dictionary<string, ParameterAccumulator>(StringComparer.OrdinalIgnoreCase);

            foreach (var statement in document.RootStatements)
            {
                TrackStatementReferences(parameters, RootSectionName, statement);
            }

            foreach (var section in document.Sections)
            {
                var isConstantsSection = section.Name.Equals("Constants", StringComparison.OrdinalIgnoreCase);
                var isKeySection = IsKeySection(section.Name);

                foreach (var statement in section.Statements)
                {
                    TrackStatementReferences(parameters, section.Name, statement);

                    if (isConstantsSection && statement.Kind == ModConfigStatementKind.GlobalDeclaration)
                    {
                        var parameter = GetOrAddParameter(parameters, statement.VariableName);
                        parameter.IsDeclaredInConstants = true;
                        parameter.IsPersisted |= statement.Name.IndexOf("persist", StringComparison.OrdinalIgnoreCase) >= 0;
                        parameter.DefaultValue = statement.Value;
                    }
                }

                if (!isKeySection)
                {
                    continue;
                }

                var toggle = CreateToggle(section);
                if (toggle == null)
                {
                    continue;
                }

                result.Toggles.Add(toggle);
                foreach (var target in toggle.Targets)
                {
                    var parameter = GetOrAddParameter(parameters, target.VariableName);
                    parameter.BoundKeySections.Add(section.Name);
                    parameter.KeyBindings.AddRange(toggle.KeyBindings);
                    parameter.ValueOptions.AddRange(target.Values);
                    parameter.ToggleTypes.Add(toggle.ToggleType);
                }
            }

            var finalizedParameters = parameters.Values
                .Select(FinalizeParameter)
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var variableDependencyGraph = BuildVariableDependencyGraph(document);
            var variableExpressionsByVariable = BuildVariableExpressions(document);

            var parameterLookup = finalizedParameters.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var toggle in result.Toggles)
            {
                toggle.IsStandardizationCandidate = toggle.ToggleType.Equals("cycle", StringComparison.OrdinalIgnoreCase) &&
                    toggle.Targets.Count == 1 &&
                    parameterLookup.TryGetValue(toggle.Targets[0].VariableName, out var parameter) &&
                    parameter.CanRename &&
                    parameter.Kind != ModConfigParameterKind.InternalSystem;
            }

            result.Parameters = finalizedParameters;
            result.VisibilityItems = BuildVisibilityItems(document, parameterLookup, variableDependencyGraph, variableExpressionsByVariable);
            ApplyParameterLinksAndKinds(finalizedParameters, result.VisibilityItems, variableDependencyGraph);
            return result;
        }

        public ModConfigAnalysisResult AnalyzeFile(string filePath)
        {
            return Analyze(_parser.ParseFile(filePath));
        }

        private static void TrackStatementReferences(
            Dictionary<string, ParameterAccumulator> parameters,
            string sectionName,
            ModConfigStatement statement)
        {
            if (statement.Kind == ModConfigStatementKind.BlankLine || statement.Kind == ModConfigStatementKind.Comment)
            {
                return;
            }

            foreach (Match match in VariableReferenceRegex.Matches(statement.RawText))
            {
                var variableName = match.Groups["var"].Value;
                var parameter = GetOrAddParameter(parameters, variableName);
                parameter.ReferencedInSections.Add(sectionName);
            }
        }

        private static ModToggleDefinition? CreateToggle(ModConfigSection section)
        {
            var keyBindings = section.Statements
                .Where(s => s.Kind == ModConfigStatementKind.Assignment && s.Name.Equals("key", StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            var targets = section.Statements
                .Where(s => s.Kind == ModConfigStatementKind.VariableAssignment)
                .Select(statement => new ModToggleTarget
                {
                    VariableName = statement.VariableName,
                    Values = SplitValues(statement.Value)
                })
                .Where(target => !string.IsNullOrWhiteSpace(target.VariableName))
                .ToList();

            if (keyBindings.Count == 0 && targets.Count == 0)
            {
                return null;
            }

            var condition = section.Statements.FirstOrDefault(s =>
                s.Kind == ModConfigStatementKind.Assignment &&
                s.Name.Equals("condition", StringComparison.OrdinalIgnoreCase));
            var type = section.Statements.FirstOrDefault(s =>
                s.Kind == ModConfigStatementKind.Assignment &&
                s.Name.Equals("type", StringComparison.OrdinalIgnoreCase));

            return new ModToggleDefinition
            {
                SectionName = section.Name,
                DisplayName = GetDisplayName(section.Name),
                ConditionText = condition?.Value ?? string.Empty,
                ToggleType = type?.Value ?? string.Empty,
                KeyBindings = keyBindings,
                Targets = targets
            };
        }

        private static string GetDisplayName(string sectionName)
        {
            if (sectionName.StartsWith("Key", StringComparison.OrdinalIgnoreCase) && sectionName.Length > 3)
            {
                return sectionName.Substring(3).TrimStart(' ', '_', '.');
            }

            return sectionName;
        }

        private static bool IsKeySection(string sectionName)
        {
            return sectionName.StartsWith("Key", StringComparison.OrdinalIgnoreCase);
        }

        private static List<string> SplitValues(string rawValue)
        {
            return rawValue
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => value.Length > 0)
                .ToList();
        }

        private static ParameterAccumulator GetOrAddParameter(
            Dictionary<string, ParameterAccumulator> parameters,
            string variableName)
        {
            if (!parameters.TryGetValue(variableName, out var parameter))
            {
                parameter = new ParameterAccumulator(variableName);
                parameters[variableName] = parameter;
            }

            return parameter;
        }

        private static ModParameterDefinition FinalizeParameter(ParameterAccumulator parameter)
        {
            var valueOptions = parameter.ValueOptions
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var boundKeySections = parameter.BoundKeySections
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var keyBindings = parameter.KeyBindings
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var referencedSections = parameter.ReferencedInSections
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var toggleTypes = parameter.ToggleTypes
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var kind = DeterminePreliminaryParameterKind(parameter.Name);

            return new ModParameterDefinition
            {
                Name = parameter.Name,
                DefaultValue = parameter.DefaultValue,
                IsDeclaredInConstants = parameter.IsDeclaredInConstants,
                IsPersisted = parameter.IsPersisted,
                Kind = kind,
                ValueOptions = valueOptions,
                BoundKeySections = boundKeySections,
                KeyBindings = keyBindings,
                ToggleTypes = toggleTypes,
                ReferencedInSections = referencedSections,
                LinkedParameterNames = new List<string>(),
                CanRename = boundKeySections.Count > 0 && kind != ModConfigParameterKind.InternalSystem && !parameter.Name.Contains('\\')
            };
        }

        private static ModConfigParameterKind DeterminePreliminaryParameterKind(string variableName)
        {
            if (IsInternalSystemVariable(variableName))
            {
                return ModConfigParameterKind.InternalSystem;
            }

            return ModConfigParameterKind.Unknown;
        }

        private static bool IsInternalSystemVariable(string variableName)
        {
            return variableName.StartsWith("$required_", StringComparison.OrdinalIgnoreCase) ||
                variableName.StartsWith("$object_", StringComparison.OrdinalIgnoreCase) ||
                variableName.StartsWith("$mesh_", StringComparison.OrdinalIgnoreCase) ||
                variableName.StartsWith("$shapekey_", StringComparison.OrdinalIgnoreCase) ||
                variableName.StartsWith("$merge_status", StringComparison.OrdinalIgnoreCase) ||
                variableName.IndexOf("\\WWMIv1\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                variableName.Equals("$mod_id", StringComparison.OrdinalIgnoreCase) ||
                variableName.Equals("$state_id", StringComparison.OrdinalIgnoreCase) ||
                variableName.Equals("$mod_enabled", StringComparison.OrdinalIgnoreCase);
        }

        private static List<ModVisibilityItem> BuildVisibilityItems(
            ModConfigDocument document,
            IReadOnlyDictionary<string, ModParameterDefinition> parameterLookup,
            IReadOnlyDictionary<string, IReadOnlyList<string>> variableDependencyGraph,
            IReadOnlyDictionary<string, IReadOnlyList<string>> variableExpressionsByVariable)
        {
            var sectionLookup = document.Sections
                .GroupBy(section => section.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            return document.Sections
                .Where(section => section.Name.StartsWith("TextureOverride", StringComparison.OrdinalIgnoreCase))
                .SelectMany(section => BuildVisibilityItems(section, sectionLookup, parameterLookup, variableDependencyGraph, variableExpressionsByVariable))
                .OrderBy(item => item.SectionName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static IEnumerable<ModVisibilityItem> BuildVisibilityItems(
            ModConfigSection componentSection,
            IReadOnlyDictionary<string, ModConfigSection> sectionLookup,
            IReadOnlyDictionary<string, ModParameterDefinition> parameterLookup,
            IReadOnlyDictionary<string, IReadOnlyList<string>> variableDependencyGraph,
            IReadOnlyDictionary<string, IReadOnlyList<string>> variableExpressionsByVariable)
        {
            var drawEntries = new List<VisibilityDrawEntry>();
            TraverseVisibilityDrawEntries(
                componentSection,
                sectionLookup,
                new List<ModConfigSection>(),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                Array.Empty<string>(),
                Array.Empty<string>(),
                drawEntries);

            if (drawEntries.Count == 0)
            {
                yield return BuildFallbackVisibilityItem(componentSection, sectionLookup, parameterLookup, variableDependencyGraph);
                yield break;
            }

            foreach (var drawEntry in drawEntries)
            {
                yield return BuildVisibilityItem(componentSection, drawEntry, parameterLookup, variableDependencyGraph, variableExpressionsByVariable);
            }
        }

        private static ModVisibilityItem BuildVisibilityItem(
            ModConfigSection componentSection,
            VisibilityDrawEntry drawEntry,
            IReadOnlyDictionary<string, ModParameterDefinition> parameterLookup,
            IReadOnlyDictionary<string, IReadOnlyList<string>> variableDependencyGraph,
            IReadOnlyDictionary<string, IReadOnlyList<string>> variableExpressionsByVariable)
        {
            var controllingParameters = ResolveControllingParameters(
                drawEntry.ControllingVariables,
                parameterLookup,
                variableDependencyGraph);
            var modelParameters = ResolveModelParameters(drawEntry.ControllingVariables, parameterLookup);
            var keyParameterBindings = ResolveVisibilityKeyParameterBindings(
                drawEntry,
                parameterLookup,
                variableDependencyGraph,
                variableExpressionsByVariable);
            var controllingKeySections = controllingParameters
                .SelectMany(variableName => parameterLookup[variableName].BoundKeySections)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var controllingKeyBindings = controllingParameters
                .SelectMany(variableName => parameterLookup[variableName].KeyBindings)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var label = string.IsNullOrWhiteSpace(drawEntry.DrawLabel)
                    ? drawEntry.DrawCommandText
                    : drawEntry.DrawLabel.Replace("Draw", "").Replace("Component", "").Trim();

            return new ModVisibilityItem
            {
                SectionName = componentSection.Name,
                NavigateLine = drawEntry.LineNumber,
                Hash = GetAssignmentValue(componentSection, "hash"),
                MatchFirstIndex = GetAssignmentValue(componentSection, "match_first_index"),
                MatchIndexCount = GetAssignmentValue(componentSection, "match_index_count"),
                HandlingMode = GetAssignmentValue(componentSection, "handling"),
                DrawCallCount = 1,
                DrawLabels = new List<string> { label },
                RelatedSectionNames = drawEntry.RelatedSectionNames,
                ModelParameters = modelParameters,
                KeyParameterBindings = keyParameterBindings,
                ControllingParameters = controllingParameters,
                ControllingKeySections = controllingKeySections,
                ControllingKeyBindings = controllingKeyBindings,
                ControlExpressions = drawEntry.ControlExpressions,
                Confidence = ClassifyVisibilityConfidence(1, controllingParameters.Count)
            };
        }

        private static ModVisibilityItem BuildFallbackVisibilityItem(
            ModConfigSection componentSection,
            IReadOnlyDictionary<string, ModConfigSection> sectionLookup,
            IReadOnlyDictionary<string, ModParameterDefinition> parameterLookup,
            IReadOnlyDictionary<string, IReadOnlyList<string>> variableDependencyGraph)
        {
            var relatedSections = CollectRelatedSections(componentSection, sectionLookup);
            var referencedVariables = relatedSections.SelectMany(GetReferencedVariables)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var controllingParameters = ResolveControllingParameters(
                referencedVariables,
                parameterLookup,
                variableDependencyGraph);
            var controllingKeySections = controllingParameters
                .SelectMany(variableName => parameterLookup[variableName].BoundKeySections)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var controllingKeyBindings = controllingParameters
                .SelectMany(variableName => parameterLookup[variableName].KeyBindings)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var modelParameters = ResolveModelParameters(referencedVariables, parameterLookup);

            return new ModVisibilityItem
            {
                SectionName = componentSection.Name,
                NavigateLine = componentSection.HeaderLineNumber,
                Hash = GetAssignmentValue(componentSection, "hash"),
                MatchFirstIndex = GetAssignmentValue(componentSection, "match_first_index"),
                MatchIndexCount = GetAssignmentValue(componentSection, "match_index_count"),
                HandlingMode = GetAssignmentValue(componentSection, "handling"),
                DrawCallCount = 0,
                DrawLabels = relatedSections
                    .SelectMany(GetDrawLabels)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                RelatedSectionNames = relatedSections
                    .Select(section => section.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                ModelParameters = modelParameters,
                ControllingParameters = controllingParameters,
                ControllingKeySections = controllingKeySections,
                ControllingKeyBindings = controllingKeyBindings,
                ControlExpressions = new List<string>(),
                Confidence = ClassifyVisibilityConfidence(0, controllingParameters.Count)
            };
        }

        private static void TraverseVisibilityDrawEntries(
            ModConfigSection currentSection,
            IReadOnlyDictionary<string, ModConfigSection> sectionLookup,
            List<ModConfigSection> pathSections,
            HashSet<string> activePathSectionNames,
            IReadOnlyCollection<string> inheritedConditionVariables,
            IReadOnlyCollection<string> inheritedConditionExpressions,
            List<VisibilityDrawEntry> drawEntries)
        {
            pathSections.Add(currentSection);
            activePathSectionNames.Add(currentSection.Name);

            try
            {
                var controlStack = new Stack<string>();
                var pendingDrawLabel = string.Empty;

                foreach (var statement in currentSection.Statements)
                {
                    switch (statement.Kind)
                    {
                        case ModConfigStatementKind.Comment:
                            if (TryGetDrawLabel(statement.RawText, out var drawLabel))
                            {
                                pendingDrawLabel = drawLabel;
                            }
                            break;

                        case ModConfigStatementKind.BlankLine:
                            pendingDrawLabel = string.Empty;
                            break;

                        case ModConfigStatementKind.ControlLine:
                            var trimmedControlLine = statement.RawText.TrimStart();
                            UpdateControlStack(controlStack, trimmedControlLine.Trim());
                            if (trimmedControlLine.StartsWith("elif", StringComparison.OrdinalIgnoreCase) ||
                                trimmedControlLine.StartsWith("else", StringComparison.OrdinalIgnoreCase) ||
                                trimmedControlLine.StartsWith("endif", StringComparison.OrdinalIgnoreCase))
                            {
                                pendingDrawLabel = string.Empty;
                            }
                            break;

                        case ModConfigStatementKind.Assignment when statement.Name.Equals("drawindexed", StringComparison.OrdinalIgnoreCase):
                            drawEntries.Add(new VisibilityDrawEntry
                            {
                                LineNumber = statement.LineNumber,
                                DrawLabel = pendingDrawLabel,
                                DrawCommandText = statement.RawText.Trim(),
                                RelatedSectionNames = pathSections
                                    .Select(section => section.Name)
                                    .Distinct(StringComparer.OrdinalIgnoreCase)
                                    .ToList(),
                                ControllingVariables = inheritedConditionVariables
                                    .Concat(GetActiveControlVariables(controlStack))
                                    .Distinct(StringComparer.OrdinalIgnoreCase)
                                    .ToList(),
                                ControlExpressions = inheritedConditionExpressions
                                    .Concat(GetActiveControlExpressions(controlStack))
                                    .Distinct(StringComparer.OrdinalIgnoreCase)
                                    .ToList()
                            });
                            pendingDrawLabel = string.Empty;
                            break;

                        case ModConfigStatementKind.Assignment when statement.Name.Equals("run", StringComparison.OrdinalIgnoreCase):
                            if (!sectionLookup.TryGetValue(statement.Value, out var targetSection) ||
                                activePathSectionNames.Contains(targetSection.Name))
                            {
                                break;
                            }

                            TraverseVisibilityDrawEntries(
                                targetSection,
                                sectionLookup,
                                pathSections,
                                activePathSectionNames,
                                inheritedConditionVariables
                                    .Concat(GetActiveControlVariables(controlStack))
                                    .Distinct(StringComparer.OrdinalIgnoreCase)
                                    .ToList(),
                                inheritedConditionExpressions
                                    .Concat(GetActiveControlExpressions(controlStack))
                                    .Distinct(StringComparer.OrdinalIgnoreCase)
                                    .ToList(),
                                drawEntries);
                            break;
                    }
                }
            }
            finally
            {
                activePathSectionNames.Remove(currentSection.Name);
                pathSections.RemoveAt(pathSections.Count - 1);
            }
        }

        private static List<ModConfigSection> CollectRelatedSections(
            ModConfigSection componentSection,
            IReadOnlyDictionary<string, ModConfigSection> sectionLookup)
        {
            var result = new List<ModConfigSection> { componentSection };
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { componentSection.Name };
            var queue = new Queue<ModConfigSection>();
            queue.Enqueue(componentSection);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var runTarget in current.Statements
                    .Where(statement => statement.Kind == ModConfigStatementKind.Assignment && statement.Name.Equals("run", StringComparison.OrdinalIgnoreCase))
                    .Select(statement => statement.Value))
                {
                    if (!sectionLookup.TryGetValue(runTarget, out var targetSection) || !visited.Add(targetSection.Name))
                    {
                        continue;
                    }

                    result.Add(targetSection);
                    queue.Enqueue(targetSection);
                }
            }

            return result;
        }

        private static IEnumerable<string> GetReferencedVariables(ModConfigSection section)
        {
            foreach (var statement in section.Statements)
            {
                if (statement.Kind == ModConfigStatementKind.BlankLine || statement.Kind == ModConfigStatementKind.Comment)
                {
                    continue;
                }

                foreach (Match match in VariableReferenceRegex.Matches(statement.RawText))
                {
                    yield return match.Groups["var"].Value;
                }
            }
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildVariableDependencyGraph(ModConfigDocument document)
        {
            var graph = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var statement in document.RootStatements)
            {
                TrackVariableDependencies(graph, statement);
            }

            foreach (var section in document.Sections)
            {
                foreach (var statement in section.Statements)
                {
                    TrackVariableDependencies(graph, statement);
                }
            }

            return graph.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)pair.Value
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildVariableExpressions(ModConfigDocument document)
        {
            var expressionsByVariable = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var statement in document.RootStatements)
            {
                TrackVariableExpression(expressionsByVariable, statement);
            }

            foreach (var section in document.Sections)
            {
                foreach (var statement in section.Statements)
                {
                    TrackVariableExpression(expressionsByVariable, statement);
                }
            }

            return expressionsByVariable.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)pair.Value
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);
        }

        private static void TrackVariableDependencies(
            IDictionary<string, List<string>> graph,
            ModConfigStatement statement)
        {
            if (string.IsNullOrWhiteSpace(statement.VariableName) ||
                (statement.Kind != ModConfigStatementKind.VariableAssignment &&
                 statement.Kind != ModConfigStatementKind.GlobalDeclaration))
            {
                return;
            }

            var dependencies = VariableReferenceRegex.Matches(statement.Value ?? string.Empty)
                .Select(match => match.Groups["var"].Value)
                .Where(variableName => !variableName.Equals(statement.VariableName, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (dependencies.Count == 0)
            {
                return;
            }

            if (!graph.TryGetValue(statement.VariableName, out var targets))
            {
                targets = new List<string>();
                graph[statement.VariableName] = targets;
            }

            targets.AddRange(dependencies);
        }

        private static void TrackVariableExpression(
            IDictionary<string, List<string>> expressionsByVariable,
            ModConfigStatement statement)
        {
            if (string.IsNullOrWhiteSpace(statement.VariableName) ||
                (statement.Kind != ModConfigStatementKind.VariableAssignment &&
                 statement.Kind != ModConfigStatementKind.GlobalDeclaration) ||
                string.IsNullOrWhiteSpace(statement.Value))
            {
                return;
            }

            if (!expressionsByVariable.TryGetValue(statement.VariableName, out var expressions))
            {
                expressions = new List<string>();
                expressionsByVariable[statement.VariableName] = expressions;
            }

            expressions.Add(statement.Value.Trim());
        }

        private static List<string> ResolveControllingParameters(
            IEnumerable<string> candidateVariables,
            IReadOnlyDictionary<string, ModParameterDefinition> parameterLookup,
            IReadOnlyDictionary<string, IReadOnlyList<string>> variableDependencyGraph)
        {
            var controllingParameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var candidateVariable in candidateVariables
                         .Where(variableName => !string.IsNullOrWhiteSpace(variableName))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (parameterLookup.TryGetValue(candidateVariable, out var directParameter) &&
                    directParameter.BoundKeySections.Count > 0 &&
                    directParameter.Kind != ModConfigParameterKind.InternalSystem)
                {
                    controllingParameters.Add(candidateVariable);
                    continue;
                }

                var resolvedVariables = ResolveSourceVariables(candidateVariable, variableDependencyGraph).ToList();
                var resolvedParameters = resolvedVariables
                    .Where(variableName => parameterLookup.TryGetValue(variableName, out var parameter) &&
                        parameter.BoundKeySections.Count > 0 &&
                        parameter.Kind != ModConfigParameterKind.InternalSystem)
                    .ToList();

                if (resolvedParameters.Count == 0)
                {
                    continue;
                }

                foreach (var resolvedParameter in resolvedParameters)
                {
                    controllingParameters.Add(resolvedParameter);
                }
            }

            return controllingParameters
                .OrderBy(variableName => variableName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static IEnumerable<string> ResolveSourceVariables(
            string variableName,
            IReadOnlyDictionary<string, IReadOnlyList<string>> variableDependencyGraph)
        {
            return ResolveSourceVariables(variableName, variableDependencyGraph, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        private static IEnumerable<string> ResolveSourceVariables(
            string variableName,
            IReadOnlyDictionary<string, IReadOnlyList<string>> variableDependencyGraph,
            HashSet<string> visited)
        {
            if (!visited.Add(variableName))
            {
                yield break;
            }

            if (!variableDependencyGraph.TryGetValue(variableName, out var dependencies) || dependencies.Count == 0)
            {
                yield return variableName;
                yield break;
            }

            var yieldedAny = false;
            foreach (var dependency in dependencies)
            {
                foreach (var resolvedVariable in ResolveSourceVariables(dependency, variableDependencyGraph, visited))
                {
                    yieldedAny = true;
                    yield return resolvedVariable;
                }
            }

            if (!yieldedAny)
            {
                yield return variableName;
            }
        }

        private static IEnumerable<string> GetActiveControlVariables(IEnumerable<string> controlStack)
        {
            foreach (var controlExpression in controlStack)
            {
                foreach (Match match in VariableReferenceRegex.Matches(controlExpression))
                {
                    yield return match.Groups["var"].Value;
                }
            }
        }

        private static IEnumerable<string> GetActiveControlExpressions(IEnumerable<string> controlStack)
        {
            return controlStack.Reverse();
        }

        private static void UpdateControlStack(Stack<string> controlStack, string rawText)
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

            if (rawText.Equals("endif", StringComparison.OrdinalIgnoreCase) && controlStack.Count > 0)
            {
                controlStack.Pop();
            }
        }

        private static IEnumerable<string> GetDrawLabels(ModConfigSection section)
        {
            foreach (var statement in section.Statements)
            {
                if (statement.Kind != ModConfigStatementKind.Comment)
                {
                    continue;
                }

                if (TryGetDrawLabel(statement.RawText, out var drawLabel))
                {
                    yield return drawLabel;
                }
            }
        }

        private static bool TryGetDrawLabel(string rawText, out string drawLabel)
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

        private static string GetAssignmentValue(ModConfigSection section, string name)
        {
            return section.Statements.FirstOrDefault(statement =>
                statement.Kind == ModConfigStatementKind.Assignment &&
                statement.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;
        }

        private static List<string> ResolveModelParameters(
            IEnumerable<string> candidateVariables,
            IReadOnlyDictionary<string, ModParameterDefinition> parameterLookup)
        {
            return candidateVariables
                .Where(variableName =>
                {
                    if (IsInternalSystemVariable(variableName))
                    {
                        return false;
                    }

                    if (!parameterLookup.TryGetValue(variableName, out var parameter))
                    {
                        // Variable not tracked in early analysis (e.g. only appears in if
                        // conditions inside TextureOverrideComponent). Treat it as a model
                        // parameter so the UI can still surface it.
                        return true;
                    }

                    return parameter.BoundKeySections.Count == 0;
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(variableName => variableName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<ModVisibilityKeyParameterBinding> ResolveVisibilityKeyParameterBindings(
            VisibilityDrawEntry drawEntry,
            IReadOnlyDictionary<string, ModParameterDefinition> parameterLookup,
            IReadOnlyDictionary<string, IReadOnlyList<string>> variableDependencyGraph,
            IReadOnlyDictionary<string, IReadOnlyList<string>> variableExpressionsByVariable)
        {
            var bindingsByParameter = new Dictionary<string, ModVisibilityKeyParameterBinding>(StringComparer.OrdinalIgnoreCase);

            foreach (var candidateVariable in drawEntry.ControllingVariables
                         .Where(variableName => !string.IsNullOrWhiteSpace(variableName) && !IsInternalSystemVariable(variableName))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                foreach (var bindingPath in ResolveBindingPathsToBoundParameters(candidateVariable, parameterLookup, variableDependencyGraph))
                {
                    var keyParameterName = bindingPath[bindingPath.Count - 1];
                    if (!parameterLookup.TryGetValue(keyParameterName, out var keyParameter))
                    {
                        continue;
                    }

                    if (!bindingsByParameter.TryGetValue(keyParameterName, out var binding))
                    {
                        binding = new ModVisibilityKeyParameterBinding
                        {
                            ParameterName = keyParameterName
                        };
                        bindingsByParameter[keyParameterName] = binding;
                    }

                    binding.KeySections.AddRange(keyParameter.BoundKeySections);
                    binding.KeyBindings.AddRange(keyParameter.KeyBindings);
                    binding.EffectiveValues.AddRange(ResolveEffectiveValuesForBindingPath(
                        bindingPath,
                        drawEntry.ControlExpressions,
                        variableExpressionsByVariable));
                }
            }

            return bindingsByParameter.Values
                .Select(binding =>
                {
                    binding.KeySections = binding.KeySections
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    binding.KeyBindings = binding.KeyBindings
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    binding.EffectiveValues = binding.EffectiveValues
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    return binding;
                })
                .OrderBy(binding => binding.ParameterName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static IReadOnlyList<IReadOnlyList<string>> ResolveBindingPathsToBoundParameters(
            string variableName,
            IReadOnlyDictionary<string, ModParameterDefinition> parameterLookup,
            IReadOnlyDictionary<string, IReadOnlyList<string>> variableDependencyGraph)
        {
            var result = new List<IReadOnlyList<string>>();
            ResolveBindingPathsToBoundParameters(
                variableName,
                parameterLookup,
                variableDependencyGraph,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new List<string>(),
                result);
            return result;
        }

        private static void ResolveBindingPathsToBoundParameters(
            string variableName,
            IReadOnlyDictionary<string, ModParameterDefinition> parameterLookup,
            IReadOnlyDictionary<string, IReadOnlyList<string>> variableDependencyGraph,
            HashSet<string> visited,
            List<string> currentPath,
            List<IReadOnlyList<string>> result)
        {
            if (!visited.Add(variableName))
            {
                return;
            }

            currentPath.Add(variableName);
            try
            {
                if (parameterLookup.TryGetValue(variableName, out var parameter) &&
                    parameter.BoundKeySections.Count > 0 &&
                    !IsInternalSystemVariable(variableName))
                {
                    result.Add(currentPath.ToList());
                    return;
                }

                if (!variableDependencyGraph.TryGetValue(variableName, out var dependencies) || dependencies.Count == 0)
                {
                    return;
                }

                foreach (var dependency in dependencies)
                {
                    ResolveBindingPathsToBoundParameters(
                        dependency,
                        parameterLookup,
                        variableDependencyGraph,
                        visited,
                        currentPath,
                        result);
                }
            }
            finally
            {
                currentPath.RemoveAt(currentPath.Count - 1);
                visited.Remove(variableName);
            }
        }

        private static IReadOnlyList<string> ResolveEffectiveValuesForBindingPath(
            IReadOnlyList<string> bindingPath,
            IReadOnlyCollection<string> controlExpressions,
            IReadOnlyDictionary<string, IReadOnlyList<string>> variableExpressionsByVariable)
        {
            var currentValues = ExtractRequiredValuesForVariableFromExpressions(controlExpressions, bindingPath[0]);

            for (var index = 0; index < bindingPath.Count - 1; index++)
            {
                currentValues = ResolveUpstreamValues(
                    bindingPath[index],
                    bindingPath[index + 1],
                    currentValues,
                    variableExpressionsByVariable);
            }

            return currentValues;
        }

        private static IReadOnlyList<string> ResolveUpstreamValues(
            string currentVariable,
            string upstreamVariable,
            IReadOnlyList<string> currentValues,
            IReadOnlyDictionary<string, IReadOnlyList<string>> variableExpressionsByVariable)
        {
            if (!variableExpressionsByVariable.TryGetValue(currentVariable, out var expressions))
            {
                return currentValues;
            }

            foreach (var expression in expressions)
            {
                var explicitValues = ExtractRequiredValuesForVariableFromExpressions(new[] { expression }, upstreamVariable);
                if (explicitValues.Count > 0)
                {
                    return explicitValues;
                }

                if (IsDirectVariablePassthrough(expression, upstreamVariable))
                {
                    return currentValues;
                }
            }

            return currentValues;
        }

        private static IReadOnlyList<string> ExtractRequiredValuesForVariableFromExpressions(
            IEnumerable<string> expressions,
            string variableName)
        {
            var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var expression in expressions.Where(expression => !string.IsNullOrWhiteSpace(expression)))
            {
                foreach (Match match in VariableComparisonRegex.Matches(expression))
                {
                    if (match.Groups["var"].Value.Equals(variableName, StringComparison.OrdinalIgnoreCase))
                    {
                        values.Add(match.Groups["value"].Value);
                    }
                }

                var normalizedExpression = TrimEnclosingParentheses(expression.Trim());
                if (normalizedExpression.Equals(variableName, StringComparison.OrdinalIgnoreCase))
                {
                    values.Add("1");
                }
                else if (normalizedExpression.Equals("!" + variableName, StringComparison.OrdinalIgnoreCase))
                {
                    values.Add("0");
                }
            }

            return values.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static bool IsDirectVariablePassthrough(string expression, string variableName)
        {
            return TrimEnclosingParentheses(expression.Trim())
                .Equals(variableName, StringComparison.OrdinalIgnoreCase);
        }

        private static string TrimEnclosingParentheses(string value)
        {
            while (value.Length >= 2 && value[0] == '(' && value[^1] == ')')
            {
                value = value.Substring(1, value.Length - 2).Trim();
            }

            return value;
        }

        private static void ApplyParameterLinksAndKinds(
            IReadOnlyList<ModParameterDefinition> parameters,
            IReadOnlyList<ModVisibilityItem> visibilityItems,
            IReadOnlyDictionary<string, IReadOnlyList<string>> variableDependencyGraph)
        {
            var parameterLookup = parameters.ToDictionary(parameter => parameter.Name, StringComparer.OrdinalIgnoreCase);
            var adjacency = BuildParameterAdjacency(parameterLookup, variableDependencyGraph);
            var textureParameters = new HashSet<string>(
                visibilityItems.SelectMany(item => item.ModelParameters),
                StringComparer.OrdinalIgnoreCase);
            var linkParameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var textureParameter in textureParameters)
            {
                foreach (var path in ResolveBindingPathsToBoundParameters(textureParameter, parameterLookup, variableDependencyGraph))
                {
                    for (var index = 1; index < path.Count - 1; index++)
                    {
                        if (parameterLookup.ContainsKey(path[index]) && !IsInternalSystemVariable(path[index]))
                        {
                            linkParameters.Add(path[index]);
                        }
                    }
                }
            }

            foreach (var parameter in parameters)
            {
                parameter.LinkedParameterNames = ResolveLinkedParameterNames(parameter.Name, adjacency);

                if (IsInternalSystemVariable(parameter.Name))
                {
                    parameter.Kind = ModConfigParameterKind.InternalSystem;
                }
                else if (parameter.BoundKeySections.Count > 0 &&
                    parameter.ToggleTypes.Count > 0 &&
                    parameter.ToggleTypes.All(type => type.Equals("hold", StringComparison.OrdinalIgnoreCase)))
                {
                    parameter.Kind = ModConfigParameterKind.Unknown;
                }
                else if (parameter.BoundKeySections.Count > 0)
                {
                    parameter.Kind = ModConfigParameterKind.Toggle;
                }
                else if (textureParameters.Contains(parameter.Name))
                {
                    parameter.Kind = ModConfigParameterKind.Texture;
                }
                else if (linkParameters.Contains(parameter.Name))
                {
                    parameter.Kind = ModConfigParameterKind.Link;
                }
                else
                {
                    parameter.Kind = ModConfigParameterKind.Unknown;
                }
            }
        }

        private static Dictionary<string, HashSet<string>> BuildParameterAdjacency(
            IReadOnlyDictionary<string, ModParameterDefinition> parameterLookup,
            IReadOnlyDictionary<string, IReadOnlyList<string>> variableDependencyGraph)
        {
            var adjacency = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var parameterName in parameterLookup.Keys)
            {
                adjacency[parameterName] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            foreach (var pair in variableDependencyGraph)
            {
                if (!parameterLookup.ContainsKey(pair.Key) || IsInternalSystemVariable(pair.Key))
                {
                    continue;
                }

                foreach (var dependency in pair.Value)
                {
                    if (!parameterLookup.ContainsKey(dependency) || IsInternalSystemVariable(dependency))
                    {
                        continue;
                    }

                    adjacency[pair.Key].Add(dependency);
                    adjacency[dependency].Add(pair.Key);
                }
            }

            return adjacency;
        }

        private static List<string> ResolveLinkedParameterNames(
            string parameterName,
            IReadOnlyDictionary<string, HashSet<string>> adjacency)
        {
            if (!adjacency.TryGetValue(parameterName, out var neighbors) || neighbors.Count == 0)
            {
                return new List<string>();
            }

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { parameterName };
            var queue = new Queue<string>(neighbors);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!visited.Add(current) || !adjacency.TryGetValue(current, out var nextNeighbors))
                {
                    continue;
                }

                foreach (var nextNeighbor in nextNeighbors)
                {
                    if (!visited.Contains(nextNeighbor))
                    {
                        queue.Enqueue(nextNeighbor);
                    }
                }
            }

            return visited
                .Where(name => !name.Equals(parameterName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static ModConfigVisibilityConfidence ClassifyVisibilityConfidence(int drawCallCount, int controllingParameterCount)
        {
            if (drawCallCount == 0)
            {
                return ModConfigVisibilityConfidence.Low;
            }

            if (controllingParameterCount > 0)
            {
                return ModConfigVisibilityConfidence.High;
            }

            return ModConfigVisibilityConfidence.Medium;
        }

        private sealed class ParameterAccumulator
        {
            public ParameterAccumulator(string name)
            {
                Name = name;
            }

            public string Name { get; }
            public string DefaultValue { get; set; } = string.Empty;
            public bool IsDeclaredInConstants { get; set; }
            public bool IsPersisted { get; set; }
            public List<string> ValueOptions { get; } = new List<string>();
            public List<string> BoundKeySections { get; } = new List<string>();
            public List<string> KeyBindings { get; } = new List<string>();
            public List<string> ToggleTypes { get; } = new List<string>();
            public List<string> ReferencedInSections { get; } = new List<string>();
        }

        private sealed class VisibilityDrawEntry
        {
            public int LineNumber { get; set; }
            public string DrawLabel { get; set; } = string.Empty;
            public string DrawCommandText { get; set; } = string.Empty;
            public List<string> RelatedSectionNames { get; set; } = new List<string>();
            public List<string> ControllingVariables { get; set; } = new List<string>();
            public List<string> ControlExpressions { get; set; } = new List<string>();
        }
    }
}