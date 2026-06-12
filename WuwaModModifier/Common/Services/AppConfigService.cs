using System;
using System.Configuration;

namespace WuwaModModifier.Common
{
    /// <summary>
    /// Default implementation of <see cref="IAppConfigService"/> that reads from
    /// the standard App.config <c>appSettings</c> section. Registered as a singleton
    /// in the DI container.
    /// </summary>
    public class AppConfigService : IAppConfigService
    {
        public string DefaultModPath { get; }
        public string DefaultWwmiPath { get; }
        public string OtherFolderPath { get; }
        public string StandardToggleTemplatePath { get; }

        public AppConfigService()
        {
            try
            {
                DefaultModPath = ConfigurationManager.AppSettings["DefaultModPath"] ?? "";
                DefaultWwmiPath = ConfigurationManager.AppSettings["DefaultWwmiPath"] ?? "";
                OtherFolderPath = ConfigurationManager.AppSettings["OtherFolderPath"] ?? "";
                StandardToggleTemplatePath = ConfigurationManager.AppSettings["StandardToggleTemplatePath"] ?? "";
            }
            catch (Exception ex)
            {
                LogManager.Error("AppConfigService ctor: ", ex);
                DefaultModPath = "";
                DefaultWwmiPath = "";
                OtherFolderPath = "";
                StandardToggleTemplatePath = "";
            }
        }
    }
}
