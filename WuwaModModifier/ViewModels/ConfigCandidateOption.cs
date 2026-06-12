namespace WuwaModModifier.ViewModels
{
    /// <summary>
    /// Represents a candidate config file option for the config source switcher.
    /// Extracted from MainViewModel nested record.
    /// </summary>
    public sealed record ConfigCandidateOption(string DisplayPath, string FullPath);
}
