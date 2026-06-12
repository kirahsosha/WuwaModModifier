using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using WuwaModModifier.Common;
using WuwaModModifier.Data.ViewModels;
using WuwaModModifier.Model;

namespace WuwaModModifier.ViewModels
{
    /// <summary>
    /// Manages config file selection, analysis, raw text editing, and navigation.
    /// The largest sub-ViewModel extracted from MainViewModel as part of R-02.
    /// </summary>
    public class ConfigEditorViewModel : ViewModelBase
    {
        private readonly IMainViewModelSession _session;
        private readonly IFileSystemService _fileSystem;
        private readonly IModConfigParser _configParser;
        private readonly IModConfigAnalysisService _configAnalysisService;
        private readonly IModConfigUpdateService _configUpdateService;
        private readonly IModConfigDiscoveryService _configDiscoveryService;
        private readonly IMessageService _messages;

        private ModConfigEditBuffer? _selectedConfigBuffer;
        private string _selectedConfigPath;
        private string _selectedConfigCandidatesText;
        private ObservableCollection<ConfigCandidateOption> _selectedConfigCandidates;
        private string? _selectedConfigCandidatePath;
        private string _selectedConfigAnalysisStatus;
        private string _selectedConfigEditStatus;
        private string _standardToggleTemplatePath;
        private bool _hasPendingConfigChanges;
        private string _rawConfigEditorText;
        private string _rawConfigLineEnding;
        private int _rawConfigNavigateLine;
        private int _rawConfigNavigateRequestVersion;
        private bool _isRawConfigDirty;
        private ModConfigSaveTarget _selectedConfigSource;
        private ObservableCollection<ConfigTextHighlightItem> _rawConfigHighlights;

        public ConfigEditorViewModel(
            IMainViewModelSession session,
            IFileSystemService fileSystem,
            IModConfigParser configParser,
            IModConfigAnalysisService configAnalysisService,
            IModConfigUpdateService configUpdateService,
            IModConfigDiscoveryService configDiscoveryService,
            IMessageService messages)
        {
            _session = session;
            _fileSystem = fileSystem;
            _configParser = configParser;
            _configAnalysisService = configAnalysisService;
            _configUpdateService = configUpdateService;
            _configDiscoveryService = configDiscoveryService;
            _messages = messages;

            _selectedConfigPath = string.Empty;
            _selectedConfigCandidatesText = string.Empty;
            _selectedConfigCandidates = new ObservableCollection<ConfigCandidateOption>();
            _selectedConfigCandidatePath = null;
            _selectedConfigAnalysisStatus = Properties.Resources.StatusSelectModForAnalysis;
            _selectedConfigEditStatus = Properties.Resources.StatusNoEditableConfig;
            _standardToggleTemplatePath = string.Empty;
            _hasPendingConfigChanges = false;
            _rawConfigEditorText = string.Empty;
            _rawConfigLineEnding = Environment.NewLine;
            _isRawConfigDirty = false;
            _selectedConfigSource = ModConfigSaveTarget.ModDirectory;
            _rawConfigHighlights = new ObservableCollection<ConfigTextHighlightItem>();

            ToggleConfigSourceCommand = new RelayCommand(ExecuteToggleConfigSource, CanToggleConfigSource);
        }

        // ── Config Path ──

        public string SelectedConfigPath { get => _selectedConfigPath; protected set => SetProperty(ref _selectedConfigPath, value); }
        public string SelectedConfigCandidatesText { get => _selectedConfigCandidatesText; protected set => SetProperty(ref _selectedConfigCandidatesText, value); }
        public ObservableCollection<ConfigCandidateOption> SelectedConfigCandidates => _selectedConfigCandidates;

        public string? SelectedConfigCandidatePath
        {
            get => _selectedConfigCandidatePath;
            set
            {
                if (SetProperty(ref _selectedConfigCandidatePath, value))
                {
                    HandleSelectedConfigCandidateChanged();
                }
            }
        }

        // ── Analysis Status ──

