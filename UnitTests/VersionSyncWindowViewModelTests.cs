using System;
using System.Collections.Generic;
using System.Linq;
using WuwaModModifier.Common;
using WuwaModModifier.Model;
using WuwaModModifier.ViewModels;

namespace UnitTests
{
    public class VersionSyncWindowViewModelTests
    {
        [Fact]
        public void SelectedJobFilterMode_ShouldFilterVisibleJobsBySummaryState()
        {
            var importedRoot = @"C:\mods\Encore";
            var autoJob = CreateJob("[301]old_auto", "[301]new_auto", @"C:\mods\new\out_auto\mod.ini");
            var manualJob = CreateJob("[302]old_manual", "[302]new_manual", @"C:\mods\new\out_manual\mod.ini");
            var unchangedJob = CreateJob("[303]old_same", "[303]new_same", @"C:\mods\new\out_same\mod.ini");
            var service = CreateVersionSyncService(importedRoot, autoJob, manualJob, unchangedJob);
            service.ComparisonsByOutputPath[manualJob.OutputConfigPath] = CreateComparison(
                manualJob,
                visibilityDiffItems: new[]
                {
                    new VersionSyncVisibilityDiffItem
                    {
                        DisplayText = "Body",
                        Status = VersionSyncDiffStatus.ManualReview,
                        Detail = "manual"
                    }
                });
            service.ComparisonsByOutputPath[unchangedJob.OutputConfigPath] = CreateComparison(unchangedJob);
            var viewModel = new VersionSyncWindowViewModel(importedRoot, new TestFileSystemService(importedRoot), new TestMessageService(), service);

            Assert.Equal(3, viewModel.PairingJobs.Count);

            viewModel.SelectedJobFilterMode = VersionSyncJobFilterMode.AutoApplicableOnly;
            Assert.Single(viewModel.PairingJobs);
            Assert.Equal(autoJob.OutputConfigPath, viewModel.PairingJobs[0].OutputConfigPath);

            viewModel.SelectedJobFilterMode = VersionSyncJobFilterMode.ManualReviewOnly;
            Assert.Single(viewModel.PairingJobs);
            Assert.Equal(manualJob.OutputConfigPath, viewModel.PairingJobs[0].OutputConfigPath);

            viewModel.SelectedJobFilterMode = VersionSyncJobFilterMode.HasDifferences;
            Assert.Equal(2, viewModel.PairingJobs.Count);
            Assert.DoesNotContain(viewModel.PairingJobs, item => item.OutputConfigPath == unchangedJob.OutputConfigPath);
        }

        [Fact]
        public void ApplyAllJobsCommand_ShouldApplyEveryJobAndUpdateSummary()
        {
            var importedRoot = @"C:\mods\Encore";
            var jobA = CreateJob("[101]old_a", "[101]new_a", @"C:\mods\new\out_a\mod.ini");
            var jobB = CreateJob("[102]old_b", "[102]new_b", @"C:\mods\new\out_b\mod.ini");
            var service = CreateVersionSyncService(importedRoot, jobA, jobB);
            var fileSystem = new TestFileSystemService(importedRoot);
            var messages = new TestMessageService();
            var viewModel = new VersionSyncWindowViewModel(importedRoot, fileSystem, messages, service);
            service.BuiltOutputs.Clear();

            Assert.True(viewModel.ApplyAllJobsCommand.CanExecute(null));

            viewModel.ApplyAllJobsCommand.Execute(null);

            Assert.True(service.BuiltOutputs.Count >= 2);
            Assert.Contains(jobA.OutputConfigPath, service.BuiltOutputs);
            Assert.Contains(jobB.OutputConfigPath, service.BuiltOutputs);
            Assert.Equal(2, service.AppliedOutputs.Count);
            Assert.Contains("成功 2 个，跳过 0 个，失败 0 个", viewModel.BatchApplySummaryText);
            Assert.Contains(jobA.OutputConfigPath, viewModel.BatchApplyLogText);
            Assert.Contains(jobB.OutputConfigPath, viewModel.BatchApplyLogText);
            Assert.Contains(jobB.OutputConfigPath, viewModel.LastApplyText);
            Assert.NotNull(messages.LastInfoMessage);
            Assert.Null(messages.LastErrorMessage);
        }

