using System;
using System.IO;
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
        // WWMI 目录下文件夹名：[Encore][12345]MyMod
        // character = Encore, id = 12345, name = MyMod
        private static readonly Regex WwmiFolderNameRegex =
            new Regex(@"^\[(?<character>[^\]]+)\]\[(?<id>[^\]]+)\](?<name>.+)$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex WhitespaceOrHyphenRegex =
            new Regex(@"[\s\-]+", RegexOptions.Compiled);

        private static readonly Regex DuplicateUnderscoreRegex =
            new Regex(@"_+", RegexOptions.Compiled);

        private static readonly Regex TrailingVersionSuffixRegex =
            new Regex(@"(?:_v\d+)+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex TrailingVerSuffixRegex =
            new Regex(@"(?:_ver\d+)+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex TrailingHashSuffixRegex =
            new Regex(@"(?:_[0-9a-f]{5,})+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex MultiConfigSuffixRegex =
            new Regex(@"_p\d+$", RegexOptions.Compiled);

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
        /// 从 MOD 目录完整路径中解析角色名、id 和 modName。
        /// 例如：E:\Mods\13 Rover\[589362] rover_bikini_warrior_a9e13
        /// </summary>
        public static bool TryParseModDirectoryPath(
            string folderPath,
            out string characterName,
            out string id,
            out string modName)
        {
            characterName = string.Empty;
            id = string.Empty;
            modName = string.Empty;

            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return false;
            }

            var normalizedPath = folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var folderName = Path.GetFileName(normalizedPath);
            var parentDirectory = Path.GetDirectoryName(normalizedPath);
            var parsedCharacterName = GetCharacterNameFromFolder(parentDirectory ?? string.Empty);
            var (parsedId, parsedModName) = ParseModFolderName(folderName);

            if (string.IsNullOrWhiteSpace(parsedCharacterName) ||
                string.IsNullOrWhiteSpace(parsedId) ||
                string.IsNullOrWhiteSpace(parsedModName))
            {
                return false;
            }

            characterName = parsedCharacterName;
            id = parsedId;
            modName = parsedModName.Trim();
            return true;
        }

        /// <summary>
        /// 生成版本同步配对时使用的语义键。
        /// 规则：统一大小写与分隔符，去掉末尾 hash / v1-vN / ver123 等版本噪音。
        /// </summary>
        public static string NormalizeVersionSyncKey(string modName)
        {
            if (string.IsNullOrWhiteSpace(modName))
            {
                return string.Empty;
            }

            var normalized = modName.Trim().ToLowerInvariant();
            normalized = WhitespaceOrHyphenRegex.Replace(normalized, "_");
            normalized = DuplicateUnderscoreRegex.Replace(normalized, "_").Trim('_');

            while (!string.IsNullOrWhiteSpace(normalized))
            {
                var updated = TrailingVersionSuffixRegex.Replace(normalized, string.Empty);
                updated = TrailingVerSuffixRegex.Replace(updated, string.Empty);
                updated = MultiConfigSuffixRegex.Replace(updated, string.Empty);
                updated = TrailingHashSuffixRegex.Replace(updated, string.Empty);
                updated = updated.Trim('_');

                if (updated.Equals(normalized, StringComparison.Ordinal))
                {
                    break;
                }

                normalized = updated;
            }

            return normalized;
        }

        public static bool IsMultiConfigMod(string modName)
        {
            return !string.IsNullOrWhiteSpace(modName) && MultiConfigSuffixRegex.IsMatch(modName);
        }

        public static string GetBaseModName(string modName)
        {
            if (string.IsNullOrWhiteSpace(modName))
            {
                return string.Empty;
            }

            return MultiConfigSuffixRegex.Replace(modName, "");
        }

        public static string StripMultiConfigSuffix(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            return MultiConfigSuffixRegex.Replace(name, "");
        }

        /// <summary>
        /// 从 WWMI 中的 MOD 完整路径解析角色名、id、modName
        /// 例如：E:\...\[Encore][123]MyMod
        /// </summary>
        public static (string CharacterName, string Id, string ModName) ParseWwmiFolderPath(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                return (string.Empty, string.Empty, string.Empty);

            var currentPath = folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            while (!string.IsNullOrWhiteSpace(currentPath))
            {
                var folderName = Path.GetFileName(currentPath);
                var parsed = ParseWwmiFolderName(folderName);
                if (!string.IsNullOrWhiteSpace(parsed.CharacterName) &&
                    !string.IsNullOrWhiteSpace(parsed.Id) &&
                    !string.IsNullOrWhiteSpace(parsed.ModName))
                {
                    return parsed;
                }

                var parentPath = Path.GetDirectoryName(currentPath);
                if (string.IsNullOrWhiteSpace(parentPath) ||
                    parentPath.Equals(currentPath, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                currentPath = parentPath;
            }

            return (string.Empty, string.Empty, string.Empty);
        }

        /// <summary>
        /// 从 WWMI 中的 MOD 目录名解析角色名、id、modName
        /// 例如：[Encore][123]MyMod
        /// </summary>
        public static (string CharacterName, string Id, string ModName) ParseWwmiFolderName(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName))
                return (string.Empty, string.Empty, string.Empty);

            var match = WwmiFolderNameRegex.Match(folderName);
            if (!match.Success)
                return (string.Empty, string.Empty, string.Empty);

            var character = match.Groups["character"].Value;
            var id = match.Groups["id"].Value;
            var name = match.Groups["name"].Value;

            return (character, id, name);
        }
    }
}


