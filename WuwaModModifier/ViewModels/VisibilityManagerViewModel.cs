using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using WuwaModModifier.Common;
using WuwaModModifier.Model;

namespace WuwaModModifier.ViewModels
{
    /// <summary>
    /// Manages visibility-related operations: toggle visibility state, bind to parameters,
    /// create/remove visibility bindings. Extracted from MainViewModel as part of R-02.
    /// </summary>
    public class VisibilityManagerViewModel : ViewModelBase
    {
        private readonly IMainViewModelSession _session;
        private readonly IModConfigUpdateService _configUpdateService;
        private readonly IModConfigAnalysisService _configAnalysisService;
        private readonly IMessageService _messages;

        private ObservableCollection<ConfigVisibilitySummaryItem> _selectedVisibilityItems;
        private ConfigVisibilitySummaryItem? _selectedVisibilityItem;
        private VisibilityBindingMode _selectedVisibilityBindingMode;
        private IVisibilityBindingTarget? _selectedVisibilityBindingParameter;
        private ConfigToggleSummaryItem? _selectedVisibilityBindingToggle;
        private ObservableCollection<ConfigParameterSummaryItem> _visibilityBindingParameterCandidates;
        private ObservableCollection<ConfigToggleSummaryItem> _visibilityBindingToggleCandidates;
        private ObservableCollection<ConfigVisibilityBindingRemovalCandidate> _visibilityBindingRemovalCandidates;
        private string _visibilityBindingNewParameterName;
        private string _visibilityBindingNewKeyBindingsText;
        private bool _visibilityTargetIsVisible;

        public VisibilityManagerViewModel(
            IMainViewModelSession session,
            IModConfigUpdateService configUpdateService,
            IModConfigAnalysisService configAnalysisService,
            IMessageService messages)
        {
            _session = session;
            _configUpdateService = configUpdateService;
            _configAnalysisService = configAnalysisService;
            _messages = messages;

            _selectedVisibilityItems = new ObservableCollection<ConfigVisibilitySummaryItem>();
            _visibilityBindingParameterCandidates = new ObservableCollection<ConfigParameterSummaryItem>();
            _visibilityBindingToggleCandidates = new ObservableCollection<ConfigToggleSummaryItem>();
            _visibilityBindingRemovalCandidates = new ObservableCollection<ConfigVisibilityBindingRemovalCandidate>();
            _visibilityBindingNewParameterName = string.Empty;
            _visibilityBindingNewKeyBindingsText = string.Empty;
            _selectedVisibilityBindingMode = VisibilityBindingMode.ExistingParameter;
            _visibilityTargetIsVisible = true;

            ApplyVisibilityChangeCommand = new RelayCommand(ExecuteApplyVisibilityChange, CanApplyVisibilityChange);
            ApplyVisibilityBindingCommand = new RelayCommand(ExecuteApplyVisibilityBinding, CanApplyVisibilityBinding);
        }

        // ── Collections ──

        public ObservableCollection<ConfigVisibilitySummaryItem> SelectedVisibilityItems
        {
            get => _selectedVisibilityItems;
            set => SetProperty(ref _selectedVisibilityItems, value);
        }

        public ObservableCollection<ConfigParameterSummaryItem> VisibilityBindingParameterCandidates => _visibilityBindingParameterCandidates;
        public ObservableCollection<ConfigToggleSummaryItem> VisibilityBindingToggleCandidates => _visibilityBindingToggleCandidates;
        public ObservableCollection<ConfigVisibilityBindingRemovalCandidate> VisibilityBindingRemovalCandidates => _visibilityBindingRemovalCandidates;

        // ── Selection ──

