using System;
using System.Collections.Generic;
using System.Linq;
using WuwaModModifier.Common.Helpers;
using WuwaModModifier.Model;

namespace WuwaModModifier.ViewModels
{
    public enum ConfigTextHighlightKind
    {
        Toggle,
        Parameter,
        Visibility
    }

    public class ConfigTextHighlightItem
    {
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public ConfigTextHighlightKind Kind { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    public class ConfigToggleSummaryItem
    {
        public string SectionName { get; set; } = string.Empty;
        public string ToggleType { get; set; } = string.Empty;
        public string ConditionText { get; set; } = string.Empty;
        public string KeyBindingsText { get; set; } = string.Empty;
        public string TargetsText { get; set; } = string.Empty;
        public string PrimaryVariableName { get; set; } = string.Empty;
        public int NavigateLine { get; set; }
        public bool IsStandardizationCandidate { get; set; }
        public bool CanBindVisibilitySafely { get; set; }
        public string VisibilityBindingDisplayText =>
            string.IsNullOrWhiteSpace(KeyBindingsText)
                ? $"{SectionName} -> {PrimaryVariableName}"
                : $"{SectionName} ({KeyBindingsText}) -> {PrimaryVariableName}";

        public static ConfigToggleSummaryItem FromModel(ModToggleDefinition toggle)
        {
            return new ConfigToggleSummaryItem
            {
                SectionName = toggle.SectionName,
                ToggleType = toggle.ToggleType,
                ConditionText = toggle.ConditionText,
                KeyBindingsText = Join(toggle.KeyBindings),
                TargetsText = Join(toggle.Targets.Select(target =>
                    $"{target.VariableName} = {Join(target.Values)}")),
                PrimaryVariableName = toggle.Targets.Count == 1 ? toggle.Targets[0].VariableName : string.Empty,
                IsStandardizationCandidate = toggle.IsStandardizationCandidate
            };
        }

        private static string Join(IEnumerable<string> values)
        {
            return StringFormattingHelper.JoinNonEmpty(values);
        }
    }

    public class ConfigParameterSummaryItem : IVisibilityBindingTarget
    {
        public string Name { get; set; } = string.Empty;
        public string DefaultValue { get; set; } = string.Empty;
        public string KindText { get; set; } = string.Empty;
        public string ValueOptionsText { get; set; } = string.Empty;
        public string BoundKeySectionsText { get; set; } = string.Empty;
        public string KeyBindingsText { get; set; } = string.Empty;
        public string LinkedParametersText { get; set; } = string.Empty;
        public int NavigateLine { get; set; }
        public bool CanRename { get; set; }
        public bool CanBindVisibilitySafely { get; set; }
        public bool CanCreateToggleBinding { get; set; }
        public string ParameterName => Name;
        public string DisplayName => VisibilityBindingDisplayText;
        public string VisibilityBindingDisplayText =>
            string.IsNullOrWhiteSpace(KeyBindingsText)
                ? Name
                : $"{Name} ({KeyBindingsText})";

        public static ConfigParameterSummaryItem FromModel(ModParameterDefinition parameter)
        {
            return new ConfigParameterSummaryItem
            {
                Name = parameter.Name,
                DefaultValue = parameter.DefaultValue,
                KindText = parameter.Kind.ToString(),
                ValueOptionsText = Join(parameter.ValueOptions),
                BoundKeySectionsText = Join(parameter.BoundKeySections),
                KeyBindingsText = Join(parameter.KeyBindings),
                LinkedParametersText = JoinSemicolon(parameter.LinkedParameterNames),
                CanRename = parameter.CanRename
            };
        }

        private static string Join(IEnumerable<string> values)
        {
            return StringFormattingHelper.JoinNonEmpty(values);
        }

        private static string JoinSemicolon(IEnumerable<string> values)
        {
            return StringFormattingHelper.JoinSemicolon(values);
        }
    }

    public class ConfigVisibilitySummaryItem
    {
        public string SectionName { get; set; } = string.Empty;
        public string ConfidenceText { get; set; } = string.Empty;
        public int DrawCallCount { get; set; }
        public string DrawLabelsText { get; set; } = string.Empty;
        public string ControllingParametersText { get; set; } = string.Empty;
        public string ModelParametersText { get; set; } = string.Empty;
        public string KeyParametersText { get; set; } = string.Empty;
        public string ControllingKeyBindingsText { get; set; } = string.Empty;
        public int NavigateLine { get; set; }
        public bool CanToggleSafely { get; set; }
        public bool CanBindSafely { get; set; }
        public bool IsDirectlyHidden { get; set; }

        public static ConfigVisibilitySummaryItem FromModel(ModVisibilityItem item)
        {
            var canToggleSafely = item.DrawCallCount > 0 && item.ControllingParameters.Count == 1;
            var canBindSafely = item.DrawCallCount > 0 &&
                item.ControllingParameters.Count == 0 &&
                item.ControllingKeySections.Count == 0 &&
                item.ControllingKeyBindings.Count == 0;

            return new ConfigVisibilitySummaryItem
            {
                SectionName = item.SectionName,
                ConfidenceText = item.Confidence.ToString(),
                DrawCallCount = item.DrawCallCount,
                DrawLabelsText = Join(item.DrawLabels),
                ControllingParametersText = Join(item.ControllingParameters),
                ModelParametersText = JoinSemicolon(item.ModelParameters),
                KeyParametersText = JoinSemicolon(item.KeyParameterBindings.Select(FormatKeyParameterBinding)),
                ControllingKeyBindingsText = Join(item.ControllingKeyBindings),
                CanToggleSafely = canToggleSafely,
                CanBindSafely = canBindSafely,
                IsDirectlyHidden = canBindSafely && item.ControlExpressions.Any(expression =>
                    expression.Trim().Equals("0", StringComparison.OrdinalIgnoreCase))
            };
        }

        private static string Join(IEnumerable<string> values)
        {
            return StringFormattingHelper.JoinNonEmpty(values);
        }

        private static string JoinSemicolon(IEnumerable<string> values)
        {
            return StringFormattingHelper.JoinSemicolon(values);
        }

        private static string FormatKeyParameterBinding(ModVisibilityKeyParameterBinding binding)
        {
            var effectiveValues = string.Join(", ", binding.EffectiveValues.Where(value => !string.IsNullOrWhiteSpace(value)));
            return string.IsNullOrWhiteSpace(effectiveValues)
                ? binding.ParameterName
                : $"{binding.ParameterName} = {effectiveValues}";
        }
    }

    public class ConfigStandardizationSummaryItem
    {
        public string StatusText { get; set; } = string.Empty;
        public string OriginalSectionName { get; set; } = string.Empty;
        public string FinalSectionName { get; set; } = string.Empty;
        public string OriginalVariableName { get; set; } = string.Empty;
        public string FinalVariableName { get; set; } = string.Empty;
        public string TargetKeyBindingsText { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;

        public static ConfigStandardizationSummaryItem FromModel(ModConfigStandardizationItemResult item)
        {
            return new ConfigStandardizationSummaryItem
            {
                StatusText = item.Status switch
                {
                    ModConfigStandardizationStatus.FullyStandardized => Properties.Resources.StandardizationFully,
                    ModConfigStandardizationStatus.PartiallyStandardized => Properties.Resources.StandardizationPartially,
                    _ => Properties.Resources.StandardizationSkipped
                },
                OriginalSectionName = item.OriginalSectionName,
                FinalSectionName = item.FinalSectionName,
                OriginalVariableName = item.OriginalVariableName,
                FinalVariableName = item.FinalVariableName,
                TargetKeyBindingsText = Join(item.TargetKeyBindings),
                Reason = item.Reason
            };
        }

        private static string Join(IEnumerable<string> values)
        {
            return StringFormattingHelper.JoinNonEmpty(values);
        }
    }

    public class ConfigModificationHistoryItem
    {
        public string TimestampText { get; set; } = string.Empty;
        public string OperationTypeText { get; set; } = string.Empty;
        public string TargetText { get; set; } = string.Empty;
        public string SummaryText { get; set; } = string.Empty;

        public static ConfigModificationHistoryItem Create(string operationType, string target, string summary)
        {
            return new ConfigModificationHistoryItem
            {
                TimestampText = DateTime.Now.ToString("HH:mm:ss"),
                OperationTypeText = operationType,
                TargetText = target,
                SummaryText = summary
            };
        }
    }

    public enum VersionSyncJobFilterMode
    {
        All,
        HasDifferences,
        ManualReviewOnly,
        AutoApplicableOnly
    }

    public enum VersionSyncBatchApplyMode
    {
        AllVisibleJobs,
        AutoApplicableOnly
    }

    public class ConfigVisibilityBindingRemovalCandidate : IVisibilityBindingTarget
    {
        public string ParameterName { get; set; } = string.Empty;
        public string KeyBindingsText { get; set; } = string.Empty;
        public string KeySectionName { get; set; } = string.Empty;
        public string DisplayText =>
            string.IsNullOrWhiteSpace(KeyBindingsText)
                ? $"{ParameterName} → {KeySectionName}"
                : $"{ParameterName} → {KeyBindingsText} ({KeySectionName})";
        public string DisplayName => DisplayText;

        public string VisibilityBindingDisplayText => DisplayText;
    }

    public class VersionSyncJobSummaryItem : ViewModelBase
    {
        private int _differenceCount;
        private int _applicableChangeCount;
        private int _manualReviewCount;
        private string _previewErrorMessage = string.Empty;

        public VersionSyncPairingJob Job { get; set; } = null!;

        public string OldFolderName => Job.OldCandidate.FolderName;

        public string NewFolderName => Job.NewCandidate.FolderName;

        public string OutputConfigPath => Job.OutputConfigPath;

        public string ModeText => Job.JobKind == VersionSyncJobKind.DirectUpdate ? "直接写回" : "复制模板";

        public int DifferenceCount
        {
            get => _differenceCount;
            private set => SetProperty(ref _differenceCount, value);
        }

        public int ApplicableChangeCount
        {
            get => _applicableChangeCount;
            private set => SetProperty(ref _applicableChangeCount, value);
        }

        public int ManualReviewCount
        {
            get => _manualReviewCount;
            private set => SetProperty(ref _manualReviewCount, value);
        }

        public string PreviewErrorMessage
        {
            get => _previewErrorMessage;
            private set => SetProperty(ref _previewErrorMessage, value);
        }

        public bool HasPreviewError => !string.IsNullOrWhiteSpace(PreviewErrorMessage);

        public bool HasChanges => DifferenceCount > 0;

        public bool RequiresManualReview => ManualReviewCount > 0 || HasPreviewError;

        public bool CanAutoApply => ApplicableChangeCount > 0 && !RequiresManualReview;

        public string StatusText
        {
            get
            {
                if (HasPreviewError)
                {
                    return "预览失败";
                }

                if (ManualReviewCount > 0)
                {
                    return "需人工确认";
                }

                if (CanAutoApply)
                {
                    return "可自动应用";
                }

                return HasChanges ? "仅供参考" : "无变更";
            }
        }

        public string ChangeSummaryText => HasPreviewError
            ? PreviewErrorMessage
            : $"差异 {DifferenceCount} 项 / 可应用 {ApplicableChangeCount} 项 / 人工确认 {ManualReviewCount} 项";

        public static VersionSyncJobSummaryItem FromComparison(VersionSyncComparisonResult comparison)
        {
            var item = new VersionSyncJobSummaryItem
            {
                Job = comparison.Job
            };
            item.ApplyComparison(comparison);
            return item;
        }

        public static VersionSyncJobSummaryItem FromPreviewError(VersionSyncPairingJob job, string errorMessage)
        {
            var item = new VersionSyncJobSummaryItem
            {
                Job = job
            };
            item.ApplyPreviewError(errorMessage);
            return item;
        }

        public void ApplyComparison(VersionSyncComparisonResult comparison)
        {
            DifferenceCount = CountDifferences(comparison.ToggleDiffItems, item => item.Status) +
                CountDifferences(comparison.ParameterDiffItems, item => item.Status) +
                CountDifferences(comparison.VisibilityDiffItems, item => item.Status);
            ApplicableChangeCount = comparison.ToggleDiffItems.Count(item => item.CanApply) +
                comparison.ParameterDiffItems.Count(item => item.CanApply) +
                comparison.VisibilityDiffItems.Count(item => item.CanApply);
            ManualReviewCount = comparison.ToggleDiffItems.Count(item => item.Status == VersionSyncDiffStatus.ManualReview) +
                comparison.ParameterDiffItems.Count(item => item.Status == VersionSyncDiffStatus.ManualReview) +
                comparison.VisibilityDiffItems.Count(item => item.Status == VersionSyncDiffStatus.ManualReview);
            PreviewErrorMessage = string.Empty;
            NotifyDerivedStateChanged();
        }

        public void ApplyPreviewError(string errorMessage)
        {
            DifferenceCount = 0;
            ApplicableChangeCount = 0;
            ManualReviewCount = 0;
            PreviewErrorMessage = errorMessage;
            NotifyDerivedStateChanged();
        }

        public void NotifyJobSwapped()
        {
            OnPropertyChanged(nameof(OldFolderName));
            OnPropertyChanged(nameof(NewFolderName));
            OnPropertyChanged(nameof(OutputConfigPath));
            OnPropertyChanged(nameof(ModeText));
        }

        private void NotifyDerivedStateChanged()
        {
            OnPropertyChanged(nameof(HasPreviewError));
            OnPropertyChanged(nameof(HasChanges));
            OnPropertyChanged(nameof(RequiresManualReview));
            OnPropertyChanged(nameof(CanAutoApply));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(ChangeSummaryText));
        }

        private static int CountDifferences<T>(IEnumerable<T> items, Func<T, VersionSyncDiffStatus> statusSelector)
        {
            return items.Count(item => statusSelector(item) != VersionSyncDiffStatus.Unchanged);
        }
    }
}