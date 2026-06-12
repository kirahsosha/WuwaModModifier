using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using WuwaModModifier.Common;
using WuwaModModifier.Model;

namespace WuwaModModifier.ViewModels
{
    public class VersionSyncWindowViewModel : ViewModelBase
    {
        private readonly IFileSystemService _fileSystem;
        private readonly IMessageService _messages;
        private readonly IModConfigUpdateService _configUpdateService;
        private readonly IModConfigVersionSyncService _versionSyncService;
        private readonly IModConfigParser _configParser;
        private readonly IModConfigAnalysisService _configAnalysisService;
        private readonly Dictionary<string, VersionSyncComparisonResult> _comparisonCache;
        private readonly List<VersionSyncFolderCandidate> _importedCandidates;

        private string _oldModRootPath;
        private string _newModRootPath;
        private string _statusText;
        private string _oldConfigText;
        private string _newConfigText;
        private string _resultConfigText;
        private string _lastAppliedOutputPath;
        private string _batchApplySummaryText;
        private string _batchApplyLogText;
        private int _candidateCount;
        private VersionSyncJobFilterMode _selectedJobFilterMode;
        private VersionSyncBatchApplyMode _selectedBatchApplyMode;
        private VersionSyncJobSummaryItem? _selectedPairingJob;
        private VersionSyncComparisonResult? _selectedComparison;
        private bool _isSynchronizingResultText;

        private readonly List<VersionSyncJobSummaryItem> _allPairingJobs;
        private readonly ObservableCollection<VersionSyncJobSummaryItem> _pairingJobs;
        private readonly ObservableCollection<VersionSyncToggleDiffItem> _toggleDiffItems;
        private readonly ObservableCollection<VersionSyncParameterDiffItem> _parameterDiffItems;
        private readonly ObservableCollection<VersionSyncVisibilityDiffItem> _visibilityDiffItems;

        public VersionSyncWindowViewModel(
            IFileSystemService fileSystem,
            IMessageService messageService,
            IModConfigVersionSyncService versionSyncService,
            IModConfigUpdateService configUpdateService,
            IModConfigParser configParser,
            IModConfigAnalysisService configAnalysisService)
        {
            _fileSystem = fileSystem;
            _messages = messageService;
            _configParser = configParser;
            _configAnalysisService = configAnalysisService;
            _configUpdateService = configUpdateService;
            _versionSyncService = versionSyncService;
            _comparisonCache = new Dictionary<string, VersionSyncComparisonResult>(StringComparer.OrdinalIgnoreCase);
            _importedCandidates = new List<VersionSyncFolderCandidate>();

            _oldModRootPath = string.Empty;
            _newModRootPath = string.Empty;
            _statusText = Properties.Resources.VersionSyncNoImport;
            _oldConfigText = string.Empty;
            _newConfigText = string.Empty;
            _resultConfigText = string.Empty;
            _lastAppliedOutputPath = string.Empty;
            _batchApplySummaryText = Properties.Resources.VersionSyncNoBatchYet;
            _batchApplyLogText = string.Empty;
            _selectedJobFilterMode = VersionSyncJobFilterMode.All;
            _selectedBatchApplyMode = VersionSyncBatchApplyMode.AllVisibleJobs;
            _allPairingJobs = new List<VersionSyncJobSummaryItem>();
            _pairingJobs = new ObservableCollection<VersionSyncJobSummaryItem>();
            _toggleDiffItems = new ObservableCollection<VersionSyncToggleDiffItem>();
            _parameterDiffItems = new ObservableCollection<VersionSyncParameterDiffItem>();
            _visibilityDiffItems = new ObservableCollection<VersionSyncVisibilityDiffItem>();

            RefreshPairingsCommand = new RelayCommand(ExecuteRefreshPairings, CanRefreshPairings);
            RefreshCurrentPairingsCommand = new RelayCommand(ExecuteRefreshCurrentPairings, CanRefreshCurrentPairings);
            ApplySelectedJobCommand = new RelayCommand(ExecuteApplySelectedJob, CanApplySelectedJob);
            ApplyAllJobsCommand = new RelayCommand(ExecuteApplyAllJobs, CanApplyAllJobs);
            DeleteSelectedPairingCommand = new RelayCommand(ExecuteDeleteSelectedPairing, CanDeleteSelectedPairing);
            SwapSelectedPairingCommand = new RelayCommand(ExecuteSwapSelectedPairing, CanSwapSelectedPairing);
            ApplyStructuredPreviewEditsCommand = new RelayCommand(ExecuteApplyStructuredPreviewEdits, CanApplyStructuredPreviewEdits);
            SaveNewConfigTextCommand = new RelayCommand(ExecuteSaveNewConfigText, CanSaveNewConfigText);
            SyncToggleDiffItemCommand = new RelayCommand<VersionSyncToggleDiffItem>(ExecuteSyncToggleDiffItem, CanSyncToggleDiffItem);
            SyncParameterDiffItemCommand = new RelayCommand<VersionSyncParameterDiffItem>(ExecuteSyncParameterDiffItem, CanSyncParameterDiffItem);
            SyncVisibilityDiffItemCommand = new RelayCommand<VersionSyncVisibilityDiffItem>(ExecuteSyncVisibilityDiffItem, CanSyncVisibilityDiffItem);
        }

        public void SetImportedDirectory(string? path)
        {
            _oldModRootPath = string.IsNullOrWhiteSpace(path) ? string.Empty : path;
            _newModRootPath = _oldModRootPath;
            _statusText = string.IsNullOrWhiteSpace(_newModRootPath)
                ? Properties.Resources.VersionSyncNoImport
                : Properties.Resources.VersionSyncImporting;

            if (CanRefreshPairings())
            {
                ExecuteRefreshPairings();
            }
        }

        public string ImportedModDirectoryPath => _newModRootPath;

        public IReadOnlyList<VersionSyncFolderCandidate> ManualPairingCandidates => _importedCandidates;

