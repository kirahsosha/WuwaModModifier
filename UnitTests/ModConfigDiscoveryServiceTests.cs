using System.Collections.Generic;
using System.IO;
using System.Linq;
using WuwaModModifier.Common;

namespace UnitTests
{
    public class ModConfigDiscoveryServiceTests
    {
        [Fact]
        public void GetPrimaryConfigPath_ShouldPreferModIni_WhenPresent()
        {
            var modDirectory = CreateTempDirectory("[100]TestMod");

            try
            {
                File.WriteAllText(Path.Combine(modDirectory, "readme.ini"), "; helper\n");
                File.WriteAllText(
                    Path.Combine(modDirectory, "mod.ini"),
                    "[Constants]\n" +
                    "global $key_1 = 1\n\n" +
                    "[Key key_1]\n" +
                    "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1\n" +
                    "$key_1 = 0,1\n");

                var service = new ModConfigDiscoveryService(new FileSystemService());

                var primary = service.GetPrimaryConfigPath(modDirectory);

                Assert.NotNull(primary);
                Assert.Equal("mod.ini", Path.GetFileName(primary));
            }
            finally
            {
                Directory.Delete(modDirectory, true);
            }
        }

        [Fact]
        public void GetPrimaryConfigPath_ShouldPreferStructuredFileMatchingModName_WhenModIniMissing()
        {
            var modDirectory = CreateTempDirectory("[101]FancyDress");

            try
            {
                File.WriteAllText(Path.Combine(modDirectory, "notes.ini"), "; notes only\n");
                File.WriteAllText(
                    Path.Combine(modDirectory, "FancyDress.ini"),
                    "[Constants]\n" +
                    "global $Bra = 0\n\n" +
                    "[KeyBra]\n" +
                    "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1\n" +
                    "type = cycle\n" +
                    "$Bra = 0,1,2\n");

                var service = new ModConfigDiscoveryService(new FileSystemService());

                var primary = service.GetPrimaryConfigPath(modDirectory);

                Assert.NotNull(primary);
                Assert.Equal("FancyDress.ini", Path.GetFileName(primary));
            }
            finally
            {
                Directory.Delete(modDirectory, true);
            }
        }

