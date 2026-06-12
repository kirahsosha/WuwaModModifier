using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WuwaModModifier.Model;

namespace WuwaModModifier.Common
{
    public class ModConfigVersionSyncService : IModConfigVersionSyncService
    {
        private const int MinimumFallbackPairingScore = 120;

        private readonly IFileSystemService _fileSystem;
        private readonly IModConfigDiscoveryService _configDiscoveryService;
        private readonly IModConfigParser _configParser;
        private readonly IModConfigAnalysisService _configAnalysisService;
        private readonly IModConfigUpdateService _configUpdateService;

        public ModConfigVersionSyncService(
            IFileSystemService fileSystem,
            IModConfigDiscoveryService? configDiscoveryService = null,
            IModConfigParser? configParser = null,
            IModConfigAnalysisService? configAnalysisService = null,
            IModConfigUpdateService? configUpdateService = null)
        {
            _fileSystem = fileSystem;
            _configDiscoveryService = configDiscoveryService ?? new ModConfigDiscoveryService(fileSystem);
            _configParser = configParser ?? new ModConfigParser(fileSystem);
            _configAnalysisService = configAnalysisService ?? new ModConfigAnalysisService(_configParser);
            _configUpdateService = configUpdateService ?? new ModConfigUpdateService(fileSystem, _configParser, _configAnalysisService);
        }

