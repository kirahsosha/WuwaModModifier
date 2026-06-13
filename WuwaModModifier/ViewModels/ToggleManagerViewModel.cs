using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using WuwaModModifier.Common;
using WuwaModModifier.Model;

namespace WuwaModModifier.ViewModels
{
    /// <summary>
    /// Manages toggle-related operations: listing, key-binding edits, and toggle creation.
    /// Extracted from MainViewModel as part of R-02 refactoring.
    /// </summary>
    public class ToggleManagerViewModel : ViewModelBase
    {
        private readonly IMainViewModelSession _session;
        private readonly IModConfigUpdateService _configUpdateService;
        private readonly IMessageService _messages;

        private ObservableCollection<ConfigToggleSummaryItem> _selectedToggleItems;
        private ConfigToggleSummaryItem? _selectedToggleItem;
        private ToggleCreationMode _selectedToggleCreationMode;
        private string _toggleKeyBindingEditorText;
        private string _toggleCreationNewParameterName;
        private string _toggleCreationKeyBindingsText;
        private ConfigParameterSummaryItem? _selectedToggleCreationParameter;
        private ObservableCollection<ConfigParameterSummaryItem> _toggleCreationParameterCandidates;

        public ToggleManagerViewModel(
            IMainViewModelSession session,
            IModConfigUpdateService configUpdateService,
            IMessageService messages)
        {
            _session = session;
            _configUpdateService = configUpdateService;
            _messages = messages;

            _selectedToggleItems = new ObservableCollection<ConfigToggleSummaryItem>();
            _toggleCreationParameterCandidates = new ObservableCollection<ConfigParameterSummaryItem>();
            _toggleKeyBindingEditorText = string.Empty;
            _toggleCreationNewParameterName = string.Empty;
            _toggleCreationKeyBindingsText = string.Empty;
            _selectedToggleCreationMode = ToggleCreationMode.ExistingParameter;

            ApplyToggleKeyBindingsCommand = new RelayCommand(ExecuteApplyToggleKeyBindings, CanApplyToggleKeyBindings);
            CreateToggleCommand = new RelayCommand(ExecuteCreateToggle, CanCreateToggle);
        }

        // ── Collections ──

        public ObservableCollection<ConfigToggleSummaryItem> SelectedToggleItems
        {
            get => _selectedToggleItems;
            set => SetProperty(ref _selectedToggleItems, value);
        }

        // ── Selection ──