        [Fact]
        public void ApplyAllJobsCommand_ShouldRespectAutoApplicableOnlyStrategy()
        {
            var importedRoot = @"C:\mods\Encore";
            var autoJob = CreateJob("[401]old_auto", "[401]new_auto", @"C:\mods\new\out_auto\mod.ini");
            var manualJob = CreateJob("[402]old_manual", "[402]new_manual", @"C:\mods\new\out_manual\mod.ini");
            var unchangedJob = CreateJob("[403]old_same", "[403]new_same", @"C:\mods\new\out_same\mod.ini");
            var service = CreateVersionSyncService(importedRoot, autoJob, manualJob, unchangedJob);
            service.ComparisonsByOutputPath[manualJob.OutputConfigPath] = CreateComparison(
                manualJob,
                visibilityDiffItems: new[]
                {
                    new VersionSyncVisibilityDiffItem
                    {
                        DisplayText = "Body",
                        Status = VersionSyncDiffStatus.ManualReview,
                        Detail = "manual"
                    }
                });
            service.ComparisonsByOutputPath[unchangedJob.OutputConfigPath] = CreateComparison(unchangedJob);
            var messages = new TestMessageService();
            var viewModel = new VersionSyncWindowViewModel(importedRoot, new TestFileSystemService(importedRoot), messages, service)
            {
                SelectedBatchApplyMode = VersionSyncBatchApplyMode.AutoApplicableOnly
            };
            service.BuiltOutputs.Clear();

            Assert.True(viewModel.ApplyAllJobsCommand.CanExecute(null));

            viewModel.ApplyAllJobsCommand.Execute(null);

            Assert.Single(service.AppliedOutputs);
            Assert.Contains(autoJob.OutputConfigPath, service.AppliedOutputs);
            Assert.Contains("成功 1 个，跳过 2 个，失败 0 个", viewModel.BatchApplySummaryText);
            Assert.Contains("跳过 | [402]old_manual -> [402]new_manual", viewModel.BatchApplyLogText);
            Assert.Contains("跳过 | [403]old_same -> [403]new_same", viewModel.BatchApplyLogText);
            Assert.Contains(autoJob.OutputConfigPath, viewModel.LastApplyText);
            Assert.NotNull(messages.LastInfoMessage);
            Assert.Null(messages.LastErrorMessage);
        }

        [Fact]
        public void ApplyAllJobsCommand_ShouldContinueAfterFailureAndReportErrors()
        {
            var importedRoot = @"C:\mods\Encore";
            var jobA = CreateJob("[201]old_a", "[201]new_a", @"C:\mods\new\out_a\mod.ini");
            var jobB = CreateJob("[202]old_b", "[202]new_b", @"C:\mods\new\out_b\mod.ini");
            var service = CreateVersionSyncService(importedRoot, jobA, jobB);
            service.FailingOutputs.Add(jobB.OutputConfigPath);
            var fileSystem = new TestFileSystemService(importedRoot);
            var messages = new TestMessageService();
            var viewModel = new VersionSyncWindowViewModel(importedRoot, fileSystem, messages, service);
            service.BuiltOutputs.Clear();

            viewModel.ApplyAllJobsCommand.Execute(null);

            Assert.True(service.BuiltOutputs.Count >= 2);
            Assert.Contains(jobA.OutputConfigPath, service.BuiltOutputs);
            Assert.Contains(jobB.OutputConfigPath, service.BuiltOutputs);
            Assert.Single(service.AppliedOutputs);
            Assert.Contains("成功 1 个，跳过 0 个，失败 1 个", viewModel.BatchApplySummaryText);
            Assert.Contains("失败", viewModel.BatchApplyLogText);
            Assert.Contains(jobA.OutputConfigPath, viewModel.LastApplyText);
            Assert.Null(messages.LastInfoMessage);
            Assert.NotNull(messages.LastErrorMessage);
        }

