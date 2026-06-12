using WuwaModModifier.Common;
using WuwaModModifier.ViewModels;

namespace UnitTests
{
    /// <summary>
    /// Shared test helpers for constructing ViewModels with real service instances.
    /// </summary>
    internal static class TestHelper
    {
        /// <summary>
        /// Creates a MainViewModel with a stub message service that does nothing.
        /// Use the overload with IMessageService parameter when you need to assert on messages.
        /// </summary>
        public static MainViewModel CreateMainViewModel()
        {
            return CreateMainViewModel(new StubMessageService());
        }

        /// <summary>
        /// Creates a MainViewModel with the specified message service (e.g., TestMessageService).
        /// </summary>
        public static MainViewModel CreateMainViewModel(IMessageService messages)
        {
            var fs = new FileSystemService();
            var parser = new ModConfigParser(fs);
            var analysis = new ModConfigAnalysisService(parser);
            return new MainViewModel(
                fs,
                messages,
                new AppConfigService(),
                new ModConfigDiscoveryService(fs),
                analysis,
                parser,
                new ModConfigUpdateService(fs, parser, analysis));
        }

        private sealed class StubMessageService : IMessageService
        {
            public void ShowInfo(string message, string? caption = null) { }
            public void ShowError(string message, string? caption = null) { }
            public bool Confirm(string message, string? caption = null) => true;
        }

        /// <summary>
        /// Creates a VersionSyncWindowViewModel with all required service dependencies.
        /// </summary>
        public static VersionSyncWindowViewModel CreateVersionSyncWindowViewModel(
            IFileSystemService fileSystem,
            IMessageService messages,
            IModConfigVersionSyncService versionSyncService)
        {
            var parser = new ModConfigParser(fileSystem);
            var analysis = new ModConfigAnalysisService(parser);
            var configUpdateService = new ModConfigUpdateService(fileSystem, parser, analysis);
            return new VersionSyncWindowViewModel(
                fileSystem,
                messages,
                versionSyncService,
                configUpdateService,
                parser,
                analysis);
        }
    }
}