        public string SelectedConfigAnalysisStatus { get => _selectedConfigAnalysisStatus; set => SetProperty(ref _selectedConfigAnalysisStatus, value); }
        public string SelectedConfigEditStatus { get => _selectedConfigEditStatus; set => SetProperty(ref _selectedConfigEditStatus, value); }

        // ── Raw Editor ──

        public string RawConfigEditorText
        {
            get => _rawConfigEditorText;
            set
            {
                if (SetProperty(ref _rawConfigEditorText, value))
                {
                    UpdateRawConfigDirtyState();
                }
            }
        }

        public bool IsRawConfigDirty => _isRawConfigDirty;
        public bool HasPendingConfigChanges { get => _hasPendingConfigChanges; set => SetProperty(ref _hasPendingConfigChanges, value); }

        public string StandardToggleTemplatePath { get => _standardToggleTemplatePath; set => SetProperty(ref _standardToggleTemplatePath, value); }

        public int RawConfigNavigateLine => _rawConfigNavigateLine;
        public int RawConfigNavigateRequestVersion => _rawConfigNavigateRequestVersion;

        public ObservableCollection<ConfigTextHighlightItem> RawConfigHighlights => _rawConfigHighlights;

        public ModConfigSaveTarget CurrentConfigSource => _selectedConfigSource;

        // ── Computed ──

        public string CurrentConfigSourceText =>
            _selectedConfigSource == ModConfigSaveTarget.ModDirectory ? "Mod配置" : "WWMI配置";

        public string ToggleConfigSourceButtonText =>
            _selectedConfigSource == ModConfigSaveTarget.ModDirectory ? "切换到 WWMI 配置" : "切换到 Mod 配置";

        // ── Commands ──

        public ICommand ToggleConfigSourceCommand { get; }

        // ── Public helpers ──

        public ModConfigEditBuffer? CurrentBuffer => _selectedConfigBuffer;

        internal void SetBuffer(ModConfigEditBuffer? buffer)
        {
            _selectedConfigBuffer = buffer;
        }

        internal void SetRawConfigDirty(bool dirty)
        {
            _isRawConfigDirty = dirty;
            OnPropertyChanged(nameof(IsRawConfigDirty));
        }

        internal void SetSelectedConfigSource(ModConfigSaveTarget source)
        {
            _selectedConfigSource = source;
            OnPropertyChanged(nameof(CurrentConfigSourceText));
            OnPropertyChanged(nameof(ToggleConfigSourceButtonText));
        }

        internal void NavigateToLine(int line)
        {
            _rawConfigNavigateLine = Math.Max(line, 0);
            _rawConfigNavigateRequestVersion++;
            OnPropertyChanged(nameof(RawConfigNavigateLine));
            OnPropertyChanged(nameof(RawConfigNavigateRequestVersion));
        }

        internal void ResetNavigation()
        {
            _rawConfigNavigateLine = 0;
            _rawConfigNavigateRequestVersion = 0;
            OnPropertyChanged(nameof(RawConfigNavigateLine));
            OnPropertyChanged(nameof(RawConfigNavigateRequestVersion));
        }

        internal void ReplaceHighlights(IEnumerable<ConfigTextHighlightItem> items)
        {
            _rawConfigHighlights.Clear();
            foreach (var item in items) _rawConfigHighlights.Add(item);
        }

        internal void ReplaceCandidates(IEnumerable<ConfigCandidateOption> items)
        {
            _selectedConfigCandidates.Clear();
            foreach (var item in items) _selectedConfigCandidates.Add(item);
        }

        // ── Private methods ──

        private void HandleSelectedConfigCandidateChanged() { /* Full logic in MainViewModel */ }
        private void UpdateRawConfigDirtyState() { /* Full logic in MainViewModel */ }

        private void ExecuteToggleConfigSource() { /* Full logic in MainViewModel */ }
        private bool CanToggleConfigSource() { return _selectedDirectoryItem != null; }

        private DirectoryItemViewModel? _selectedDirectoryItem;
        public DirectoryItemViewModel? SelectedDirectoryItem
        {
            get => _selectedDirectoryItem;
            set => SetProperty(ref _selectedDirectoryItem, value);
        }
    }
}
