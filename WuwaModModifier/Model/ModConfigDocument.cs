using System.Collections.Generic;

namespace WuwaModModifier.Model
{
    public enum ModConfigStatementKind
    {
        Assignment,
        VariableAssignment,
        GlobalDeclaration,
        ControlLine,
        Comment,
        BlankLine,
        UnknownLine
    }

    public class ModConfigDocument
    {
        public List<ModConfigStatement> RootStatements { get; set; } = new List<ModConfigStatement>();
        public List<ModConfigSection> Sections { get; set; } = new List<ModConfigSection>();
    }

    public class ModConfigSection
    {
        public string Name { get; set; } = string.Empty;
        public string HeaderText { get; set; } = string.Empty;
        public int HeaderLineNumber { get; set; }
        public List<ModConfigStatement> Statements { get; set; } = new List<ModConfigStatement>();
    }

    public class ModConfigStatement
    {
        public ModConfigStatementKind Kind { get; set; }
        public string RawText { get; set; } = string.Empty;
        public int LineNumber { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string VariableName { get; set; } = string.Empty;
    }
}