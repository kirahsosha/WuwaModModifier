using System.Text.RegularExpressions;

namespace WuwaModModifier.Common
{
    /// <summary>
    /// MOD 目录解析工具类
    /// </summary>
    public static class ModPathHelper
    {
        // 示例：
        // Mod 根目录下角色文件夹：E:\Game\wuwa mods\Mods\Encore
        // 只需要最后一段名称
        private static readonly Regex CharacterFolderRegex =
            new Regex(@"(?<name>[^\\\/]+)$", RegexOptions.Compiled);

        // 示例：
        // MOD 文件夹名：[12345]MyMod
        // id = 12345, name = MyMod
        private static readonly Regex ModFolderNameRegex =
            new Regex(@"^\[(?<id>[^\]]+)\](?<name>.+)$", RegexOptions.Compiled);

        // 示例：
        // WWMI 目录下文件夹：E:\...\[Encore][12345]MyMod
        // character = Encore, id = 12345, name = MyMod
        private static readonly Regex WwmiFolderPathRegex =
            new Regex(@".*\\\[(?<character>[^\]]+)\]\[(?<id>[^\]]+)\](?<name>.+)$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// 从角色目录路径中获取角色名
        /// </summary>
        public static string GetCharacterNameFromFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                return string.Empty;

            var match = CharacterFolderRegex.Match(folderPath);
            return match.Success ? match.Groups["name"].Value : string.Empty;
        }

        /// <summary>
        /// 从 MOD 目录名中解析 id 和 modName（例如：[123]ModName）
        /// </summary>
        public static (string Id, string ModName) ParseModFolderName(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName))
                return (string.Empty, string.Empty);

            var match = ModFolderNameRegex.Match(folderName);
            if (!match.Success)
                return (string.Empty, folderName);

            var id = match.Groups["id"].Value;
            var name = match.Groups["name"].Value;
            return (id, name);
        }

        /// <summary>
        /// 从 WWMI 中的 MOD 完整路径解析角色名、id、modName
        /// 例如：E:\...\[Encore][123]MyMod
        /// </summary>
        public static (string CharacterName, string Id, string ModName) ParseWwmiFolderPath(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                return (string.Empty, string.Empty, string.Empty);

            var match = WwmiFolderPathRegex.Match(folderPath);
            if (!match.Success)
                return (string.Empty, string.Empty, string.Empty);

            var character = match.Groups["character"].Value;
            var id = match.Groups["id"].Value;
            var name = match.Groups["name"].Value;

            return (character, id, name);
        }
    }
}