        public ConfigVisibilitySummaryItem? SelectedVisibilityItem
        {
            get => _selectedVisibilityItem;
            set
            {
                if (SetProperty(ref _selectedVisibilityItem, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public VisibilityBindingMode SelectedVisibilityBindingMode
        {
            get => _selectedVisibilityBindingMode;
            set => SetProperty(ref _selectedVisibilityBindingMode, value);
        }

        public IVisibilityBindingTarget? SelectedVisibilityBindingParameter
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

        // ── Mode selectors ──

        public bool UseExistingVisibilityParameterBinding
        {
            get => _selectedVisibilityBindingMode == VisibilityBindingMode.ExistingParameter;
            set { if (value) _selectedVisibilityBindingMode = VisibilityBindingMode.ExistingParameter; }
        }

        public bool UseExistingVisibilityToggleBinding
        {
            get => _selectedVisibilityBindingMode == VisibilityBindingMode.ExistingToggle;
            set { if (value) _selectedVisibilityBindingMode = VisibilityBindingMode.ExistingToggle; }
        }

        public bool UseNewVisibilityBinding
        {
            get => _selectedVisibilityBindingMode == VisibilityBindingMode.NewParameterAndToggle;
            set { if (value) _selectedVisibilityBindingMode = VisibilityBindingMode.NewParameterAndToggle; }
        }

        public bool UseRemoveExistingVisibilityBinding
        {
            get => _selectedVisibilityBindingMode == VisibilityBindingMode.RemoveExistingBinding;
            set { if (value) _selectedVisibilityBindingMode = VisibilityBindingMode.RemoveExistingBinding; }
        }

        // ── Commands ──

        public ICommand ApplyVisibilityChangeCommand { get; }
        public ICommand ApplyVisibilityBindingCommand { get; }

        // ── Public helpers ──

        public void ReplaceCollection(IEnumerable<ConfigVisibilitySummaryItem> items)
        {
            _selectedVisibilityItems.Clear();
            foreach (var item in items) _selectedVisibilityItems.Add(item);
        }

        public void ClearSelection()
        {
            SelectedVisibilityItem = null;
            ResetState();
        }

        internal void ResetState()
        {
            SelectedVisibilityItem = null;
            SelectedVisibilityBindingParameter = null;
            SelectedVisibilityBindingToggle = null;
            _visibilityBindingNewParameterName = string.Empty;
            _visibilityBindingNewKeyBindingsText = string.Empty;
            OnPropertyChanged(nameof(VisibilityBindingNewParameterName));
            OnPropertyChanged(nameof(VisibilityBindingNewKeyBindingsText));
        }

        // ── Private methods (simplified — full logic stays in MainViewModel for now) ──

        private void ExecuteApplyVisibilityChange()
        {
            if (_session.CurrentBuffer == null || SelectedVisibilityItem == null) return;
            try
            {
                var updatedBuffer = _configUpdateService.ToggleVisibility(
                    _session.CurrentBuffer, SelectedVisibilityItem.SectionName,
                    SelectedVisibilityItem.DrawLabelsText, _visibilityTargetIsVisible);
                var stateText = _visibilityTargetIsVisible ? "显示" : "隐藏";
                _session.ApplyBufferAnalysis(updatedBuffer,
                    $"已切换 {SelectedVisibilityItem.SectionName} / {SelectedVisibilityItem.DrawLabelsText} 为{stateText}。",
                    preferredVisibilitySection: SelectedVisibilityItem.SectionName,
                    preferredVisibilityLabel: SelectedVisibilityItem.DrawLabelsText);
                _session.AppendModificationHistory("可见性切换",
                    $"{SelectedVisibilityItem.SectionName}/{SelectedVisibilityItem.DrawLabelsText}",
                    $"切换为{stateText}");
            }
            catch (Exception ex)
            {
                LogManager.Error("ApplyVisibilityChange: ", ex);
                _messages.ShowError($"可见性切换失败：{ex.Message}");
            }
        }

        private bool CanApplyVisibilityChange()
        {
            return _session.CurrentBuffer != null && !_session.IsRawConfigDirty &&
                SelectedVisibilityItem != null && SelectedVisibilityItem.CanToggleSafely;
        }

        private void ExecuteApplyVisibilityBinding()
        {
            if (_session.CurrentBuffer == null || SelectedVisibilityItem == null) return;
            // Complex 4-mode logic — delegates to the full MainViewModel implementation for now
            _messages.ShowInfo("此功能将在后续细化中从 MainViewModel 完整迁移。");
        }

        private bool CanApplyVisibilityBinding()
        {
            return _session.CurrentBuffer != null && !_session.IsRawConfigDirty && SelectedVisibilityItem != null;
        }
    }
}
