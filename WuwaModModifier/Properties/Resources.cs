using System.Globalization;
using System.Resources;

namespace WuwaModModifier.Properties
{
    /// <summary>
    /// Strongly-typed wrapper around the embedded Resources.resx file.
    /// Provides compile-time-safe access to localised Chinese UI strings.
    /// </summary>
    internal static class Resources
    {
        private static readonly ResourceManager ResourceMan =
            new ResourceManager("WuwaModModifier.Properties.Resources", typeof(Resources).Assembly);

        internal static string StatusSelectModForAnalysis => Get("StatusSelectModForAnalysis");
        internal static string StatusSelectCharacterDirectory => Get("StatusSelectCharacterDirectory");
        internal static string StatusNoEditableConfig => Get("StatusNoEditableConfig");
        internal static string StatusNoConfigSelected => Get("StatusNoConfigSelected");
        internal static string StatusModDirectoryNotExists => Get("StatusModDirectoryNotExists");
        internal static string StatusAnalysisFailed => Get("StatusAnalysisFailed");

        internal static string StatusUnsavedChanges => Get("StatusUnsavedChanges");
        internal static string StatusSynced => Get("StatusSynced");
        internal static string StatusBufferLoaded => Get("StatusBufferLoaded");
        internal static string StatusPendingChanges => Get("StatusPendingChanges");
        internal static string StatusInSync => Get("StatusInSync");

        internal static string ModsFoundCount => Get("ModsFoundCount");
        internal static string ModsNotFound => Get("ModsNotFound");
        internal static string ModsDeletedCount => Get("ModsDeletedCount");
        internal static string ModsLoadedCount => Get("ModsLoadedCount");
        internal static string ModsLoadFailed => Get("ModsLoadFailed");
        internal static string ModsDeleteFailed => Get("ModsDeleteFailed");

        internal static string ConfigSourceSwitchToWwmi => Get("ConfigSourceSwitchToWwmi");
        internal static string ConfigSourceSwitchToMod => Get("ConfigSourceSwitchToMod");
        internal static string ConfigUnsavedSwitchWarning => Get("ConfigUnsavedSwitchWarning");

        internal static string VisibilityBindingAvailable => Get("VisibilityBindingAvailable");
        internal static string VisibilityBindingUnbound => Get("VisibilityBindingUnbound");
        internal static string VisibilityBindingBound => Get("VisibilityBindingBound");
        internal static string VisibilityBindingNotApplicable => Get("VisibilityBindingNotApplicable");

        internal static string WwmiMainConfigNotFound => Get("WwmiMainConfigNotFound");
        internal static string MainConfigNotFound => Get("MainConfigNotFound");

        internal static string WillOverwriteTarget => Get("WillOverwriteTarget");
        internal static string WillCreateTarget => Get("WillCreateTarget");

        internal static string VersionSyncNoImport => Get("VersionSyncNoImport");
        internal static string VersionSyncImporting => Get("VersionSyncImporting");
        internal static string VersionSyncNoBatchYet => Get("VersionSyncNoBatchYet");

        internal static string DialogCaptionConfigSwitch => Get("DialogCaptionConfigSwitch");
        internal static string DialogCaptionVersionSync => Get("DialogCaptionVersionSync");
        internal static string DialogCaptionManualPairing => Get("DialogCaptionManualPairing");
        internal static string DialogManualPairingSelectBoth => Get("DialogManualPairingSelectBoth");
        internal static string DialogManualPairingSameDir => Get("DialogManualPairingSameDir");

        internal static string StandardizationFully => Get("StandardizationFully");
        internal static string StandardizationPartially => Get("StandardizationPartially");
        internal static string StandardizationSkipped => Get("StandardizationSkipped");

        internal static string GenericNoDifference => Get("GenericNoDifference");
        internal static string GenericCandidateSwitchFailed => Get("GenericCandidateSwitchFailed");

        private static string Get(string name) => ResourceMan.GetString(name, CultureInfo.CurrentCulture) ?? name;
    }
}