        public ConfigToggleSummaryItem? SelectedToggleItem
        {
            get => _selectedToggleItem;
            set
            {
                if (SetProperty(ref _selectedToggleItem, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        // ── Toggle Creation ──

        public ToggleCreationMode SelectedToggleCreationMode
        {
            get => _selectedToggleCreationMode;
            set => SetProperty(ref _selectedToggleCreationMode, value);
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

        public ObservableCollection<ConfigParameterSummaryItem> ToggleCreationParameterCandidates => _toggleCreationParameterCandidates;

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

        // ── Computed ──

        public bool UseExistingToggleCreationParameter
        {
            get => _selectedToggleCreationMode == ToggleCreationMode.ExistingParameter;
            set { if (value) SetToggleCreationMode(ToggleCreationMode.ExistingParameter); }
        }

        public bool UseNewToggleCreationParameter
        {
            get => _selectedToggleCreationMode == ToggleCreationMode.NewParameter;
            set { if (value) SetToggleCreationMode(ToggleCreationMode.NewParameter); }
        }

        // ── Commands ──

        public ICommand ApplyToggleKeyBindingsCommand { get; }
        public ICommand CreateToggleCommand { get; }

        // ── Public helpers ──

        public void ReplaceCollection(IEnumerable<ConfigToggleSummaryItem> items)
        {
            _selectedToggleItems.Clear();
            foreach (var item in items) _selectedToggleItems.Add(item);
        }

        public void ClearSelection()
        {
            SelectedToggleItem = null;
            _toggleKeyBindingEditorText = string.Empty;
            OnPropertyChanged(nameof(ToggleKeyBindingEditorText));
        }

        internal void ResetState()
        {
            SelectedToggleItem = null;
            SelectedToggleCreationParameter = null;
            _toggleKeyBindingEditorText = string.Empty;
            _toggleCreationNewParameterName = string.Empty;
            _toggleCreationKeyBindingsText = string.Empty;
            OnPropertyChanged(nameof(ToggleKeyBindingEditorText));
            OnPropertyChanged(nameof(ToggleCreationNewParameterName));
            OnPropertyChanged(nameof(ToggleCreationKeyBindingsText));
        }

        public void RefreshCandidates(IEnumerable<ConfigParameterSummaryItem> parameters)
        {
            _toggleCreationParameterCandidates.Clear();
            foreach (var p in parameters.Where(p => CanCreateToggleBindingForParameter(p)))
                _toggleCreationParameterCandidates.Add(p);
        }

        // ── Private methods ──

        private void ExecuteApplyToggleKeyBindings()
        {
            if (_session.CurrentBuffer == null || SelectedToggleItem == null) return;

            try
            {
                var bindings = MainViewModel.SplitEditorValues(ToggleKeyBindingEditorText);
                var updatedBuffer = _configUpdateService.UpdateKeyBindings(
                    _session.CurrentBuffer, SelectedToggleItem.SectionName, bindings);
                _session.ApplyBufferAnalysis(updatedBuffer, $"已更新 {SelectedToggleItem.SectionName} 的快捷键。",
                    preferredToggleSection: SelectedToggleItem.SectionName);
                _session.AppendModificationHistory("快捷键更新", SelectedToggleItem.SectionName,
                    $"新快捷键: {string.Join(", ", bindings)}");
            }
            catch (Exception ex)
            {
                LogManager.Error("ApplyToggleKeyBindings: ", ex);
                _messages.ShowError($"更新快捷键失败：{ex.Message}");
            }
        }

        private bool CanApplyToggleKeyBindings()
        {
            return _session.CurrentBuffer != null && !_session.IsRawConfigDirty &&
                SelectedToggleItem != null &&
                MainViewModel.SplitEditorValues(ToggleKeyBindingEditorText).Count > 0;
        }

        private void ExecuteCreateToggle()
        {
            if (_session.CurrentBuffer == null) return;

            try
            {
                var bindings = MainViewModel.SplitEditorValues(ToggleCreationKeyBindingsText);
                ModConfigEditBuffer updatedBuffer;
                string historySummary;

                if (_selectedToggleCreationMode == ToggleCreationMode.ExistingParameter && SelectedToggleCreationParameter != null)
                {
                    updatedBuffer = _configUpdateService.CreateToggleBinding(
                        _session.CurrentBuffer, SelectedToggleCreationParameter.Name, bindings);
                    historySummary = $"绑定到已有参数 {SelectedToggleCreationParameter.Name}。";
                }
                else
                {
                    var varName = ParameterManagerViewModel.NormalizeVariableName(ToggleCreationNewParameterName);
                    updatedBuffer = _configUpdateService.CreateToggleBinding(_session.CurrentBuffer, varName, bindings);
                    historySummary = $"新建参数 {varName}。";
                }

                _session.ClearStandardizationResults();
                _session.ApplyBufferAnalysis(updatedBuffer, "已创建新的按键绑定。");
                _session.AppendModificationHistory("创建按键", "新按键", historySummary);
            }
            catch (Exception ex)
            {
                LogManager.Error("CreateToggle: ", ex);
                _messages.ShowError($"创建按键失败：{ex.Message}");
            }
        }

        private bool CanCreateToggle()
        {
            if (_session.CurrentBuffer == null || _session.IsRawConfigDirty) return false;
            if (MainViewModel.SplitEditorValues(ToggleCreationKeyBindingsText).Count == 0) return false;

            return _selectedToggleCreationMode switch
            {
                ToggleCreationMode.ExistingParameter => SelectedToggleCreationParameter != null,
                _ => !string.IsNullOrWhiteSpace(ParameterManagerViewModel.NormalizeVariableName(ToggleCreationNewParameterName))
            };
        }

        private void SetToggleCreationMode(ToggleCreationMode mode)
        {
            _selectedToggleCreationMode = mode;
            OnPropertyChanged(nameof(UseExistingToggleCreationParameter));
            OnPropertyChanged(nameof(UseNewToggleCreationParameter));
            CommandManager.InvalidateRequerySuggested();
        }

        private static bool CanCreateToggleBindingForParameter(ConfigParameterSummaryItem parameter)
        {
            return !parameter.KindText.Equals(nameof(ModConfigParameterKind.System), StringComparison.OrdinalIgnoreCase);
        }
    }
}
