using System;
using System.Collections.Generic;

namespace WuwaModModifier.Model
{
    public enum ModConfigSaveTarget
    {
        ModDirectory,
        WwmiDirectory
    }

    public enum ModConfigSyncDirection
    {
        ModToWwmi,
        WwmiToMod
    }

    public enum ModConfigStandardizationStatus
    {
        FullyStandardized,
        PartiallyStandardized,
        Skipped
    }

    public class ModConfigEditBuffer
    {
        public string SourcePath { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string LineEnding { get; set; } = Environment.NewLine;
        public List<string> AppliedChanges { get; set; } = new List<string>();
    }

    public class ModConfigSaveResult
    {
        public string TargetPath { get; set; } = string.Empty;
    }

    public class ModConfigSyncResult
    {
        public string SourcePath { get; set; } = string.Empty;
        public string TargetPath { get; set; } = string.Empty;
    }

    public class ModConfigTargetPreview
    {
        public string SourcePath { get; set; } = string.Empty;
        public string TargetPath { get; set; } = string.Empty;
        public bool TargetExists { get; set; }
    }

    public class ModConfigStandardizationItemResult
    {
        public string OriginalSectionName { get; set; } = string.Empty;
        public string FinalSectionName { get; set; } = string.Empty;
        public string OriginalVariableName { get; set; } = string.Empty;
        public string FinalVariableName { get; set; } = string.Empty;
        public List<string> TargetKeyBindings { get; set; } = new List<string>();
        public ModConfigStandardizationStatus Status { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class ModConfigStandardizationResult
    {
        public ModConfigEditBuffer Buffer { get; set; } = new ModConfigEditBuffer();
        public int FullyStandardizedCount { get; set; }
        public int PartiallyStandardizedCount { get; set; }
        public int SkippedCount { get; set; }
        public List<ModConfigStandardizationItemResult> Items { get; set; } = new List<ModConfigStandardizationItemResult>();
    }
}