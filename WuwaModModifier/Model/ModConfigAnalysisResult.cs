using System.Collections.Generic;

namespace WuwaModModifier.Model
{
    public enum ModConfigParameterKind
    {
        Unknown = 0,
        System = 1,
        Toggle = 2,
        Texture = 3,
        Link = 4
    }

    public enum ModConfigVisibilityConfidence
    {
        Low,
        Medium,
        High
    }

    public class ModConfigAnalysisResult
    {
        public List<ModToggleDefinition> Toggles { get; set; } = new List<ModToggleDefinition>();
        public List<ModParameterDefinition> Parameters { get; set; } = new List<ModParameterDefinition>();
        public List<ModVisibilityItem> VisibilityItems { get; set; } = new List<ModVisibilityItem>();
    }

    public class ModToggleDefinition
    {
        public string SectionName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ConditionText { get; set; } = string.Empty;
        public string ToggleType { get; set; } = string.Empty;
        public List<string> KeyBindings { get; set; } = new List<string>();
        public List<ModToggleTarget> Targets { get; set; } = new List<ModToggleTarget>();
        public bool IsStandardizationCandidate { get; set; }
    }

    public class ModToggleTarget
    {
        public string VariableName { get; set; } = string.Empty;
        public List<string> Values { get; set; } = new List<string>();
    }

    public class ModParameterDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string DefaultValue { get; set; } = string.Empty;
        public bool IsDeclaredInConstants { get; set; }
        public bool IsPersisted { get; set; }
        public ModConfigParameterKind Kind { get; set; }
        public List<string> ValueOptions { get; set; } = new List<string>();
        public List<string> BoundKeySections { get; set; } = new List<string>();
        public List<string> KeyBindings { get; set; } = new List<string>();
        public List<string> ToggleTypes { get; set; } = new List<string>();
        public List<string> ReferencedInSections { get; set; } = new List<string>();
        public List<string> LinkedParameterNames { get; set; } = new List<string>();
        public bool CanRename { get; set; }
    }

    public class ModVisibilityKeyParameterBinding
    {
        public string ParameterName { get; set; } = string.Empty;
        public List<string> EffectiveValues { get; set; } = new List<string>();
        public List<string> KeySections { get; set; } = new List<string>();
        public List<string> KeyBindings { get; set; } = new List<string>();
    }

    public class ModVisibilityItem
    {
        public string SectionName { get; set; } = string.Empty;
        public int NavigateLine { get; set; }
        public string Hash { get; set; } = string.Empty;
        public string MatchFirstIndex { get; set; } = string.Empty;
        public string MatchIndexCount { get; set; } = string.Empty;
        public string HandlingMode { get; set; } = string.Empty;
        public int DrawCallCount { get; set; }
        public List<string> DrawLabels { get; set; } = new List<string>();
        public List<string> RelatedSectionNames { get; set; } = new List<string>();
        public List<string> ModelParameters { get; set; } = new List<string>();
        public List<ModVisibilityKeyParameterBinding> KeyParameterBindings { get; set; } = new List<ModVisibilityKeyParameterBinding>();
        public List<string> ControllingParameters { get; set; } = new List<string>();
        public List<string> ControllingKeySections { get; set; } = new List<string>();
        public List<string> ControllingKeyBindings { get; set; } = new List<string>();
        public List<string> ControlExpressions { get; set; } = new List<string>();
        public ModConfigVisibilityConfidence Confidence { get; set; }
    }
}