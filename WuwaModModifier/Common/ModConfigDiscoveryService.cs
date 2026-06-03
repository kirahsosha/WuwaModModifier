using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace WuwaModModifier.Common
{
    /// <summary>
    /// 根据当前仓库的 Mod 目录习惯，对 ini 文件进行排序，优先找出主配置文件。
    /// </summary>
    public class ModConfigDiscoveryService : IModConfigDiscoveryService
    {
        private const int NestedSearchMaxDepth = 3;

        private static readonly Regex KeySectionRegex =
            new Regex(@"^\s*\[Key[^\]]*\]", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        private readonly IFileSystemService _fileSystem;

        public ModConfigDiscoveryService(IFileSystemService fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public IReadOnlyList<string> GetConfigCandidates(string modDirectory)
        {
            if (string.IsNullOrWhiteSpace(modDirectory) || !_fileSystem.DirectoryExists(modDirectory))
            {
                return Array.Empty<string>();
            }

            var folderName = ModPathHelper.GetCharacterNameFromFolder(modDirectory);
            var (_, modName) = ModPathHelper.ParseModFolderName(folderName);
            var allCandidates = new List<string>();
            allCandidates.AddRange(GetIniFilesSafe(modDirectory));

            for (var depth = 1; depth <= NestedSearchMaxDepth; depth++)
            {
                allCandidates.AddRange(GetIniFilesAtDepth(modDirectory, depth));
            }

            if (allCandidates.Count == 0)
            {
                return Array.Empty<string>();
            }

            return OrderCandidates(allCandidates.Distinct(StringComparer.OrdinalIgnoreCase), modDirectory, folderName, modName);
        }

        private IReadOnlyList<string> OrderCandidates(IEnumerable<string> iniFiles, string rootDirectory, string folderName, string modName)
        {
            return iniFiles
                .OrderByDescending(path => ScoreCandidate(path, folderName, modName))
                .ThenBy(path => GetRelativePathDepth(rootDirectory, path))
                .ThenBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ThenBy(path => Path.GetRelativePath(rootDirectory, path), StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static int GetRelativePathDepth(string rootDirectory, string filePath)
        {
            var relativePath = Path.GetRelativePath(rootDirectory, filePath);
            return relativePath
                .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries)
                .Length;
        }

        private List<string> GetIniFilesAtDepth(string rootDirectory, int targetDepth)
        {
            var results = new List<string>();
            var currentLevel = new List<string> { rootDirectory };

            for (var depth = 0; depth <= targetDepth; depth++)
            {
                if (depth == targetDepth)
                {
                    foreach (var directory in currentLevel)
                    {
                        results.AddRange(GetIniFilesSafe(directory));
                    }

                    break;
                }

                var nextLevel = new List<string>();
                foreach (var directory in currentLevel)
                {
                    nextLevel.AddRange(GetDirectoriesSafe(directory));
                }

                currentLevel = nextLevel;
                if (currentLevel.Count == 0)
                {
                    break;
                }
            }

            return results;
        }

        private List<string> GetIniFilesSafe(string directory)
        {
            try
            {
                return _fileSystem.GetFiles(directory, "*.ini").ToList();
            }
            catch (IOException)
            {
                return new List<string>();
            }
            catch (UnauthorizedAccessException)
            {
                return new List<string>();
            }
        }

        private List<string> GetDirectoriesSafe(string directory)
        {
            try
            {
                return _fileSystem.GetDirectories(directory).ToList();
            }
            catch (IOException)
            {
                return new List<string>();
            }
            catch (UnauthorizedAccessException)
            {
                return new List<string>();
            }
        }

        public string? GetPrimaryConfigPath(string modDirectory)
        {
            var candidates = GetConfigCandidates(modDirectory);
            return candidates.Count > 0 ? candidates[0] : null;
        }

        private int ScoreCandidate(string filePath, string folderName, string modName)
        {
            var score = 0;
            var fileName = Path.GetFileNameWithoutExtension(filePath);

            if (string.Equals(fileName, "mod", StringComparison.OrdinalIgnoreCase))
            {
                score += 300;
            }

            if (!string.IsNullOrWhiteSpace(modName) && string.Equals(fileName, modName, StringComparison.OrdinalIgnoreCase))
            {
                score += 220;
            }

            if (string.Equals(fileName, folderName, StringComparison.OrdinalIgnoreCase))
            {
                score += 180;
            }

            if (string.Equals(fileName, "toggle", StringComparison.OrdinalIgnoreCase))
            {
                score -= 80;
            }

            try
            {
                var content = _fileSystem.ReadAllText(filePath);
                if (content.IndexOf("[Constants]", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score += 60;
                }

                if (KeySectionRegex.IsMatch(content))
                {
                    score += 80;
                }

                if (content.IndexOf("TextureOverrideComponent", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score += 40;
                }

                if (content.IndexOf("CommandList", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score += 20;
                }

                if (content.IndexOf("$object_detected", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score += 10;
                }

                if (content.Length < 64)
                {
                    score -= 20;
                }
            }
            catch (IOException)
            {
                score -= 10;
            }
            catch (UnauthorizedAccessException)
            {
                score -= 10;
            }

            return score;
        }
    }
}