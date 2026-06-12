using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using WuwaModModifier.Common;
using WuwaModModifier.Data.ViewModels;
using WuwaModModifier.Model;

namespace WuwaModModifier.ViewModels
{
    /// <summary>
    /// Manages the directory tree, mod loading, and WWMI interactions.
    /// Extracted from MainViewModel as part of R-02 refactoring.
    /// </summary>
    public class ModDirectoryViewModel : ViewModelBase
    {
        private readonly IMainViewModelSession _session;
        private readonly IFileSystemService _fileSystem;
        private readonly IMessageService _messages;
        private readonly IAppConfigService _appConfig;
        private readonly IModConfigDiscoveryService _configDiscoveryService;

        private string _modFolderPath;
        private string _wwmiFolderPath;
        private string _modPathLoadStatusText;
        private string _wwmiPathLoadStatusText;
        private string _otherFolderPath;
        private bool _ignoreWeapon;
        private bool _ignoreOther;
        private List<WuwaMods> _allMods;
        private List<WuwaMod> _wwmiMods;
        private ObservableCollection<DirectoryItemViewModel> _directoryItems;
        private DirectoryItemViewModel? _selectedDirectoryItem;

        public ModDirectoryViewModel(
            IMainViewModelSession session,
            IFileSystemService fileSystem,
            IMessageService messages,
            IAppConfigService appConfig,
            IModConfigDiscoveryService configDiscoveryService)
        {
            _session = session;
            _fileSystem = fileSystem;
            _messages = messages;
            _appConfig = appConfig;
            _configDiscoveryService = configDiscoveryService;

            _modFolderPath = _appConfig.DefaultModPath;
            _wwmiFolderPath = _appConfig.DefaultWwmiPath;
            _modPathLoadStatusText = string.Empty;
            _wwmiPathLoadStatusText = string.Empty;
            _otherFolderPath = _appConfig.OtherFolderPath;
            _ignoreWeapon = true;
            _ignoreOther = true;
            _allMods = new List<WuwaMods>();
            _wwmiMods = new List<WuwaMod>();
            _directoryItems = new ObservableCollection<DirectoryItemViewModel>();

            BtnModPathCommand = new RelayCommand(ExecuteBtnModPath);
            BtnWwmiPathCommand = new RelayCommand(ExecuteBtnWwmiPath);
            BtnClearAllModsCommand = new RelayCommand(ExecuteBtnClearAllMods);
            BtnRandomLoadAllModsCommand = new RelayCommand(ExecuteBtnRandomLoadAllMods);
            BtnLoadSelectedModsCommand = new RelayCommand(ExecuteBtnLoadSelectedMods);
            OpenModFolderCommand = new RelayCommand(ExecuteOpenModFolderBySelected, CanOpenModFolderBySelected);
        }

        // ── Paths ──

        public string ModFolderPath { get => _modFolderPath; set => SetProperty(ref _modFolderPath, value); }
        public string WwmiFolderPath { get => _wwmiFolderPath; set => SetProperty(ref _wwmiFolderPath, value); }
        public string OtherFolderPath { get => _otherFolderPath; set => SetProperty(ref _otherFolderPath, value); }

        public string ModPathLoadStatusText { get => _modPathLoadStatusText; set => SetProperty(ref _modPathLoadStatusText, value); }
        public string WwmiPathLoadStatusText { get => _wwmiPathLoadStatusText; set => SetProperty(ref _wwmiPathLoadStatusText, value); }

        // ── Filters ──

        public bool IgnoreWeapon { get => _ignoreWeapon; set => SetProperty(ref _ignoreWeapon, value); }
        public bool IgnoreOther { get => _ignoreOther; set => SetProperty(ref _ignoreOther, value); }

        // ── Tree ──

        public ObservableCollection<DirectoryItemViewModel> DirectoryItems
        {
            get => _directoryItems;
            set => SetProperty(ref _directoryItems, value);
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
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        // ── Computed ──

        public bool CanOpenVersionSync =>
            SelectedDirectoryItem != null &&
            SelectedDirectoryItem.IsDirectory &&
            SelectedDirectoryItem.Parent == null &&
            !string.IsNullOrWhiteSpace(SelectedDirectoryItem.FullPath);

        public string SelectedVersionSyncDirectoryPath =>
            CanOpenVersionSync ? SelectedDirectoryItem!.FullPath : string.Empty;

        // ── Commands ──

        public ICommand BtnModPathCommand { get; }
        public ICommand BtnWwmiPathCommand { get; }
        public ICommand BtnClearAllModsCommand { get; }
        public ICommand BtnRandomLoadAllModsCommand { get; }
        public ICommand BtnLoadSelectedModsCommand { get; }
        public ICommand OpenModFolderCommand { get; }

        // ── Public helpers ──

        internal List<WuwaMods> AllMods => _allMods;
        internal List<WuwaMod> WwmiMods => _wwmiMods;

        // ── Private methods (delegated to MainViewModel for now) ──

        private void ExecuteBtnModPath()
        {
            // Full implementation remains in MainViewModel
        }

        private void ExecuteBtnWwmiPath()
        {
            // Full implementation remains in MainViewModel
        }

        private void ExecuteBtnClearAllMods()
        {
            // Full implementation remains in MainViewModel
        }

        private void ExecuteBtnRandomLoadAllMods()
        {
            // Full implementation remains in MainViewModel
        }

        private void ExecuteBtnLoadSelectedMods()
        {
            // Full implementation remains in MainViewModel
        }

        private void ExecuteOpenModFolderBySelected()
        {
            if (SelectedDirectoryItem?.FullPath == null) return;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{SelectedDirectoryItem.FullPath}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                LogManager.Error("OpenModFolder: ", ex);
                _messages.ShowError($"打开文件夹失败：{ex.Message}");
            }
        }

        private bool CanOpenModFolderBySelected()
        {
            return SelectedDirectoryItem != null &&
                !string.IsNullOrWhiteSpace(SelectedDirectoryItem.FullPath);
        }
    }
}
