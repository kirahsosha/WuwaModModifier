using System.Configuration;

namespace WuwaModModifier.Common
{
    public class AppConfig
    {
        public static string DefaultModPath { get; private set; } = "";
        public static string DefaultWwmiPath { get; private set; } = "";
        public static string OtherFolderPath { get; private set; } = "";
        public static string StandardToggleTemplatePath { get; private set; } = "";

        static AppConfig()
        {
            InitConfig();
        }

        private static void InitConfig()
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
                LogManager.Error("AppConfig.InitConfig: ", ex);
            }

        }
    }
}