        [Fact]
        public void Constructor_ShouldAutoPairImportedDirectoryAndIgnoreSameFolderJobs()
        {
            var importedRoot = @"C:\mods\Encore";
            var selfJob = CreateJob("[501]same", "[501]same", @"C:\mods\Encore\[501]same\mod.ini");
            selfJob.NewCandidate.FullPath = selfJob.OldCandidate.FullPath;
            selfJob.NewCandidate.ConfigPath = selfJob.OldCandidate.ConfigPath;
            selfJob.OutputDirectoryPath = selfJob.NewCandidate.FullPath;
            selfJob.OutputConfigPath = selfJob.NewCandidate.ConfigPath;

            var pairedJob = CreateJob("[502]old", "[502]new", @"C:\mods\Encore\[502]new\mod.ini");
            var service = CreateVersionSyncService(importedRoot, selfJob, pairedJob);

            var viewModel = new VersionSyncWindowViewModel(importedRoot, new TestFileSystemService(importedRoot), new TestMessageService(), service);

            Assert.Equal(importedRoot, viewModel.ImportedModDirectoryPath);
            Assert.Equal(1, service.DiscoverCallCount);
            Assert.Equal(1, service.CreatePairingsCallCount);
            Assert.Single(viewModel.PairingJobs);
            Assert.Equal(pairedJob.OutputConfigPath, viewModel.PairingJobs[0].OutputConfigPath);
        }

        [Fact]
        public void TryAddManualPairing_ShouldReplaceConflictingJobAndSelectNewPair()
        {
            var importedRoot = @"C:\mods\Encore";
            var autoJob = CreateJob("[601]old_auto", "[601]new_auto", @"C:\mods\new\out_auto\mod.ini");
            var manualNewCandidate = CreateCandidate("[601]new_manual", @"C:\mods\new\[601]new_manual");
            var manualJob = new VersionSyncPairingJob
            {
                OldCandidate = autoJob.OldCandidate,
                NewCandidate = manualNewCandidate,
                JobKind = VersionSyncJobKind.DirectUpdate,
                OutputDirectoryPath = manualNewCandidate.FullPath,
                OutputConfigPath = $@"{manualNewCandidate.FullPath}\mod.ini",
                Sequence = 9
            };

            var service = CreateVersionSyncService(importedRoot, autoJob);
            service.CandidatesByPath[importedRoot] = new[] { autoJob.OldCandidate, autoJob.NewCandidate, manualNewCandidate };
            service.PairingJobsByCandidatePair[$"{autoJob.OldCandidate.FullPath}|{manualNewCandidate.FullPath}"] = manualJob;
            service.ComparisonsByOutputPath[manualJob.OutputConfigPath] = CreateComparison(
                manualJob,
                toggleDiffItems: new[]
                {
                    new VersionSyncToggleDiffItem
                    {
                        SectionName = "KeyA",
                        Status = VersionSyncDiffStatus.Updated,
                        CanApply = true,
                        ResultKeyBindingsText = "F3"
                    }
                });

            var viewModel = new VersionSyncWindowViewModel(importedRoot, new TestFileSystemService(importedRoot), new TestMessageService(), service);

            var added = viewModel.TryAddManualPairing(autoJob.OldCandidate.FullPath, manualNewCandidate.FullPath, out var errorMessage);

            Assert.True(added);
            Assert.Equal(string.Empty, errorMessage);
            Assert.Single(viewModel.PairingJobs);
            Assert.Equal(manualJob.OutputConfigPath, viewModel.SelectedPairingJob?.OutputConfigPath);
            Assert.Equal(manualNewCandidate.FolderName, viewModel.SelectedPairingJob?.NewFolderName);
        }

        [Fact]
        public void DeleteSelectedPairingCommand_ShouldRemoveCurrentJobAndReselectRemainingItem()
        {
            var importedRoot = @"C:\mods\Encore";
            var jobA = CreateJob("[701]old_a", "[701]new_a", @"C:\mods\new\out_a\mod.ini");
            var jobB = CreateJob("[702]old_b", "[702]new_b", @"C:\mods\new\out_b\mod.ini");
            var viewModel = new VersionSyncWindowViewModel(importedRoot, new TestFileSystemService(importedRoot), new TestMessageService(), CreateVersionSyncService(importedRoot, jobA, jobB));

            viewModel.SelectedPairingJob = viewModel.PairingJobs.First(item => item.OutputConfigPath == jobA.OutputConfigPath);
            viewModel.DeleteSelectedPairingCommand.Execute(null);

            Assert.Single(viewModel.PairingJobs);
            Assert.DoesNotContain(viewModel.PairingJobs, item => item.OutputConfigPath == jobA.OutputConfigPath);
            Assert.Equal(jobB.OutputConfigPath, viewModel.SelectedPairingJob?.OutputConfigPath);
        }

