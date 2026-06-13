namespace WuwaModModifier.Common
{
    /// <summary>
    /// Injectable abstraction over application configuration (App.config settings).
    /// Replaces the static <see cref="AppConfig"/> class.
    /// </summary>
    public interface IAppConfigService
    {
        string DefaultModPath { get; }
        string DefaultWwmiPath { get; }
        string OtherFolderPath { get; }
        string StandardToggleTemplatePath { get; }
        void SaveModPath(string path);
        void SaveWwmiPath(string path);
    }
}
