using System.Collections.ObjectModel;
using WuwaModModifier.Data.ViewModels;
using WuwaModModifier.Model;

namespace WuwaModModifier.ViewModels
{
    /// <summary>
    /// Shared session state provided by MainViewModel (coordinator) to sub-ViewModels.
    /// Sub-VMs use this interface to access shared state and trigger cross-VM coordination
    /// without directly depending on MainViewModel.
    /// </summary>
    public interface IMainViewModelSession
    {
        /// <summary>
        /// The current config buffer being edited. Null when no config is loaded.
        /// </summary>
        ModConfigEditBuffer? CurrentBuffer { get; }

        /// <summary>
        /// Whether the raw config editor has unsaved changes.
        /// </summary>
        bool IsRawConfigDirty { get; }

        /// <summary>
        /// Whether the analysis has pending (uncommitted) changes.
        /// </summary>
        bool HasPendingConfigChanges { get; }

        /// <summary>
        /// The current config source (ModDirectory or WwmiDirectory).
        /// </summary>
        ModConfigSaveTarget CurrentConfigSource { get; }

        /// <summary>
        /// Parses the buffer content, refreshes all analysis collections,
        /// updates UI state, and preserves selections when possible.
        /// </summary>
        void ApplyBufferAnalysis(
            ModConfigEditBuffer buffer,
            string editStatus,
            string? preferredToggleSection = null,
            string? preferredParameterName = null,
            string? preferredVisibilitySection = null,
            string? preferredVisibilityLabel = null);

        /// <summary>
        /// Clears all standardization result items.
        /// </summary>
        void ClearStandardizationResults();

        /// <summary>
        /// Adds an entry to the modification history audit log.
        /// </summary>
        void AppendModificationHistory(string operationType, string target, string summary);

        /// <summary>
        /// Navigates the raw config editor to the specified line.
        /// </summary>
        void RequestRawConfigNavigation(int line);

        /// <summary>
        /// Clears all analysis collections (toggle, parameter, visibility, candidates).
        /// </summary>
        void ClearAnalysisCollections();

        /// <summary>
        /// Resets all config editing state to defaults.
        /// </summary>
        void ClearConfigEditingState();

        /// <summary>
        /// Refreshes config analysis from the currently selected config path.
        /// </summary>
        void RefreshSelectedConfigAnalysis();

        /// <summary>Root mod folder path.</summary>
        string ModFolderPath { get; }

        /// <summary>WWMI installation directory path.</summary>
        string WwmiFolderPath { get; }

        /// <summary>Current config file path.</summary>
        string SelectedConfigPath { get; }

        /// <summary>Notifies UI of preview path changes.</summary>
        void OnPathPreviewChanged();

        /// <summary>Standard toggle template file path.</summary>
        string StandardToggleTemplatePath { get; }

        /// <summary>All toggle summary items in the current config.</summary>
        ObservableCollection<ConfigToggleSummaryItem> SelectedToggleItems { get; }

        /// <summary>Currently selected directory tree node.</summary>
        DirectoryItemViewModel? SelectedDirectoryItem { get; }
    }
}
