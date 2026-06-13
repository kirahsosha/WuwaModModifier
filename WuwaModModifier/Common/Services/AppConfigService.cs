using System;
using System.Configuration;

namespace WuwaModModifier.Common
{
    public class AppConfigService : IAppConfigService
    {
        public string DefaultModPath { get; private set; }
        public string DefaultWwmiPath { get; private set; }
        public string OtherFolderPath { get; private set; }
        public string StandardToggleTemplatePath { get; private set; }

        public AppConfigService()
        {
            try
            {
                DefaultModPath = ConfigurationManager.AppSettings["DefaultModPath"] ?? "";
                DefaultWwmiPath = ConfigurationManager.AppSettings["DefaultWwmiPath"] ?? "";
                OtherFolderPath = ConfigurationManager.AppSettings["OtherFolderPath"] ?? "Other";
                StandardToggleTemplatePath = ConfigurationManager.AppSettings["StandardToggleTemplatePath"] ?? "";
            }
            catch (Exception ex)
            {
                LogManager.Error("AppConfigService ctor: ", ex);
                DefaultModPath = "";
                DefaultWwmiPath = "";
                OtherFolderPath = "Other";
                StandardToggleTemplatePath = "";
            }
        }

        public void SaveModPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            SaveSetting("DefaultModPath", path);
            DefaultModPath = path;
        }

        public void SaveWwmiPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            SaveSetting("DefaultWwmiPath", path);
            DefaultWwmiPath = path;
        }

        private static void SaveSetting(string key, string value)
        {
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                if (config.AppSettings.Settings[key] != null)
                {
                    config.AppSettings.Settings[key].Value = value;
                }
                else
                {
                    config.AppSettings.Settings.Add(key, value);
                }
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
            }
            catch (Exception ex)
            {
                LogManager.Error($"AppConfigService.SaveSetting({key}): ", ex);
            }
        }
    }
}