        [Fact]
        public void GetConfigCandidates_ShouldReturnEmpty_WhenDirectoryDoesNotExist()
        {
            var service = new ModConfigDiscoveryService(new FileSystemService());

            var candidates = service.GetConfigCandidates(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

            Assert.Empty(candidates);
        }

        [Fact]
        public void GetConfigCandidates_ShouldFallbackToNestedDirectory_WhenRootHasNoIni()
        {
            var modDirectory = CreateTempDirectory("[102]RootOnly");

            try
            {
                var nestedDirectory = Path.Combine(modDirectory, "Succubus Camellya");
                Directory.CreateDirectory(nestedDirectory);

                File.WriteAllText(
                    Path.Combine(nestedDirectory, "mod.ini"),
                    "[Constants]\n" +
                    "global $key_1 = 1\n\n" +
                    "[Key key_1]\n" +
                    "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1\n" +
                    "$key_1 = 0,1\n");

                var service = new ModConfigDiscoveryService(new FileSystemService());

                var candidates = service.GetConfigCandidates(modDirectory);

                Assert.Single(candidates);
                Assert.Equal(Path.Combine(nestedDirectory, "mod.ini"), candidates.Single());
            }
            finally
            {
                Directory.Delete(modDirectory, true);
            }
        }

        [Fact]
        public void GetConfigCandidates_ShouldIncludeRootAndNestedCandidates_WhenBothExist()
        {
            var modDirectory = CreateTempDirectory("[103]PreferRoot");

            try
            {
                var nestedDirectory = Path.Combine(modDirectory, "Succubus Camellya");
                Directory.CreateDirectory(nestedDirectory);

                File.WriteAllText(
                    Path.Combine(modDirectory, "mod.ini"),
                    "[Constants]\n" +
                    "global $key_1 = 1\n\n" +
                    "[Key key_1]\n" +
                    "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1\n" +
                    "$key_1 = 0,1\n");
                File.WriteAllText(
                    Path.Combine(nestedDirectory, "mod.ini"),
                    "[Constants]\n" +
                    "global $key_2 = 1\n\n" +
                    "[Key key_2]\n" +
                    "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD2\n" +
                    "$key_2 = 0,1\n");

                var service = new ModConfigDiscoveryService(new FileSystemService());

                var candidates = service.GetConfigCandidates(modDirectory);

                Assert.Equal(2, candidates.Count);
                Assert.Equal(Path.Combine(modDirectory, "mod.ini"), candidates[0]);
                Assert.Equal(Path.Combine(nestedDirectory, "mod.ini"), candidates[1]);
            }
            finally
            {
                Directory.Delete(modDirectory, true);
            }
        }

        [Fact]
        public void GetConfigCandidates_ShouldReturnAllRootIniFiles_WhenMultipleMatchesExist()
        {
            var modDirectory = CreateTempDirectory("[104]MultiConfig");

            try
            {
                File.WriteAllText(
                    Path.Combine(modDirectory, "mod.ini"),
                    "[Constants]\n" +
                    "global $key_1 = 1\n");
                File.WriteAllText(
                    Path.Combine(modDirectory, "form_a.ini"),
                    "[Constants]\n" +
                    "global $key_2 = 2\n");
                File.WriteAllText(
                    Path.Combine(modDirectory, "form_b.ini"),
                    "[Constants]\n" +
                    "global $key_3 = 3\n");

                var service = new ModConfigDiscoveryService(new FileSystemService());

                var candidates = service.GetConfigCandidates(modDirectory);

                Assert.Equal(3, candidates.Count);
                Assert.Contains(Path.Combine(modDirectory, "mod.ini"), candidates);
                Assert.Contains(Path.Combine(modDirectory, "form_a.ini"), candidates);
                Assert.Contains(Path.Combine(modDirectory, "form_b.ini"), candidates);
            }
            finally
            {
                Directory.Delete(modDirectory, true);
            }
        }

        [Fact]
        public void GetConfigCandidates_ShouldDiscoverDeniaLikeLayeredModIniStructure()
        {
            var modDirectory = CreateTempDirectory("[678974] Denia Lace lingerie set & Topless monsoon");

            try
            {
                var expectedPaths = new List<string>
                {
                    Path.Combine(modDirectory, "mod.ini"),
                    Path.Combine(modDirectory, "1", "mod.ini"),
                    Path.Combine(modDirectory, "2", "mod.ini"),
                    Path.Combine(modDirectory, "3", "mod.ini"),
                    Path.Combine(modDirectory, "4", "mod.ini")
                };

                foreach (var expectedPath in expectedPaths)
                {
                    var parentDirectory = Path.GetDirectoryName(expectedPath)!;
                    Directory.CreateDirectory(parentDirectory);
                    File.WriteAllText(
                        expectedPath,
                        "[Constants]\n" +
                        "global $layer = 1\n\n" +
                        "[Key key_1]\n" +
                        "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1\n" +
                        "$layer = 0,1\n");
                }

                var service = new ModConfigDiscoveryService(new FileSystemService());

                var candidates = service.GetConfigCandidates(modDirectory);

                Assert.Equal(5, candidates.Count);
                Assert.Equal(expectedPaths[0], candidates[0]);
                foreach (var expectedPath in expectedPaths)
                {
                    Assert.Contains(expectedPath, candidates);
                }
            }
            finally
            {
                Directory.Delete(modDirectory, true);
            }
        }

        private static string CreateTempDirectory(string folderName)
        {
            var root = Path.Combine(Path.GetTempPath(), $"WuwaModModifierTests_{Path.GetRandomFileName()}");
            var directory = Path.Combine(root, folderName);
            Directory.CreateDirectory(directory);
            return directory;
        }
    }
}