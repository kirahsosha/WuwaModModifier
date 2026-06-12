using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using WuwaModModifier.Common;
using WuwaModModifier.Model;

namespace WuwaModModifier.ViewModels
{
    /// <summary>
    /// Manages parameter-related operations: listing, filtering, renaming, and creation.
    /// Extracted from MainViewModel as part of R-02 refactoring.
    /// </summary>
    public class ParameterManagerViewModel : ViewModelBase
    {
        private readonly IMainViewModelSession _session;
        private readonly IModConfigUpdateService _configUpdateService;
        private readonly IMessageService _messages;

        private ObservableCollection<ConfigParameterSummaryItem> _selectedParameterItems;
        private readonly ICollectionView _selectedParameterItemsView;
        private ConfigParameterSummaryItem? _selectedParameterItem;
        private string _parameterRenameText;
        private string _parameterCreationName;
        private bool _hideInternalSystemParameters;

        public ParameterManagerViewModel(
            IMainViewModelSession session,
            IModConfigUpdateService configUpdateService,
            IMessageService messages)
        {
            _session = session;
            _configUpdateService = configUpdateService;
            _messages = messages;

            _selectedParameterItems = new ObservableCollection<ConfigParameterSummaryItem>();
            _selectedParameterItemsView = CollectionViewSource.GetDefaultView(_selectedParameterItems);
            _selectedParameterItemsView.Filter = FilterSelectedParameterItem;
            _parameterRenameText = string.Empty;
            _parameterCreationName = string.Empty;
            _hideInternalSystemParameters = true;

            RenameParameterCommand = new RelayCommand(ExecuteRenameParameter, CanRenameParameter);
            CreateParameterCommand = new RelayCommand(ExecuteCreateParameter, CanCreateParameter);
        }

        // ── Collections ──

        public ObservableCollection<ConfigParameterSummaryItem> SelectedParameterItems
        {
            get => _selectedParameterItems;
            set => SetProperty(ref _selectedParameterItems, value);
        }

        public ICollectionView SelectedParameterItemsView => _selectedParameterItemsView;

        // ── Selection ──

        public ConfigParameterSummaryItem? SelectedParameterItem
        {
            get => _selectedParameterItem;
            set
            {
                if (SetProperty(ref _selectedParameterItem, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        /// <summary>Preferred parameter name to preserve selection across buffer refreshes.</summary>
        public string? PreferredParameterName => _selectedParameterItem?.Name;

        // ── Rename ──

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

        // ── Create ──

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

        // ── Filter ──

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

        // ── Commands ──

        public ICommand RenameParameterCommand { get; }
        public ICommand CreateParameterCommand { get; }

        // ── Public helpers (called by MainViewModel) ──

        public void ReplaceCollection(IEnumerable<ConfigParameterSummaryItem> items)
        {
            _selectedParameterItems.Clear();
            foreach (var item in items)
            {
                _selectedParameterItems.Add(item);
            }
        }

        public void ClearSelection()
        {
            SelectedParameterItem = null;
            _parameterRenameText = string.Empty;
            OnPropertyChanged(nameof(ParameterRenameText));
            _parameterCreationName = string.Empty;
            OnPropertyChanged(nameof(ParameterCreationName));
        }

        public void RefreshView()
        {
            _selectedParameterItemsView.Refresh();

            if (SelectedParameterItem != null &&
                HideInternalSystemParameters &&
                IsInternalSystemParameter(SelectedParameterItem))
            {
                SelectedParameterItem = null;
            }
        }

        internal void ResetState()
        {
            SelectedParameterItem = null;
            _parameterRenameText = string.Empty;
            OnPropertyChanged(nameof(ParameterRenameText));
            _parameterCreationName = string.Empty;
            OnPropertyChanged(nameof(ParameterCreationName));
        }

        // ── Private methods ──

        private void ExecuteRenameParameter()
        {
            if (_session.CurrentBuffer == null || SelectedParameterItem == null)
            {
                return;
            }

            try
            {
                var originalName = SelectedParameterItem.Name;
                var newName = ParameterRenameText.Trim();
                var updatedBuffer = _configUpdateService.RenameParameter(
                    _session.CurrentBuffer,
                    originalName,
                    newName);
                _session.ClearStandardizationResults();
                _session.ApplyBufferAnalysis(
                    updatedBuffer,
                    $"已在缓冲区将 {originalName} 重命名为 {newName}。",
                    preferredParameterName: newName);
                _session.AppendModificationHistory("参数重命名", originalName, $"重命名为 {newName}。");
            }
            catch (Exception ex)
            {
                LogManager.Error("RenameParameter: ", ex);
                _messages.ShowError($"参数重命名失败：{ex.Message}");
            }
        }

        private bool CanRenameParameter()
        {
            return _session.CurrentBuffer != null &&
                !_session.IsRawConfigDirty &&
                SelectedParameterItem != null &&
                SelectedParameterItem.CanRename &&
                !string.IsNullOrWhiteSpace(ParameterRenameText);
        }

        private void ExecuteCreateParameter()
        {
            if (_session.CurrentBuffer == null)
            {
                return;
            }

            try
            {
                var newParameterName = NormalizeVariableName(ParameterCreationName);
                var updatedBuffer = _configUpdateService.CreateParameter(_session.CurrentBuffer, newParameterName);
                ParameterCreationName = string.Empty;
                _session.ClearStandardizationResults();
                _session.ApplyBufferAnalysis(
                    updatedBuffer,
                    $"已在缓冲区新增参数 {newParameterName}。",
                    preferredParameterName: newParameterName);
                _session.AppendModificationHistory("新增参数", newParameterName, "默认值为 1。");
            }
            catch (Exception ex)
            {
                LogManager.Error("CreateParameter: ", ex);
                _messages.ShowError($"新增参数失败：{ex.Message}");
            }
        }

        private bool CanCreateParameter()
        {
            return _session.CurrentBuffer != null &&
                !_session.IsRawConfigDirty &&
                !string.IsNullOrWhiteSpace(NormalizeVariableName(ParameterCreationName));
        }

        // ── Helpers ──

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

        internal static string NormalizeVariableName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            var trimmed = name.Trim();
            return trimmed.StartsWith('$') ? trimmed : $"${trimmed}";
        }
    }
}