        [Fact]
        public void ApplyStructuredPreviewEditsCommand_ShouldRefreshResultTextAndApplyEditedContent()
        {
            var importedRoot = @"C:\mods\Encore";
            var job = CreateJob("[801]old", "[801]new", @"C:\mods\new\out_structured\mod.ini");
            var service = CreateVersionSyncService(importedRoot, job);
            service.ComparisonsByOutputPath[job.OutputConfigPath] = CreateComparison(
                job,
                oldConfigText: "old",
                newConfigText: "new",
                resultConfigText:
                    "[Constants]\n" +
                    "global persist $show = 2\n\n" +
                    "[Key show]\n" +
                    "condition = $object_detected\n" +
                    "key = F5\n" +
                    "type = cycle\n" +
                    "$show = 0,1\n",
                toggleDiffItems: new[]
                {
                    new VersionSyncToggleDiffItem
                    {
                        SectionName = "Key show",
                        OldKeyBindingsText = "F5",
                        NewKeyBindingsText = "F1",
                        ResultKeyBindingsText = "F5",
                        OldTargetValuesText = "$show = 0, 1, 2, 3",
                        NewTargetValuesText = "$show = 0, 1",
                        ResultTargetValuesText = "$show = 0, 1, 2, 3",
                        Status = VersionSyncDiffStatus.Updated,
                        CanApply = true
                    }
                },
                parameterDiffItems: new[]
                {
                    new VersionSyncParameterDiffItem
                    {
                        Name = "$show",
                        OldDefaultValue = "2",
                        NewDefaultValue = "0",
                        ResultDefaultValue = "2",
                        Status = VersionSyncDiffStatus.Updated,
                        CanApply = true
                    }
                });

            var viewModel = new VersionSyncWindowViewModel(
                importedRoot,
                new TestFileSystemService(importedRoot),
                new TestMessageService(),
                service,
                new ModConfigUpdateService(new TestFileSystemService(importedRoot)));

            viewModel.ToggleDiffItems[0].ResultKeyBindingsText = "F8";
            viewModel.ToggleDiffItems[0].ResultTargetValuesText = "$show = 0, 1, 2, 3, 4";
            viewModel.ParameterDiffItems[0].ResultDefaultValue = "7";

            viewModel.ApplyStructuredPreviewEditsCommand.Execute(null);
            viewModel.ApplySelectedJobCommand.Execute(null);

            Assert.Contains("key = F8", viewModel.ResultConfigText);
            Assert.Contains("$show = 0, 1, 2, 3, 4", viewModel.ResultConfigText);
            Assert.Contains("global persist $show = 7", viewModel.ResultConfigText);
            Assert.Single(service.AppliedContents);
            Assert.Equal(viewModel.ResultConfigText, service.AppliedContents[0]);
        }

        [Fact]
        public void ApplySelectedJobCommand_ShouldUseEditedResultText()
        {
            var importedRoot = @"C:\mods\Encore";
            var job = CreateJob("[901]old", "[901]new", @"C:\mods\new\out_free_text\mod.ini");
            var service = CreateVersionSyncService(importedRoot, job);
            service.ComparisonsByOutputPath[job.OutputConfigPath] = CreateComparison(
                job,
                oldConfigText: "old",
                newConfigText: "new",
                resultConfigText: "original result",
                toggleDiffItems: new[]
                {
                    new VersionSyncToggleDiffItem
                    {
                        SectionName = "KeyA",
                        Status = VersionSyncDiffStatus.Updated,
                        CanApply = true,
                        ResultKeyBindingsText = "F1"
                    }
                });
            var viewModel = new VersionSyncWindowViewModel(importedRoot, new TestFileSystemService(importedRoot), new TestMessageService(), service);

            viewModel.ResultConfigText = "custom edited result";
            viewModel.ApplySelectedJobCommand.Execute(null);

            Assert.Single(service.AppliedContents);
            Assert.Equal("custom edited result", service.AppliedContents[0]);
        }

