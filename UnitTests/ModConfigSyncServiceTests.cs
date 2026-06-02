using System;
using System.IO;
using Xunit;
using WuwaModModifier.Common;
using WuwaModModifier.Model;

namespace UnitTests
{
    public class ModConfigSyncServiceTests
    {
        [Fact]
        public void SaveBufferToTarget_ShouldWriteBufferToResolvedWwmiPath()
        {
            var service = CreateService();
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifier_SyncTests_{Path.GetRandomFileName()}");
            var modRoot = Path.Combine(tempRoot, "Mods");
            var wwmiRoot = Path.Combine(tempRoot, "WWMI");
            var modDirectory = Path.Combine(modRoot, "Encore", "[123]FancyDress");
            Directory.CreateDirectory(modDirectory);

            var sourcePath = Path.Combine(modDirectory, "mod.ini");
            File.WriteAllText(sourcePath, "[KeyHat]\nkey = NUMPAD1\n");

            try
            {
                var buffer = service.LoadBuffer(sourcePath);
                var updated = service.UpdateKeyBindings(buffer, "KeyHat", new[] { "CTRL ALT NUMPAD1" });
                var saveResult = service.SaveBufferToTarget(updated, ModConfigSaveTarget.WwmiDirectory, modRoot, wwmiRoot);

                var expectedTarget = Path.Combine(wwmiRoot, "[Encore][123]FancyDress", "mod.ini");
                Assert.Equal(expectedTarget, saveResult.TargetPath);
                Assert.True(File.Exists(expectedTarget));
                Assert.Contains("key = CTRL ALT NUMPAD1", File.ReadAllText(expectedTarget));
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void SaveBufferToTarget_ShouldWriteBufferToResolvedModPath()
        {
            var service = CreateService();
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifier_SyncTests_{Path.GetRandomFileName()}");
            var modRoot = Path.Combine(tempRoot, "Mods");
            var wwmiRoot = Path.Combine(tempRoot, "WWMI");
            var wwmiDirectory = Path.Combine(wwmiRoot, "[Encore][123]FancyDress");
            Directory.CreateDirectory(wwmiDirectory);

            var sourcePath = Path.Combine(wwmiDirectory, "mod.ini");
            File.WriteAllText(sourcePath, "[KeyHat]\nkey = NUMPAD1\n");

            try
            {
                var buffer = service.LoadBuffer(sourcePath);
                var updated = service.UpdateKeyBindings(buffer, "KeyHat", new[] { "CTRL ALT NUMPAD1" });
                var saveResult = service.SaveBufferToTarget(updated, ModConfigSaveTarget.ModDirectory, modRoot, wwmiRoot);

                var expectedTarget = Path.Combine(modRoot, "Encore", "[123]FancyDress", "mod.ini");
                Assert.Equal(expectedTarget, saveResult.TargetPath);
                Assert.True(File.Exists(expectedTarget));
                Assert.Contains("key = CTRL ALT NUMPAD1", File.ReadAllText(expectedTarget));
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void PreviewSaveTarget_ShouldReportWhenTargetWillBeCreated()
        {
            var service = CreateService();
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifier_SyncTests_{Path.GetRandomFileName()}");
            var modRoot = Path.Combine(tempRoot, "Mods");
            var wwmiRoot = Path.Combine(tempRoot, "WWMI");
            var modDirectory = Path.Combine(modRoot, "Encore", "[123]FancyDress");
            Directory.CreateDirectory(modDirectory);

            var sourcePath = Path.Combine(modDirectory, "mod.ini");
            File.WriteAllText(sourcePath, "[KeyHat]\nkey = NUMPAD1\n");

            try
            {
                var preview = service.PreviewSaveTarget(sourcePath, ModConfigSaveTarget.WwmiDirectory, modRoot, wwmiRoot);

                Assert.Equal(sourcePath, preview.SourcePath);
                Assert.Equal(Path.Combine(wwmiRoot, "[Encore][123]FancyDress", "mod.ini"), preview.TargetPath);
                Assert.False(preview.TargetExists);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void SyncConfig_ShouldCopyFromModToWwmi()
        {
            var service = CreateService();
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifier_SyncTests_{Path.GetRandomFileName()}");
            var modRoot = Path.Combine(tempRoot, "Mods");
            var wwmiRoot = Path.Combine(tempRoot, "WWMI");
            var modDirectory = Path.Combine(modRoot, "Encore", "[123]FancyDress");
            var wwmiDirectory = Path.Combine(wwmiRoot, "[Encore][123]FancyDress");
            Directory.CreateDirectory(modDirectory);
            Directory.CreateDirectory(wwmiDirectory);

            var sourcePath = Path.Combine(modDirectory, "mod.ini");
            var targetPath = Path.Combine(wwmiDirectory, "mod.ini");
            File.WriteAllText(sourcePath, "mod-from-mod");
            File.WriteAllText(targetPath, "stale");

            try
            {
                var result = service.SyncConfig(sourcePath, ModConfigSyncDirection.ModToWwmi, modRoot, wwmiRoot);

                Assert.Equal(sourcePath, result.SourcePath);
                Assert.Equal(targetPath, result.TargetPath);
                Assert.Equal("mod-from-mod", File.ReadAllText(targetPath));
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void SyncConfig_ShouldCopyFromWwmiToMod()
        {
            var service = CreateService();
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifier_SyncTests_{Path.GetRandomFileName()}");
            var modRoot = Path.Combine(tempRoot, "Mods");
            var wwmiRoot = Path.Combine(tempRoot, "WWMI");
            var modDirectory = Path.Combine(modRoot, "Encore", "[123]FancyDress");
            var wwmiDirectory = Path.Combine(wwmiRoot, "[Encore][123]FancyDress");
            Directory.CreateDirectory(modDirectory);
            Directory.CreateDirectory(wwmiDirectory);

            var sourcePath = Path.Combine(wwmiDirectory, "mod.ini");
            var targetPath = Path.Combine(modDirectory, "mod.ini");
            File.WriteAllText(sourcePath, "mod-from-wwmi");
            File.WriteAllText(targetPath, "stale");

            try
            {
                var result = service.SyncConfig(sourcePath, ModConfigSyncDirection.WwmiToMod, modRoot, wwmiRoot);

                Assert.Equal(sourcePath, result.SourcePath);
                Assert.Equal(targetPath, result.TargetPath);
                Assert.Equal("mod-from-wwmi", File.ReadAllText(targetPath));
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void PreviewSync_ShouldReportWhenTargetWillBeOverwritten()
        {
            var service = CreateService();
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifier_SyncTests_{Path.GetRandomFileName()}");
            var modRoot = Path.Combine(tempRoot, "Mods");
            var wwmiRoot = Path.Combine(tempRoot, "WWMI");
            var modDirectory = Path.Combine(modRoot, "Encore", "[123]FancyDress");
            var wwmiDirectory = Path.Combine(wwmiRoot, "[Encore][123]FancyDress");
            Directory.CreateDirectory(modDirectory);
            Directory.CreateDirectory(wwmiDirectory);

            var sourcePath = Path.Combine(modDirectory, "mod.ini");
            var targetPath = Path.Combine(wwmiDirectory, "mod.ini");
            File.WriteAllText(sourcePath, "mod-from-mod");
            File.WriteAllText(targetPath, "existing-target");

            try
            {
                var preview = service.PreviewSync(sourcePath, ModConfigSyncDirection.ModToWwmi, modRoot, wwmiRoot);

                Assert.Equal(sourcePath, preview.SourcePath);
                Assert.Equal(targetPath, preview.TargetPath);
                Assert.True(preview.TargetExists);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void SaveBufferToTarget_ShouldPreserveNestedRelativePath_WhenConfigLivesInChildDirectory()
        {
            var service = CreateService();
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifier_SyncTests_{Path.GetRandomFileName()}");
            var modRoot = Path.Combine(tempRoot, "Mods");
            var wwmiRoot = Path.Combine(tempRoot, "WWMI");
            var modDirectory = Path.Combine(modRoot, "Camellya", "[596119] camellya_succubus_nsfw", "Succubus Camellya");
            Directory.CreateDirectory(modDirectory);

            var sourcePath = Path.Combine(modDirectory, "mod.ini");
            File.WriteAllText(sourcePath, "[KeyHat]\nkey = NUMPAD1\n");

            try
            {
                var buffer = service.LoadBuffer(sourcePath);
                var updated = service.UpdateKeyBindings(buffer, "KeyHat", new[] { "CTRL ALT NUMPAD1" });
                var saveResult = service.SaveBufferToTarget(updated, ModConfigSaveTarget.WwmiDirectory, modRoot, wwmiRoot);

                var expectedTarget = Path.Combine(wwmiRoot, "[Camellya][596119] camellya_succubus_nsfw", "Succubus Camellya", "mod.ini");
                Assert.Equal(expectedTarget, saveResult.TargetPath);
                Assert.True(File.Exists(expectedTarget));
                Assert.Contains("key = CTRL ALT NUMPAD1", File.ReadAllText(expectedTarget));
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void SaveBufferToTarget_ShouldResolveNestedWwmiPathBackToModStructure()
        {
            var service = CreateService();
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifier_SyncTests_{Path.GetRandomFileName()}");
            var modRoot = Path.Combine(tempRoot, "Mods");
            var wwmiRoot = Path.Combine(tempRoot, "WWMI");
            var wwmiDirectory = Path.Combine(wwmiRoot, "[Youhu][587337] youhu-myrtle-ver220", "YouHu-Myrtle");
            Directory.CreateDirectory(wwmiDirectory);

            var sourcePath = Path.Combine(wwmiDirectory, "mod.ini");
            File.WriteAllText(sourcePath, "[KeyHat]\nkey = NUMPAD1\n");

            try
            {
                var buffer = service.LoadBuffer(sourcePath);
                var updated = service.UpdateKeyBindings(buffer, "KeyHat", new[] { "CTRL ALT NUMPAD1" });
                var saveResult = service.SaveBufferToTarget(updated, ModConfigSaveTarget.ModDirectory, modRoot, wwmiRoot);

                var expectedTarget = Path.Combine(modRoot, "Youhu", "[587337] youhu-myrtle-ver220", "YouHu-Myrtle", "mod.ini");
                Assert.Equal(expectedTarget, saveResult.TargetPath);
                Assert.True(File.Exists(expectedTarget));
                Assert.Contains("key = CTRL ALT NUMPAD1", File.ReadAllText(expectedTarget));
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        private static ModConfigUpdateService CreateService()
        {
            var fileSystem = new FileSystemService();
            var parser = new ModConfigParser(fileSystem);
            var analysis = new ModConfigAnalysisService(parser);
            return new ModConfigUpdateService(fileSystem, parser, analysis);
        }
    }
}