        public IReadOnlyList<VersionSyncFolderCandidate> DiscoverModCandidates(string modRootPath)
        {
            if (string.IsNullOrWhiteSpace(modRootPath) || !_fileSystem.DirectoryExists(modRootPath))
            {
                return Array.Empty<VersionSyncFolderCandidate>();
            }

            return DiscoverCandidateDirectories(modRootPath)
                .SelectMany(CreateCandidates)
                .Where(candidate => candidate != null)
                .Cast<VersionSyncFolderCandidate>()
                .DistinctBy(candidate => candidate.DisplayText, StringComparer.OrdinalIgnoreCase)
                .OrderBy(candidate => candidate.CharacterName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.Id, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.FolderName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.ConfigRelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public IReadOnlyList<VersionSyncPairingJob> CreatePairingJobs(
            IReadOnlyList<VersionSyncFolderCandidate> oldCandidates,
            IReadOnlyList<VersionSyncFolderCandidate> newCandidates)
        {
            if (HasOverlappingCandidateDirectories(oldCandidates, newCandidates))
            {
                return CreateSingleDirectoryPairingJobs(oldCandidates, newCandidates);
            }

            var jobs = new List<VersionSyncPairingJob>();
            var groupedOldCandidates = GroupCandidatesByCharacterAndId(oldCandidates);
            var groupedNewCandidates = GroupCandidatesByCharacterAndId(newCandidates);
            var keys = groupedOldCandidates.Keys
                .Intersect(groupedNewCandidates.Keys)
                .OrderBy(key => key.CharacterName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(key => key.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var key in keys)
            {
                jobs.AddRange(CreatePairingJobsWithinCharacterGroup(
                    groupedOldCandidates[key],
                    groupedNewCandidates[key]));
            }

            return jobs;
        }

        private IReadOnlyList<VersionSyncPairingJob> CreateSingleDirectoryPairingJobs(
            IReadOnlyList<VersionSyncFolderCandidate> oldCandidates,
            IReadOnlyList<VersionSyncFolderCandidate> newCandidates)
        {
            var jobs = new List<VersionSyncPairingJob>();
            var mergedCandidates = oldCandidates
                .Concat(newCandidates)
                .Where(candidate => candidate != null && !string.IsNullOrWhiteSpace(candidate.FullPath))
                .GroupBy(GetCandidateIdentityKey, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
            var groupedCandidates = GroupCandidatesByCharacterAndId(mergedCandidates);

            foreach (var key in groupedCandidates.Keys
                         .OrderBy(candidateKey => candidateKey.CharacterName, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(candidateKey => candidateKey.Id, StringComparer.OrdinalIgnoreCase))
            {
                jobs.AddRange(CreateSingleDirectoryPairingJobsWithinGroup(groupedCandidates[key]));
            }

            return jobs;
        }

        public VersionSyncComparisonResult BuildComparison(VersionSyncPairingJob job)
        {
            if (job == null)
            {
                throw new ArgumentNullException(nameof(job));
            }

            var oldBuffer = _configUpdateService.LoadBuffer(job.OldCandidate.ConfigPath);
            var newBuffer = _configUpdateService.LoadBuffer(job.NewCandidate.ConfigPath);
            return BuildComparison(job, oldBuffer, newBuffer);
        }

        public VersionSyncComparisonResult BuildComparison(VersionSyncPairingJob job, ModConfigEditBuffer newBuffer)
        {
            if (job == null)
            {
                throw new ArgumentNullException(nameof(job));
            }

            if (newBuffer == null)
            {
                throw new ArgumentNullException(nameof(newBuffer));
            }

            var oldBuffer = _configUpdateService.LoadBuffer(job.OldCandidate.ConfigPath);
            return BuildComparison(job, oldBuffer, CloneBuffer(newBuffer, job.NewCandidate.ConfigPath));
        }

        private VersionSyncComparisonResult BuildComparison(
            VersionSyncPairingJob job,
            ModConfigEditBuffer oldBuffer,
            ModConfigEditBuffer newBuffer)
        {
            var oldAnalysis = AnalyzeBuffer(oldBuffer);
            var resultBuffer = CloneBuffer(newBuffer, job.OutputConfigPath);
            var initialNewAnalysis = AnalyzeBuffer(newBuffer);

            var toggleDiffItems = BuildToggleDiffItems(oldAnalysis, initialNewAnalysis, ref resultBuffer);
            var analysisAfterToggleSync = AnalyzeBuffer(resultBuffer);
            var parameterDiffItems = BuildParameterDiffItems(oldAnalysis, analysisAfterToggleSync, ref resultBuffer);
            var analysisAfterParameterSync = AnalyzeBuffer(resultBuffer);
            var visibilityDiffItems = BuildVisibilityDiffItems(oldAnalysis, analysisAfterParameterSync, ref resultBuffer, toggleDiffItems, parameterDiffItems);

            return new VersionSyncComparisonResult
            {
                Job = job,
                OldConfigText = oldBuffer.Content,
                NewConfigText = newBuffer.Content,
                NewBuffer = CloneBuffer(newBuffer, job.NewCandidate.ConfigPath),
                ResultBuffer = CloneBuffer(resultBuffer, job.OutputConfigPath),
                ToggleDiffItems = toggleDiffItems,
                ParameterDiffItems = parameterDiffItems,
                VisibilityDiffItems = visibilityDiffItems
            };
        }

        private static ModConfigEditBuffer CloneBuffer(ModConfigEditBuffer buffer, string fallbackSourcePath)
        {
            return new ModConfigEditBuffer
            {
                SourcePath = string.IsNullOrWhiteSpace(buffer.SourcePath)
                    ? fallbackSourcePath
                    : buffer.SourcePath,
                Content = buffer.Content,
                LineEnding = string.IsNullOrWhiteSpace(buffer.LineEnding)
                    ? Environment.NewLine
                    : buffer.LineEnding,
                AppliedChanges = buffer.AppliedChanges.ToList()
            };
        }

        public VersionSyncApplyResult ApplyComparison(VersionSyncComparisonResult comparison)
        {
            if (comparison == null)
            {
                throw new ArgumentNullException(nameof(comparison));
            }

            if (comparison.Job.JobKind == VersionSyncJobKind.CloneFromTemplate)
            {
                if (_fileSystem.DirectoryExists(comparison.Job.OutputDirectoryPath))
                {
                    _fileSystem.DeleteDirectory(comparison.Job.OutputDirectoryPath, true);
                }

                _fileSystem.CopyDirectory(comparison.Job.NewCandidate.FullPath, comparison.Job.OutputDirectoryPath);
            }

            var saveResult = _configUpdateService.SaveBuffer(comparison.ResultBuffer, comparison.Job.OutputConfigPath);
            return new VersionSyncApplyResult
            {
                Job = comparison.Job,
                OutputDirectoryPath = comparison.Job.OutputDirectoryPath,
                OutputConfigPath = saveResult.TargetPath,
                AppliedChangeCount = comparison.ResultBuffer.AppliedChanges.Count
            };
        }

        private static VersionSyncPairingJob CreateJob(
            VersionSyncFolderCandidate oldCandidate,
            VersionSyncFolderCandidate newCandidate,
            VersionSyncJobKind jobKind,
            string outputDirectoryPath,
            int sequence)
        {
            return new VersionSyncPairingJob
            {
                OldCandidate = oldCandidate,
                NewCandidate = newCandidate,
                JobKind = jobKind,
                OutputDirectoryPath = outputDirectoryPath,
                OutputConfigPath = Path.Combine(outputDirectoryPath, newCandidate.ConfigRelativePath),
                Sequence = sequence
            };
        }

        private string BuildCloneOutputDirectoryPath(VersionSyncFolderCandidate templateCandidate, int sequence)
        {
            var parentDirectory = Path.GetDirectoryName(templateCandidate.FullPath) ?? string.Empty;
            var baseFolderName = templateCandidate.FolderName;
            var suffixIndex = sequence;
            string candidatePath;

            do
            {
                var candidateFolderName = $"{baseFolderName}__sync_{suffixIndex:00}";
                candidatePath = Path.Combine(parentDirectory, candidateFolderName);
                suffixIndex++;
            }
            while (_fileSystem.DirectoryExists(candidatePath));

            return candidatePath;
        }

        private List<VersionSyncToggleDiffItem> BuildToggleDiffItems(
            ModConfigAnalysisResult oldAnalysis,
            ModConfigAnalysisResult newAnalysis,
            ref ModConfigEditBuffer resultBuffer)
        {
            var diffItems = new List<VersionSyncToggleDiffItem>();
            var oldToggles = BuildDuplicateAwareLookup(oldAnalysis.Toggles, toggle => toggle.SectionName);
            var newToggles = BuildDuplicateAwareLookup(newAnalysis.Toggles, toggle => toggle.SectionName);
            var sectionNames = oldToggles.Items.Keys
                .Union(newToggles.Items.Keys, StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var sectionName in sectionNames)
            {
                var hasOld = oldToggles.Items.TryGetValue(sectionName, out var oldToggle);
                var hasNew = newToggles.Items.TryGetValue(sectionName, out var newToggle);
                var oldBindingsText = hasOld ? JoinValues(oldToggle!.KeyBindings) : string.Empty;
                var newBindingsText = hasNew ? JoinValues(newToggle!.KeyBindings) : string.Empty;
                var oldTargetValuesText = hasOld ? BuildToggleTargetValuesText(oldToggle!.Targets) : string.Empty;
                var newTargetValuesText = hasNew ? BuildToggleTargetValuesText(newToggle!.Targets) : string.Empty;
                var hasDuplicateSections = oldToggles.DuplicateKeys.Contains(sectionName) ||
                    newToggles.DuplicateKeys.Contains(sectionName);

                var hasDuplicateTargets = false;
                ToggleTargetMergeResult? targetMergeResult = null;
                if (hasOld && hasNew)
                {
                    targetMergeResult = MergeToggleTargets(oldToggle!.Targets, newToggle!.Targets);
                    hasDuplicateTargets = targetMergeResult.HasDuplicateTargets;
                }
                else if (hasOld)
                {
                    hasDuplicateTargets = BuildDuplicateAwareLookup(oldToggle!.Targets, target => target.VariableName).HasDuplicates;
                }
                else if (hasNew)
                {
                    hasDuplicateTargets = BuildDuplicateAwareLookup(newToggle!.Targets, target => target.VariableName).HasDuplicates;
                }

                if (hasDuplicateSections || hasDuplicateTargets)
                {
                    diffItems.Add(new VersionSyncToggleDiffItem
                    {
                        SectionName = sectionName,
                        OldKeyBindingsText = oldBindingsText,
                        NewKeyBindingsText = newBindingsText,
                        ResultKeyBindingsText = newBindingsText,
                        OldTargetValuesText = oldTargetValuesText,
                        NewTargetValuesText = newTargetValuesText,
                        ResultTargetValuesText = newTargetValuesText,
                        Status = VersionSyncDiffStatus.ManualReview,
                        CanApply = false
                    });
                    continue;
                }

                if (hasOld && hasNew)
                {
                    var resultBindingsText = newBindingsText;
                    var resultTargetValuesText = newTargetValuesText;
                    var hasChanges = false;

                    if (!oldBindingsText.Equals(newBindingsText, StringComparison.OrdinalIgnoreCase))
                    {
                        resultBuffer = _configUpdateService.UpdateKeyBindings(resultBuffer, newToggle!.SectionName, oldToggle!.KeyBindings);
                        resultBindingsText = oldBindingsText;
                        hasChanges = true;
                    }

                    targetMergeResult ??= MergeToggleTargets(oldToggle!.Targets, newToggle!.Targets);
                    foreach (var target in targetMergeResult.ChangedTargets)
                    {
                        resultBuffer = _configUpdateService.UpdateToggleTargetValues(
                            resultBuffer,
                            newToggle.SectionName,
                            target.VariableName,
                            target.Values);
                    }

                    if (targetMergeResult.ChangedTargets.Count > 0)
                    {
                        resultTargetValuesText = BuildToggleTargetValuesText(targetMergeResult.ResultTargets);
                        hasChanges = true;
                    }

                    if (!hasChanges && !targetMergeResult.HasOldOnlyTargets)
                    {
                        diffItems.Add(new VersionSyncToggleDiffItem
                        {
                            SectionName = sectionName,
                            OldKeyBindingsText = oldBindingsText,
                            NewKeyBindingsText = newBindingsText,
                            ResultKeyBindingsText = newBindingsText,
                            OldTargetValuesText = oldTargetValuesText,
                            NewTargetValuesText = newTargetValuesText,
                            ResultTargetValuesText = newTargetValuesText,
                            Status = VersionSyncDiffStatus.Unchanged
                        });
                        continue;
                    }

                    diffItems.Add(new VersionSyncToggleDiffItem
                    {
                        SectionName = sectionName,
                        OldKeyBindingsText = oldBindingsText,
                        NewKeyBindingsText = newBindingsText,
                        ResultKeyBindingsText = resultBindingsText,
                        OldTargetValuesText = oldTargetValuesText,
                        NewTargetValuesText = newTargetValuesText,
                        ResultTargetValuesText = resultTargetValuesText,
                        Status = targetMergeResult.HasOldOnlyTargets && !hasChanges
                            ? VersionSyncDiffStatus.ManualReview
                            : VersionSyncDiffStatus.Updated,
                        CanApply = hasChanges
                    });
                    continue;
                }

                diffItems.Add(new VersionSyncToggleDiffItem
                {
                    SectionName = sectionName,
                    OldKeyBindingsText = oldBindingsText,
                    NewKeyBindingsText = newBindingsText,
                    ResultKeyBindingsText = newBindingsText,
                    OldTargetValuesText = oldTargetValuesText,
                    NewTargetValuesText = newTargetValuesText,
                    ResultTargetValuesText = newTargetValuesText,
                    Status = hasOld ? VersionSyncDiffStatus.OldOnly : VersionSyncDiffStatus.NewOnly
                });
            }

            return diffItems;
        }

        private List<VersionSyncParameterDiffItem> BuildParameterDiffItems(
            ModConfigAnalysisResult oldAnalysis,
            ModConfigAnalysisResult newAnalysis,
            ref ModConfigEditBuffer resultBuffer)
        {
            var diffItems = new List<VersionSyncParameterDiffItem>();
            var oldParameters = BuildDuplicateAwareLookup(oldAnalysis.Parameters, parameter => parameter.Name);
            var newParameters = BuildDuplicateAwareLookup(newAnalysis.Parameters, parameter => parameter.Name);
            var parameterNames = oldParameters.Items.Keys
                .Union(newParameters.Items.Keys, StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var parameterName in parameterNames)
            {
                var hasOld = oldParameters.Items.TryGetValue(parameterName, out var oldParameter);
                var hasNew = newParameters.Items.TryGetValue(parameterName, out var newParameter);
                var oldDefaultValue = hasOld ? oldParameter!.DefaultValue : string.Empty;
                var newDefaultValue = hasNew ? newParameter!.DefaultValue : string.Empty;
                var hasDuplicateParameters = oldParameters.DuplicateKeys.Contains(parameterName) ||
                    newParameters.DuplicateKeys.Contains(parameterName);

                if (hasDuplicateParameters)
                {
                    diffItems.Add(new VersionSyncParameterDiffItem
                    {
                        Name = parameterName,
                        OldDefaultValue = oldDefaultValue,
                        NewDefaultValue = newDefaultValue,
                        ResultDefaultValue = newDefaultValue,
                        Status = VersionSyncDiffStatus.ManualReview,
                        CanApply = false
                    });
                    continue;
                }

                if (hasOld && hasNew)
                {
                    if (oldDefaultValue.Equals(newDefaultValue, StringComparison.OrdinalIgnoreCase))
                    {
                        diffItems.Add(new VersionSyncParameterDiffItem
                        {
                            Name = parameterName,
                            OldDefaultValue = oldDefaultValue,
                            NewDefaultValue = newDefaultValue,
                            ResultDefaultValue = newDefaultValue,
                            Status = VersionSyncDiffStatus.Unchanged
                        });
                        continue;
                    }

                    resultBuffer = _configUpdateService.UpdateParameterDefaultValue(resultBuffer, parameterName, oldDefaultValue);
                    diffItems.Add(new VersionSyncParameterDiffItem
                    {
                        Name = parameterName,
                        OldDefaultValue = oldDefaultValue,
                        NewDefaultValue = newDefaultValue,
                        ResultDefaultValue = oldDefaultValue,
                        Status = VersionSyncDiffStatus.Updated,
                        CanApply = true
                    });
                    continue;
                }

                diffItems.Add(new VersionSyncParameterDiffItem
                {
                    Name = parameterName,
                    OldDefaultValue = oldDefaultValue,
                    NewDefaultValue = newDefaultValue,
                    ResultDefaultValue = newDefaultValue,
                    Status = hasOld ? VersionSyncDiffStatus.OldOnly : VersionSyncDiffStatus.NewOnly
                });
            }

            return diffItems;
        }

        private List<VersionSyncVisibilityDiffItem> BuildVisibilityDiffItems(
            ModConfigAnalysisResult oldAnalysis,
            ModConfigAnalysisResult newAnalysis,
            ref ModConfigEditBuffer resultBuffer,
            List<VersionSyncToggleDiffItem> toggleDiffItems,
            List<VersionSyncParameterDiffItem> parameterDiffItems)
        {
            var diffItems = new List<VersionSyncVisibilityDiffItem>();
            var oldVisibilityItems = BuildDuplicateAwareLookup(oldAnalysis.VisibilityItems, BuildVisibilityKey);
            var newVisibilityItems = BuildDuplicateAwareLookup(newAnalysis.VisibilityItems, BuildVisibilityKey);
            var visibilityKeys = oldVisibilityItems.Items.Keys
                .Union(newVisibilityItems.Items.Keys, StringComparer.OrdinalIgnoreCase)
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var visibilityKey in visibilityKeys)
            {
                oldVisibilityItems.Items.TryGetValue(visibilityKey, out var oldVisibilityItem);
                var currentAnalysis = AnalyzeBuffer(resultBuffer);
                var currentVisibilityItems = BuildDuplicateAwareLookup(currentAnalysis.VisibilityItems, BuildVisibilityKey);
                currentVisibilityItems.Items.TryGetValue(visibilityKey, out var currentVisibilityItem);
                var hasOldDuplicateVisibility = oldVisibilityItems.DuplicateKeys.Contains(visibilityKey);
                var hasNewDuplicateVisibility = newVisibilityItems.DuplicateKeys.Contains(visibilityKey) ||
                    currentVisibilityItems.DuplicateKeys.Contains(visibilityKey);

                if (hasOldDuplicateVisibility || hasNewDuplicateVisibility)
                {
                    diffItems.Add(new VersionSyncVisibilityDiffItem
                    {
                        VisibilityKey = visibilityKey,
                        DisplayText = oldVisibilityItem != null
                            ? BuildVisibilityDisplayText(oldVisibilityItem)
                            : currentVisibilityItem != null
                                ? BuildVisibilityDisplayText(currentVisibilityItem)
                                : visibilityKey,
                        OldBindingText = oldVisibilityItem == null ? string.Empty : FormatVisibilityBinding(oldVisibilityItem),
                        NewBindingText = currentVisibilityItem == null ? string.Empty : FormatVisibilityBinding(currentVisibilityItem),
                        ResultBindingText = currentVisibilityItem == null ? string.Empty : FormatVisibilityBinding(currentVisibilityItem),
                        Status = VersionSyncDiffStatus.ManualReview,
                        Detail = BuildDuplicateVisibilityDetail(hasOldDuplicateVisibility, hasNewDuplicateVisibility)
                    });
                    continue;
                }

                if (oldVisibilityItem != null && currentVisibilityItem != null)
                {
                    var oldBindingText = FormatVisibilityBinding(oldVisibilityItem);
                    var newBindingText = FormatVisibilityBinding(currentVisibilityItem);

                    if (HasSingleParameterBinding(oldVisibilityItem) && IsUnboundVisibility(currentVisibilityItem))
                    {
                        var outcome = TryApplyMissingVisibilityBinding(
                            oldAnalysis,
                            oldVisibilityItem,
                            currentVisibilityItem,
                            currentAnalysis,
                            resultBuffer);
                        if (outcome.Success)
                        {
                            resultBuffer = outcome.Buffer;
                            var resultAnalysis = AnalyzeBuffer(resultBuffer);
                            var resultVisibilityItem = resultAnalysis.VisibilityItems.FirstOrDefault(item =>
                                BuildVisibilityKey(item).Equals(visibilityKey, StringComparison.OrdinalIgnoreCase));

                            UpdateRelatedDiffItems(
                                oldAnalysis,
                                outcome.VariableName,
                                outcome.CreatedToggle,
                                outcome.CreatedParameter,
                                resultAnalysis,
                                toggleDiffItems,
                                parameterDiffItems);

                            diffItems.Add(new VersionSyncVisibilityDiffItem
                            {
                                VisibilityKey = visibilityKey,
                                DisplayText = BuildVisibilityDisplayText(currentVisibilityItem),
                                OldBindingText = oldBindingText,
                                NewBindingText = newBindingText,
                                ResultBindingText = resultVisibilityItem == null ? newBindingText : FormatVisibilityBinding(resultVisibilityItem),
                                Status = VersionSyncDiffStatus.Created,
                                CanApply = true,
                                TargetSectionName = currentVisibilityItem.SectionName,
                                TargetDrawLabel = JoinValues(currentVisibilityItem.DrawLabels),
                                VariableName = outcome.VariableName,
                                ResultDefaultValue = oldAnalysis.Parameters
                                    .SingleOrDefault(parameter => parameter.Name.Equals(outcome.VariableName, StringComparison.OrdinalIgnoreCase))?.DefaultValue ?? string.Empty,
                                ResultKeyBindingsText = JoinValues(
                                    oldAnalysis.Parameters
                                        .SingleOrDefault(parameter => parameter.Name.Equals(outcome.VariableName, StringComparison.OrdinalIgnoreCase))?
                                        .KeyBindings
                                        .Cast<string>()
                                    ?? Enumerable.Empty<string>()),
                                Detail = outcome.Detail
                            });
                            continue;
                        }

                        diffItems.Add(new VersionSyncVisibilityDiffItem
                        {
                            VisibilityKey = visibilityKey,
                            DisplayText = BuildVisibilityDisplayText(currentVisibilityItem),
                            OldBindingText = oldBindingText,
                            NewBindingText = newBindingText,
                            ResultBindingText = newBindingText,
                            Status = VersionSyncDiffStatus.ManualReview,
                            Detail = outcome.Detail
                        });
                        continue;
                    }

                    diffItems.Add(new VersionSyncVisibilityDiffItem
                    {
                        VisibilityKey = visibilityKey,
                        DisplayText = BuildVisibilityDisplayText(currentVisibilityItem),
                        OldBindingText = oldBindingText,
                        NewBindingText = newBindingText,
                        ResultBindingText = newBindingText,
                        Status = oldBindingText.Equals(newBindingText, StringComparison.OrdinalIgnoreCase)
                            ? VersionSyncDiffStatus.Unchanged
                            : VersionSyncDiffStatus.ManualReview,
                        Detail = oldBindingText.Equals(newBindingText, StringComparison.OrdinalIgnoreCase)
                            ? string.Empty
                            : "绑定关系已存在差异，需要手工确认。"
                    });
                    continue;
                }

                diffItems.Add(new VersionSyncVisibilityDiffItem
                {
                    VisibilityKey = visibilityKey,
                    DisplayText = oldVisibilityItem != null
                        ? BuildVisibilityDisplayText(oldVisibilityItem)
                        : BuildVisibilityDisplayText(currentVisibilityItem!),
                    OldBindingText = oldVisibilityItem == null ? string.Empty : FormatVisibilityBinding(oldVisibilityItem),
                    NewBindingText = currentVisibilityItem == null ? string.Empty : FormatVisibilityBinding(currentVisibilityItem),
                    ResultBindingText = currentVisibilityItem == null ? string.Empty : FormatVisibilityBinding(currentVisibilityItem),
                    Status = oldVisibilityItem != null ? VersionSyncDiffStatus.OldOnly : VersionSyncDiffStatus.NewOnly
                });
            }

            return diffItems;
        }

        private VisibilitySyncOutcome TryApplyMissingVisibilityBinding(
            ModConfigAnalysisResult oldAnalysis,
            ModVisibilityItem oldVisibilityItem,
            ModVisibilityItem currentVisibilityItem,
            ModConfigAnalysisResult currentAnalysis,
            ModConfigEditBuffer currentBuffer)
        {
            var variableName = oldVisibilityItem.ControllingParameters[0];
            var oldParameter = oldAnalysis.Parameters.SingleOrDefault(parameter =>
                parameter.Name.Equals(variableName, StringComparison.OrdinalIgnoreCase));
            if (oldParameter == null || !oldParameter.IsDeclaredInConstants)
            {
                return VisibilitySyncOutcome.Fail("旧配置中的控制参数不可安全迁移。", variableName);
            }

            var oldKeyBindings = oldParameter.KeyBindings
                .Where(binding => !string.IsNullOrWhiteSpace(binding))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var drawLabel = JoinValues(currentVisibilityItem.DrawLabels);

            try
            {
                var workingBuffer = currentBuffer;
                var existingParameter = currentAnalysis.Parameters.SingleOrDefault(parameter =>
                    parameter.Name.Equals(variableName, StringComparison.OrdinalIgnoreCase));
                var createdParameter = false;
                var createdToggle = false;

                if (existingParameter == null)
                {
                    if (oldKeyBindings.Count == 0)
                    {
                        return VisibilitySyncOutcome.Fail("旧配置中的模型绑定没有可迁移的快捷键。", variableName);
                    }

                    workingBuffer = _configUpdateService.CreateVisibilityBinding(
                        workingBuffer,
                        currentVisibilityItem.SectionName,
                        drawLabel,
                        variableName,
                        oldKeyBindings);
                    createdParameter = true;
                    createdToggle = true;
                }
                else
                {
                    if (existingParameter.KeyBindings.Count == 0)
                    {
                        if (oldKeyBindings.Count == 0)
                        {
                            return VisibilitySyncOutcome.Fail("新版参数存在，但旧配置中没有可迁移的快捷键。", variableName);
                        }

                        workingBuffer = _configUpdateService.CreateToggleBinding(workingBuffer, variableName, oldKeyBindings);
                        createdToggle = true;
                    }

                    workingBuffer = _configUpdateService.BindVisibilityToParameter(
                        workingBuffer,
                        currentVisibilityItem.SectionName,
                        drawLabel,
                        variableName);
                }

                if (!string.IsNullOrWhiteSpace(oldParameter.DefaultValue))
                {
                    workingBuffer = _configUpdateService.UpdateParameterDefaultValue(
                        workingBuffer,
                        variableName,
                        oldParameter.DefaultValue);
                }

                return VisibilitySyncOutcome.SuccessResult(
                    workingBuffer,
                    variableName,
                    createdParameter,
                    createdToggle,
                    createdParameter || createdToggle ? "已在新版配置中补建模型控制绑定。" : "已将旧模型绑定迁移到新版配置。");
            }
            catch (Exception ex)
            {
                return VisibilitySyncOutcome.Fail(ex.Message, variableName);
            }
        }

        private static void UpdateRelatedDiffItems(
            ModConfigAnalysisResult oldAnalysis,
            string variableName,
            bool createdToggle,
            bool createdParameter,
            ModConfigAnalysisResult resultAnalysis,
            List<VersionSyncToggleDiffItem> toggleDiffItems,
            List<VersionSyncParameterDiffItem> parameterDiffItems)
        {
            var oldParameter = oldAnalysis.Parameters.SingleOrDefault(parameter =>
                parameter.Name.Equals(variableName, StringComparison.OrdinalIgnoreCase));
            var resultParameter = resultAnalysis.Parameters.SingleOrDefault(parameter =>
                parameter.Name.Equals(variableName, StringComparison.OrdinalIgnoreCase));

            if (resultParameter != null)
            {
                var parameterDiffItem = parameterDiffItems.FirstOrDefault(item =>
                    item.Name.Equals(variableName, StringComparison.OrdinalIgnoreCase));
                if (parameterDiffItem == null)
                {
                    parameterDiffItems.Add(new VersionSyncParameterDiffItem
                    {
                        Name = variableName,
                        OldDefaultValue = oldParameter?.DefaultValue ?? string.Empty,
                        ResultDefaultValue = resultParameter.DefaultValue,
                        Status = VersionSyncDiffStatus.Created,
                        CanApply = true
                    });
                }
                else if (createdParameter || parameterDiffItem.Status == VersionSyncDiffStatus.OldOnly)
                {
                    parameterDiffItem.ResultDefaultValue = resultParameter.DefaultValue;
                    parameterDiffItem.Status = VersionSyncDiffStatus.Created;
                    parameterDiffItem.CanApply = true;
                }
            }

            if (!createdToggle)
            {
                return;
            }

            if (oldParameter != null)
            {
                toggleDiffItems.RemoveAll(item =>
                    item.Status == VersionSyncDiffStatus.OldOnly &&
                    oldParameter.BoundKeySections.Any(section => section.Equals(item.SectionName, StringComparison.OrdinalIgnoreCase)));
            }

            var resultToggle = resultAnalysis.Toggles.FirstOrDefault(toggle =>
                toggle.Targets.Count == 1 &&
                toggle.Targets[0].VariableName.Equals(variableName, StringComparison.OrdinalIgnoreCase));
            if (resultToggle == null)
            {
                return;
            }

            if (toggleDiffItems.Any(item => item.SectionName.Equals(resultToggle.SectionName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            toggleDiffItems.Add(new VersionSyncToggleDiffItem
            {
                SectionName = resultToggle.SectionName,
                OldKeyBindingsText = oldParameter == null ? string.Empty : JoinValues(oldParameter.KeyBindings),
                ResultKeyBindingsText = JoinValues(resultToggle.KeyBindings),
                OldTargetValuesText = string.Empty,
                ResultTargetValuesText = BuildToggleTargetValuesText(resultToggle.Targets),
                Status = VersionSyncDiffStatus.Created,
                CanApply = true
            });
        }

        private ModConfigAnalysisResult AnalyzeBuffer(ModConfigEditBuffer buffer)
        {
            return _configAnalysisService.Analyze(_configParser.Parse(buffer.Content));
        }

        private IReadOnlyList<string> DiscoverCandidateDirectories(string modRootPath)
        {
            var candidateDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            TryAddCandidateDirectory(candidateDirectories, modRootPath);

            var directDirectories = GetDirectoriesSafe(modRootPath)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var directory in directDirectories)
            {
                TryAddCandidateDirectory(candidateDirectories, directory);
            }

            foreach (var directory in directDirectories)
            {
                foreach (var nestedDirectory in GetDirectoriesSafe(directory).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    TryAddCandidateDirectory(candidateDirectories, nestedDirectory);
                }
            }

            return candidateDirectories
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private IEnumerable<VersionSyncFolderCandidate> CreateCandidates(string modDirectory)
        {
            if (!ModPathHelper.TryParseModDirectoryPath(modDirectory, out var characterName, out var id, out var modName))
            {
                return Array.Empty<VersionSyncFolderCandidate>();
            }

            var configPaths = _configDiscoveryService.GetConfigCandidates(modDirectory);
            if (configPaths.Count == 0)
            {
                return Array.Empty<VersionSyncFolderCandidate>();
            }

            return configPaths.Select(configPath => new VersionSyncFolderCandidate
            {
                CharacterName = characterName,
                Id = id,
                ModName = modName,
                FolderName = Path.GetFileName(modDirectory),
                FullPath = modDirectory,
                ConfigPath = configPath,
                ConfigRelativePath = Path.GetRelativePath(modDirectory, configPath),
                NormalizedNameKey = ModPathHelper.NormalizeVersionSyncKey(modName)
            });
        }

        private List<VersionSyncPairingJob> CreatePairingJobsWithinCharacterGroup(
            IReadOnlyList<VersionSyncFolderCandidate> oldCandidates,
            IReadOnlyList<VersionSyncFolderCandidate> newCandidates)
        {
            var jobs = new List<VersionSyncPairingJob>();
            var remainingOldCandidates = oldCandidates
                .OrderBy(candidate => candidate.FolderName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var remainingNewCandidates = newCandidates
                .OrderBy(candidate => candidate.FolderName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var sequence = 1;

            var exactKeys = remainingOldCandidates.Select(candidate => candidate.NormalizedNameKey)
                .Intersect(remainingNewCandidates.Select(candidate => candidate.NormalizedNameKey), StringComparer.OrdinalIgnoreCase)
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var exactKey in exactKeys)
            {
                var exactOldCandidates = remainingOldCandidates
                    .Where(candidate => candidate.NormalizedNameKey.Equals(exactKey, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var exactNewCandidates = remainingNewCandidates
                    .Where(candidate => candidate.NormalizedNameKey.Equals(exactKey, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (IsSameModMultiVariant(exactOldCandidates.Concat(exactNewCandidates).ToList()))
                {
                    remainingOldCandidates.RemoveAll(candidate => candidate.NormalizedNameKey.Equals(exactKey, StringComparison.OrdinalIgnoreCase));
                    remainingNewCandidates.RemoveAll(candidate => candidate.NormalizedNameKey.Equals(exactKey, StringComparison.OrdinalIgnoreCase));
                    continue;
                }

                jobs.AddRange(CreateJobsForMatchedCandidates(exactOldCandidates, exactNewCandidates, ref sequence));
                remainingOldCandidates.RemoveAll(candidate => candidate.NormalizedNameKey.Equals(exactKey, StringComparison.OrdinalIgnoreCase));
                remainingNewCandidates.RemoveAll(candidate => candidate.NormalizedNameKey.Equals(exactKey, StringComparison.OrdinalIgnoreCase));
            }

            jobs.AddRange(CreateFallbackPairingJobs(remainingOldCandidates, remainingNewCandidates, ref sequence));
            return jobs;
        }

        private List<VersionSyncPairingJob> CreateSingleDirectoryPairingJobsWithinGroup(
            IReadOnlyList<VersionSyncFolderCandidate> candidates)
        {
            var jobs = new List<VersionSyncPairingJob>();
            var remainingCandidates = candidates
                .Where(candidate => candidate != null &&
                    !string.IsNullOrWhiteSpace(candidate.FullPath) &&
                    !string.IsNullOrWhiteSpace(candidate.NormalizedNameKey))
                .OrderBy(candidate => candidate.FolderName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.ConfigRelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var sequence = 1;

            foreach (var exactGroup in remainingCandidates
                         .GroupBy(candidate => candidate.NormalizedNameKey, StringComparer.OrdinalIgnoreCase)
                         .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                         .ToList())
            {
                if (exactGroup.Count() < 2)
                {
                    continue;
                }

                var groupedCandidates = exactGroup.ToList();
                if (IsSameModMultiVariant(groupedCandidates) || IsSameDirectoryMultiConfig(groupedCandidates))
                {
                    continue;
                }

                jobs.AddRange(CreateSingleDirectoryExactKeyJobs(groupedCandidates, ref sequence));

                var groupedPaths = groupedCandidates
                    .Select(GetCandidateIdentityKey)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                remainingCandidates.RemoveAll(candidate => groupedPaths.Contains(GetCandidateIdentityKey(candidate)));
            }

            jobs.AddRange(CreateSingleDirectoryFallbackJobs(remainingCandidates, ref sequence));
            return jobs;
        }

        private List<VersionSyncPairingJob> CreateSingleDirectoryExactKeyJobs(
            IReadOnlyList<VersionSyncFolderCandidate> candidates,
            ref int sequence)
        {
            var jobs = new List<VersionSyncPairingJob>();
            if (candidates.Count < 2)
            {
                return jobs;
            }

            var templateCandidate = candidates.Aggregate((bestCandidate, currentCandidate) =>
                CompareSingleDirectoryCandidateRecency(currentCandidate, bestCandidate) > 0
                    ? currentCandidate
                    : bestCandidate);
            var oldCandidates = candidates
                .Where(candidate => !GetCandidateIdentityKey(candidate).Equals(GetCandidateIdentityKey(templateCandidate), StringComparison.OrdinalIgnoreCase))
                .OrderBy(candidate => candidate, SingleDirectoryOldCandidateComparer.Instance)
                    .ToList();

            if (oldCandidates.Count == 1)
            {
                var oldCandidate = oldCandidates[0];
                jobs.Add(CreateJob(
                    oldCandidate,
                    templateCandidate,
                    VersionSyncJobKind.DirectUpdate,
                    templateCandidate.FullPath,
                    sequence));
                sequence++;
                return jobs;
            }

            foreach (var oldCandidate in oldCandidates)
            {
                var outputDirectoryPath = BuildCloneOutputDirectoryPath(templateCandidate, sequence);
                jobs.Add(CreateJob(
                    oldCandidate,
                    templateCandidate,
                    VersionSyncJobKind.CloneFromTemplate,
                    outputDirectoryPath,
                    sequence));
                sequence++;
            }

            return jobs;
        }

        private List<VersionSyncPairingJob> CreateSingleDirectoryFallbackJobs(
            IReadOnlyList<VersionSyncFolderCandidate> candidates,
            ref int sequence)
        {
            var jobs = new List<VersionSyncPairingJob>();
            if (candidates.Count < 2)
            {
                return jobs;
            }

            var candidatePairs = new List<CandidatePair>();
            for (var index = 0; index < candidates.Count - 1; index++)
            {
                for (var nestedIndex = index + 1; nestedIndex < candidates.Count; nestedIndex++)
                {
                    var leftCandidate = candidates[index];
                    var rightCandidate = candidates[nestedIndex];

                    if (!CanFallbackPairInSingleDirectory(leftCandidate, rightCandidate))
                    {
                        continue;
                    }

                    var recencyComparison = CompareSingleDirectoryCandidateRecency(leftCandidate, rightCandidate);
                    if (recencyComparison == 0)
                    {
                        continue;
                    }

                    var oldCandidate = recencyComparison < 0 ? leftCandidate : rightCandidate;
                    var newCandidate = recencyComparison < 0 ? rightCandidate : leftCandidate;
                    candidatePairs.Add(new CandidatePair(
                        oldCandidate,
                        newCandidate,
                        CalculatePairingScore(oldCandidate, newCandidate)));
                }
            }

            var matchedOldCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var matchedNewCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in candidatePairs
                         .OrderByDescending(item => item.Score)
                         .ThenBy(item => item.OldCandidate.FolderName, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(item => item.OldCandidate.ConfigRelativePath, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(item => item.NewCandidate.ConfigRelativePath, StringComparer.OrdinalIgnoreCase))
            {
                if (!matchedOldCandidates.Add(GetCandidateIdentityKey(pair.OldCandidate)) ||
                    !matchedNewCandidates.Add(GetCandidateIdentityKey(pair.NewCandidate)))
                {
                    continue;
                }

                jobs.Add(CreateJob(
                    pair.OldCandidate,
                    pair.NewCandidate,
                    VersionSyncJobKind.DirectUpdate,
                    pair.NewCandidate.FullPath,
                    sequence));
                sequence++;
            }

            return jobs;
        }

        private List<VersionSyncPairingJob> CreateJobsForMatchedCandidates(
            IReadOnlyList<VersionSyncFolderCandidate> oldCandidates,
            IReadOnlyList<VersionSyncFolderCandidate> newCandidates,
            ref int sequence)
        {
            var jobs = new List<VersionSyncPairingJob>();
            if (oldCandidates.Count == 0 || newCandidates.Count == 0)
            {
                return jobs;
            }

            if (newCandidates.Count == 1 && oldCandidates.Count > 1)
            {
                var templateCandidate = newCandidates[0];
                foreach (var oldCandidate in oldCandidates.OrderBy(candidate => candidate.FolderName, StringComparer.OrdinalIgnoreCase))
                {
                    var outputDirectoryPath = BuildCloneOutputDirectoryPath(templateCandidate, sequence);
                    jobs.Add(CreateJob(
                        oldCandidate,
                        templateCandidate,
                        VersionSyncJobKind.CloneFromTemplate,
                        outputDirectoryPath,
                        sequence));
                    sequence++;
                }

                return jobs;
            }

            var directPairCount = Math.Min(oldCandidates.Count, newCandidates.Count);
            var sortedOld = oldCandidates
                .OrderBy(c => c.ConfigRelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var sortedNew = newCandidates
                .OrderBy(c => c.ConfigRelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (var index = 0; index < directPairCount; index++)
            {
                jobs.Add(CreateJob(
                    sortedOld[index],
                    sortedNew[index],
                    VersionSyncJobKind.DirectUpdate,
                    sortedNew[index].FullPath,
                    sequence));
                sequence++;
            }

            // Remaining old candidates (many-to-one): clone from the first new candidate.
            for (var index = directPairCount; index < sortedOld.Count; index++)
            {
                var templateCandidate = sortedNew[0];
                var outputDirectoryPath = BuildCloneOutputDirectoryPath(templateCandidate, sequence);
                jobs.Add(CreateJob(
                    sortedOld[index],
                    templateCandidate,
                    VersionSyncJobKind.CloneFromTemplate,
                    outputDirectoryPath,
                    sequence));
                sequence++;
            }

            return jobs;
        }

        private List<VersionSyncPairingJob> CreateFallbackPairingJobs(
            IReadOnlyList<VersionSyncFolderCandidate> oldCandidates,
            IReadOnlyList<VersionSyncFolderCandidate> newCandidates,
            ref int sequence)
        {
            var jobs = new List<VersionSyncPairingJob>();
            if (oldCandidates.Count == 0 || newCandidates.Count == 0)
            {
                return jobs;
            }

            if (newCandidates.Count == 1 && oldCandidates.Count > 1)
            {
                var templateCandidate = newCandidates[0];
                var cloneCandidates = oldCandidates
                    .Select(candidate => new { Candidate = candidate, Score = CalculatePairingScore(candidate, templateCandidate) })
                    .Where(item => item.Score >= MinimumFallbackPairingScore)
                    .OrderByDescending(item => item.Score)
                    .ThenBy(item => item.Candidate.FolderName, StringComparer.OrdinalIgnoreCase)
                    .Select(item => item.Candidate)
                    .ToList();

                foreach (var oldCandidate in cloneCandidates)
                {
                    var outputDirectoryPath = BuildCloneOutputDirectoryPath(templateCandidate, sequence);
                    jobs.Add(CreateJob(
                        oldCandidate,
                        templateCandidate,
                        VersionSyncJobKind.CloneFromTemplate,
                        outputDirectoryPath,
                        sequence));
                    sequence++;
                }

                return jobs;
            }

            var candidatePairs = oldCandidates
                .SelectMany(oldCandidate => newCandidates.Select(newCandidate => new CandidatePair(
                    oldCandidate,
                    newCandidate,
                    CalculatePairingScore(oldCandidate, newCandidate))))
                .Where(pair => pair.Score >= MinimumFallbackPairingScore)
                .OrderByDescending(pair => pair.Score)
                .ThenBy(pair => pair.OldCandidate.FolderName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(pair => pair.NewCandidate.FolderName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var matchedOldCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var matchedNewCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in candidatePairs)
            {
                if (!matchedOldCandidates.Add(GetCandidateIdentityKey(pair.OldCandidate)) ||
                    !matchedNewCandidates.Add(GetCandidateIdentityKey(pair.NewCandidate)))
                {
                    continue;
                }

                jobs.Add(CreateJob(
                    pair.OldCandidate,
                    pair.NewCandidate,
                    VersionSyncJobKind.DirectUpdate,
                    pair.NewCandidate.FullPath,
                    sequence));
                sequence++;
            }

            return jobs;
        }

        /// <summary>
        /// Same-hash candidates whose ModName differs only by _vN suffix are
        /// multi-variant configs of the same mod, not version-sync pairs.
        /// </summary>
        private static bool IsSameModMultiVariant(IReadOnlyList<VersionSyncFolderCandidate> candidates)
        {
            if (candidates.Count < 2)
            {
                return false;
            }

            var firstId = candidates[0].Id;
            if (string.IsNullOrWhiteSpace(firstId) ||
                !candidates.All(candidate => string.Equals(candidate.Id, firstId, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            var versionStripped = candidates
                .Select(candidate => StripTrailingVersionSuffix(candidate.ModName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return versionStripped.Count == 1;
        }

        private static string StripTrailingVersionSuffix(string modName)
        {
            if (string.IsNullOrWhiteSpace(modName))
            {
                return string.Empty;
            }

            var updated = TrailingVersionSuffixRegex.Replace(modName, string.Empty);
            updated = TrailingVerSuffixRegex.Replace(updated, string.Empty);
            return updated.TrimEnd('_');
        }

        private static readonly Regex TrailingVersionSuffixRegex =
            new Regex(@"(?:_v\d+)+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex TrailingVerSuffixRegex =
            new Regex(@"(?:_ver\d+)+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static bool IsSameDirectoryMultiConfig(IReadOnlyList<VersionSyncFolderCandidate> candidates)
        {
            if (candidates.Count < 2)
            {
                return false;
            }

            var firstPath = candidates[0].FullPath;
            return candidates.All(c =>
                c.FullPath.Equals(firstPath, StringComparison.OrdinalIgnoreCase));
        }

        private static int CalculatePairingScore(
            VersionSyncFolderCandidate oldCandidate,
            VersionSyncFolderCandidate newCandidate)
        {
            if (!oldCandidate.CharacterName.Equals(newCandidate.CharacterName, StringComparison.OrdinalIgnoreCase) ||
                !oldCandidate.Id.Equals(newCandidate.Id, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (oldCandidate.NormalizedNameKey.Equals(newCandidate.NormalizedNameKey, StringComparison.OrdinalIgnoreCase))
            {
                return int.MaxValue;
            }

            var oldTokens = SplitNormalizedNameKey(oldCandidate.NormalizedNameKey);
            var newTokens = SplitNormalizedNameKey(newCandidate.NormalizedNameKey);
            if (oldTokens.Count == 0 || newTokens.Count == 0)
            {
                return 0;
            }

            var overlapCount = oldTokens.Intersect(newTokens, StringComparer.OrdinalIgnoreCase).Count();
            if (overlapCount == 0)
            {
                return 0;
            }

            var sharedPrefixLength = GetSharedPrefixLength(oldTokens, newTokens);
            var containsOther = oldCandidate.NormalizedNameKey.Contains(newCandidate.NormalizedNameKey, StringComparison.OrdinalIgnoreCase) ||
                newCandidate.NormalizedNameKey.Contains(oldCandidate.NormalizedNameKey, StringComparison.OrdinalIgnoreCase);
            var shorterTokenCount = Math.Min(oldTokens.Count, newTokens.Count);

            var score = overlapCount * 40 + sharedPrefixLength * 30 - Math.Abs(oldTokens.Count - newTokens.Count) * 5;
            if (containsOther)
            {
                score += 120;
            }

            if (overlapCount == shorterTokenCount)
            {
                score += 40;
            }

            return score;
        }

        private static bool HasOverlappingCandidateDirectories(
            IReadOnlyList<VersionSyncFolderCandidate> oldCandidates,
            IReadOnlyList<VersionSyncFolderCandidate> newCandidates)
        {
            var oldPaths = oldCandidates
                .Where(candidate => candidate != null && !string.IsNullOrWhiteSpace(candidate.FullPath))
                .Select(GetCandidateIdentityKey);
            var newPaths = newCandidates
                .Where(candidate => candidate != null && !string.IsNullOrWhiteSpace(candidate.FullPath))
                .Select(GetCandidateIdentityKey);

            return oldPaths.Intersect(newPaths, StringComparer.OrdinalIgnoreCase).Any();
        }

        private static string GetCandidateIdentityKey(VersionSyncFolderCandidate candidate)
        {
            if (candidate == null)
            {
                return string.Empty;
            }

            return string.Join("|", candidate.FullPath, candidate.ConfigRelativePath);
        }

        private static bool CanFallbackPairInSingleDirectory(
            VersionSyncFolderCandidate leftCandidate,
            VersionSyncFolderCandidate rightCandidate)
        {
            if (!leftCandidate.CharacterName.Equals(rightCandidate.CharacterName, StringComparison.OrdinalIgnoreCase) ||
                !leftCandidate.Id.Equals(rightCandidate.Id, StringComparison.OrdinalIgnoreCase) ||
                leftCandidate.NormalizedNameKey.Equals(rightCandidate.NormalizedNameKey, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var leftContainsRight = leftCandidate.NormalizedNameKey.Contains(rightCandidate.NormalizedNameKey, StringComparison.OrdinalIgnoreCase);
            var rightContainsLeft = rightCandidate.NormalizedNameKey.Contains(leftCandidate.NormalizedNameKey, StringComparison.OrdinalIgnoreCase);
            if (!leftContainsRight && !rightContainsLeft)
            {
                return false;
            }

            return CalculatePairingScore(leftCandidate, rightCandidate) >= MinimumFallbackPairingScore;
        }

        private static int CompareSingleDirectoryCandidateRecency(
            VersionSyncFolderCandidate leftCandidate,
            VersionSyncFolderCandidate rightCandidate)
        {
            var leftVersion = ExtractVersionNumber(leftCandidate.FolderName);
            var rightVersion = ExtractVersionNumber(rightCandidate.FolderName);
            if (leftVersion.HasValue && rightVersion.HasValue && leftVersion.Value != rightVersion.Value)
            {
                return leftVersion.Value.CompareTo(rightVersion.Value);
            }

            if (leftVersion.HasValue != rightVersion.HasValue)
            {
                return leftVersion.HasValue ? -1 : 1;
            }

            if (!leftCandidate.NormalizedNameKey.Equals(rightCandidate.NormalizedNameKey, StringComparison.OrdinalIgnoreCase))
            {
                var leftContainsRight = leftCandidate.NormalizedNameKey.Contains(rightCandidate.NormalizedNameKey, StringComparison.OrdinalIgnoreCase);
                var rightContainsLeft = rightCandidate.NormalizedNameKey.Contains(leftCandidate.NormalizedNameKey, StringComparison.OrdinalIgnoreCase);
                if (leftContainsRight != rightContainsLeft)
                {
                    return leftContainsRight ? -1 : 1;
                }
            }

            var leftWriteTimeUtc = GetCandidateLastWriteTimeUtc(leftCandidate);
            var rightWriteTimeUtc = GetCandidateLastWriteTimeUtc(rightCandidate);
            if (leftWriteTimeUtc != rightWriteTimeUtc)
            {
                return leftWriteTimeUtc.CompareTo(rightWriteTimeUtc);
            }

            if (HasVersionMarker(leftCandidate.FolderName) != HasVersionMarker(rightCandidate.FolderName))
            {
                return HasVersionMarker(leftCandidate.FolderName) ? -1 : 1;
            }

            var leftNameLength = GetCandidateNameLength(leftCandidate);
            var rightNameLength = GetCandidateNameLength(rightCandidate);
            if (leftNameLength != rightNameLength)
            {
                return rightNameLength.CompareTo(leftNameLength);
            }

            var leftHashSuffix = ExtractTrailingHashSuffix(leftCandidate.FolderName);
            var rightHashSuffix = ExtractTrailingHashSuffix(rightCandidate.FolderName);
            if (!string.IsNullOrWhiteSpace(leftHashSuffix) &&
                !string.IsNullOrWhiteSpace(rightHashSuffix) &&
                !leftHashSuffix.Equals(rightHashSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return -StringComparer.OrdinalIgnoreCase.Compare(leftHashSuffix, rightHashSuffix);
            }

            return -StringComparer.OrdinalIgnoreCase.Compare(leftCandidate.FolderName, rightCandidate.FolderName);
        }

        private static DateTime GetCandidateLastWriteTimeUtc(VersionSyncFolderCandidate candidate)
        {
            if (!string.IsNullOrWhiteSpace(candidate.ConfigPath) && File.Exists(candidate.ConfigPath))
            {
                return File.GetLastWriteTimeUtc(candidate.ConfigPath);
            }

            if (!string.IsNullOrWhiteSpace(candidate.FullPath) && Directory.Exists(candidate.FullPath))
            {
                return Directory.GetLastWriteTimeUtc(candidate.FullPath);
            }

            return DateTime.MinValue;
        }

        private static string? ExtractTrailingHashSuffix(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return null;
            }

            var tokens = folderName.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                return null;
            }

            var suffix = tokens[^1];
            return suffix.Length >= 5 && suffix.All(Uri.IsHexDigit)
                ? suffix
                : null;
        }

        private static int GetCandidateNameLength(VersionSyncFolderCandidate candidate)
        {
            return string.IsNullOrWhiteSpace(candidate.ModName)
                ? candidate.FolderName.Length
                : candidate.ModName.Length;
        }

        private static bool HasVersionMarker(string folderName)
        {
            return ExtractVersionNumber(folderName).HasValue;
        }

        private static int? ExtractVersionNumber(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return null;
            }

            foreach (var token in folderName.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (token.Length > 1 && (token[0] == 'v' || token[0] == 'V') && int.TryParse(token[1..], out var shortVersion))
                {
                    return shortVersion;
                }

                if (token.Length > 3 && token.StartsWith("ver", StringComparison.OrdinalIgnoreCase) && int.TryParse(token[3..], out var longVersion))
                {
                    return longVersion;
                }
            }

            return null;
        }

        private static List<string> SplitNormalizedNameKey(string normalizedNameKey)
        {
            return normalizedNameKey
                .Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();
        }

        private static int GetSharedPrefixLength(IReadOnlyList<string> leftTokens, IReadOnlyList<string> rightTokens)
        {
            var length = 0;
            var maxLength = Math.Min(leftTokens.Count, rightTokens.Count);
            while (length < maxLength && leftTokens[length].Equals(rightTokens[length], StringComparison.OrdinalIgnoreCase))
            {
                length++;
            }

            return length;
        }

        private void TryAddCandidateDirectory(HashSet<string> candidateDirectories, string directoryPath)
        {
            if (!_fileSystem.DirectoryExists(directoryPath))
            {
                return;
            }

            var normalizedPath = directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (ModPathHelper.TryParseModDirectoryPath(normalizedPath, out _, out _, out _))
            {
                candidateDirectories.Add(normalizedPath);
            }
        }

        private Dictionary<VersionSyncCharacterGroupKey, List<VersionSyncFolderCandidate>> GroupCandidatesByCharacterAndId(
            IReadOnlyList<VersionSyncFolderCandidate> candidates)
        {
            return candidates
                .Where(candidate => candidate != null &&
                    !string.IsNullOrWhiteSpace(candidate.CharacterName) &&
                    !string.IsNullOrWhiteSpace(candidate.Id))
                .GroupBy(candidate => new VersionSyncCharacterGroupKey(
                    candidate.CharacterName,
                    candidate.Id))
                .ToDictionary(group => group.Key, group => group.ToList());
        }

        private static bool HasSingleParameterBinding(ModVisibilityItem visibilityItem)
        {
            return visibilityItem.ControllingParameters.Count == 1;
        }

        private static bool IsUnboundVisibility(ModVisibilityItem visibilityItem)
        {
            return visibilityItem.ControllingParameters.Count == 0 &&
                visibilityItem.ControllingKeySections.Count == 0 &&
                visibilityItem.ControllingKeyBindings.Count == 0;
        }

        private static string BuildVisibilityKey(ModVisibilityItem visibilityItem)
        {
            return $"{visibilityItem.SectionName}|{JoinValues(visibilityItem.DrawLabels.Select(label => label.Trim()))}";
        }

        private static string BuildVisibilityDisplayText(ModVisibilityItem visibilityItem)
        {
            var labelsText = JoinValues(visibilityItem.DrawLabels);
            return string.IsNullOrWhiteSpace(labelsText)
                ? visibilityItem.SectionName
                : $"{visibilityItem.SectionName} / {labelsText}";
        }

        private static string FormatVisibilityBinding(ModVisibilityItem visibilityItem)
        {
            var parameterText = JoinValues(visibilityItem.ControllingParameters);
            var keyText = JoinValues(visibilityItem.ControllingKeyBindings);

            if (string.IsNullOrWhiteSpace(parameterText) && string.IsNullOrWhiteSpace(keyText))
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(keyText))
            {
                return parameterText;
            }

            if (string.IsNullOrWhiteSpace(parameterText))
            {
                return keyText;
            }

            return $"{parameterText} ({keyText})";
        }

        private static string JoinValues(IEnumerable<string> values)
        {
            return string.Join(" | ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private static string BuildToggleTargetValuesText(IEnumerable<ModToggleTarget> targets)
        {
            return JoinValues(targets.Select(target =>
                $"{target.VariableName} = {string.Join(", ", target.Values.Where(value => !string.IsNullOrWhiteSpace(value)))}"));
        }

        private static ToggleTargetMergeResult MergeToggleTargets(
            IReadOnlyList<ModToggleTarget> oldTargets,
            IReadOnlyList<ModToggleTarget> newTargets)
        {
            var oldTargetLookup = BuildDuplicateAwareLookup(oldTargets, target => target.VariableName);
            var newTargetLookup = BuildDuplicateAwareLookup(newTargets, target => target.VariableName);
            var resultTargets = new List<ModToggleTarget>();
            var changedTargets = new List<ModToggleTarget>();

            foreach (var newTarget in newTargetLookup.Items.Values)
            {
                if (!oldTargetLookup.Items.TryGetValue(newTarget.VariableName, out var oldTarget))
                {
                    resultTargets.Add(CloneTarget(newTarget));
                    continue;
                }

                var mergedValues = MergeDistinctValues(oldTarget.Values, newTarget.Values);
                var mergedTarget = new ModToggleTarget
                {
                    VariableName = newTarget.VariableName,
                    Values = mergedValues
                };

                resultTargets.Add(mergedTarget);
                if (!HaveEquivalentValues(mergedValues, newTarget.Values))
                {
                    changedTargets.Add(CloneTarget(mergedTarget));
                }
            }

            var hasOldOnlyTargets = oldTargetLookup.Items.Keys.Any(variableName => !newTargetLookup.Items.ContainsKey(variableName));

            return new ToggleTargetMergeResult(
                resultTargets,
                changedTargets,
                hasOldOnlyTargets,
                oldTargetLookup.HasDuplicates || newTargetLookup.HasDuplicates);
        }

        private static DuplicateAwareLookup<TItem> BuildDuplicateAwareLookup<TItem>(
            IEnumerable<TItem> items,
            Func<TItem, string> keySelector)
        {
            var itemLookup = new Dictionary<string, TItem>(StringComparer.OrdinalIgnoreCase);
            var duplicateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in items)
            {
                var key = keySelector(item);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (!itemLookup.TryAdd(key, item))
                {
                    duplicateKeys.Add(key);
                }
            }

            return new DuplicateAwareLookup<TItem>(itemLookup, duplicateKeys);
        }

        private static string BuildDuplicateVisibilityDetail(bool hasOldDuplicateVisibility, bool hasNewDuplicateVisibility)
        {
            if (hasOldDuplicateVisibility && hasNewDuplicateVisibility)
            {
                return "旧版和新版都存在重复的模型项键，无法自动同步，请手工确认。";
            }

            return hasOldDuplicateVisibility
                ? "旧版存在重复的模型项键，无法自动同步，请手工确认。"
                : "新版存在重复的模型项键，无法自动同步，请手工确认。";
        }

        private static List<string> MergeDistinctValues(IEnumerable<string> primaryValues, IEnumerable<string> secondaryValues)
        {
            var result = new List<string>();

            foreach (var value in primaryValues.Concat(secondaryValues))
            {
                if (string.IsNullOrWhiteSpace(value) || result.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(value.Trim());
            }

            return result;
        }

        private static bool HaveEquivalentValues(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            for (var index = 0; index < left.Count; index++)
            {
                if (!left[index].Equals(right[index], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private static ModToggleTarget CloneTarget(ModToggleTarget target)
        {
            return new ModToggleTarget
            {
                VariableName = target.VariableName,
                Values = target.Values.ToList()
            };
        }

        private IReadOnlyList<string> GetDirectoriesSafe(string directory)
        {
            try
            {
                return _fileSystem.GetDirectories(directory);
            }
            catch (IOException)
            {
                return Array.Empty<string>();
            }
            catch (UnauthorizedAccessException)
            {
                return Array.Empty<string>();
            }
        }

        private sealed record VersionSyncCharacterGroupKey(string CharacterName, string Id);

        private sealed record CandidatePair(
            VersionSyncFolderCandidate OldCandidate,
            VersionSyncFolderCandidate NewCandidate,
            int Score);

        private sealed record DuplicateAwareLookup<TItem>(
            Dictionary<string, TItem> Items,
            HashSet<string> DuplicateKeys)
        {
            public bool HasDuplicates => DuplicateKeys.Count > 0;
        }

        private sealed record ToggleTargetMergeResult(
            List<ModToggleTarget> ResultTargets,
            List<ModToggleTarget> ChangedTargets,
            bool HasOldOnlyTargets,
            bool HasDuplicateTargets);

        private sealed class SingleDirectoryOldCandidateComparer : IComparer<VersionSyncFolderCandidate>
        {
            public static SingleDirectoryOldCandidateComparer Instance { get; } = new();

            public int Compare(VersionSyncFolderCandidate? left, VersionSyncFolderCandidate? right)
            {
                if (ReferenceEquals(left, right))
                {
                    return 0;
                }

                if (left == null)
                {
                    return -1;
                }

                if (right == null)
                {
                    return 1;
                }

                var recencyComparison = CompareSingleDirectoryCandidateRecency(left, right);
                if (recencyComparison != 0)
                {
                    return recencyComparison;
                }

                return StringComparer.OrdinalIgnoreCase.Compare(left.FolderName, right.FolderName);
            }
        }

        private sealed class VisibilitySyncOutcome
        {
            public bool Success { get; private set; }
            public ModConfigEditBuffer Buffer { get; private set; } = new ModConfigEditBuffer();
            public string VariableName { get; private set; } = string.Empty;
            public bool CreatedParameter { get; private set; }
            public bool CreatedToggle { get; private set; }
            public string Detail { get; private set; } = string.Empty;

            public static VisibilitySyncOutcome SuccessResult(
                ModConfigEditBuffer buffer,
                string variableName,
                bool createdParameter,
                bool createdToggle,
                string detail)
            {
                return new VisibilitySyncOutcome
                {
                    Success = true,
                    Buffer = buffer,
                    VariableName = variableName,
                    CreatedParameter = createdParameter,
                    CreatedToggle = createdToggle,
                    Detail = detail
                };
            }

            public static VisibilitySyncOutcome Fail(string detail, string variableName)
            {
                return new VisibilitySyncOutcome
                {
                    Success = false,
                    VariableName = variableName,
                    Detail = detail
                };
            }
        }
    }
}