using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Data;
using WuwaModModifier.Common;
using WuwaModModifier.Model;
using WuwaModModifier.Data.ViewModels;

namespace WuwaModModifier.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private enum VisibilityBindingMode
        {
            ExistingParameter,
            ExistingToggle,
            NewParameterAndToggle
        }

        private enum ToggleCreationMode
        {
            ExistingParameter,
            NewParameter
        }

        private readonly IFileSystemService _fileSystem;
        private readonly IMessageService _messages;
        private readonly IModConfigDiscoveryService _configDiscoveryService;
        private readonly IModConfigAnalysisService _configAnalysisService;
        private readonly IModConfigParser _configParser;
        private readonly IModConfigUpdateService _configUpdateService;

        private string _modFolderPath;
        private string _wwmiFolderPath;
        private string _modPathLoadStatusText;
        private string _wwmiPathLoadStatusText;
        private string _otherFolderPath;
        private string _selectedConfigPath;
        private string _selectedConfigAnalysisStatus;
        private string _selectedConfigEditStatus;
        private string _standardToggleTemplatePath;
        private bool _hasPendingConfigChanges;
        private bool _ignoreWeapon;
        private bool _ignoreOther;
        private List<WuwaMods> _allMods;
        private List<WuwaMod> _wwmiMods;
        private ObservableCollection<DirectoryItemViewModel> _directoryItems;
        private DirectoryItemViewModel? _selectedDirectoryItem;
        private ObservableCollection<ConfigToggleSummaryItem> _selectedToggleItems;
        private ObservableCollection<ConfigParameterSummaryItem> _selectedParameterItems;
        private readonly ICollectionView _selectedParameterItemsView;
        private ObservableCollection<ConfigVisibilitySummaryItem> _selectedVisibilityItems;
        private ObservableCollection<ConfigParameterSummaryItem> _toggleCreationParameterCandidates;
        private ObservableCollection<ConfigParameterSummaryItem> _visibilityBindingParameterCandidates;
        private ObservableCollection<ConfigToggleSummaryItem> _visibilityBindingToggleCandidates;
        private ObservableCollection<string> _visibilityBindingAvailableKeyOptions;
        private ObservableCollection<ConfigStandardizationSummaryItem> _latestStandardizationItems;
        private ObservableCollection<ConfigModificationHistoryItem> _modificationHistoryItems;
        private ObservableCollection<ConfigTextHighlightItem> _rawConfigHighlights;
        private ModConfigEditBuffer? _selectedConfigBuffer;
        private ConfigToggleSummaryItem? _selectedToggleItem;
        private ConfigParameterSummaryItem? _selectedParameterItem;
        private ConfigVisibilitySummaryItem? _selectedVisibilityItem;
        private ConfigParameterSummaryItem? _selectedToggleCreationParameter;
        private ConfigParameterSummaryItem? _selectedVisibilityBindingParameter;
        private ConfigToggleSummaryItem? _selectedVisibilityBindingToggle;
        private string _toggleKeyBindingEditorText;
        private string _toggleCreationNewParameterName;
        private string _toggleCreationKeyBindingsText;
        private string _parameterRenameText;
        private string _parameterCreationName;
        private string _visibilityBindingNewParameterName;
        private string _visibilityBindingNewKeyBindingsText;
        private string _rawConfigEditorText;
        private string _rawConfigLineEnding;
        private int _rawConfigNavigateLine;
        private int _rawConfigNavigateRequestVersion;
        private bool _isRawConfigDirty;
        private bool _hideInternalSystemParameters;
        private bool _visibilityTargetIsVisible;
        private ToggleCreationMode _selectedToggleCreationMode;
        private VisibilityBindingMode _selectedVisibilityBindingMode;

        public MainViewModel()
            : this(
                new FileSystemService(),
                new MessageService(),
                null,
                null,
                null,
                null)
        {
        }

        public MainViewModel(IFileSystemService fileSystem, IMessageService messages)
            : this(fileSystem, messages, null, null, null, null)
        {
        }

        public MainViewModel(
            IFileSystemService fileSystem,
            IMessageService messages,
            IModConfigDiscoveryService? configDiscoveryService,
            IModConfigAnalysisService? configAnalysisService,
            IModConfigParser? configParser,
            IModConfigUpdateService? configUpdateService)
        {
            _fileSystem = fileSystem;
            _messages = messages;
            _configDiscoveryService = configDiscoveryService ?? new ModConfigDiscoveryService(fileSystem);
            _configParser = configParser ?? new ModConfigParser(fileSystem);
            _configAnalysisService = configAnalysisService ?? new ModConfigAnalysisService(_configParser);
            _configUpdateService = configUpdateService ?? new ModConfigUpdateService(fileSystem, _configParser, _configAnalysisService);

            _modFolderPath = AppConfig.DefaultModPath;
            _wwmiFolderPath = AppConfig.DefaultWwmiPath;
            _modPathLoadStatusText = string.Empty;
            _wwmiPathLoadStatusText = string.Empty;
            _otherFolderPath = AppConfig.OtherFolderPath;
            _selectedConfigPath = string.Empty;
            _selectedConfigAnalysisStatus = "请选择具体 MOD 查看配置分析。";
            _selectedConfigEditStatus = "当前无可编辑配置。";
            _standardToggleTemplatePath = ResolveDefaultStandardToggleTemplatePath();
            _hasPendingConfigChanges = false;
            _ignoreWeapon = true;
            _ignoreOther = true;
            _allMods = new List<WuwaMods>();
            _wwmiMods = new List<WuwaMod>();
            _directoryItems = new ObservableCollection<DirectoryItemViewModel>();
            _selectedToggleItems = new ObservableCollection<ConfigToggleSummaryItem>();
            _selectedParameterItems = new ObservableCollection<ConfigParameterSummaryItem>();
            _selectedParameterItemsView = CollectionViewSource.GetDefaultView(_selectedParameterItems);
            _selectedParameterItemsView.Filter = FilterSelectedParameterItem;
            _selectedVisibilityItems = new ObservableCollection<ConfigVisibilitySummaryItem>();
            _toggleCreationParameterCandidates = new ObservableCollection<ConfigParameterSummaryItem>();
            _visibilityBindingParameterCandidates = new ObservableCollection<ConfigParameterSummaryItem>();
            _visibilityBindingToggleCandidates = new ObservableCollection<ConfigToggleSummaryItem>();
            _visibilityBindingAvailableKeyOptions = new ObservableCollection<string>();
            _latestStandardizationItems = new ObservableCollection<ConfigStandardizationSummaryItem>();
            _modificationHistoryItems = new ObservableCollection<ConfigModificationHistoryItem>();
            _rawConfigHighlights = new ObservableCollection<ConfigTextHighlightItem>();
            _toggleKeyBindingEditorText = string.Empty;
            _toggleCreationNewParameterName = string.Empty;
            _toggleCreationKeyBindingsText = string.Empty;
            _parameterRenameText = string.Empty;
            _parameterCreationName = string.Empty;
            _visibilityBindingNewParameterName = string.Empty;
            _visibilityBindingNewKeyBindingsText = string.Empty;
            _rawConfigEditorText = string.Empty;
            _rawConfigLineEnding = Environment.NewLine;
            _isRawConfigDirty = false;
            _hideInternalSystemParameters = true;
            _visibilityTargetIsVisible = true;
            _selectedToggleCreationMode = ToggleCreationMode.ExistingParameter;
            _selectedVisibilityBindingMode = VisibilityBindingMode.ExistingParameter;

            BtnModPathCommand = new RelayCommand(ExecuteBtnModPath);
            BtnWwmiPathCommand = new RelayCommand(ExecuteBtnWwmiPath);
            BtnClearAllModsCommand = new RelayCommand(ExecuteBtnClearAllMods);
            BtnRandomLoadAllModsCommand = new RelayCommand(ExecuteBtnRandomLoadAllMods);
            BtnLoadSelectedModsCommand = new RelayCommand(ExecuteBtnLoadSelectedMods);
            OpenModFolderCommand = new RelayCommand(ExecuteOpenModFolderBySelected, CanOpenModFolderBySelected);
            ApplyStandardizationCommand = new RelayCommand(ExecuteApplyStandardization, CanApplyStandardization);
            ApplyToggleKeyBindingsCommand = new RelayCommand(ExecuteApplyToggleKeyBindings, CanApplyToggleKeyBindings);
            CreateToggleCommand = new RelayCommand(ExecuteCreateToggle, CanCreateToggle);
            RenameParameterCommand = new RelayCommand(ExecuteRenameParameter, CanRenameParameter);
            CreateParameterCommand = new RelayCommand(ExecuteCreateParameter, CanCreateParameter);
            ApplyVisibilityChangeCommand = new RelayCommand(ExecuteApplyVisibilityChange, CanApplyVisibilityChange);
            ApplyVisibilityBindingCommand = new RelayCommand(ExecuteApplyVisibilityBinding, CanApplyVisibilityBinding);
            SaveRawConfigCommand = new RelayCommand(ExecuteSaveRawConfig, CanSaveRawConfig);
            SaveConfigToModCommand = new RelayCommand(ExecuteSaveConfigToMod, CanSaveConfigToMod);
            SaveConfigToWwmiCommand = new RelayCommand(ExecuteSaveConfigToWwmi, CanSaveConfigToWwmi);
            SyncModToWwmiCommand = new RelayCommand(ExecuteSyncModToWwmi, CanSyncModToWwmi);
            SyncWwmiToModCommand = new RelayCommand(ExecuteSyncWwmiToMod, CanSyncWwmiToMod);
        }

        public DirectoryItemViewModel? SelectedDirectoryItem
        {
            get => _selectedDirectoryItem;
            set
            {
                if (SetProperty(ref _selectedDirectoryItem, value))
                {
                    OnPropertyChanged(nameof(CanOpenVersionSync));
                    OnPropertyChanged(nameof(SelectedVersionSyncDirectoryPath));
                    RefreshSelectedConfigAnalysis();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool CanOpenVersionSync =>
            SelectedDirectoryItem != null &&
            SelectedDirectoryItem.IsDirectory &&
            SelectedDirectoryItem.Parent == null &&
            !string.IsNullOrWhiteSpace(SelectedDirectoryItem.FullPath);

        public string SelectedVersionSyncDirectoryPath =>
            CanOpenVersionSync
                ? SelectedDirectoryItem!.FullPath
                : string.Empty;

        public string SelectedConfigPath
        {
            get => _selectedConfigPath;
            private set
            {
                if (SetProperty(ref _selectedConfigPath, value))
                {
                    OnPropertyChanged(nameof(RawConfigEditorStatusText));
                    OnPathPreviewChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string SelectedConfigAnalysisStatus
        {
            get => _selectedConfigAnalysisStatus;
            private set => SetProperty(ref _selectedConfigAnalysisStatus, value);
        }

        public string SelectedConfigEditStatus
        {
            get => _selectedConfigEditStatus;
            private set => SetProperty(ref _selectedConfigEditStatus, value);
        }

        public string ModPathLoadStatusText
        {
            get => _modPathLoadStatusText;
            private set => SetProperty(ref _modPathLoadStatusText, value);
        }

        public string WwmiPathLoadStatusText
        {
            get => _wwmiPathLoadStatusText;
            private set => SetProperty(ref _wwmiPathLoadStatusText, value);
        }

        public string RawConfigEditorText
        {
            get => _rawConfigEditorText;
            set
            {
                var normalizedText = NormalizeLineEndings(value, _rawConfigLineEnding);
                if (SetProperty(ref _rawConfigEditorText, normalizedText))
                {
                    UpdateRawConfigDirtyState();
                }
            }
        }

        public bool IsRawConfigDirty => _isRawConfigDirty;

        public string RawConfigEditorStatusText =>
            string.IsNullOrWhiteSpace(SelectedConfigPath)
                ? "当前未选择配置文件。"
                : IsRawConfigDirty
                    ? "右侧原文存在未保存修改，请先保存回当前文件。"
                    : "右侧原文与当前源文件一致。";

        public string SaveToModPreviewPath => GetSavePreviewPath(ModConfigSaveTarget.ModDirectory);

        public string SaveToWwmiPreviewPath => GetSavePreviewPath(ModConfigSaveTarget.WwmiDirectory);

        public string SyncModToWwmiPreviewText => GetSyncPreviewText(ModConfigSyncDirection.ModToWwmi);

        public string SyncWwmiToModPreviewText => GetSyncPreviewText(ModConfigSyncDirection.WwmiToMod);

        public bool HasPendingConfigChanges
        {
            get => _hasPendingConfigChanges;
            private set
            {
                if (SetProperty(ref _hasPendingConfigChanges, value))
                {
                    OnPropertyChanged(nameof(ConfigBufferStateText));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string ConfigBufferStateText =>
            HasPendingConfigChanges
                ? "缓冲区存在尚未保存到当前源文件的修改。"
                : "缓冲区与当前源文件一致。";

        public string StandardToggleTemplatePath
        {
            get => _standardToggleTemplatePath;
            set
            {
                if (SetProperty(ref _standardToggleTemplatePath, value))
                {
                    RefreshSharedAvailableKeyOptions();
                    ApplyToggleCreationDefaults(force: false);
                    ApplyVisibilityBindingDefaults(force: false);
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string ModFolderPath
        {
            get => _modFolderPath;
            set
            {
                if (SetProperty(ref _modFolderPath, value))
                {
                    ModPathLoadStatusText = string.Empty;
                    OnPathPreviewChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string WwmiFolderPath
        {
            get => _wwmiFolderPath;
            set
            {
                if (SetProperty(ref _wwmiFolderPath, value))
                {
                    WwmiPathLoadStatusText = string.Empty;
                    OnPathPreviewChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool IgnoreWeapon
        {
            get => _ignoreWeapon;
            set => SetProperty(ref _ignoreWeapon, value);
        }

        public bool IgnoreOther
        {
            get => _ignoreOther;
            set => SetProperty(ref _ignoreOther, value);
        }

        public ObservableCollection<DirectoryItemViewModel> DirectoryItems
        {
            get => _directoryItems;
            set => SetProperty(ref _directoryItems, value);
        }

        public ObservableCollection<ConfigToggleSummaryItem> SelectedToggleItems => _selectedToggleItems;

        public ObservableCollection<ConfigParameterSummaryItem> SelectedParameterItems => _selectedParameterItems;

        public ICollectionView SelectedParameterItemsView => _selectedParameterItemsView;

        public ObservableCollection<ConfigVisibilitySummaryItem> SelectedVisibilityItems => _selectedVisibilityItems;

        public ObservableCollection<ConfigParameterSummaryItem> ToggleCreationParameterCandidates => _toggleCreationParameterCandidates;

        public ObservableCollection<ConfigParameterSummaryItem> VisibilityBindingParameterCandidates => _visibilityBindingParameterCandidates;

        public ObservableCollection<ConfigToggleSummaryItem> VisibilityBindingToggleCandidates => _visibilityBindingToggleCandidates;

        public ObservableCollection<string> VisibilityBindingAvailableKeyOptions => _visibilityBindingAvailableKeyOptions;

        public ObservableCollection<string> ToggleCreationAvailableKeyOptions => _visibilityBindingAvailableKeyOptions;

        public ObservableCollection<ConfigStandardizationSummaryItem> LatestStandardizationItems => _latestStandardizationItems;

        public ObservableCollection<ConfigModificationHistoryItem> ModificationHistoryItems => _modificationHistoryItems;

        public ObservableCollection<ConfigTextHighlightItem> RawConfigHighlights => _rawConfigHighlights;

        public bool HideInternalSystemParameters
        {
            get => _hideInternalSystemParameters;
            set
            {
                if (SetProperty(ref _hideInternalSystemParameters, value))
                {
                    RefreshSelectedParameterItemsView();
                }
            }
        }

        public int RawConfigNavigateLine
        {
            get => _rawConfigNavigateLine;
            private set => SetProperty(ref _rawConfigNavigateLine, value);
        }

        public int RawConfigNavigateRequestVersion
        {
            get => _rawConfigNavigateRequestVersion;
            private set => SetProperty(ref _rawConfigNavigateRequestVersion, value);
        }

        public ConfigToggleSummaryItem? SelectedToggleItem
        {
            get => _selectedToggleItem;
            set
            {
                if (SetProperty(ref _selectedToggleItem, value))
                {
                    ToggleKeyBindingEditorText = value == null
                        ? string.Empty
                        : string.Join(Environment.NewLine, SplitEditorValues(value.KeyBindingsText));
                    if (value != null)
                    {
                        RequestRawConfigNavigation(value.NavigateLine);
                    }
                }
            }
        }

        public ConfigParameterSummaryItem? SelectedParameterItem
        {
            get => _selectedParameterItem;
            set
            {
                if (SetProperty(ref _selectedParameterItem, value))
                {
                    ParameterRenameText = value?.Name ?? string.Empty;
                    if (value != null && value.CanCreateToggleBinding)
                    {
                        SelectedToggleCreationParameter = _toggleCreationParameterCandidates.FirstOrDefault(item =>
                            item.Name.Equals(value.Name, StringComparison.OrdinalIgnoreCase)) ?? SelectedToggleCreationParameter;
                    }

                    if (value != null)
                    {
                        RequestRawConfigNavigation(value.NavigateLine);
                    }
                }
            }
        }

        public ConfigVisibilitySummaryItem? SelectedVisibilityItem
        {
            get => _selectedVisibilityItem;
            set
            {
                if (SetProperty(ref _selectedVisibilityItem, value))
                {
                    if (value != null)
                    {
                        RequestRawConfigNavigation(value.NavigateLine);
                    }

                    ResetVisibilityBindingSelectionState();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ConfigParameterSummaryItem? SelectedToggleCreationParameter
        {
            get => _selectedToggleCreationParameter;
            set
            {
                if (SetProperty(ref _selectedToggleCreationParameter, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ConfigParameterSummaryItem? SelectedVisibilityBindingParameter
        {
            get => _selectedVisibilityBindingParameter;
            set
            {
                if (SetProperty(ref _selectedVisibilityBindingParameter, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ConfigToggleSummaryItem? SelectedVisibilityBindingToggle
        {
            get => _selectedVisibilityBindingToggle;
            set
            {
                if (SetProperty(ref _selectedVisibilityBindingToggle, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string ToggleKeyBindingEditorText
        {
            get => _toggleKeyBindingEditorText;
            set
            {
                if (SetProperty(ref _toggleKeyBindingEditorText, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string ToggleCreationNewParameterName
        {
            get => _toggleCreationNewParameterName;
            set
            {
                if (SetProperty(ref _toggleCreationNewParameterName, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string ToggleCreationKeyBindingsText
        {
            get => _toggleCreationKeyBindingsText;
            set
            {
                if (SetProperty(ref _toggleCreationKeyBindingsText, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string ParameterRenameText
        {
            get => _parameterRenameText;
            set
            {
                if (SetProperty(ref _parameterRenameText, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string ParameterCreationName
        {
            get => _parameterCreationName;
            set
            {
                if (SetProperty(ref _parameterCreationName, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string VisibilityBindingNewParameterName
        {
            get => _visibilityBindingNewParameterName;
            set
            {
                if (SetProperty(ref _visibilityBindingNewParameterName, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string VisibilityBindingNewKeyBindingsText
        {
            get => _visibilityBindingNewKeyBindingsText;
            set
            {
                if (SetProperty(ref _visibilityBindingNewKeyBindingsText, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool VisibilityTargetIsVisible
        {
            get => _visibilityTargetIsVisible;
            set => SetProperty(ref _visibilityTargetIsVisible, value);
        }

        public bool CanBindSelectedVisibilityItem => SelectedVisibilityItem?.CanBindSafely == true;

        public bool CanCreateNewVisibilityBinding =>
            SelectedVisibilityItem != null &&
            SelectedVisibilityItem.DrawCallCount > 0 &&
            (SelectedVisibilityItem.CanBindSafely || SelectedVisibilityItem.CanToggleSafely);

        public string VisibilityBindingStatusText
        {
            get
            {
                if (SelectedVisibilityItem == null)
                {
                    return "当前支持为模型显示项建立或更改绑定。";
                }

                if (SelectedVisibilityItem.CanBindSafely)
                {
                    return "当前项未绑定控制参数，可绑定到现有按键及参数，或新建一对参数+按键。";
                }

                return SelectedVisibilityItem.CanToggleSafely
                    ? "当前项已绑定控制参数，可直接应用模型显示修改，或新建一对参数+按键来更改当前绑定。"
                    : "当前项不满足自动绑定条件。";
            }
        }

        public bool UseExistingToggleCreationParameter
        {
            get => _selectedToggleCreationMode == ToggleCreationMode.ExistingParameter;
            set
            {
                if (value)
                {
                    SetToggleCreationMode(ToggleCreationMode.ExistingParameter);
                }
            }
        }

        public bool UseNewToggleCreationParameter
        {
            get => _selectedToggleCreationMode == ToggleCreationMode.NewParameter;
            set
            {
                if (value)
                {
                    SetToggleCreationMode(ToggleCreationMode.NewParameter);
                }
            }
        }

        public bool UseExistingVisibilityParameterBinding
        {
            get => _selectedVisibilityBindingMode == VisibilityBindingMode.ExistingParameter;
            set
            {
                if (value)
                {
                    SetVisibilityBindingMode(VisibilityBindingMode.ExistingParameter);
                }
            }
        }

        public bool UseExistingVisibilityToggleBinding
        {
            get => _selectedVisibilityBindingMode == VisibilityBindingMode.ExistingToggle;
            set
            {
                if (value)
                {
                    SetVisibilityBindingMode(VisibilityBindingMode.ExistingToggle);
                }
            }
        }

        public bool UseNewVisibilityBinding
        {
            get => _selectedVisibilityBindingMode == VisibilityBindingMode.NewParameterAndToggle;
            set
            {
                if (value)
                {
                    SetVisibilityBindingMode(VisibilityBindingMode.NewParameterAndToggle);
                }
            }
        }

        public ICommand BtnModPathCommand { get; }
        public ICommand BtnWwmiPathCommand { get; }
        public ICommand BtnClearAllModsCommand { get; }
        public ICommand BtnRandomLoadAllModsCommand { get; }
        public ICommand BtnLoadSelectedModsCommand { get; }
        public ICommand OpenModFolderCommand { get; }
        public ICommand ApplyStandardizationCommand { get; }
        public ICommand ApplyToggleKeyBindingsCommand { get; }
        public ICommand CreateToggleCommand { get; }
        public ICommand RenameParameterCommand { get; }
        public ICommand CreateParameterCommand { get; }
        public ICommand ApplyVisibilityChangeCommand { get; }
        public ICommand ApplyVisibilityBindingCommand { get; }
        public ICommand SaveRawConfigCommand { get; }
        public ICommand SaveConfigToModCommand { get; }
        public ICommand SaveConfigToWwmiCommand { get; }
        public ICommand SyncModToWwmiCommand { get; }
        public ICommand SyncWwmiToModCommand { get; }

        /// <summary>
        /// BtnModPath Click Event
        /// </summary>
        private void ExecuteBtnModPath()
        {
            if (GetMod(out var characterCount, out var modCount))
            {
                // 加载目录树
                LoadDirectoryTree();
                ModPathLoadStatusText = $"共找到 {characterCount} 名角色共 {modCount} 个 MOD。";
            }
            else
            {
                ModPathLoadStatusText = "未找到 MOD。";
            }
        }

        /// <summary>
        /// BtnWwmiPath Click Event
        /// </summary>
        private void ExecuteBtnWwmiPath()
        {
            if (GetWwmi(out var modCount))
            {
                // 选中已加载的MOD
                SelectLoadedMods();
                WwmiPathLoadStatusText = $"共找到 {modCount} 个 MOD。";
            }
            else
            {
                WwmiPathLoadStatusText = "未找到 MOD。";
            }
        }

        /// <summary>
        /// BtnClearAllMods Click Event
        /// </summary>
        private void ExecuteBtnClearAllMods()
        {
            if (ClearWwmi(out var modCount))
            {
                _messages.ShowInfo($"共删除 {modCount} 个MOD。");
            }
            else
            {
                _messages.ShowInfo("未找到MOD。");
            }
        }

        /// <summary>
        /// BtnRandomLoadAllMods Click Event
        /// </summary>
        private void ExecuteBtnRandomLoadAllMods()
        {
            if (ClearWwmi(out var oldModCount))
            {
                if (RandomLoadMods(out var newModCount, out var loadedMods))
                {
                    _messages.ShowInfo($"共删除 {oldModCount} 个MOD,加载 {newModCount} 个MOD。");
                    _wwmiMods = loadedMods;
                    // 选中加载的MOD
                    SelectRandomLoadedMods(loadedMods);
                }
                else
                {
                    _messages.ShowError("加载新MOD失败。");
                }
            }
            else
            {
                _messages.ShowError("删除已安装MOD失败。");
            }
        }

        /// <summary>
        /// BtnLoadSelectedMods
        /// </summary>
        private void ExecuteBtnLoadSelectedMods()
        {
            LoadSelectedMods();
        }

        private void RefreshSelectedConfigAnalysis()
        {
            ClearAnalysisCollections();
            ClearConfigEditingState();
            SelectedConfigPath = string.Empty;

            var selectedItem = SelectedDirectoryItem;
            if (selectedItem == null)
            {
                SelectedConfigAnalysisStatus = "请选择具体 MOD 查看配置分析。";
                return;
            }

            if (selectedItem.IsDirectory)
            {
                SelectedConfigAnalysisStatus = "当前选择的是角色目录，请选择具体 MOD。";
                return;
            }

            if (string.IsNullOrWhiteSpace(selectedItem.FullPath) || !_fileSystem.DirectoryExists(selectedItem.FullPath))
            {
                SelectedConfigAnalysisStatus = "所选 MOD 目录不存在。";
                return;
            }

            try
            {
                var configPath = _configDiscoveryService.GetPrimaryConfigPath(selectedItem.FullPath);
                if (string.IsNullOrWhiteSpace(configPath))
                {
                    SelectedConfigAnalysisStatus = "未找到主配置文件。";
                    return;
                }

                SelectedConfigPath = configPath;
                ApplyBufferAnalysis(_configUpdateService.LoadBuffer(configPath), "已加载配置缓冲区。");
            }
            catch (Exception ex)
            {
                LogManager.Error("RefreshSelectedConfigAnalysis: ", ex);
                SelectedConfigPath = string.Empty;
                ClearAnalysisCollections();
                ClearConfigEditingState();
                SelectedConfigAnalysisStatus = "配置分析失败。";
            }
        }

        private void ClearAnalysisCollections()
        {
            _selectedToggleItems.Clear();
            _selectedParameterItems.Clear();
            RefreshSelectedParameterItemsView();
            _selectedVisibilityItems.Clear();
            _toggleCreationParameterCandidates.Clear();
            _visibilityBindingParameterCandidates.Clear();
            _visibilityBindingToggleCandidates.Clear();
            _visibilityBindingAvailableKeyOptions.Clear();
        }

        private void ClearConfigEditingState()
        {
            _selectedConfigBuffer = null;
            HasPendingConfigChanges = false;
            ClearStandardizationResults();
            ClearModificationHistory();
            ResetRawConfigNavigation();
            ClearRawConfigEditorState();
            SelectedToggleItem = null;
            SelectedParameterItem = null;
            SelectedVisibilityItem = null;
            ToggleKeyBindingEditorText = string.Empty;
            SelectedToggleCreationParameter = null;
            ToggleCreationNewParameterName = string.Empty;
            ToggleCreationKeyBindingsText = string.Empty;
            SetToggleCreationMode(ToggleCreationMode.ExistingParameter);
            ParameterRenameText = string.Empty;
            ParameterCreationName = string.Empty;
            SelectedVisibilityBindingParameter = null;
            SelectedVisibilityBindingToggle = null;
            VisibilityBindingNewParameterName = string.Empty;
            VisibilityBindingNewKeyBindingsText = string.Empty;
            SetVisibilityBindingMode(VisibilityBindingMode.ExistingParameter);
            VisibilityTargetIsVisible = true;
            SelectedConfigEditStatus = "当前无可编辑配置。";
        }

        private void ClearStandardizationResults()
        {
            _latestStandardizationItems.Clear();
        }

        private void ClearModificationHistory()
        {
            _modificationHistoryItems.Clear();
        }

        private void ClearRawConfigEditorState()
        {
            _rawConfigLineEnding = Environment.NewLine;
            ReplaceCollection(_rawConfigHighlights, Enumerable.Empty<ConfigTextHighlightItem>());
            OnPropertyChanged(nameof(RawConfigHighlights));

            if (!string.IsNullOrEmpty(_rawConfigEditorText))
            {
                _rawConfigEditorText = string.Empty;
                OnPropertyChanged(nameof(RawConfigEditorText));
            }

            SetRawConfigDirty(false);
        }

        private void ReplaceStandardizationResults(IEnumerable<ConfigStandardizationSummaryItem> items)
        {
            ReplaceCollection(_latestStandardizationItems, items);
        }

        private void AppendModificationHistory(string operationType, string target, string summary)
        {
            _modificationHistoryItems.Add(ConfigModificationHistoryItem.Create(
                operationType,
                string.IsNullOrWhiteSpace(target) ? "当前配置" : target,
                summary));
        }

        private void ResetRawConfigNavigation()
        {
            RawConfigNavigateLine = 0;
            RawConfigNavigateRequestVersion = 0;
        }

        private void RequestRawConfigNavigation(int line)
        {
            RawConfigNavigateLine = Math.Max(line, 0);
            RawConfigNavigateRequestVersion = RawConfigNavigateRequestVersion + 1;
        }

        internal void RequestRawConfigNavigationForLine(int line)
        {
            if (line > 0)
            {
                RequestRawConfigNavigation(line);
            }
        }

        private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> values)
        {
            target.Clear();
            foreach (var value in values)
            {
                target.Add(value);
            }
        }

        private void ApplyBufferAnalysis(
            ModConfigEditBuffer buffer,
            string editStatus,
            string? preferredToggleSection = null,
            string? preferredParameterName = null,
            string? preferredVisibilitySection = null,
            string? preferredVisibilityLabel = null)
        {
            var currentToggleSection = preferredToggleSection ?? _selectedToggleItem?.SectionName;
            var currentParameterName = preferredParameterName ?? _selectedParameterItem?.Name;
            var currentVisibilitySection = preferredVisibilitySection ?? _selectedVisibilityItem?.SectionName;
            var currentVisibilityLabel = preferredVisibilityLabel ?? _selectedVisibilityItem?.DrawLabelsText;
            var document = _configParser.Parse(buffer.Content);
            var analysis = _configAnalysisService.Analyze(document);
            var toggleItems = CreateToggleSummaryItems(document, analysis.Toggles, analysis.Parameters);
            var parameterItems = CreateParameterSummaryItems(document, analysis.Parameters);
            var visibilityItems = CreateVisibilitySummaryItems(document, analysis.VisibilityItems);

            _selectedConfigBuffer = buffer;
            ReplaceCollection(_selectedToggleItems, toggleItems);
            ReplaceCollection(_selectedParameterItems, parameterItems);
            ReplaceCollection(_selectedVisibilityItems, visibilityItems);
            RefreshSharedAvailableKeyOptions();
            RefreshToggleCreationCandidates();
            RefreshVisibilityBindingCandidates();
            SynchronizeRawConfigEditor(buffer, document, analysis);

            SelectedToggleItem = null;
            SelectedParameterItem = null;
            SelectedVisibilityItem = null;

            if (!string.IsNullOrWhiteSpace(currentToggleSection))
            {
                SelectedToggleItem = _selectedToggleItems.FirstOrDefault(item =>
                    item.SectionName.Equals(currentToggleSection, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(currentParameterName))
            {
                SelectedParameterItem = _selectedParameterItems.FirstOrDefault(item =>
                    item.Name.Equals(currentParameterName, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(currentVisibilitySection) && !string.IsNullOrWhiteSpace(currentVisibilityLabel))
            {
                SelectedVisibilityItem = _selectedVisibilityItems.FirstOrDefault(item =>
                    item.SectionName.Equals(currentVisibilitySection, StringComparison.OrdinalIgnoreCase) &&
                    item.DrawLabelsText.Equals(currentVisibilityLabel, StringComparison.OrdinalIgnoreCase));
            }

            RefreshSelectedParameterItemsView();

            HasPendingConfigChanges = buffer.AppliedChanges.Count > 0;
            SelectedConfigAnalysisStatus = $"已分析 {analysis.Toggles.Count} 个按键项，{analysis.Parameters.Count} 个参数，{analysis.VisibilityItems.Count} 个模型显示项。";
            SelectedConfigEditStatus = editStatus;
        }

        private bool FilterSelectedParameterItem(object item)
        {
            return item is not ConfigParameterSummaryItem parameterItem ||
                !HideInternalSystemParameters ||
                !IsInternalSystemParameter(parameterItem);
        }

        private void RefreshSelectedParameterItemsView()
        {
            _selectedParameterItemsView.Refresh();

            if (SelectedParameterItem != null &&
                HideInternalSystemParameters &&
                IsInternalSystemParameter(SelectedParameterItem))
            {
                SelectedParameterItem = null;
            }
        }

        private static bool IsInternalSystemParameter(ConfigParameterSummaryItem item)
        {
            return item.KindText.Equals(nameof(ModConfigParameterKind.InternalSystem), StringComparison.OrdinalIgnoreCase);
        }

        private List<ConfigToggleSummaryItem> CreateToggleSummaryItems(
            ModConfigDocument document,
            IEnumerable<ModToggleDefinition> toggles,
            IEnumerable<ModParameterDefinition> parameters)
        {
            var bindableParameterNames = new HashSet<string>(
                parameters
                    .Where(CanBindVisibilityToParameter)
                    .Select(parameter => parameter.Name),
                StringComparer.OrdinalIgnoreCase);

            return toggles.Select(toggle =>
            {
                var summaryItem = ConfigToggleSummaryItem.FromModel(toggle);
                summaryItem.NavigateLine = GetSectionStartLine(document, toggle.SectionName);
                summaryItem.CanBindVisibilitySafely =
                    toggle.ToggleType.Equals("cycle", StringComparison.OrdinalIgnoreCase) &&
                    toggle.Targets.Count == 1 &&
                    IsBinaryVisibilityToggleValues(toggle.Targets[0].Values) &&
                    bindableParameterNames.Contains(summaryItem.PrimaryVariableName);
                return summaryItem;
            }).ToList();
        }

        private List<ConfigParameterSummaryItem> CreateParameterSummaryItems(
            ModConfigDocument document,
            IEnumerable<ModParameterDefinition> parameters)
        {
            return parameters.Select(parameter =>
            {
                var summaryItem = ConfigParameterSummaryItem.FromModel(parameter);
                summaryItem.NavigateLine = FindParameterLines(document, parameter.Name)
                    .OrderBy(line => line)
                    .FirstOrDefault();
                summaryItem.CanBindVisibilitySafely = CanBindVisibilityToParameter(parameter);
                summaryItem.CanCreateToggleBinding = CanCreateToggleBindingForParameter(parameter);
                return summaryItem;
            }).ToList();
        }

        private List<ConfigVisibilitySummaryItem> CreateVisibilitySummaryItems(
            ModConfigDocument document,
            IEnumerable<ModVisibilityItem> visibilityItems)
        {
            return visibilityItems.Select(visibility =>
            {
                var summaryItem = ConfigVisibilitySummaryItem.FromModel(visibility);
                summaryItem.NavigateLine = GetSectionStartLine(document, visibility.SectionName);
                return summaryItem;
            }).ToList();
        }

        private static bool CanBindVisibilityToParameter(ModParameterDefinition parameter)
        {
            var availableValues = parameter.ValueOptions
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (availableValues.Count == 0 && (parameter.DefaultValue == "0" || parameter.DefaultValue == "1"))
            {
                availableValues.Add("0");
                availableValues.Add("1");
            }

            return parameter.IsDeclaredInConstants &&
                parameter.Kind != ModConfigParameterKind.InternalSystem &&
                parameter.BoundKeySections.Count > 0 &&
                availableValues.Count > 0 &&
                availableValues.All(value => value == "0" || value == "1") &&
                availableValues.Contains("0", StringComparer.OrdinalIgnoreCase) &&
                availableValues.Contains("1", StringComparer.OrdinalIgnoreCase);
        }

        private static bool CanCreateToggleBindingForParameter(ModParameterDefinition parameter)
        {
            return parameter.IsDeclaredInConstants &&
                !IsReservedInternalParameterName(parameter.Name);
        }

        private static bool IsReservedInternalParameterName(string parameterName)
        {
            return parameterName.StartsWith("$required_", StringComparison.OrdinalIgnoreCase) ||
                parameterName.StartsWith("$object_", StringComparison.OrdinalIgnoreCase) ||
                parameterName.StartsWith("$mesh_", StringComparison.OrdinalIgnoreCase) ||
                parameterName.StartsWith("$shapekey_", StringComparison.OrdinalIgnoreCase) ||
                parameterName.StartsWith("$merge_status", StringComparison.OrdinalIgnoreCase) ||
                parameterName.Equals("$mod_id", StringComparison.OrdinalIgnoreCase) ||
                parameterName.Equals("$state_id", StringComparison.OrdinalIgnoreCase) ||
                parameterName.Equals("$mod_enabled", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBinaryVisibilityToggleValues(IEnumerable<string> values)
        {
            var normalizedValues = values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return normalizedValues.Count > 0 &&
                normalizedValues.All(value => value == "0" || value == "1") &&
                normalizedValues.Contains("0", StringComparer.OrdinalIgnoreCase) &&
                normalizedValues.Contains("1", StringComparer.OrdinalIgnoreCase);
        }

        private void RefreshToggleCreationCandidates()
        {
            var currentParameterName = _selectedToggleCreationParameter?.Name ?? _selectedParameterItem?.Name;

            ReplaceCollection(
                _toggleCreationParameterCandidates,
                _selectedParameterItems.Where(item => item.CanCreateToggleBinding));

            SelectedToggleCreationParameter = !string.IsNullOrWhiteSpace(currentParameterName)
                ? _toggleCreationParameterCandidates.FirstOrDefault(item =>
                    item.Name.Equals(currentParameterName, StringComparison.OrdinalIgnoreCase))
                : _toggleCreationParameterCandidates.FirstOrDefault();

            SetToggleCreationMode(DeterminePreferredToggleCreationMode());
            ApplyToggleCreationDefaults(force: false);
        }

        private void RefreshVisibilityBindingCandidates()
        {
            var currentParameterName = _selectedVisibilityBindingParameter?.Name;
            var currentToggleSection = _selectedVisibilityBindingToggle?.SectionName;

            ReplaceCollection(
                _visibilityBindingParameterCandidates,
                _selectedParameterItems.Where(item => item.CanBindVisibilitySafely));
            ReplaceCollection(
                _visibilityBindingToggleCandidates,
                _selectedToggleItems.Where(item => item.CanBindVisibilitySafely));

            SelectedVisibilityBindingParameter = !string.IsNullOrWhiteSpace(currentParameterName)
                ? _visibilityBindingParameterCandidates.FirstOrDefault(item =>
                    item.Name.Equals(currentParameterName, StringComparison.OrdinalIgnoreCase))
                : _visibilityBindingParameterCandidates.FirstOrDefault();
            SelectedVisibilityBindingToggle = !string.IsNullOrWhiteSpace(currentToggleSection)
                ? _visibilityBindingToggleCandidates.FirstOrDefault(item =>
                    item.SectionName.Equals(currentToggleSection, StringComparison.OrdinalIgnoreCase))
                : _visibilityBindingToggleCandidates.FirstOrDefault();
        }

        private void ResetVisibilityBindingSelectionState()
        {
            RefreshVisibilityBindingCandidates();
            RefreshSharedAvailableKeyOptions();
            VisibilityBindingNewParameterName = string.Empty;
            VisibilityBindingNewKeyBindingsText = string.Empty;
            SetVisibilityBindingMode(DeterminePreferredVisibilityBindingMode());
            ApplyVisibilityBindingDefaults(force: true);
            OnPropertyChanged(nameof(CanBindSelectedVisibilityItem));
            OnPropertyChanged(nameof(CanCreateNewVisibilityBinding));
            OnPropertyChanged(nameof(VisibilityBindingStatusText));
        }

        private VisibilityBindingMode DeterminePreferredVisibilityBindingMode()
        {
            if (CanBindSelectedVisibilityItem)
            {
                if (_visibilityBindingParameterCandidates.Count > 0)
                {
                    return VisibilityBindingMode.ExistingParameter;
                }

                if (_visibilityBindingToggleCandidates.Count > 0)
                {
                    return VisibilityBindingMode.ExistingToggle;
                }

                return VisibilityBindingMode.NewParameterAndToggle;
            }

            if (CanCreateNewVisibilityBinding)
            {
                return VisibilityBindingMode.NewParameterAndToggle;
            }

            return VisibilityBindingMode.ExistingParameter;
        }

        private ToggleCreationMode DeterminePreferredToggleCreationMode()
        {
            return _toggleCreationParameterCandidates.Count > 0
                ? ToggleCreationMode.ExistingParameter
                : ToggleCreationMode.NewParameter;
        }

        private void SetToggleCreationMode(ToggleCreationMode mode)
        {
            if (_selectedToggleCreationMode == mode)
            {
                return;
            }

            _selectedToggleCreationMode = mode;
            OnPropertyChanged(nameof(UseExistingToggleCreationParameter));
            OnPropertyChanged(nameof(UseNewToggleCreationParameter));
            ApplyToggleCreationDefaults(force: false);
            CommandManager.InvalidateRequerySuggested();
        }

        private void SetVisibilityBindingMode(VisibilityBindingMode mode)
        {
            if (_selectedVisibilityBindingMode == mode)
            {
                return;
            }

            _selectedVisibilityBindingMode = mode;
            OnPropertyChanged(nameof(UseExistingVisibilityParameterBinding));
            OnPropertyChanged(nameof(UseExistingVisibilityToggleBinding));
            OnPropertyChanged(nameof(UseNewVisibilityBinding));
            ApplyVisibilityBindingDefaults(force: false);
            CommandManager.InvalidateRequerySuggested();
        }

        private void RefreshSharedAvailableKeyOptions()
        {
            ReplaceCollection(_visibilityBindingAvailableKeyOptions, GetUnusedStandardKeyOptions());
        }

        private List<string> GetUnusedStandardKeyOptions()
        {
            if (string.IsNullOrWhiteSpace(StandardToggleTemplatePath) || !_fileSystem.FileExists(StandardToggleTemplatePath))
            {
                return new List<string>();
            }

            try
            {
                var templateDocument = _configParser.ParseFile(StandardToggleTemplatePath);
                var templateAnalysis = _configAnalysisService.Analyze(templateDocument);
                var templateBindings = templateAnalysis.Toggles
                    .SelectMany(toggle => toggle.KeyBindings)
                    .Where(binding => !string.IsNullOrWhiteSpace(binding))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var usedBindings = _selectedToggleItems
                    .SelectMany(item => SplitEditorValues(item.KeyBindingsText))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                return templateBindings
                    .Where(binding => !usedBindings.Contains(binding))
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private void ApplyToggleCreationDefaults(bool force)
        {
            if (_selectedConfigBuffer == null)
            {
                return;
            }

            if (force || string.IsNullOrWhiteSpace(ToggleCreationKeyBindingsText))
            {
                ToggleCreationKeyBindingsText = _visibilityBindingAvailableKeyOptions.FirstOrDefault() ?? string.Empty;
            }
        }

        private void ApplyVisibilityBindingDefaults(bool force)
        {
            if (!CanCreateNewVisibilityBinding)
            {
                return;
            }

            if (force || string.IsNullOrWhiteSpace(VisibilityBindingNewParameterName))
            {
                VisibilityBindingNewParameterName = BuildDefaultVisibilityBindingParameterName(SelectedVisibilityItem?.DrawLabelsText);
            }

            if (force || string.IsNullOrWhiteSpace(VisibilityBindingNewKeyBindingsText))
            {
                VisibilityBindingNewKeyBindingsText = _visibilityBindingAvailableKeyOptions.FirstOrDefault() ?? string.Empty;
            }
        }

        private static string BuildDefaultVisibilityBindingParameterName(string? drawLabelsText)
        {
            var firstLabel = SplitEditorValues(drawLabelsText ?? string.Empty).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(firstLabel))
            {
                return string.Empty;
            }

            var normalizedLabel = firstLabel.Trim();
            var componentSeparatorIndex = normalizedLabel.IndexOf('.');
            if (componentSeparatorIndex >= 0 && componentSeparatorIndex < normalizedLabel.Length - 1)
            {
                normalizedLabel = normalizedLabel.Substring(componentSeparatorIndex + 1);
            }

            var sanitizedCharacters = normalizedLabel
                .Select(character => char.IsLetterOrDigit(character) || character == '_' ? character : '_')
                .ToArray();
            var sanitizedName = new string(sanitizedCharacters).Trim('_');

            if (string.IsNullOrWhiteSpace(sanitizedName))
            {
                sanitizedName = "visibility";
            }

            if (char.IsDigit(sanitizedName[0]))
            {
                sanitizedName = $"component_{sanitizedName}";
            }

            return $"${sanitizedName}";
        }

        private static string NormalizeVariableName(string variableName)
        {
            var normalizedName = (variableName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return string.Empty;
            }

            return normalizedName.StartsWith("$") ? normalizedName : "$" + normalizedName;
        }

        private void SynchronizeRawConfigEditor(
            ModConfigEditBuffer buffer,
            ModConfigDocument document,
            ModConfigAnalysisResult analysis)
        {
            _rawConfigLineEnding = string.IsNullOrWhiteSpace(buffer.LineEnding)
                ? Environment.NewLine
                : buffer.LineEnding;

            if (!string.Equals(_rawConfigEditorText, buffer.Content, StringComparison.Ordinal))
            {
                _rawConfigEditorText = buffer.Content;
                OnPropertyChanged(nameof(RawConfigEditorText));
            }

            ReplaceCollection(_rawConfigHighlights, BuildRawConfigHighlights(document, analysis));
            OnPropertyChanged(nameof(RawConfigHighlights));
            SetRawConfigDirty(false);
        }

        private IEnumerable<ConfigTextHighlightItem> BuildRawConfigHighlights(
            ModConfigDocument document,
            ModConfigAnalysisResult analysis)
        {
            var highlights = new List<ConfigTextHighlightItem>();
            var occupiedLines = new HashSet<int>();

            foreach (var visibility in analysis.VisibilityItems)
            {
                if (!TryGetSectionRange(document, visibility.SectionName, out var startLine, out var endLine))
                {
                    continue;
                }

                highlights.Add(new ConfigTextHighlightItem
                {
                    StartLine = startLine,
                    EndLine = endLine,
                    Kind = ConfigTextHighlightKind.Visibility,
                    Label = visibility.SectionName
                });
                AddLineRange(occupiedLines, startLine, endLine);
            }

            foreach (var toggle in analysis.Toggles)
            {
                if (!TryGetSectionRange(document, toggle.SectionName, out var startLine, out var endLine))
                {
                    continue;
                }

                highlights.Add(new ConfigTextHighlightItem
                {
                    StartLine = startLine,
                    EndLine = endLine,
                    Kind = ConfigTextHighlightKind.Toggle,
                    Label = toggle.SectionName
                });
                AddLineRange(occupiedLines, startLine, endLine);
            }

            var parameterLines = new SortedSet<int>();
            foreach (var parameter in analysis.Parameters.Where(item => !string.IsNullOrWhiteSpace(item.Name)))
            {
                foreach (var line in FindParameterLines(document, parameter.Name))
                {
                    if (!occupiedLines.Contains(line))
                    {
                        parameterLines.Add(line);
                    }
                }
            }

            highlights.AddRange(CreateParameterHighlights(parameterLines));
            return highlights
                .OrderBy(item => item.StartLine)
                .ThenBy(item => item.EndLine)
                .ToList();
        }

        private IEnumerable<int> FindParameterLines(ModConfigDocument document, string variableName)
        {
            var lines = new HashSet<int>();
            foreach (var statement in document.RootStatements)
            {
                if (StatementContainsVariable(statement, variableName))
                {
                    lines.Add(statement.LineNumber);
                }
            }

            foreach (var section in document.Sections)
            {
                foreach (var statement in section.Statements)
                {
                    if (StatementContainsVariable(statement, variableName))
                    {
                        lines.Add(statement.LineNumber);
                    }
                }
            }

            return lines;
        }

        private static bool StatementContainsVariable(ModConfigStatement statement, string variableName)
        {
            return (!string.IsNullOrWhiteSpace(statement.VariableName) &&
                    statement.VariableName.Equals(variableName, StringComparison.OrdinalIgnoreCase)) ||
                   statement.RawText.Contains(variableName, StringComparison.OrdinalIgnoreCase);
        }

        private IEnumerable<ConfigTextHighlightItem> CreateParameterHighlights(IEnumerable<int> lines)
        {
            var orderedLines = lines.OrderBy(line => line).ToList();
            if (orderedLines.Count == 0)
            {
                return Enumerable.Empty<ConfigTextHighlightItem>();
            }

            var highlights = new List<ConfigTextHighlightItem>();
            var startLine = orderedLines[0];
            var previousLine = orderedLines[0];

            for (var index = 1; index < orderedLines.Count; index++)
            {
                var currentLine = orderedLines[index];
                if (currentLine == previousLine + 1)
                {
                    previousLine = currentLine;
                    continue;
                }

                highlights.Add(new ConfigTextHighlightItem
                {
                    StartLine = startLine,
                    EndLine = previousLine,
                    Kind = ConfigTextHighlightKind.Parameter,
                    Label = "参数"
                });

                startLine = currentLine;
                previousLine = currentLine;
            }

            highlights.Add(new ConfigTextHighlightItem
            {
                StartLine = startLine,
                EndLine = previousLine,
                Kind = ConfigTextHighlightKind.Parameter,
                Label = "参数"
            });

            return highlights;
        }

        private static bool TryGetSectionRange(
            ModConfigDocument document,
            string sectionName,
            out int startLine,
            out int endLine)
        {
            var section = document.Sections.FirstOrDefault(item =>
                item.Name.Equals(sectionName, StringComparison.OrdinalIgnoreCase));
            if (section == null)
            {
                startLine = 0;
                endLine = 0;
                return false;
            }

            startLine = section.HeaderLineNumber;
            endLine = section.Statements.Count == 0
                ? section.HeaderLineNumber
                : section.Statements.Max(item => item.LineNumber);
            return true;
        }

        private static int GetSectionStartLine(ModConfigDocument document, string sectionName)
        {
            return TryGetSectionRange(document, sectionName, out var startLine, out _)
                ? startLine
                : 0;
        }

        private static void AddLineRange(HashSet<int> target, int startLine, int endLine)
        {
            for (var line = startLine; line <= endLine; line++)
            {
                target.Add(line);
            }
        }

        private void UpdateRawConfigDirtyState()
        {
            var baselineText = NormalizeLineEndings(_selectedConfigBuffer?.Content ?? string.Empty, _rawConfigLineEnding);
            SetRawConfigDirty(!string.Equals(_rawConfigEditorText, baselineText, StringComparison.Ordinal));
        }

        private void SetRawConfigDirty(bool isDirty)
        {
            if (_isRawConfigDirty == isDirty)
            {
                OnPropertyChanged(nameof(RawConfigEditorStatusText));
                return;
            }

            _isRawConfigDirty = isDirty;
            OnPropertyChanged(nameof(IsRawConfigDirty));
            OnPropertyChanged(nameof(RawConfigEditorStatusText));
            CommandManager.InvalidateRequerySuggested();
        }

        private void ExecuteApplyToggleKeyBindings()
        {
            if (_selectedConfigBuffer == null || SelectedToggleItem == null)
            {
                return;
            }

            try
            {
                var sectionName = SelectedToggleItem.SectionName;
                var normalizedKeyBindings = string.Join(" | ", SplitEditorValues(ToggleKeyBindingEditorText));
                var updatedBuffer = _configUpdateService.UpdateKeyBindings(
                    _selectedConfigBuffer,
                    sectionName,
                    SplitEditorValues(ToggleKeyBindingEditorText));
                ClearStandardizationResults();
                ApplyBufferAnalysis(
                    updatedBuffer,
                    $"已在缓冲区更新 {sectionName} 的快捷键。",
                    preferredToggleSection: sectionName);
                AppendModificationHistory("按键修改", sectionName, $"快捷键更新为 {normalizedKeyBindings}。");
            }
            catch (Exception ex)
            {
                LogManager.Error("ApplyToggleKeyBindings: ", ex);
                _messages.ShowError($"更新快捷键失败：{ex.Message}");
            }
        }

        private void ExecuteApplyStandardization()
        {
            if (_selectedConfigBuffer == null)
            {
                return;
            }

            try
            {
                var templateName = Path.GetFileName(StandardToggleTemplatePath);
                var result = _configUpdateService.StandardizeToggleSlots(_selectedConfigBuffer, StandardToggleTemplatePath);
                ApplyBufferAnalysis(
                    result.Buffer,
                    $"批量标准化完成：完全 {result.FullyStandardizedCount}，部分 {result.PartiallyStandardizedCount}，跳过 {result.SkippedCount}。",
                    preferredParameterName: SelectedParameterItem?.Name,
                    preferredVisibilitySection: SelectedVisibilityItem?.SectionName,
                    preferredVisibilityLabel: SelectedVisibilityItem?.DrawLabelsText);
                ReplaceStandardizationResults(result.Items.Select(ConfigStandardizationSummaryItem.FromModel));
                AppendModificationHistory(
                    "批量标准化",
                    templateName,
                    $"完全 {result.FullyStandardizedCount}，部分 {result.PartiallyStandardizedCount}，跳过 {result.SkippedCount}。");
            }
            catch (Exception ex)
            {
                LogManager.Error("ApplyStandardization: ", ex);
                _messages.ShowError($"批量标准化失败：{ex.Message}");
            }
        }

        private bool CanApplyStandardization()
        {
            return _selectedConfigBuffer != null &&
                !IsRawConfigDirty &&
                _selectedToggleItems.Count > 0 &&
                !string.IsNullOrWhiteSpace(StandardToggleTemplatePath) &&
                _fileSystem.FileExists(StandardToggleTemplatePath);
        }

        private bool CanApplyToggleKeyBindings()
        {
            return _selectedConfigBuffer != null &&
                !IsRawConfigDirty &&
                SelectedToggleItem != null &&
                SplitEditorValues(ToggleKeyBindingEditorText).Count > 0;
        }

        private void ExecuteCreateToggle()
        {
            if (_selectedConfigBuffer == null)
            {
                return;
            }

            try
            {
                var normalizedKeyBindings = SplitEditorValues(ToggleCreationKeyBindingsText);
                ModConfigEditBuffer updatedBuffer;
                string targetParameterName;
                string historySummary;

                switch (_selectedToggleCreationMode)
                {
                    case ToggleCreationMode.ExistingParameter:
                        if (SelectedToggleCreationParameter == null)
                        {
                            return;
                        }

                        targetParameterName = SelectedToggleCreationParameter.Name;
                        updatedBuffer = _configUpdateService.CreateToggleBinding(
                            _selectedConfigBuffer,
                            targetParameterName,
                            normalizedKeyBindings);
                        historySummary = $"绑定到现有参数 {targetParameterName}，快捷键 {string.Join(" | ", normalizedKeyBindings)}。";
                        break;

                    default:
                        targetParameterName = NormalizeVariableName(ToggleCreationNewParameterName);
                        updatedBuffer = _configUpdateService.CreateParameter(_selectedConfigBuffer, targetParameterName);
                        updatedBuffer = _configUpdateService.CreateToggleBinding(
                            updatedBuffer,
                            targetParameterName,
                            normalizedKeyBindings);
                        historySummary = $"新建参数 {targetParameterName}，快捷键 {string.Join(" | ", normalizedKeyBindings)}。";
                        break;
                }
                ToggleCreationNewParameterName = string.Empty;
                ToggleCreationKeyBindingsText = string.Empty;
                ClearStandardizationResults();
                ApplyBufferAnalysis(
                    updatedBuffer,
                    $"已在缓冲区新增按键绑定 {targetParameterName}。",
                    preferredParameterName: targetParameterName);
                AppendModificationHistory("新增按键", targetParameterName, historySummary);
            }
            catch (Exception ex)
            {
                LogManager.Error("CreateToggle: ", ex);
                _messages.ShowError($"新增按键失败：{ex.Message}");
            }
        }

        private bool CanCreateToggle()
        {
            if (_selectedConfigBuffer == null ||
                IsRawConfigDirty ||
                SplitEditorValues(ToggleCreationKeyBindingsText).Count == 0)
            {
                return false;
            }

            return _selectedToggleCreationMode switch
            {
                ToggleCreationMode.ExistingParameter => SelectedToggleCreationParameter != null,
                _ => !string.IsNullOrWhiteSpace(NormalizeVariableName(ToggleCreationNewParameterName))
            };
        }

        private void ExecuteRenameParameter()
        {
            if (_selectedConfigBuffer == null || SelectedParameterItem == null)
            {
                return;
            }

            try
            {
                var originalName = SelectedParameterItem.Name;
                var newName = ParameterRenameText.Trim();
                var updatedBuffer = _configUpdateService.RenameParameter(
                    _selectedConfigBuffer,
                    originalName,
                    newName);
                ClearStandardizationResults();
                ApplyBufferAnalysis(
                    updatedBuffer,
                    $"已在缓冲区将 {originalName} 重命名为 {newName}。",
                    preferredParameterName: newName);
                AppendModificationHistory("参数重命名", originalName, $"重命名为 {newName}。");
            }
            catch (Exception ex)
            {
                LogManager.Error("RenameParameter: ", ex);
                _messages.ShowError($"参数重命名失败：{ex.Message}");
            }
        }

        private bool CanRenameParameter()
        {
            return _selectedConfigBuffer != null &&
                !IsRawConfigDirty &&
                SelectedParameterItem != null &&
                SelectedParameterItem.CanRename &&
                !string.IsNullOrWhiteSpace(ParameterRenameText);
        }

        private void ExecuteCreateParameter()
        {
            if (_selectedConfigBuffer == null)
            {
                return;
            }

            try
            {
                var newParameterName = NormalizeVariableName(ParameterCreationName);
                var updatedBuffer = _configUpdateService.CreateParameter(_selectedConfigBuffer, newParameterName);
                ParameterCreationName = string.Empty;
                ClearStandardizationResults();
                ApplyBufferAnalysis(
                    updatedBuffer,
                    $"已在缓冲区新增参数 {newParameterName}。",
                    preferredParameterName: newParameterName);
                AppendModificationHistory("新增参数", newParameterName, "默认值为 1。");
            }
            catch (Exception ex)
            {
                LogManager.Error("CreateParameter: ", ex);
                _messages.ShowError($"新增参数失败：{ex.Message}");
            }
        }

        private bool CanCreateParameter()
        {
            return _selectedConfigBuffer != null &&
                !IsRawConfigDirty &&
                !string.IsNullOrWhiteSpace(NormalizeVariableName(ParameterCreationName));
        }

        private void ExecuteApplyVisibilityChange()
        {
            if (_selectedConfigBuffer == null || SelectedVisibilityItem == null)
            {
                return;
            }

            try
            {
                var sectionName = SelectedVisibilityItem.SectionName;
                var drawLabelsText = SelectedVisibilityItem.DrawLabelsText;
                var targetStateText = VisibilityTargetIsVisible ? "显示" : "隐藏";
                var updatedBuffer = _configUpdateService.ToggleVisibility(
                    _selectedConfigBuffer,
                    sectionName,
                    drawLabelsText,
                    VisibilityTargetIsVisible);
                ClearStandardizationResults();
                ApplyBufferAnalysis(
                    updatedBuffer,
                    $"已在缓冲区将 {drawLabelsText} 设置为{targetStateText}。",
                    preferredVisibilitySection: sectionName,
                    preferredVisibilityLabel: drawLabelsText);
                AppendModificationHistory(
                    "模型显示修改",
                    string.IsNullOrWhiteSpace(drawLabelsText) ? sectionName : drawLabelsText,
                    $"[{sectionName}] 设置为{targetStateText}。");
            }
            catch (Exception ex)
            {
                LogManager.Error("ApplyVisibilityChange: ", ex);
                _messages.ShowError($"更新模型显示失败：{ex.Message}");
            }
        }

        private bool CanApplyVisibilityChange()
        {
            return _selectedConfigBuffer != null &&
                !IsRawConfigDirty &&
                SelectedVisibilityItem != null &&
                SelectedVisibilityItem.CanToggleSafely;
        }

        private void ExecuteApplyVisibilityBinding()
        {
            if (_selectedConfigBuffer == null || SelectedVisibilityItem == null)
            {
                return;
            }

            try
            {
                var sectionName = SelectedVisibilityItem.SectionName;
                var drawLabelsText = SelectedVisibilityItem.DrawLabelsText;
                ModConfigEditBuffer updatedBuffer;
                string historySummary;

                switch (_selectedVisibilityBindingMode)
                {
                    case VisibilityBindingMode.ExistingParameter:
                        if (SelectedVisibilityBindingParameter == null)
                        {
                            return;
                        }

                        updatedBuffer = _configUpdateService.BindVisibilityToParameter(
                            _selectedConfigBuffer,
                            sectionName,
                            drawLabelsText,
                            SelectedVisibilityBindingParameter.Name);
                        historySummary = $"绑定到现有参数 {SelectedVisibilityBindingParameter.Name}。";
                        break;

                    case VisibilityBindingMode.ExistingToggle:
                        if (SelectedVisibilityBindingToggle == null ||
                            string.IsNullOrWhiteSpace(SelectedVisibilityBindingToggle.PrimaryVariableName))
                        {
                            return;
                        }

                        updatedBuffer = _configUpdateService.BindVisibilityToParameter(
                            _selectedConfigBuffer,
                            sectionName,
                            drawLabelsText,
                            SelectedVisibilityBindingToggle.PrimaryVariableName);
                        historySummary = $"复用 {SelectedVisibilityBindingToggle.SectionName} 当前控制的参数 {SelectedVisibilityBindingToggle.PrimaryVariableName}。";
                        break;

                    default:
                        var newVariableName = NormalizeVariableName(VisibilityBindingNewParameterName);
                        var newKeyBindings = SplitEditorValues(VisibilityBindingNewKeyBindingsText);
                        updatedBuffer = _configUpdateService.CreateVisibilityBinding(
                            _selectedConfigBuffer,
                            sectionName,
                            drawLabelsText,
                            newVariableName,
                            newKeyBindings);
                        historySummary = $"新建参数 {newVariableName} 与快捷键 {string.Join(" | ", newKeyBindings)}。";
                        break;
                }

                ClearStandardizationResults();
                ApplyBufferAnalysis(
                    updatedBuffer,
                    $"已在缓冲区为 {drawLabelsText} 建立控制绑定。",
                    preferredVisibilitySection: sectionName,
                    preferredVisibilityLabel: drawLabelsText);
                AppendModificationHistory(
                    "模型绑定",
                    string.IsNullOrWhiteSpace(drawLabelsText) ? sectionName : drawLabelsText,
                    historySummary);
            }
            catch (Exception ex)
            {
                LogManager.Error("ApplyVisibilityBinding: ", ex);
                _messages.ShowError($"模型绑定失败：{ex.Message}");
            }
        }

        private bool CanApplyVisibilityBinding()
        {
            if (_selectedConfigBuffer == null ||
                IsRawConfigDirty ||
                SelectedVisibilityItem == null)
            {
                return false;
            }

            return _selectedVisibilityBindingMode switch
            {
                VisibilityBindingMode.ExistingParameter => CanBindSelectedVisibilityItem && SelectedVisibilityBindingParameter != null,
                VisibilityBindingMode.ExistingToggle => SelectedVisibilityBindingToggle != null &&
                    !string.IsNullOrWhiteSpace(SelectedVisibilityBindingToggle.PrimaryVariableName) &&
                    CanBindSelectedVisibilityItem,
                _ => CanCreateNewVisibilityBinding &&
                    !string.IsNullOrWhiteSpace(NormalizeVariableName(VisibilityBindingNewParameterName)) &&
                    SplitEditorValues(VisibilityBindingNewKeyBindingsText).Count > 0
            };
        }

        private void ExecuteSaveRawConfig()
        {
            if (string.IsNullOrWhiteSpace(SelectedConfigPath))
            {
                return;
            }

            var contentToSave = NormalizeLineEndings(RawConfigEditorText, _rawConfigLineEnding);
            var rawBuffer = new ModConfigEditBuffer
            {
                SourcePath = SelectedConfigPath,
                Content = contentToSave,
                LineEnding = _rawConfigLineEnding
            };

            try
            {
                _configUpdateService.SaveBuffer(rawBuffer, SelectedConfigPath);
                ApplyBufferAnalysis(
                    _configUpdateService.LoadBuffer(SelectedConfigPath),
                    "已将右侧原文保存到当前配置文件。",
                    preferredToggleSection: SelectedToggleItem?.SectionName,
                    preferredParameterName: SelectedParameterItem?.Name,
                    preferredVisibilitySection: SelectedVisibilityItem?.SectionName,
                    preferredVisibilityLabel: SelectedVisibilityItem?.DrawLabelsText);
                _messages.ShowInfo(SelectedConfigEditStatus);
            }
            catch (Exception ex)
            {
                LogManager.Error("SaveRawConfig: ", ex);
                _messages.ShowError($"保存右侧原文失败：{ex.Message}");
            }
        }

        private bool CanSaveRawConfig()
        {
            return _selectedConfigBuffer != null &&
                !string.IsNullOrWhiteSpace(SelectedConfigPath) &&
                IsRawConfigDirty;
        }

        private void ExecuteSaveConfigToMod()
        {
            ExecuteSaveConfig(ModConfigSaveTarget.ModDirectory);
        }

        private void ExecuteSaveConfigToWwmi()
        {
            ExecuteSaveConfig(ModConfigSaveTarget.WwmiDirectory);
        }

        private void ExecuteSaveConfig(ModConfigSaveTarget saveTarget)
        {
            if (_selectedConfigBuffer == null)
            {
                return;
            }

            try
            {
                var preview = _configUpdateService.PreviewSaveTarget(SelectedConfigPath, saveTarget, ModFolderPath, WwmiFolderPath);
                if (!_messages.Confirm($"即将把当前缓冲内容写入：\n{preview.TargetPath}\n\n该操作{GetTargetActionText(preview.TargetExists)}，是否继续？", "确认保存"))
                {
                    return;
                }

                var saveResult = _configUpdateService.SaveBufferToTarget(
                    _selectedConfigBuffer,
                    saveTarget,
                    ModFolderPath,
                    WwmiFolderPath);

                if (IsSourceSaveTarget(saveTarget))
                {
                    ApplyBufferAnalysis(
                        _configUpdateService.LoadBuffer(SelectedConfigPath),
                        $"已保存到 {saveResult.TargetPath}。",
                        preferredToggleSection: SelectedToggleItem?.SectionName,
                        preferredParameterName: SelectedParameterItem?.Name,
                        preferredVisibilitySection: SelectedVisibilityItem?.SectionName,
                        preferredVisibilityLabel: SelectedVisibilityItem?.DrawLabelsText);
                }
                else
                {
                    SelectedConfigEditStatus = $"已导出到 {saveResult.TargetPath}。";
                }

                OnPathPreviewChanged();

                _messages.ShowInfo(SelectedConfigEditStatus);
            }
            catch (Exception ex)
            {
                LogManager.Error("SaveConfig: ", ex);
                _messages.ShowError($"保存配置失败：{ex.Message}");
            }
        }

        private bool CanSaveConfigToMod()
        {
            return _selectedConfigBuffer != null &&
                !IsRawConfigDirty &&
                HasPendingConfigChanges &&
                !string.IsNullOrWhiteSpace(ModFolderPath) &&
                !string.IsNullOrWhiteSpace(SelectedConfigPath);
        }

        private bool CanSaveConfigToWwmi()
        {
            return _selectedConfigBuffer != null &&
                !IsRawConfigDirty &&
                HasPendingConfigChanges &&
                !string.IsNullOrWhiteSpace(WwmiFolderPath) &&
                !string.IsNullOrWhiteSpace(SelectedConfigPath);
        }

        private void ExecuteSyncModToWwmi()
        {
            ExecuteSyncConfig(ModConfigSyncDirection.ModToWwmi);
        }

        private void ExecuteSyncWwmiToMod()
        {
            ExecuteSyncConfig(ModConfigSyncDirection.WwmiToMod);
        }

        private void ExecuteSyncConfig(ModConfigSyncDirection direction)
        {
            if (string.IsNullOrWhiteSpace(SelectedConfigPath))
            {
                return;
            }

            try
            {
                var preview = _configUpdateService.PreviewSync(SelectedConfigPath, direction, ModFolderPath, WwmiFolderPath);
                if (!_messages.Confirm($"即将同步配置文件：\n源：{preview.SourcePath}\n目标：{preview.TargetPath}\n\n该操作{GetTargetActionText(preview.TargetExists)}，是否继续？", "确认同步"))
                {
                    return;
                }

                var syncResult = _configUpdateService.SyncConfig(SelectedConfigPath, direction, ModFolderPath, WwmiFolderPath);
                SelectedConfigEditStatus = $"已同步 {syncResult.SourcePath} -> {syncResult.TargetPath}。";
                OnPathPreviewChanged();
                _messages.ShowInfo(SelectedConfigEditStatus);
            }
            catch (Exception ex)
            {
                LogManager.Error("SyncConfig: ", ex);
                _messages.ShowError($"同步配置失败：{ex.Message}");
            }
        }

        private bool CanSyncModToWwmi()
        {
            return CanSyncConfig(ModConfigSyncDirection.ModToWwmi);
        }

        private bool CanSyncWwmiToMod()
        {
            return CanSyncConfig(ModConfigSyncDirection.WwmiToMod);
        }

        private bool CanSyncConfig(ModConfigSyncDirection direction)
        {
            if (!CanSyncConfigBase())
            {
                return false;
            }

            try
            {
                var preview = _configUpdateService.PreviewSync(SelectedConfigPath, direction, ModFolderPath, WwmiFolderPath);
                return _fileSystem.FileExists(preview.SourcePath) && preview.TargetExists;
            }
            catch
            {
                return false;
            }
        }

        private bool CanSyncConfigBase()
        {
            return !IsRawConfigDirty &&
                !HasPendingConfigChanges &&
                !string.IsNullOrWhiteSpace(SelectedConfigPath) &&
                !string.IsNullOrWhiteSpace(ModFolderPath) &&
                !string.IsNullOrWhiteSpace(WwmiFolderPath);
        }

        private bool IsSourceSaveTarget(ModConfigSaveTarget saveTarget)
        {
            if (saveTarget == ModConfigSaveTarget.ModDirectory && IsPathUnderRoot(SelectedConfigPath, ModFolderPath))
            {
                return true;
            }

            if (saveTarget == ModConfigSaveTarget.WwmiDirectory && IsPathUnderRoot(SelectedConfigPath, WwmiFolderPath))
            {
                return true;
            }

            var configDirectory = Path.GetDirectoryName(SelectedConfigPath);
            if (string.IsNullOrWhiteSpace(configDirectory))
            {
                return false;
            }

            var (characterName, id, modName) = ModPathHelper.ParseWwmiFolderPath(configDirectory);
            if (!string.IsNullOrWhiteSpace(characterName) &&
                !string.IsNullOrWhiteSpace(id) &&
                !string.IsNullOrWhiteSpace(modName))
            {
                return saveTarget == ModConfigSaveTarget.WwmiDirectory;
            }

            var folderName = Path.GetFileName(configDirectory);
            var parentFolder = Path.GetDirectoryName(configDirectory);
            var (modId, parsedModName) = ModPathHelper.ParseModFolderName(folderName);
            var parsedCharacterName = ModPathHelper.GetCharacterNameFromFolder(parentFolder ?? string.Empty);
            return !string.IsNullOrWhiteSpace(modId) &&
                !string.IsNullOrWhiteSpace(parsedModName) &&
                !string.IsNullOrWhiteSpace(parsedCharacterName) &&
                saveTarget == ModConfigSaveTarget.ModDirectory;
        }

        private static bool IsPathUnderRoot(string filePath, string rootPath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(rootPath))
            {
                return false;
            }

            var normalizedRoot = EnsureTrailingSeparator(Path.GetFullPath(rootPath));
            var normalizedPath = Path.GetFullPath(filePath);
            return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static string EnsureTrailingSeparator(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        private static List<string> SplitEditorValues(string text)
        {
            return text
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split(new[] { '\n', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();
        }

        private void OnPathPreviewChanged()
        {
            OnPropertyChanged(nameof(SaveToModPreviewPath));
            OnPropertyChanged(nameof(SaveToWwmiPreviewPath));
            OnPropertyChanged(nameof(SyncModToWwmiPreviewText));
            OnPropertyChanged(nameof(SyncWwmiToModPreviewText));
            CommandManager.InvalidateRequerySuggested();
        }

        private string GetSavePreviewPath(ModConfigSaveTarget saveTarget)
        {
            if (string.IsNullOrWhiteSpace(SelectedConfigPath))
            {
                return "未选择配置文件。";
            }

            if (saveTarget == ModConfigSaveTarget.ModDirectory && string.IsNullOrWhiteSpace(ModFolderPath))
            {
                return "未设置 Mod 目录。";
            }

            if (saveTarget == ModConfigSaveTarget.WwmiDirectory && string.IsNullOrWhiteSpace(WwmiFolderPath))
            {
                return "未设置 WWMI 目录。";
            }

            try
            {
                var preview = _configUpdateService.PreviewSaveTarget(SelectedConfigPath, saveTarget, ModFolderPath, WwmiFolderPath);
                return $"{preview.TargetPath}（{GetTargetActionText(preview.TargetExists)}）";
            }
            catch (Exception ex)
            {
                return $"无法解析：{ex.Message}";
            }
        }

        private string GetSyncPreviewText(ModConfigSyncDirection direction)
        {
            if (string.IsNullOrWhiteSpace(SelectedConfigPath))
            {
                return "未选择配置文件。";
            }

            if (string.IsNullOrWhiteSpace(ModFolderPath) || string.IsNullOrWhiteSpace(WwmiFolderPath))
            {
                return "未设置 Mod/WWMI 目录。";
            }

            try
            {
                var preview = _configUpdateService.PreviewSync(SelectedConfigPath, direction, ModFolderPath, WwmiFolderPath);
                return $"{preview.SourcePath} -> {preview.TargetPath}（{GetTargetActionText(preview.TargetExists)}）";
            }
            catch (Exception ex)
            {
                return $"无法解析：{ex.Message}";
            }
        }

        private static string GetTargetActionText(bool targetExists)
        {
            return targetExists ? "将覆盖已有文件" : "将创建新文件";
        }

        private static string NormalizeLineEndings(string? text, string lineEnding)
        {
            var normalizedText = (text ?? string.Empty)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace("\r", "\n", StringComparison.Ordinal);

            var targetLineEnding = string.IsNullOrEmpty(lineEnding)
                ? Environment.NewLine
                : lineEnding;

            return normalizedText.Replace("\n", targetLineEnding, StringComparison.Ordinal);
        }

        private string ResolveDefaultStandardToggleTemplatePath()
        {
            var configuredPath = string.IsNullOrWhiteSpace(AppConfig.StandardToggleTemplatePath)
                ? Path.Combine("docs", "toggle.ini")
                : AppConfig.StandardToggleTemplatePath;

            var candidates = GetStandardToggleTemplatePathCandidates(configuredPath);

            return candidates.FirstOrDefault(_fileSystem.FileExists) ?? string.Empty;
        }

        private static string[] GetStandardToggleTemplatePathCandidates(string configuredPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                return Array.Empty<string>();
            }

            if (Path.IsPathRooted(configuredPath))
            {
                return new[] { Path.GetFullPath(configuredPath) };
            }

            return new[]
            {
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath)),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", configuredPath)),
                Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, configuredPath)),
                Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..", configuredPath))
            };
        }

        private void ExecuteOpenModFolderBySelected()
        {
            ExecuteOpenModFolder(SelectedDirectoryItem);
        }

        private bool CanOpenModFolderBySelected()
        {
            return CanOpenModFolder(SelectedDirectoryItem);
        }

        private bool GetMod(out int characterCount, out int modCount)
        {
            characterCount = 0;
            modCount = 0;

            string path = ModFolderPath;
            _allMods = new List<WuwaMods>();
            if (!string.IsNullOrWhiteSpace(path))
            {
                try
                {
                    var folders = _fileSystem.GetDirectories(path);
                    foreach (var folder in folders)
                    {
                        if (IgnoreOther && folder.Equals(_otherFolderPath))
                        {
                            continue;
                        }

                        var characterName = ModPathHelper.GetCharacterNameFromFolder(folder);
                        if (string.IsNullOrWhiteSpace(characterName))
                        {
                            continue;
                        }

                        var mods = new WuwaMods
                        {
                            CharacterName = characterName,
                            Folder = folder,
                            Mods = new List<WuwaMod>()
                        };

                        var modFolders = _fileSystem.GetDirectories(folder);
                        foreach (var modFolder in modFolders)
                        {
                            var folderName = ModPathHelper.GetCharacterNameFromFolder(modFolder);
                            var (id, modName) = ModPathHelper.ParseModFolderName(folderName);

                            var mod = new WuwaMod
                            {
                                CharacterName = characterName,
                                FullPath = modFolder,
                                Id = id,
                                ModName = modName
                            };
                            mods.Mods.Add(mod);
                        }
                        modCount += modFolders.Count();
                        _allMods.Add(mods);
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Error("GetMod: ", ex);
                    return false;
                }

                if (modCount == 0)
                {
                    return true;
                }
                else
                {
                    characterCount = _allMods.Count();
                    return true;
                }
            }
            else
            {
                return false;
            }
        }

        private bool GetWwmi(out int modCount)
        {
            modCount = 0;
            string path = WwmiFolderPath;
            _wwmiMods = new List<WuwaMod>();
            if (!string.IsNullOrWhiteSpace(path))
            {
                try
                {
                    var folders = _fileSystem.GetDirectories(path);
                    foreach (var folder in folders)
                    {
                        var (characterName, id, modName) = ModPathHelper.ParseWwmiFolderPath(folder);
                        if (string.IsNullOrWhiteSpace(characterName) || string.IsNullOrWhiteSpace(modName))
                        {
                            continue;
                        }

                        if (characterName.ToLower().Contains("weapon"))
                        {
                            continue;
                        }

                        var mod = new WuwaMod
                        {
                            CharacterName = characterName,
                            FullPath = folder,
                            Id = id,
                            ModName = modName
                        };

                        _wwmiMods.Add(mod);
                        modCount++;
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Error("GetWwmi: ", ex);
                    return false;
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        private bool ClearWwmi(out int modCount)
        {
            modCount = 0;
            if (_wwmiMods.Count == 0)
            {
                if (!GetWwmi(out modCount))
                {
                    return false;
                }
            }
            try
            {
                foreach (var mod in _wwmiMods)
                {
                    _fileSystem.DeleteDirectory(mod.FullPath, true);
                }
                modCount = _wwmiMods.Count;
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Error("ClearWwmi: ", ex);
                return false;
            }
        }

        private bool RandomLoadMods(out int modCount, out List<WuwaMod> loadedMods)
        {
            modCount = 0;
            loadedMods = new List<WuwaMod>();

            if (_allMods.Count == 0)
            {
                if (!GetMod(out _, out _))
                {
                    return false;
                }
            }
            try
            {
                var ran = new Random();
                foreach (var mods in _allMods)
                {
                    if (IgnoreWeapon && mods.CharacterName.Contains("weapon")) continue;
                    var count = mods.Mods.Count;
                    if (count == 0) continue;
                    var index = ran.Next(count);
                    var mod = mods.Mods[index];
                    _fileSystem.CopyDirectory(mod.FullPath, $"{_wwmiFolderPath}\\[{mod.CharacterName}][{mod.Id}]{mod.ModName}");
                    loadedMods.Add(mod);
                    modCount++;
                }
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Error("RandomLoadMods: ", ex);
                return false;
            }
        }

        private void LoadDirectoryTree()
        {
            DirectoryItems.Clear();
            if (_allMods == null || _allMods.Count == 0)
                return;

            foreach (var mods in _allMods)
            {
                if (IgnoreWeapon && mods.CharacterName.Contains("weapon"))
                    continue;

                var characterItem = new DirectoryItemViewModel
                {
                    Name = mods.CharacterName,
                    FullPath = mods.Folder,
                    IsDirectory = true,
                    IsChecked = false
                };

                foreach (var mod in mods.Mods)
                {
                    var modItem = new DirectoryItemViewModel
                    {
                        Name = mod.ModName,
                        Id = mod.Id,
                        FullPath = mod.FullPath,
                        IsDirectory = false,
                        IsChecked = false,
                        Parent = characterItem
                    };
                    characterItem.Children.Add(modItem);
                }

                DirectoryItems.Add(characterItem);
            }
        }

        private void LoadSelectedMods()
        {
            var selectedMods = new List<DirectoryItemViewModel>();
            CollectSelectedItems(DirectoryItems, selectedMods);

            if (selectedMods.Count == 0)
            {
                _messages.ShowInfo("请至少选择一个MOD。");
                return;
            }

            if (selectedMods.GroupBy(s => s.Parent).Where(s => s.Count() > 1).Count() > 0)
            {
                _messages.ShowInfo("每个角色只能选择一个MOD。");
                return;
            }

            if (_wwmiMods.Count == 0)
            {
                if (!GetWwmi(out _))
                {
                    _messages.ShowError("加载已安装MOD失败。");
                    return;
                }
            }

            int successCount = 0;
            int skipCount = 0;
            int failCount = 0;

            try
            {
                foreach (var item in selectedMods)
                {
                    if (!item.IsDirectory && !string.IsNullOrEmpty(item.FullPath))
                    {
                        try
                        {
                            var modName = item.Name;
                            var characterName = item.Parent?.Name ?? "";
                            var id = item.Id;
                            if (string.IsNullOrEmpty(characterName) || string.IsNullOrEmpty(id))
                            {
                                failCount++;
                                continue;
                            }

                            if (!Directory.Exists(item.FullPath))
                            {
                                failCount++;
                                continue;
                            }

                            var destinationPath = $"{_wwmiFolderPath}\\[{characterName}][{id}]{modName}";
                            var exist = _wwmiMods.Where(s => s.CharacterName == characterName).FirstOrDefault();
                            // 如果目标目录不存在，复制
                            if (exist == null)
                            {
                                _fileSystem.CopyDirectory(item.FullPath, destinationPath);
                            }
                            // 如果目标目录已存在，跳过
                            else if (exist.Id == id)
                            {
                                skipCount++;
                                continue;
                            }
                            // 存在相同characterName的其他MOD，先删除再复制
                            else
                            {
                                if (_fileSystem.DirectoryExists(exist.FullPath))
                                {
                                    _fileSystem.DeleteDirectory(exist.FullPath, true);
                                }
                                _fileSystem.CopyDirectory(item.FullPath, destinationPath);
                            }
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            LogManager.Error($"LoadSelectedMods - Failed to load MOD {item.Name}: ", ex);
                            failCount++;
                        }
                    }
                }

                //更新已安装MOD
                if (successCount > 0 && !GetWwmi(out _))
                {
                    _messages.ShowError("加载已安装MOD失败。");
                    return;
                }

                _messages.ShowInfo($"成功加载 {successCount} 个MOD{(skipCount > 0 ? $"，跳过 {skipCount} 个" : "")}{(failCount > 0 ? $"，失败 {failCount} 个" : "")}。");
            }
            catch (Exception ex)
            {
                LogManager.Error("LoadSelectedMods: ", ex);
                _messages.ShowError($"加载MOD时发生错误：{ex.Message}");
            }
        }

        private void CollectSelectedItems(ObservableCollection<DirectoryItemViewModel> items, List<DirectoryItemViewModel> selectedItems)
        {
            foreach (var item in items)
            {
                if (item.IsChecked && !item.IsDirectory)
                {
                    selectedItems.Add(item);
                }
                if (item.Children.Count > 0)
                {
                    CollectSelectedItems(item.Children, selectedItems);
                }
            }
        }

        private void SelectLoadedMods()
        {
            // 首先清除所有选中状态
            ClearSelection(DirectoryItems);

            if (_wwmiMods == null || _wwmiMods.Count == 0)
                return;

            // 遍历目录树，选中已加载的MOD
            foreach (var wwmiMod in _wwmiMods)
            {
                var characterItem = DirectoryItems.Where(d => d.Name == wwmiMod.CharacterName).FirstOrDefault();
                if (characterItem != null)
                {
                    // 展开角色节点
                    characterItem.IsSelected = true;

                    // 在子节点中查找匹配的MOD
                    // WWMI中的MOD名称格式：[ID]ModName（去掉[CharacterName]后的部分）
                    // 目录树中的MOD名称格式：[ID]ModName
                    // 直接比较MOD名称（都是[ID]ModName格式）
                    var modItem = characterItem.Children.Where(c => c.Name == wwmiMod.ModName && c.Id == wwmiMod.Id).FirstOrDefault();
                    if (modItem != null)
                    {
                        modItem.IsSelected = true;
                        modItem.IsChecked = true;
                    }
                }
            }
        }

        private void ClearSelection(ObservableCollection<DirectoryItemViewModel> items)
        {
            foreach (var item in items)
            {
                item.IsSelected = false;
                item.IsChecked = false;
                if (item.Children.Count > 0)
                {
                    ClearSelection(item.Children);
                }
            }
        }

        private void SelectRandomLoadedMods(List<WuwaMod> loadedMods)
        {
            // 首先清除所有选中状态
            ClearSelection(DirectoryItems);

            if (loadedMods == null || loadedMods.Count == 0)
                return;

            // 如果目录树未加载，先加载
            if (DirectoryItems.Count == 0)
            {
                LoadDirectoryTree();
            }

            // 遍历目录树，选中随机加载的MOD
            foreach (var loadedMod in loadedMods)
            {
                foreach (var characterItem in DirectoryItems)
                {
                    if (characterItem.Name == loadedMod.CharacterName)
                    {
                        // 展开角色节点
                        characterItem.IsSelected = true;

                        // 在子节点中查找匹配的MOD
                        foreach (var modItem in characterItem.Children)
                        {
                            // 比较MOD名称（都是[ID]ModName格式）
                            if (modItem.Name == loadedMod.ModName)
                            {
                                modItem.IsSelected = true;
                                modItem.IsChecked = true;
                                break;
                            }
                        }
                        break;
                    }
                }
            }
        }

        private void ExecuteOpenModFolder(DirectoryItemViewModel? item)
        {
            if (item == null || string.IsNullOrEmpty(item.FullPath))
                return;

            try
            {
                if (_fileSystem.DirectoryExists(item.FullPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = item.FullPath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    _messages.ShowError("文件夹不存在。");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error("OpenModFolder: ", ex);
                _messages.ShowError($"打开文件夹时发生错误：{ex.Message}");
            }
        }

        private bool CanOpenModFolder(DirectoryItemViewModel? item)
        {
            return item != null && !string.IsNullOrEmpty(item.FullPath);
        }
    }
}