        public bool CanOpenManualPairing => _importedCandidates.Count >= 2;

        public string OldModRootPath
        {
            get => _oldModRootPath;
            set
            {
                if (SetProperty(ref _oldModRootPath, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string NewModRootPath
        {
            get => _newModRootPath;
            set
            {
                if (SetProperty(ref _newModRootPath, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        public string CandidateSummaryText =>
            $"候选目录 {_candidateCount} 个，自动配对 {PairingJobs.Count} / {_allPairingJobs.Count} 个。";

        public VersionSyncJobFilterMode SelectedJobFilterMode
        {
            get => _selectedJobFilterMode;
            set
            {
                if (SetProperty(ref _selectedJobFilterMode, value))
                {
                    ApplyPairingJobFilter(_selectedPairingJob?.OutputConfigPath);
                    OnPropertyChanged(nameof(BatchApplyModeDescriptionText));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public VersionSyncBatchApplyMode SelectedBatchApplyMode
        {
            get => _selectedBatchApplyMode;
            set
            {
                if (SetProperty(ref _selectedBatchApplyMode, value))
                {
                    OnPropertyChanged(nameof(BatchApplyModeDescriptionText));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string BatchApplyModeDescriptionText => SelectedBatchApplyMode == VersionSyncBatchApplyMode.AutoApplicableOnly
            ? "批量应用仅处理当前筛选结果中可自动应用的作业，跳过需人工确认、预览失败和无变更项。"
            : "批量应用会处理当前筛选结果中所有存在差异且可生成预览的作业。";

        public string SelectedJobSummaryText
        {
            get
            {
                if (SelectedPairingJob == null)
                {
                    return "请选择左侧作业查看按键、参数、模型和文本对比。";
                }

                var modeText = SelectedPairingJob.Job.JobKind == VersionSyncJobKind.DirectUpdate
                    ? "直接写回新版目录"
                    : "复制新版模板目录后写回";
                return $"旧版：{SelectedPairingJob.OldFolderName} | 新版：{SelectedPairingJob.NewFolderName} | 模式：{modeText} | 状态：{SelectedPairingJob.StatusText} | 输出：{SelectedPairingJob.OutputConfigPath}";
            }
        }

        public string ComparisonSummaryText
        {
            get
            {
                if (_selectedComparison == null)
                {
                    return "尚未生成差异预览。";
                }

                var toggleChanges = _toggleDiffItems.Count(item => item.Status == VersionSyncDiffStatus.Updated || item.Status == VersionSyncDiffStatus.Created);
                var parameterChanges = _parameterDiffItems.Count(item => item.Status == VersionSyncDiffStatus.Updated || item.Status == VersionSyncDiffStatus.Created);
                var visibilityChanges = _visibilityDiffItems.Count(item => item.Status == VersionSyncDiffStatus.Updated || item.Status == VersionSyncDiffStatus.Created);
                var manualReviewCount = _visibilityDiffItems.Count(item => item.Status == VersionSyncDiffStatus.ManualReview) +
                    _toggleDiffItems.Count(item => item.Status == VersionSyncDiffStatus.ManualReview) +
                    _parameterDiffItems.Count(item => item.Status == VersionSyncDiffStatus.ManualReview);

                return $"按键变更 {toggleChanges} 项，参数变更 {parameterChanges} 项，模型变更 {visibilityChanges} 项，需人工确认 {manualReviewCount} 项。";
            }
        }

        public string LastApplyText => string.IsNullOrWhiteSpace(_lastAppliedOutputPath)
            ? "尚未应用同步作业。"
            : $"最近一次输出：{_lastAppliedOutputPath}";

        public string BatchApplySummaryText
        {
            get => _batchApplySummaryText;
            private set => SetProperty(ref _batchApplySummaryText, value);
        }

        public string BatchApplyLogText
        {
            get => _batchApplyLogText;
            private set => SetProperty(ref _batchApplyLogText, value);
        }

        public string OldConfigText
        {
            get => _oldConfigText;
            private set => SetProperty(ref _oldConfigText, value);
        }

        public string NewConfigText
        {
            get => _newConfigText;
            private set => SetProperty(ref _newConfigText, value);
        }

        public string ResultConfigText
        {
            get => _resultConfigText;
            set
            {
                if (SetProperty(ref _resultConfigText, value))
                {
                    if (!_isSynchronizingResultText && _selectedComparison != null)
                    {
                        _selectedComparison.ResultBuffer.Content = value;
                    }
                }
            }
        }

        public ObservableCollection<VersionSyncJobSummaryItem> PairingJobs => _pairingJobs;

        public ObservableCollection<VersionSyncToggleDiffItem> ToggleDiffItems => _toggleDiffItems;

        public ObservableCollection<VersionSyncParameterDiffItem> ParameterDiffItems => _parameterDiffItems;

        public ObservableCollection<VersionSyncVisibilityDiffItem> VisibilityDiffItems => _visibilityDiffItems;

        public VersionSyncJobSummaryItem? SelectedPairingJob
        {
            get => _selectedPairingJob;
            set
            {
                if (SetProperty(ref _selectedPairingJob, value))
                {
                    OnPropertyChanged(nameof(SelectedJobSummaryText));
                    RefreshSelectedComparison();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ICommand RefreshPairingsCommand { get; }

        public ICommand RefreshCurrentPairingsCommand { get; }

        public ICommand ApplySelectedJobCommand { get; }

        public ICommand ApplyAllJobsCommand { get; }

        public ICommand DeleteSelectedPairingCommand { get; }

        public ICommand SwapSelectedPairingCommand { get; }

        public ICommand ApplyStructuredPreviewEditsCommand { get; }

        public ICommand SaveNewConfigTextCommand { get; }

        public ICommand SyncToggleDiffItemCommand { get; }

        public ICommand SyncParameterDiffItemCommand { get; }

        public ICommand SyncVisibilityDiffItemCommand { get; }

        private bool CanRefreshPairings()
        {
            return DirectoryExists(ImportedModDirectoryPath);
        }

        private bool CanRefreshCurrentPairings()
        {
            return _allPairingJobs.Count > 0;
        }

        private bool CanApplySelectedJob()
        {
            return _selectedComparison != null && SelectedPairingJob != null && SelectedPairingJob.HasChanges && !SelectedPairingJob.HasPreviewError;
        }

        private bool CanApplyAllJobs()
        {
            return GetBatchTargetJobs().Any();
        }

        private bool CanDeleteSelectedPairing()
        {
            return SelectedPairingJob != null;
        }

        private bool CanSwapSelectedPairing()
        {
            return SelectedPairingJob != null &&
                   SelectedPairingJob.Job.JobKind == VersionSyncJobKind.DirectUpdate;
        }

        private bool CanApplyStructuredPreviewEdits()
        {
            return _selectedComparison != null && SelectedPairingJob != null && !SelectedPairingJob.HasPreviewError;
        }

        private bool CanSaveNewConfigText()
        {
            return _selectedComparison != null && SelectedPairingJob != null && !SelectedPairingJob.HasPreviewError;
        }

        private bool CanSyncToggleDiffItem(VersionSyncToggleDiffItem? item)
        {
            return _selectedComparison != null && SelectedPairingJob != null && !SelectedPairingJob.HasPreviewError && item?.CanSyncPreview == true;
        }

        private bool CanSyncParameterDiffItem(VersionSyncParameterDiffItem? item)
        {
            return _selectedComparison != null && SelectedPairingJob != null && !SelectedPairingJob.HasPreviewError && item?.CanSyncPreview == true;
        }

        private bool CanSyncVisibilityDiffItem(VersionSyncVisibilityDiffItem? item)
        {
            return _selectedComparison != null && SelectedPairingJob != null && !SelectedPairingJob.HasPreviewError && item?.CanSyncPreview == true;
        }

        private void ExecuteRefreshPairings()
        {
            try
            {
                SelectedPairingJob = null;
                ResetBatchApplyState();

                var importedCandidates = _versionSyncService.DiscoverModCandidates(ImportedModDirectoryPath);
                _importedCandidates.Clear();
                _importedCandidates.AddRange(importedCandidates);
                _candidateCount = importedCandidates.Count;
                OnPropertyChanged(nameof(CandidateSummaryText));
                OnPropertyChanged(nameof(CanOpenManualPairing));

                var jobs = FilterDifferentDirectoryJobs(_versionSyncService.CreatePairingJobs(importedCandidates, importedCandidates));
                var previewFailureCount = RebuildPairingJobSummaries(jobs, jobs.FirstOrDefault()?.OutputConfigPath);

                if (_allPairingJobs.Count == 0)
                {
                    ClearComparison();
                    StatusText = "没有生成可用的版本同步作业，请检查当前角色目录下是否存在可比较的不同 MOD 文件夹。";
                    return;
                }

                StatusText = previewFailureCount == 0
                    ? $"已从当前角色目录生成 {_allPairingJobs.Count} 个版本同步作业，当前预览首个匹配结果。"
                    : $"已从当前角色目录生成 {_allPairingJobs.Count} 个版本同步作业，其中 {previewFailureCount} 个预览失败。";
            }
            catch (Exception ex)
            {
                ClearComparison();
                StatusText = "生成版本同步作业失败。";
                _messages.ShowError(ex.Message, "版本同步");
            }
        }

        private void ExecuteRefreshCurrentPairings()
        {
            if (_allPairingJobs.Count == 0)
            {
                StatusText = "当前没有可刷新的配对作业。";
                return;
            }

            try
            {
                var currentJobs = _allPairingJobs
                    .Select(item => item.Job)
                    .ToList();
                var previewFailureCount = RebuildPairingJobSummaries(
                    currentJobs,
                    SelectedPairingJob?.OutputConfigPath);

                StatusText = previewFailureCount == 0
                    ? $"已刷新当前 {_allPairingJobs.Count} 个配对的分析结果。"
                    : $"已刷新当前 {_allPairingJobs.Count} 个配对的分析结果，其中 {previewFailureCount} 个预览失败。";
            }
            catch (Exception ex)
            {
                StatusText = "刷新当前配对分析失败。";
                _messages.ShowError(ex.Message, "版本同步");
            }
        }

        public bool TryAddManualPairing(string oldCandidatePath, string newCandidatePath, out string errorMessage)
        {
            errorMessage = string.Empty;

            var oldCandidate = _importedCandidates.FirstOrDefault(candidate =>
                candidate.FullPath.Equals(oldCandidatePath, StringComparison.OrdinalIgnoreCase));
            var newCandidate = _importedCandidates.FirstOrDefault(candidate =>
                candidate.FullPath.Equals(newCandidatePath, StringComparison.OrdinalIgnoreCase));

            if (oldCandidate == null || newCandidate == null)
            {
                errorMessage = "未找到指定的旧版或新版目录。";
                return false;
            }

            if (oldCandidate.FullPath.Equals(newCandidate.FullPath, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "手动配对不能选择同一个目录。";
                return false;
            }

            var pairingJob = FilterDifferentDirectoryJobs(_versionSyncService.CreatePairingJobs(new[] { oldCandidate }, new[] { newCandidate }))
                .FirstOrDefault(job =>
                    job.OldCandidate.FullPath.Equals(oldCandidate.FullPath, StringComparison.OrdinalIgnoreCase) &&
                    job.NewCandidate.FullPath.Equals(newCandidate.FullPath, StringComparison.OrdinalIgnoreCase));

            if (pairingJob == null)
            {
                errorMessage = "当前选择无法生成有效的版本同步配对。";
                return false;
            }

            RemoveConflictingPairings(pairingJob);
            AddPairingJobSummary(pairingJob, pairingJob.OutputConfigPath);
            StatusText = $"已加入手动配对：{pairingJob.OldCandidate.FolderName} -> {pairingJob.NewCandidate.FolderName}";
            return true;
        }

        private static List<VersionSyncPairingJob> FilterDifferentDirectoryJobs(
            IReadOnlyList<VersionSyncPairingJob> jobs)
        {
            return jobs
                .Where(job => !job.OldCandidate.FullPath.Equals(job.NewCandidate.FullPath, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private void ExecuteApplySelectedJob()
        {
            if (_selectedComparison == null || SelectedPairingJob == null)
            {
                return;
            }

            var prompt = SelectedPairingJob.Job.JobKind == VersionSyncJobKind.DirectUpdate
                ? $"这会把同步结果直接写回新版配置。\n\n目标：{SelectedPairingJob.OutputConfigPath}\n\n是否继续？"
                : $"这会先复制新版模板目录，再把同步结果写入副本。\n\n输出：{SelectedPairingJob.Job.OutputDirectoryPath}\n\n是否继续？";

            if (!_messages.Confirm(prompt, "应用版本同步"))
            {
                return;
            }

            try
            {
                var applyResult = _versionSyncService.ApplyComparison(_selectedComparison);
                _lastAppliedOutputPath = applyResult.OutputConfigPath;
                OnPropertyChanged(nameof(LastApplyText));
                _messages.ShowInfo($"已输出到：\n{applyResult.OutputConfigPath}", "版本同步");
                RebuildPairingJobSummaries(_allPairingJobs.Select(item => item.Job).ToList(), applyResult.OutputConfigPath);
                StatusText = $"已应用同步作业，输出配置：{applyResult.OutputConfigPath}";
            }
            catch (Exception ex)
            {
                StatusText = "应用版本同步失败。";
                _messages.ShowError(ex.Message, "版本同步");
            }
        }

        private void ExecuteDeleteSelectedPairing()
        {
            if (SelectedPairingJob == null)
            {
                return;
            }

            var jobToRemove = SelectedPairingJob;
            if (!_messages.Confirm(
                    $"是否删除当前配对？\n\n{BuildJobDisplayName(jobToRemove)}\n{jobToRemove.OutputConfigPath}",
                    "删除配对"))
            {
                return;
            }

            _comparisonCache.Remove(jobToRemove.OutputConfigPath);
            _allPairingJobs.RemoveAll(item =>
                item.OutputConfigPath.Equals(jobToRemove.OutputConfigPath, StringComparison.OrdinalIgnoreCase));

            if (_allPairingJobs.Count == 0)
            {
                ClearComparison();
                StatusText = "已删除当前配对，列表中已无剩余作业。";
                return;
            }

            ApplyPairingJobFilter();
            StatusText = $"已删除配对：{BuildJobDisplayName(jobToRemove)}";
        }

        private void ExecuteSwapSelectedPairing()
        {
            if (SelectedPairingJob == null)
            {
                return;
            }

            var job = SelectedPairingJob.Job;
            var oldName = job.OldCandidate.FolderName;
            var newName = job.NewCandidate.FolderName;

            // Swap old and new candidates.
            (job.OldCandidate, job.NewCandidate) = (job.NewCandidate, job.OldCandidate);

            // Update output paths to point to the new NewCandidate.
            job.OutputDirectoryPath = job.NewCandidate.FullPath;
            job.OutputConfigPath = Path.Combine(
                job.NewCandidate.FullPath,
                job.NewCandidate.ConfigRelativePath);

            // Notify UI that computed properties (OldFolderName, NewFolderName, etc.) have changed.
            SelectedPairingJob.NotifyJobSwapped();

            // Clear cached comparison so it gets rebuilt on next selection.
            _comparisonCache.Remove(SelectedPairingJob.OutputConfigPath);

            // Refresh the pairing job summaries to rebuild previews.
            var previewFailureCount = RebuildPairingJobSummaries(
                _allPairingJobs.Select(item => item.Job).ToList(),
                SelectedPairingJob?.OutputConfigPath);

            StatusText = previewFailureCount == 0
                ? $"已交换配对：{newName}（旧版）→ {oldName}（新版）"
                : $"已交换配对：{newName}（旧版）→ {oldName}（新版），部分预览刷新失败。";
        }

        private void ExecuteApplyStructuredPreviewEdits()
        {
            if (_selectedComparison == null)
            {
                return;
            }

            try
            {
                var workingBuffer = CreateWorkingResultBuffer();

                foreach (var toggleItem in _toggleDiffItems.Where(item => item.CanApply))
                {
                    workingBuffer = ApplyToggleDiffItem(workingBuffer, toggleItem);
                }

                foreach (var parameterItem in _parameterDiffItems.Where(item => item.CanApply))
                {
                    workingBuffer = ApplyParameterDiffItem(workingBuffer, parameterItem);
                }

                CommitNewConfigBaseline(
                    workingBuffer,
                    "已将结构化编辑和同步预览中的手工修改提交到新版文本，并刷新对比预览。");
            }
            catch (Exception ex)
            {
                StatusText = "结构化编辑同步失败。";
                _messages.ShowError(ex.Message, "版本同步");
            }
        }

        private void ExecuteSyncToggleDiffItem(VersionSyncToggleDiffItem? item)
        {
            if (item == null)
            {
                return;
            }

            ExecuteSingleItemSync(
                buffer => ApplyToggleDiffItem(buffer, item),
                $"已将按键项 {item.SectionName} 的变更提交到新版文本，并刷新对比预览。",
                "同步按键项失败。");
        }

        private void ExecuteSyncParameterDiffItem(VersionSyncParameterDiffItem? item)
        {
            if (item == null)
            {
                return;
            }

            ExecuteSingleItemSync(
                buffer => ApplyParameterDiffItem(buffer, item),
                $"已将参数 {item.Name} 的变更提交到新版文本，并刷新对比预览。",
                "同步参数项失败。");
        }

        private void ExecuteSyncVisibilityDiffItem(VersionSyncVisibilityDiffItem? item)
        {
            if (item == null)
            {
                return;
            }

            ExecuteSingleItemSync(
                buffer => ApplyVisibilityDiffItem(buffer, item),
                $"已将模型项 {item.DisplayText} 的变更提交到新版文本，并刷新对比预览。",
                "同步模型项失败。");
        }

        private void ExecuteSaveNewConfigText()
        {
            if (_selectedComparison == null || SelectedPairingJob == null)
            {
                return;
            }

            var targetPath = SelectedPairingJob.Job.NewCandidate.ConfigPath;
            var prompt = SelectedPairingJob.Job.JobKind == VersionSyncJobKind.CloneFromTemplate
                ? $"这会把当前窗口中的新版原始文本直接写回新版模板配置。\n\n目标：{targetPath}\n\n后续基于该模板重新生成的作业预览也会受影响。\n\n是否继续？"
                : $"这会把当前窗口中的新版原始文本直接写回新版配置。\n\n目标：{targetPath}\n\n是否继续？";

            if (!_messages.Confirm(prompt, "保存新版文本"))
            {
                return;
            }

            try
            {
                var saveResult = _configUpdateService.SaveBuffer(CreateWorkingNewBuffer(), targetPath);
                StatusText = $"已保存新版原始文本：{saveResult.TargetPath}";
                _messages.ShowInfo($"已保存到：\n{saveResult.TargetPath}", "版本同步");
            }
            catch (Exception ex)
            {
                StatusText = "保存新版原始文本失败。";
                _messages.ShowError(ex.Message, "版本同步");
            }
        }

        private void ExecuteApplyAllJobs()
        {
            if (_pairingJobs.Count == 0)
            {
                return;
            }

            var targetJobs = GetBatchTargetJobs().ToList();
            var skippedJobs = _pairingJobs.Except(targetJobs).ToList();
            if (targetJobs.Count == 0)
            {
                BatchApplySummaryText = $"批量应用未执行：当前筛选结果中 {skippedJobs.Count} 个作业全部被策略跳过。";
                BatchApplyLogText = string.Join(Environment.NewLine, skippedJobs.Select(job =>
                    $"跳过 | {BuildJobDisplayName(job)} | {BuildSkipReason(job)}"));
                StatusText = "当前筛选结果没有可批量应用的作业。";
                return;
            }

            if (!_messages.Confirm(
                    $"当前筛选结果共有 {_pairingJobs.Count} 个作业，将按策略执行 {targetJobs.Count} 个，跳过 {skippedJobs.Count} 个。\n\n是否继续？",
                    "批量应用版本同步"))
            {
                return;
            }

            var successCount = 0;
            var failureCount = 0;
            var logLines = skippedJobs
                .Select(job => $"跳过 | {BuildJobDisplayName(job)} | {BuildSkipReason(job)}")
                .ToList();
            var lastOutputPath = string.Empty;

            foreach (var job in targetJobs)
            {
                try
                {
                    if (!TryGetComparison(job.Job, out var comparison, out var errorMessage))
                    {
                        throw new InvalidOperationException(errorMessage);
                    }

                    var applyResult = _versionSyncService.ApplyComparison(comparison);
                    successCount++;
                    lastOutputPath = applyResult.OutputConfigPath;
                    logLines.Add($"成功 | {BuildJobDisplayName(job)} | {applyResult.OutputConfigPath}");
                }
                catch (Exception ex)
                {
                    failureCount++;
                    logLines.Add($"失败 | {BuildJobDisplayName(job)} | {ex.Message}");
                }
            }

            _lastAppliedOutputPath = successCount > 0 ? lastOutputPath : string.Empty;
            OnPropertyChanged(nameof(LastApplyText));
            BatchApplySummaryText = $"批量应用完成：成功 {successCount} 个，跳过 {skippedJobs.Count} 个，失败 {failureCount} 个。";
            BatchApplyLogText = string.Join(Environment.NewLine, logLines);

            if (successCount > 0)
            {
                RebuildPairingJobSummaries(_allPairingJobs.Select(item => item.Job).ToList(), SelectedPairingJob?.OutputConfigPath);
            }

            StatusText = failureCount == 0
                ? $"已批量应用 {successCount} 个同步作业，跳过 {skippedJobs.Count} 个。"
                : $"批量应用已完成：成功 {successCount} 个，跳过 {skippedJobs.Count} 个，失败 {failureCount} 个。";

            if (failureCount == 0)
            {
                _messages.ShowInfo($"批量应用完成，共成功 {successCount} 个，跳过 {skippedJobs.Count} 个。", "版本同步");
            }
            else
            {
                _messages.ShowError($"批量应用已完成：成功 {successCount} 个，跳过 {skippedJobs.Count} 个，失败 {failureCount} 个。请查看下方执行记录。", "版本同步");
            }
        }

        private void RefreshSelectedComparison()
        {
            if (SelectedPairingJob == null)
            {
                ClearComparison();
                return;
            }

            if (SelectedPairingJob.HasPreviewError && !_comparisonCache.ContainsKey(SelectedPairingJob.OutputConfigPath))
            {
                ClearComparison();
                StatusText = $"当前作业预览失败：{SelectedPairingJob.PreviewErrorMessage}";
                return;
            }

            try
            {
                if (!TryGetComparison(SelectedPairingJob.Job, out var comparison, out var errorMessage))
                {
                    throw new InvalidOperationException(errorMessage);
                }

                ApplyComparisonToCurrentState(comparison);
                StatusText = $"已生成当前作业的差异预览。{ComparisonSummaryText}";
            }
            catch (Exception ex)
            {
                SelectedPairingJob.ApplyPreviewError(ex.Message);
                ClearComparison();
                StatusText = "生成当前作业的差异预览失败。";
                _messages.ShowError(ex.Message, "版本同步");
            }
        }

        private void ClearComparison()
        {
            _selectedComparison = null;
            OldConfigText = string.Empty;
            NewConfigText = string.Empty;
            SetResultConfigTextWithoutResync(string.Empty);
            ReplaceCollection(_toggleDiffItems, Array.Empty<VersionSyncToggleDiffItem>());
            ReplaceCollection(_parameterDiffItems, Array.Empty<VersionSyncParameterDiffItem>());
            ReplaceCollection(_visibilityDiffItems, Array.Empty<VersionSyncVisibilityDiffItem>());
            OnPropertyChanged(nameof(ComparisonSummaryText));
            CommandManager.InvalidateRequerySuggested();
        }

        private bool DirectoryExists(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && _fileSystem.DirectoryExists(path);
        }

        private void ResetBatchApplyState()
        {
            _lastAppliedOutputPath = string.Empty;
            BatchApplySummaryText = "尚未批量应用作业。";
            BatchApplyLogText = string.Empty;
            OnPropertyChanged(nameof(LastApplyText));
        }

        private int RebuildPairingJobSummaries(IReadOnlyList<VersionSyncPairingJob> jobs, string? preferredSelectedOutputPath = null)
        {
            _comparisonCache.Clear();
            _allPairingJobs.Clear();

            var previewFailureCount = 0;
            foreach (var job in jobs)
            {
                if (TryGetComparison(job, out var comparison, out var errorMessage))
                {
                    _allPairingJobs.Add(VersionSyncJobSummaryItem.FromComparison(comparison));
                }
                else
                {
                    previewFailureCount++;
                    _allPairingJobs.Add(VersionSyncJobSummaryItem.FromPreviewError(job, errorMessage));
                }
            }

            ApplyPairingJobFilter(preferredSelectedOutputPath);
            CommandManager.InvalidateRequerySuggested();
            return previewFailureCount;
        }

        private void ApplyPairingJobFilter(string? preferredSelectedOutputPath = null)
        {
            var filteredJobs = _allPairingJobs.Where(MatchesSelectedJobFilter).ToList();
            ReplaceCollection(_pairingJobs, filteredJobs);
            OnPropertyChanged(nameof(CandidateSummaryText));

            var desiredOutputPath = preferredSelectedOutputPath ?? _selectedPairingJob?.OutputConfigPath;
            var nextSelection = string.IsNullOrWhiteSpace(desiredOutputPath)
                ? filteredJobs.FirstOrDefault()
                : filteredJobs.FirstOrDefault(item =>
                    item.OutputConfigPath.Equals(desiredOutputPath, StringComparison.OrdinalIgnoreCase)) ?? filteredJobs.FirstOrDefault();

            SelectedPairingJob = nextSelection;
        }

        private bool MatchesSelectedJobFilter(VersionSyncJobSummaryItem item)
        {
            return SelectedJobFilterMode switch
            {
                VersionSyncJobFilterMode.HasDifferences => item.HasChanges,
                VersionSyncJobFilterMode.ManualReviewOnly => item.RequiresManualReview,
                VersionSyncJobFilterMode.AutoApplicableOnly => item.CanAutoApply,
                _ => true
            };
        }

        private IEnumerable<VersionSyncJobSummaryItem> GetBatchTargetJobs()
        {
            return SelectedBatchApplyMode switch
            {
                VersionSyncBatchApplyMode.AutoApplicableOnly => _pairingJobs.Where(item => item.CanAutoApply),
                _ => _pairingJobs.Where(item => item.HasChanges && !item.HasPreviewError)
            };
        }

        private bool TryGetComparison(
            VersionSyncPairingJob job,
            out VersionSyncComparisonResult comparison,
            out string errorMessage)
        {
            if (_comparisonCache.TryGetValue(job.OutputConfigPath, out comparison!))
            {
                EnsureComparisonBuffers(comparison);
                comparison.OriginalResultConfigText = string.IsNullOrWhiteSpace(comparison.OriginalResultConfigText)
                    ? comparison.ResultBuffer.Content
                    : comparison.OriginalResultConfigText;
                errorMessage = string.Empty;
                return true;
            }

            try
            {
                comparison = _versionSyncService.BuildComparison(job);
                EnsureComparisonBuffers(comparison);
                comparison.OriginalResultConfigText = string.IsNullOrWhiteSpace(comparison.OriginalResultConfigText)
                    ? comparison.ResultBuffer.Content
                    : comparison.OriginalResultConfigText;
                _comparisonCache[job.OutputConfigPath] = comparison;
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                _comparisonCache.Remove(job.OutputConfigPath);
                comparison = null!;
                errorMessage = ex.Message;
                return false;
            }
        }

        private static string BuildJobDisplayName(VersionSyncJobSummaryItem job)
        {
            return $"{job.OldFolderName} -> {job.NewFolderName}";
        }

        private static string BuildSkipReason(VersionSyncJobSummaryItem job)
        {
            if (job.HasPreviewError)
            {
                return "预览失败";
            }

            if (!job.HasChanges)
            {
                return "无变更";
            }

            if (!job.CanAutoApply)
            {
                return "当前策略仅允许自动项";
            }

            return "当前策略未选中";
        }

        private void AddPairingJobSummary(VersionSyncPairingJob job, string preferredSelectedOutputPath)
        {
            if (TryGetComparison(job, out var comparison, out var errorMessage))
            {
                _allPairingJobs.Add(VersionSyncJobSummaryItem.FromComparison(comparison));
            }
            else
            {
                _allPairingJobs.Add(VersionSyncJobSummaryItem.FromPreviewError(job, errorMessage));
            }

            SortAllPairingJobs();
            ApplyPairingJobFilter(preferredSelectedOutputPath);
        }

        private void RemoveConflictingPairings(VersionSyncPairingJob job)
        {
            var conflictingJobs = _allPairingJobs
                .Where(item =>
                    item.Job.OldCandidate.FullPath.Equals(job.OldCandidate.FullPath, StringComparison.OrdinalIgnoreCase) ||
                    item.Job.NewCandidate.FullPath.Equals(job.NewCandidate.FullPath, StringComparison.OrdinalIgnoreCase) ||
                    item.OutputConfigPath.Equals(job.OutputConfigPath, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var conflictingJob in conflictingJobs)
            {
                _comparisonCache.Remove(conflictingJob.OutputConfigPath);
                _allPairingJobs.Remove(conflictingJob);
            }
        }

        private void SortAllPairingJobs()
        {
            _allPairingJobs.Sort((left, right) =>
            {
                var oldFolderComparison = StringComparer.OrdinalIgnoreCase.Compare(left.OldFolderName, right.OldFolderName);
                if (oldFolderComparison != 0)
                {
                    return oldFolderComparison;
                }

                var newFolderComparison = StringComparer.OrdinalIgnoreCase.Compare(left.NewFolderName, right.NewFolderName);
                if (newFolderComparison != 0)
                {
                    return newFolderComparison;
                }

                return StringComparer.OrdinalIgnoreCase.Compare(left.OutputConfigPath, right.OutputConfigPath);
            });
        }

        private void ExecuteSingleItemSync(
            Func<ModConfigEditBuffer, ModConfigEditBuffer> applySync,
            string successStatusText,
            string failureStatusText)
        {
            if (_selectedComparison == null)
            {
                return;
            }

            try
            {
                var workingBuffer = applySync(CreateWorkingNewBuffer());
                CommitNewConfigBaseline(workingBuffer, successStatusText);
            }
            catch (Exception ex)
            {
                StatusText = failureStatusText;
                _messages.ShowError(ex.Message, "版本同步");
            }
        }

        private void CommitNewConfigBaseline(ModConfigEditBuffer newBuffer, string successStatusText)
        {
            if (_selectedComparison == null || SelectedPairingJob == null)
            {
                return;
            }

            var refreshedComparison = _versionSyncService.BuildComparison(SelectedPairingJob.Job, NormalizeNewBuffer(newBuffer));
            EnsureComparisonBuffers(refreshedComparison);
            refreshedComparison.OriginalResultConfigText = string.IsNullOrWhiteSpace(refreshedComparison.OriginalResultConfigText)
                ? refreshedComparison.ResultBuffer.Content
                : refreshedComparison.OriginalResultConfigText;
            _comparisonCache[SelectedPairingJob.OutputConfigPath] = refreshedComparison;
            ApplyComparisonToCurrentState(refreshedComparison);
            StatusText = successStatusText;
        }

        private void ApplyComparisonToCurrentState(VersionSyncComparisonResult comparison)
        {
            EnsureComparisonBuffers(comparison);
            SelectedPairingJob?.ApplyComparison(comparison);
            _selectedComparison = comparison;
            OldConfigText = comparison.OldConfigText;
            NewConfigText = comparison.NewBuffer.Content;
            SetResultConfigTextWithoutResync(comparison.ResultBuffer.Content);
            ReplaceCollection(_toggleDiffItems, comparison.ToggleDiffItems);
            ReplaceCollection(_parameterDiffItems, comparison.ParameterDiffItems);
            ReplaceCollection(_visibilityDiffItems, comparison.VisibilityDiffItems);
            OnPropertyChanged(nameof(ComparisonSummaryText));
            OnPropertyChanged(nameof(SelectedJobSummaryText));
            CommandManager.InvalidateRequerySuggested();
        }

        private void EnsureComparisonBuffers(VersionSyncComparisonResult comparison)
        {
            if (string.IsNullOrWhiteSpace(comparison.NewBuffer.SourcePath))
            {
                comparison.NewBuffer.SourcePath = comparison.Job.NewCandidate.ConfigPath;
            }

            if (string.IsNullOrWhiteSpace(comparison.NewBuffer.LineEnding))
            {
                comparison.NewBuffer.LineEnding = string.IsNullOrWhiteSpace(comparison.ResultBuffer.LineEnding)
                    ? Environment.NewLine
                    : comparison.ResultBuffer.LineEnding;
            }

            if (string.IsNullOrEmpty(comparison.NewBuffer.Content) && !string.IsNullOrEmpty(comparison.NewConfigText))
            {
                comparison.NewBuffer.Content = comparison.NewConfigText;
            }

            comparison.NewConfigText = comparison.NewBuffer.Content;

            if (string.IsNullOrWhiteSpace(comparison.ResultBuffer.SourcePath))
            {
                comparison.ResultBuffer.SourcePath = comparison.Job.OutputConfigPath;
            }

            if (string.IsNullOrWhiteSpace(comparison.ResultBuffer.LineEnding))
            {
                comparison.ResultBuffer.LineEnding = string.IsNullOrWhiteSpace(comparison.NewBuffer.LineEnding)
                    ? Environment.NewLine
                    : comparison.NewBuffer.LineEnding;
            }
        }

        private ModConfigEditBuffer ApplyToggleDiffItem(ModConfigEditBuffer workingBuffer, VersionSyncToggleDiffItem toggleItem)
        {
            workingBuffer = _configUpdateService.UpdateKeyBindings(
                workingBuffer,
                toggleItem.SectionName,
                ParseKeyBindings(toggleItem.ResultKeyBindingsText));

            foreach (var targetEdit in ParseToggleTargetEdits(toggleItem.ResultTargetValuesText))
            {
                workingBuffer = _configUpdateService.UpdateToggleTargetValues(
                    workingBuffer,
                    toggleItem.SectionName,
                    targetEdit.VariableName,
                    targetEdit.Values);
            }

            return workingBuffer;
        }

        private ModConfigEditBuffer ApplyParameterDiffItem(ModConfigEditBuffer workingBuffer, VersionSyncParameterDiffItem parameterItem)
        {
            return _configUpdateService.UpdateParameterDefaultValue(
                workingBuffer,
                parameterItem.Name,
                parameterItem.ResultDefaultValue);
        }

        private ModConfigEditBuffer ApplyVisibilityDiffItem(ModConfigEditBuffer workingBuffer, VersionSyncVisibilityDiffItem visibilityItem)
        {
            if (string.IsNullOrWhiteSpace(visibilityItem.TargetSectionName) ||
                string.IsNullOrWhiteSpace(visibilityItem.TargetDrawLabel) ||
                string.IsNullOrWhiteSpace(visibilityItem.VariableName))
            {
                throw new InvalidOperationException("当前模型项缺少可同步的绑定信息。",
                    innerException: null);
            }

            var currentAnalysis = AnalyzeBuffer(workingBuffer);
            var existingParameter = currentAnalysis.Parameters.SingleOrDefault(parameter =>
                parameter.Name.Equals(visibilityItem.VariableName, StringComparison.OrdinalIgnoreCase));
            var desiredKeyBindings = ParseKeyBindings(visibilityItem.ResultKeyBindingsText);

            if (existingParameter == null)
            {
                if (desiredKeyBindings.Count == 0)
                {
                    throw new InvalidOperationException("当前模型项缺少可迁移的快捷键绑定。");
                }

                workingBuffer = _configUpdateService.CreateVisibilityBinding(
                    workingBuffer,
                    visibilityItem.TargetSectionName,
                    visibilityItem.TargetDrawLabel,
                    visibilityItem.VariableName,
                    desiredKeyBindings);
            }
            else
            {
                if (existingParameter.KeyBindings.Count == 0 && desiredKeyBindings.Count > 0)
                {
                    workingBuffer = _configUpdateService.CreateToggleBinding(
                        workingBuffer,
                        visibilityItem.VariableName,
                        desiredKeyBindings);
                }

                workingBuffer = _configUpdateService.BindVisibilityToParameter(
                    workingBuffer,
                    visibilityItem.TargetSectionName,
                    visibilityItem.TargetDrawLabel,
                    visibilityItem.VariableName);
            }

            if (!string.IsNullOrWhiteSpace(visibilityItem.ResultDefaultValue))
            {
                workingBuffer = _configUpdateService.UpdateParameterDefaultValue(
                    workingBuffer,
                    visibilityItem.VariableName,
                    visibilityItem.ResultDefaultValue);
            }

            return workingBuffer;
        }

        private ModConfigEditBuffer CreateWorkingResultBuffer()
        {
            if (_selectedComparison == null)
            {
                throw new InvalidOperationException("当前没有可编辑的同步预览。");
            }

            EnsureComparisonBuffers(_selectedComparison);

            return new ModConfigEditBuffer
            {
                SourcePath = _selectedComparison.ResultBuffer.SourcePath,
                Content = ResultConfigText,
                LineEnding = string.IsNullOrWhiteSpace(_selectedComparison.ResultBuffer.LineEnding)
                    ? Environment.NewLine
                    : _selectedComparison.ResultBuffer.LineEnding,
                AppliedChanges = _selectedComparison.ResultBuffer.AppliedChanges.ToList()
            };
        }

        private ModConfigEditBuffer CreateWorkingNewBuffer()
        {
            if (_selectedComparison == null)
            {
                throw new InvalidOperationException("当前没有可编辑的新版文本。");
            }

            EnsureComparisonBuffers(_selectedComparison);
            return NormalizeNewBuffer(_selectedComparison.NewBuffer);
        }

        private ModConfigEditBuffer NormalizeNewBuffer(ModConfigEditBuffer buffer)
        {
            if (SelectedPairingJob == null)
            {
                throw new InvalidOperationException("当前没有选中的同步作业。");
            }

            return new ModConfigEditBuffer
            {
                SourcePath = string.IsNullOrWhiteSpace(buffer.SourcePath)
                    ? SelectedPairingJob.Job.NewCandidate.ConfigPath
                    : buffer.SourcePath,
                Content = buffer.Content,
                LineEnding = string.IsNullOrWhiteSpace(buffer.LineEnding)
                    ? Environment.NewLine
                    : buffer.LineEnding,
                AppliedChanges = buffer.AppliedChanges.ToList()
            };
        }

        private ModConfigAnalysisResult AnalyzeBuffer(ModConfigEditBuffer buffer)
        {
            return _configAnalysisService.Analyze(_configParser.Parse(buffer.Content));
        }

        private void SetResultConfigTextWithoutResync(string value)
        {
            _isSynchronizingResultText = true;
            try
            {
                ResultConfigText = value;
            }
            finally
            {
                _isSynchronizingResultText = false;
            }
        }

        private static IReadOnlyList<string> ParseKeyBindings(string text)
        {
            return text
                .Split(new[] { '|', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();
        }

        private static IReadOnlyList<ToggleTargetEdit> ParseToggleTargetEdits(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<ToggleTargetEdit>();
            }

            return text
                .Split(new[] { '|', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(ParseToggleTargetEdit)
                .ToList();
        }

        private static ToggleTargetEdit ParseToggleTargetEdit(string text)
        {
            var equalsIndex = text.IndexOf('=');
            if (equalsIndex <= 0 || equalsIndex >= text.Length - 1)
            {
                throw new InvalidOperationException("按键目标值格式非法，应为 $变量 = 值1, 值2。\n\n多个目标请使用 | 分隔。\n示例：$swapvar_clothing = -1, 0, 1 | $swapvar_shoes = 0, 1");
            }

            var variableName = text.Substring(0, equalsIndex).Trim();
            var values = text.Substring(equalsIndex + 1)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            if (values.Count == 0)
            {
                throw new InvalidOperationException($"按键目标 {variableName} 至少需要一个可用值。");
            }

            return new ToggleTargetEdit(variableName, values);
        }

        private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> values)
        {
            target.Clear();
            foreach (var value in values)
            {
                target.Add(value);
            }
        }

        private sealed record ToggleTargetEdit(string VariableName, IReadOnlyList<string> Values);
    }
}