        private static TestVersionSyncService CreateVersionSyncService(
            string importedRoot,
            params VersionSyncPairingJob[] jobs)
        {
            var service = new TestVersionSyncService();
            service.CandidatesByPath[importedRoot] = jobs
                .SelectMany(job => new[] { job.OldCandidate, job.NewCandidate })
                .GroupBy(candidate => candidate.FullPath, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();
            service.PairingJobs = jobs;

            foreach (var job in jobs)
            {
                service.ComparisonsByOutputPath[job.OutputConfigPath] = CreateComparison(
                    job,
                    toggleDiffItems: new[]
                    {
                        new VersionSyncToggleDiffItem
                        {
                            SectionName = "KeyA",
                            Status = VersionSyncDiffStatus.Updated,
                            CanApply = true
                        }
                    });
            }

            return service;
        }

        private static VersionSyncComparisonResult CreateComparison(
            VersionSyncPairingJob job,
            string oldConfigText = "old",
            string newConfigText = "new",
            string resultConfigText = "result",
            IReadOnlyList<VersionSyncToggleDiffItem>? toggleDiffItems = null,
            IReadOnlyList<VersionSyncParameterDiffItem>? parameterDiffItems = null,
            IReadOnlyList<VersionSyncVisibilityDiffItem>? visibilityDiffItems = null)
        {
            return new VersionSyncComparisonResult
            {
                Job = job,
                OldConfigText = oldConfigText,
                NewConfigText = newConfigText,
                ResultBuffer = new ModConfigEditBuffer
                {
                    Content = resultConfigText,
                    AppliedChanges = (toggleDiffItems?.Any(item => item.CanApply) == true ||
                        parameterDiffItems?.Any(item => item.CanApply) == true ||
                        visibilityDiffItems?.Any(item => item.CanApply) == true)
                        ? new List<string> { "applied" }
                        : new List<string>()
                },
                ToggleDiffItems = toggleDiffItems?.ToList() ?? new List<VersionSyncToggleDiffItem>(),
                ParameterDiffItems = parameterDiffItems?.ToList() ?? new List<VersionSyncParameterDiffItem>(),
                VisibilityDiffItems = visibilityDiffItems?.ToList() ?? new List<VersionSyncVisibilityDiffItem>()
            };
        }

        private static VersionSyncPairingJob CreateJob(string oldFolderName, string newFolderName, string outputConfigPath)
        {
            return new VersionSyncPairingJob
            {
                OldCandidate = CreateCandidate(oldFolderName, $@"C:\mods\old\{oldFolderName}"),
                NewCandidate = CreateCandidate(newFolderName, $@"C:\mods\new\{newFolderName}"),
                JobKind = VersionSyncJobKind.DirectUpdate,
                OutputDirectoryPath = System.IO.Path.GetDirectoryName(outputConfigPath) ?? string.Empty,
                OutputConfigPath = outputConfigPath,
                Sequence = 1
            };
        }

        private static VersionSyncFolderCandidate CreateCandidate(string folderName, string fullPath)
        {
            return new VersionSyncFolderCandidate
            {
                FolderName = folderName,
                FullPath = fullPath,
                ConfigPath = $@"{fullPath}\mod.ini",
                ConfigRelativePath = "mod.ini"
            };
        }

        private sealed class TestVersionSyncService : IModConfigVersionSyncService
        {
            public int DiscoverCallCount { get; private set; }

            public int CreatePairingsCallCount { get; private set; }

            public Dictionary<string, IReadOnlyList<VersionSyncFolderCandidate>> CandidatesByPath { get; } =
                new Dictionary<string, IReadOnlyList<VersionSyncFolderCandidate>>(StringComparer.OrdinalIgnoreCase);

            public IReadOnlyList<VersionSyncPairingJob> PairingJobs { get; set; } = Array.Empty<VersionSyncPairingJob>();

            public Dictionary<string, VersionSyncPairingJob> PairingJobsByCandidatePair { get; } =
                new Dictionary<string, VersionSyncPairingJob>(StringComparer.OrdinalIgnoreCase);

            public Dictionary<string, VersionSyncComparisonResult> ComparisonsByOutputPath { get; } =
                new Dictionary<string, VersionSyncComparisonResult>(StringComparer.OrdinalIgnoreCase);

            public HashSet<string> FailingOutputs { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public List<string> BuiltOutputs { get; } = new List<string>();

            public List<string> AppliedOutputs { get; } = new List<string>();

            public List<string> AppliedContents { get; } = new List<string>();

            public IReadOnlyList<VersionSyncFolderCandidate> DiscoverModCandidates(string modRootPath)
            {
                DiscoverCallCount++;
                return CandidatesByPath.TryGetValue(modRootPath, out var candidates)
                    ? candidates
                    : Array.Empty<VersionSyncFolderCandidate>();
            }

            public IReadOnlyList<VersionSyncPairingJob> CreatePairingJobs(
                IReadOnlyList<VersionSyncFolderCandidate> oldCandidates,
                IReadOnlyList<VersionSyncFolderCandidate> newCandidates)
            {
                CreatePairingsCallCount++;

                if (oldCandidates.Count == 1 && newCandidates.Count == 1)
                {
                    var pairKey = $"{oldCandidates[0].FullPath}|{newCandidates[0].FullPath}";
                    if (PairingJobsByCandidatePair.TryGetValue(pairKey, out var manualJob))
                    {
                        return new[] { manualJob };
                    }
                }

                return PairingJobs;
            }

            public VersionSyncComparisonResult BuildComparison(VersionSyncPairingJob job)
            {
                BuiltOutputs.Add(job.OutputConfigPath);
                return ComparisonsByOutputPath[job.OutputConfigPath];
            }

            public VersionSyncApplyResult ApplyComparison(VersionSyncComparisonResult comparison)
            {
                if (FailingOutputs.Contains(comparison.Job.OutputConfigPath))
                {
                    throw new InvalidOperationException($"failed: {comparison.Job.OutputConfigPath}");
                }

                AppliedOutputs.Add(comparison.Job.OutputConfigPath);
                AppliedContents.Add(comparison.ResultBuffer.Content);
                return new VersionSyncApplyResult
                {
                    Job = comparison.Job,
                    OutputDirectoryPath = comparison.Job.OutputDirectoryPath,
                    OutputConfigPath = comparison.Job.OutputConfigPath,
                    AppliedChangeCount = comparison.ResultBuffer.AppliedChanges.Count
                };
            }
        }

        private sealed class TestFileSystemService : IFileSystemService
        {
            private readonly HashSet<string> _existingDirectories;

            public TestFileSystemService(params string[] existingDirectories)
            {
                _existingDirectories = new HashSet<string>(existingDirectories, StringComparer.OrdinalIgnoreCase);
            }

            public bool DirectoryExists(string path) => _existingDirectories.Contains(path);

            public string[] GetDirectories(string path) => Array.Empty<string>();

            public bool FileExists(string path) => false;

            public string[] GetFiles(string path, string searchPattern) => Array.Empty<string>();

            public string ReadAllText(string path) => string.Empty;

            public void WriteAllText(string path, string content)
            {
            }

            public void CreateDirectory(string path)
            {
            }

            public void DeleteDirectory(string path, bool recursive)
            {
            }

            public void CopyDirectory(string source, string destination)
            {
            }
        }

        private sealed class TestMessageService : IMessageService
        {
            public string? LastInfoMessage { get; private set; }

            public string? LastErrorMessage { get; private set; }

            public string? LastConfirmationMessage { get; private set; }

            public bool ConfirmationResult { get; set; } = true;

            public void ShowInfo(string message, string? caption = null)
            {
                LastInfoMessage = message;
            }

            public void ShowError(string message, string? caption = null)
            {
                LastErrorMessage = message;
            }

            public bool Confirm(string message, string? caption = null)
            {
                LastConfirmationMessage = message;
                return ConfirmationResult;
            }
        }
    }
}