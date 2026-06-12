using System;
using System.IO;
using System.Text.RegularExpressions;
using WuwaModModifier.Common.Helpers;
using WuwaModModifier.Model;

namespace WuwaModModifier.Common
{
    /// <summary>
    /// 宽容型配置解析器：优先保留节、语句顺序和原始文本，再由上层做语义提取。
    /// </summary>
    public class ModConfigParser : IModConfigParser
    {
        private static readonly Regex SectionHeaderRegex =
            new Regex(@"^\s*\[(?<name>[^\]]+)\]\s*$", RegexOptions.Compiled);

        private static readonly Regex VariableNameRegex =
            new Regex(@"(?<var>\$[^\s=,]+)", RegexOptions.Compiled);

        private static readonly string[] ControlKeywords = new[] { "if", "elif", "else", "endif", "post" };

        private readonly IFileSystemService _fileSystem;

        public ModConfigParser(IFileSystemService fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public ModConfigDocument Parse(string content)
        {
            var document = new ModConfigDocument();
            var normalizedContent = NormalizeLineEndings(content);
            var lines = normalizedContent.Split('\n');
            ModConfigSection? currentSection = null;

            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                var lineNumber = index + 1;
                var sectionMatch = SectionHeaderRegex.Match(line.Trim());

                if (sectionMatch.Success)
                {
                    currentSection = new ModConfigSection
                    {
                        Name = sectionMatch.Groups["name"].Value,
                        HeaderText = line,
                        HeaderLineNumber = lineNumber
                    };
                    document.Sections.Add(currentSection);
                    continue;
                }

                var statement = ParseStatement(line, lineNumber);
                if (currentSection == null)
                {
                    document.RootStatements.Add(statement);
                }
                else
                {
                    currentSection.Statements.Add(statement);
                }
            }

            return document;
        }

        public ModConfigDocument ParseFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !_fileSystem.FileExists(filePath))
            {
                throw new FileNotFoundException("配置文件不存在。", filePath);
            }

            return Parse(_fileSystem.ReadAllText(filePath));
        }

        private static ModConfigStatement ParseStatement(string line, int lineNumber)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                return CreateStatement(ModConfigStatementKind.BlankLine, line, lineNumber);
            }

            if (trimmed.StartsWith(";") || trimmed.StartsWith("#"))
            {
                return CreateStatement(ModConfigStatementKind.Comment, line, lineNumber);
            }

            if (IsControlLine(trimmed))
            {
                var keyword = GetLeadingToken(trimmed);
                return CreateStatement(ModConfigStatementKind.ControlLine, line, lineNumber, keyword, trimmed);
            }

            var equalsIndex = line.IndexOf('=');
            if (equalsIndex >= 0)
            {
                var name = line.Substring(0, equalsIndex).Trim();
                var value = line.Substring(equalsIndex + 1).Trim();

                if (name.StartsWith("$", StringComparison.Ordinal))
                {
                    return CreateStatement(ModConfigStatementKind.VariableAssignment, line, lineNumber, name, value, name);
                }

                if (name.StartsWith("global", StringComparison.OrdinalIgnoreCase))
                {
                    return CreateStatement(
                        ModConfigStatementKind.GlobalDeclaration,
                        line,
                        lineNumber,
                        name,
                        value,
                        ExtractVariableName(name));
                }

                return CreateStatement(ModConfigStatementKind.Assignment, line, lineNumber, name, value);
            }

            return CreateStatement(ModConfigStatementKind.UnknownLine, line, lineNumber, trimmed);
        }

        private static bool IsControlLine(string trimmed)
        {
            foreach (var keyword in ControlKeywords)
            {
                if (trimmed.Equals(keyword, StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith(keyword + " ", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetLeadingToken(string text)
        {
            var spaceIndex = text.IndexOf(' ');
            return spaceIndex < 0 ? text : text.Substring(0, spaceIndex);
        }

        private static string ExtractVariableName(string name)
        {
            var match = VariableNameRegex.Match(name);
            return match.Success ? match.Groups["var"].Value : string.Empty;
        }

        private static ModConfigStatement CreateStatement(
            ModConfigStatementKind kind,
            string rawText,
            int lineNumber,
            string name = "",
            string value = "",
            string variableName = "")
        {
            return new ModConfigStatement
            {
                Kind = kind,
                RawText = rawText,
                LineNumber = lineNumber,
                Name = name,
                Value = value,
                VariableName = variableName
            };
        }

        private static string NormalizeLineEndings(string content)
        {
            return TextHelper.NormalizeLineEndings(content);
        }
    }
}