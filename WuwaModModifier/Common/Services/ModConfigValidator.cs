using System;
using WuwaModModifier.Model;

namespace WuwaModModifier.Common
{
    /// <summary>
    /// Lightweight, standalone validators for core domain model classes.
    /// These checks are called at service entry points to fail early with
    /// meaningful messages rather than letting null-propagated exceptions
    /// surface deep in the processing pipeline.
    /// </summary>
    public static class ModConfigValidator
    {
        public static void ValidateDocument(ModConfigDocument? document, string paramName)
        {
            if (document is null)
                throw new ArgumentNullException(paramName);
            if (document.Sections is null)
                throw new ArgumentException("Document sections list is null.", paramName);
        }

        public static void ValidateSection(ModConfigSection? section, string paramName)
        {
            if (section is null)
                throw new ArgumentNullException(paramName);
            if (string.IsNullOrWhiteSpace(section.Name))
                throw new ArgumentException("Section name is required.", paramName);
        }

        public static void ValidateStatement(ModConfigStatement? statement, string paramName)
        {
            if (statement is null)
                throw new ArgumentNullException(paramName);
            if (string.IsNullOrEmpty(statement.RawText))
                throw new ArgumentException("Statement RawText is required.", paramName);
        }

        public static void ValidateParameterName(string? parameterName, string paramName)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
                throw new ArgumentException("Parameter name is required.", paramName);
            if (!parameterName.StartsWith("$", StringComparison.Ordinal))
                throw new ArgumentException($"Parameter name must start with '$': {parameterName}", paramName);
        }

        public static void ValidateToggleDefinition(ModToggleDefinition? toggle, string paramName)
        {
            if (toggle is null)
                throw new ArgumentNullException(paramName);
            if (string.IsNullOrWhiteSpace(toggle.SectionName))
                throw new ArgumentException("Toggle section name is required.", paramName);
        }

        public static void ValidateFilePath(string? filePath, string paramName)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path is required.", paramName);
        }
    }
}
