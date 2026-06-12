using System.Collections.Generic;

namespace WuwaModModifier.Model
{
    public enum VersionSyncDiffStatus
    {
        Unchanged,
        Updated,
        Created,
        OldOnly,
        NewOnly,
        ManualReview
    }

    public enum VersionSyncJobKind
    {
        DirectUpdate,
        CloneFromTemplate
    }

    public class VersionSyncFolderCandidate
    {
        public string CharacterName { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string ModName { get; set; } = string.Empty;
        public string FolderName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string ConfigPath { get; set; } = string.Empty;
        public string ConfigRelativePath { get; set; } = string.Empty;
        public string NormalizedNameKey { get; set; } = string.Empty;

        public string DisplayText => $"{FolderName}\\{ConfigRelativePath}";
    }

    public class VersionSyncPairingJob
    {
        public VersionSyncFolderCandidate OldCandidate { get; set; } = null!;
        public VersionSyncFolderCandidate NewCandidate { get; set; } = null!;
        public VersionSyncJobKind JobKind { get; set; }
        public string OutputDirectoryPath { get; set; } = string.Empty;
        public string OutputConfigPath { get; set; } = string.Empty;
        public int Sequence { get; set; }
    }

    public class VersionSyncToggleDiffItem
    {
        public string SectionName { get; set; } = string.Empty;
        public string OldKeyBindingsText { get; set; } = string.Empty;
        public string NewKeyBindingsText { get; set; } = string.Empty;
        public string ResultKeyBindingsText { get; set; } = string.Empty;
        public string OldTargetValuesText { get; set; } = string.Empty;
        public string NewTargetValuesText { get; set; } = string.Empty;
        public string ResultTargetValuesText { get; set; } = string.Empty;
        public VersionSyncDiffStatus Status { get; set; }
        public bool CanApply { get; set; }
        public bool CanSyncPreview => CanApply && (Status == VersionSyncDiffStatus.Updated || Status == VersionSyncDiffStatus.Created);
    }

    public class VersionSyncParameterDiffItem
    {
        public string Name { get; set; } = string.Empty;
        public string OldDefaultValue { get; set; } = string.Empty;
        public string NewDefaultValue { get; set; } = string.Empty;
        public string ResultDefaultValue { get; set; } = string.Empty;
        public VersionSyncDiffStatus Status { get; set; }
        public bool CanApply { get; set; }
        public bool CanSyncPreview => CanApply && (Status == VersionSyncDiffStatus.Updated || Status == VersionSyncDiffStatus.Created);
    }

    public class VersionSyncVisibilityDiffItem
    {
        public string VisibilityKey { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
        public string OldBindingText { get; set; } = string.Empty;
        public string NewBindingText { get; set; } = string.Empty;
        public string ResultBindingText { get; set; } = string.Empty;
        public VersionSyncDiffStatus Status { get; set; }
        public string Detail { get; set; } = string.Empty;
        public bool CanApply { get; set; }
        public string TargetSectionName { get; set; } = string.Empty;
        public string TargetDrawLabel { get; set; } = string.Empty;
        public string VariableName { get; set; } = string.Empty;
        public string ResultDefaultValue { get; set; } = string.Empty;
        public string ResultKeyBindingsText { get; set; } = string.Empty;
        public bool CanSyncPreview => CanApply && (Status == VersionSyncDiffStatus.Updated || Status == VersionSyncDiffStatus.Created);
    }

    public class VersionSyncComparisonResult
    {
        public VersionSyncPairingJob Job { get; set; } = null!;
        public string OldConfigText { get; set; } = string.Empty;
        public string NewConfigText { get; set; } = string.Empty;
        public string OriginalResultConfigText { get; set; } = string.Empty;
        public ModConfigEditBuffer NewBuffer { get; set; } = new ModConfigEditBuffer();
        public ModConfigEditBuffer ResultBuffer { get; set; } = new ModConfigEditBuffer();
        public List<VersionSyncToggleDiffItem> ToggleDiffItems { get; set; } = new List<VersionSyncToggleDiffItem>();
        public List<VersionSyncParameterDiffItem> ParameterDiffItems { get; set; } = new List<VersionSyncParameterDiffItem>();
        public List<VersionSyncVisibilityDiffItem> VisibilityDiffItems { get; set; } = new List<VersionSyncVisibilityDiffItem>();
    }

    public class VersionSyncApplyResult
    {
        public VersionSyncPairingJob Job { get; set; } = null!;
        public string OutputDirectoryPath { get; set; } = string.Empty;
        public string OutputConfigPath { get; set; } = string.Empty;
        public int AppliedChangeCount { get; set; }
    }
}