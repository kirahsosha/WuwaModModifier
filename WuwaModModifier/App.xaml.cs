using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using WuwaModModifier.Common;
using WuwaModModifier.ViewModels;

namespace WuwaModModifier
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var services = new ServiceCollection();

            // Infrastructure
            services.AddSingleton<IFileSystemService, FileSystemService>();
            services.AddSingleton<ILogService, LogService>();
            services.AddSingleton<IAppConfigService, AppConfigService>();
            services.AddSingleton<ISystemVariableStrategy, DefaultSystemVariableStrategy>();

            // Parsing & Analysis
            services.AddSingleton<IModConfigParser, ModConfigParser>();
            services.AddSingleton<IModConfigAnalysisService, ModConfigAnalysisService>();
            services.AddSingleton<IModConfigDiscoveryService, ModConfigDiscoveryService>();

            // Update & Sync — dedicated services per interface
            services.AddSingleton<ModConfigUpdateService>();
            services.AddSingleton<IModConfigUpdateService>(sp => sp.GetRequiredService<ModConfigUpdateService>());
            services.AddSingleton<IModConfigToggleService, ModConfigToggleService>();
            services.AddSingleton<IModConfigParameterService, ModConfigParameterService>();
            services.AddSingleton<IModConfigVisibilityService, ModConfigVisibilityService>();
            services.AddSingleton<IModConfigVersionSyncService, ModConfigVersionSyncService>();

            // Transient
            services.AddTransient<IMessageService, MessageService>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<VersionSyncWindowViewModel>();

            ServiceProvider = services.BuildServiceProvider();

            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}
