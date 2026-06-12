using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using WuwaModModifier.Common;
using WuwaModModifier.Model;

namespace WuwaModModifier.ViewModels
{
    /// <summary>
    /// Manages standardization (toggle slot alignment) and multi-config operations.
    /// Extracted from MainViewModel as part of R-02 refactoring.
    /// </summary>
    public class StandardizationViewModel : ViewModelBase
    {
        private readonly IMainViewModelSession _session;
        private readonly IFileSystemService _fileSystem;
        private readonly IModConfigUpdateService _configUpdateService;
        private readonly IModConfigDiscoveryService _configDiscoveryService;
        private readonly IMessageService _messages;

        private ObservableCollection<ModConfigStandardizationItemResult> _latestStandardizationItems;
        private ObservableCollection<ConfigModificationHistoryItem> _modificationHistoryItems;

        public StandardizationViewModel(
            IMainViewModelSession session,
            IFileSystemService fileSystem,
            IModConfigUpdateService configUpdateService,
            IModConfigDiscoveryService configDiscoveryService,
            IMessageService messages)
        {
            _session = session;
            _fileSystem = fileSystem;
            _configUpdateService = configUpdateService;
            _configDiscoveryService = configDiscoveryService;
            _messages = messages;

            _latestStandardizationItems = new ObservableCollection<ModConfigStandardizationItemResult>();
            _modificationHistoryItems = new ObservableCollection<ConfigModificationHistoryItem>();

            ApplyStandardizationCommand = new RelayCommand(ExecuteApplyStandardization, CanApplyStandardization);
            AddMultiConfigCommand = new RelayCommand(ExecuteAddMultiConfig, CanAddMultiConfig);
        }

        // ── Collections ──

        public ObservableCollection<ModConfigStandardizationItemResult> LatestStandardizationItems
        {
            get => _latestStandardizationItems;
            set => SetProperty(ref _latestStandardizationItems, value);
        }

        public ObservableCollection<ConfigModificationHistoryItem> ModificationHistoryItems
        {
            get => _modificationHistoryItems;
            set => SetProperty(ref _modificationHistoryItems, value);
        }

        // ── Commands ──

        public ICommand ApplyStandardizationCommand { get; }
        public ICommand AddMultiConfigCommand { get; }

        // ── Public helpers ──

        public void ReplaceStandardizationItems(IEnumerable<ModConfigStandardizationItemResult> items)
        {
            _latestStandardizationItems.Clear();
            foreach (var item in items) _latestStandardizationItems.Add(item);
        }

        internal void ClearResults()
        {
            _latestStandardizationItems.Clear();
        }

        internal void ClearHistory()
        {
            _modificationHistoryItems.Clear();
        }

        // ── Private methods ──

        private void ExecuteApplyStandardization()
        {
            if (_session.CurrentBuffer == null) return;

            try
            {
                var result = _configUpdateService.StandardizeToggleSlots(
                    _session.CurrentBuffer,
                    _session.StandardToggleTemplatePath);

                ReplaceStandardizationItems(result.Items);
                _session.ApplyBufferAnalysis(
                    result.Buffer,
                    $"标准化完成：{result.FullyStandardizedCount} 完全对齐，{result.PartiallyStandardizedCount} 部分对齐，{result.SkippedCount} 跳过。");
                _session.AppendModificationHistory("批量标准化", "当前配置",
                    $"完全对齐 {result.FullyStandardizedCount}，部分对齐 {result.PartiallyStandardizedCount}，跳过 {result.SkippedCount}");
            }
            catch (Exception ex)
            {
                LogManager.Error("ApplyStandardization: ", ex);
                _messages.ShowError($"标准化失败：{ex.Message}");
            }
        }

        private bool CanApplyStandardization()
        {
            return _session.CurrentBuffer != null &&
                !_session.IsRawConfigDirty &&
                _session.SelectedToggleItems.Count > 0 &&
                _fileSystem.FileExists(_session.StandardToggleTemplatePath);
        }

        private void ExecuteAddMultiConfig()
        {
            if (string.IsNullOrWhiteSpace(_session.SelectedConfigPath)) return;

            try
            {
                var configDir = Path.GetDirectoryName(_session.SelectedConfigPath);
                if (string.IsNullOrWhiteSpace(configDir)) return;

                var index = GetNextMultiConfigIndex(configDir);
                var variantDir = $"{configDir}_p{index}";

                if (_fileSystem.DirectoryExists(variantDir))
                {
                    _messages.ShowError($"多配置目录 {variantDir} 已存在。");
                    return;
                }

                _fileSystem.CreateDirectory(variantDir);
                CopyMissingConfigFiles(configDir, variantDir);
                _session.RefreshSelectedConfigAnalysis();
                _messages.ShowInfo($"已创建多配置变体：{variantDir}");
            }
            catch (Exception ex)
            {
                LogManager.Error("AddMultiConfig: ", ex);
                _messages.ShowError($"创建多配置失败：{ex.Message}");
            }
        }

        private bool CanAddMultiConfig()
        {
            return !string.IsNullOrWhiteSpace(_session.SelectedConfigPath) &&
                _session.SelectedDirectoryItem != null &&
                !_session.SelectedDirectoryItem.IsDirectory;
        }

        private int GetNextMultiConfigIndex(string configDir)
        {
            var parent = Path.GetDirectoryName(configDir);
            if (string.IsNullOrWhiteSpace(parent)) return 1;

            var baseName = Path.GetFileName(configDir);
            if (string.IsNullOrWhiteSpace(baseName)) return 1;
            var existing = _fileSystem.GetDirectories(parent)
                .Select(Path.GetFileName)
                .Where(name => name.StartsWith(baseName + "_p", StringComparison.OrdinalIgnoreCase))
                .Select(name =>
                {
                    var suffix = name.Substring(baseName.Length + 2); // "_pX"
                    return int.TryParse(suffix, out var n) ? n : 0;
                })
                .ToList();

            return existing.Count > 0 ? existing.Max() + 1 : 1;
        }

        private void CopyMissingConfigFiles(string sourceDir, string targetDir)
        {
            foreach (var file in _fileSystem.GetFiles(sourceDir, "*.ini"))
            {
                var fileName = Path.GetFileName(file);
                var destPath = Path.Combine(targetDir, fileName);
                if (!_fileSystem.FileExists(destPath))
                {
                    var content = _fileSystem.ReadAllText(file);
                    _fileSystem.WriteAllText(destPath, content);
                }
            }

            foreach (var subDir in _fileSystem.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(subDir);
                if (IsResourceSubdirectory(dirName)) continue;
                var destSubDir = Path.Combine(targetDir, dirName);
                _fileSystem.CreateDirectory(destSubDir);
                CopyMissingConfigFiles(subDir, destSubDir);
            }
        }

        private static bool IsResourceSubdirectory(string dirName)
        {
            return dirName.Equals("Data", StringComparison.OrdinalIgnoreCase) ||
                dirName.Equals("Textures", StringComparison.OrdinalIgnoreCase) ||
                dirName.Equals("Meshes", StringComparison.OrdinalIgnoreCase) ||
                dirName.Equals("Bin", StringComparison.OrdinalIgnoreCase);
        }
    }
}
