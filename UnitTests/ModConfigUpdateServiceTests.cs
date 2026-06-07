using System;
using System.IO;
using Xunit;
using WuwaModModifier.Common;

namespace UnitTests
{
    public class ModConfigUpdateServiceTests
    {
        [Fact]
        public void UpdateKeyBindings_ShouldReplaceOnlyKeyLines()
        {
            var service = CreateService();
            var filePath = CreateTempConfig(
                "[Constants]\n" +
                "global persist $hat = 1\n\n" +
                "[KeyHat]\n" +
                "; keep comment\n" +
                "condition = $object_detected\n" +
                "key = NUMPAD1\n" +
                "key = NUMPAD2\n" +
                "type = hold\n" +
                "$hat = 0,1\n");

            try
            {
                var buffer = service.LoadBuffer(filePath);
                var updated = service.UpdateKeyBindings(buffer, "KeyHat", new[] { "CTRL ALT NUMPAD1", "SHIFT NUMPAD2" });

                Assert.Contains("; keep comment", updated.Content);
                Assert.Contains("condition = $object_detected", updated.Content);
                Assert.Contains("key = CTRL ALT NUMPAD1", updated.Content);
                Assert.Contains("key = SHIFT NUMPAD2", updated.Content);
                Assert.DoesNotContain("key = NUMPAD1", updated.Content);
                Assert.DoesNotContain("key = NUMPAD2", updated.Content);
                Assert.Contains("type = hold", updated.Content);
                Assert.Contains("$hat = 0,1", updated.Content);
            }
            finally
            {
                DeleteTempConfig(filePath);
            }
        }

        [Fact]
        public void UpdateToggleTargetValues_ShouldReplaceOnlyMatchingVariableAssignmentInKeySection()
        {
            var service = CreateService();
            var filePath = CreateTempConfig(
                "[Constants]\n" +
                "global persist $swapvar_clothing = -1\n\n" +
                "[KeySwapClothing]\n" +
                "; keep comment\n" +
                "condition = $object_detected\n" +
                "key = F1\n" +
                "type = cycle\n" +
                "$swapvar_clothing = -1,0,1,2\n" +
                "$swapvar_shoes = 0,1\n");

            try
            {
                var buffer = service.LoadBuffer(filePath);
                var updated = service.UpdateToggleTargetValues(
                    buffer,
                    "KeySwapClothing",
                    "$swapvar_clothing",
                    new[] { "-1", "0", "1", "2", "3", "4" });

                Assert.Contains("; keep comment", updated.Content);
                Assert.Contains("key = F1", updated.Content);
                Assert.Contains("$swapvar_clothing = -1, 0, 1, 2, 3, 4", updated.Content);
                Assert.DoesNotContain("$swapvar_clothing = -1,0,1,2", updated.Content);
                Assert.Contains("$swapvar_shoes = 0,1", updated.Content);
            }
            finally
            {
                DeleteTempConfig(filePath);
            }
        }

        [Fact]
        public void RenameParameter_ShouldReplaceWholeVariableTokensOnly()
        {
            var service = CreateService();
            var filePath = CreateTempConfig(
                "[Constants]\n" +
                "global persist $hat = 1\n" +
                "global persist $hat_extra = 0\n\n" +
                "[KeyHat]\n" +
                "key = NUMPAD1\n" +
                "type = cycle\n" +
                "$hat = 0,1\n" +
                "; comment $hat should stay\n\n" +
                "[TextureOverrideComponent0]\n" +
                "if $hat == 1\n" +
                "    ; Draw Component 0.HAT\n" +
                "    drawindexed = 1, 0, 0\n" +
                "endif\n");

            try
            {
                var updated = service.RenameParameter(service.LoadBuffer(filePath), "$hat", "$cap");

                Assert.Contains("global persist $cap = 1", updated.Content);
                Assert.Contains("global persist $hat_extra = 0", updated.Content);
                Assert.Contains("$cap = 0,1", updated.Content);
                Assert.Contains("if $cap == 1", updated.Content);
                Assert.Contains("; comment $hat should stay", updated.Content);
                Assert.DoesNotContain("global persist $hat = 1", updated.Content);
            }
            finally
            {
                DeleteTempConfig(filePath);
            }
        }

        [Fact]
        public void RenameParameter_ShouldRejectUnsafeParameter()
        {
            var service = CreateService();
            var filePath = CreateTempConfig(
                "[Constants]\n" +
                "global $object_detected = 0\n\n" +
                "[KeyUnsafe]\n" +
                "condition = $object_detected\n" +
                "key = NUMPAD1\n" +
                "type = cycle\n" +
                "$object_detected = 0,1\n");

            try
            {
                var buffer = service.LoadBuffer(filePath);
                Assert.Throws<InvalidOperationException>(() => service.RenameParameter(buffer, "$object_detected", "$seen"));
            }
            finally
            {
                DeleteTempConfig(filePath);
            }
        }

        [Fact]
        public void CreateParameter_ShouldInsertGlobalPersistDeclaration()
        {
            var service = CreateService();
            var filePath = CreateTempConfig(
                "[Constants]\n" +
                "global persist $hat = 1\n");

            try
            {
                var buffer = service.LoadBuffer(filePath);
                var updated = service.CreateParameter(buffer, "$showHat");

                Assert.Contains("global persist $hat = 1", updated.Content);
                Assert.Contains("global persist $showHat = 1", updated.Content);
            }
            finally
            {
                DeleteTempConfig(filePath);
            }
        }

        [Fact]
        public void UpdateParameterDefaultValue_ShouldReplaceDeclarationValue()
        {
            var service = CreateService();
            var filePath = CreateTempConfig(
                "[Constants]\n" +
                "global persist $showHat = 1\n");

            try
            {
                var buffer = service.LoadBuffer(filePath);
                var updated = service.UpdateParameterDefaultValue(buffer, "$showHat", "0");

                Assert.Contains("global persist $showHat = 0", updated.Content);
                Assert.DoesNotContain("global persist $showHat = 1", updated.Content);
            }
            finally
            {
                DeleteTempConfig(filePath);
            }
        }

        [Fact]
        public void CreateToggleBinding_ShouldInsertKeySectionForExistingParameter()
        {
            var service = CreateService();
            var filePath = CreateTempConfig(
                "[Constants]\n" +
                "global persist $showHat = 1\n");

            try
            {
                var buffer = service.LoadBuffer(filePath);
                var updated = service.CreateToggleBinding(buffer, "$showHat", new[] { "NUMPAD1" });

                Assert.Contains("global persist $showHat = 1", updated.Content);
                Assert.Contains("[Key showHat]", updated.Content);
                Assert.Contains("key = NUMPAD1", updated.Content);
                Assert.Contains("type = cycle", updated.Content);
                Assert.Contains("$showHat = 0,1", updated.Content);
            }
            finally
            {
                DeleteTempConfig(filePath);
            }
        }

        [Fact]
        public void ToggleVisibility_ShouldChangeDefaultValue_WhenBinaryToggleControlsDraw()
        {
            var service = CreateService();
            var filePath = CreateTempConfig(
                "[Constants]\n" +
                "global persist $showHat = 1\n\n" +
                "[KeyHat]\n" +
                "key = NUMPAD1\n" +
                "type = cycle\n" +
                "$showHat = 0,1\n\n" +
                "[TextureOverrideComponent0]\n" +
                "if $showHat == 1\n" +
                "    ; Draw Component 0.HAT\n" +
                "    drawindexed = 1, 0, 0\n" +
                "endif\n");

            try
            {
                var buffer = service.LoadBuffer(filePath);
                var updated = service.ToggleVisibility(buffer, "TextureOverrideComponent0", "Draw Component 0.HAT", false);

                Assert.Contains("global persist $showHat = 0", updated.Content);
            }
            finally
            {
                DeleteTempConfig(filePath);
            }
        }

        [Fact]
        public void ToggleVisibility_ShouldUseHiddenFallbackValue_ForMultiValueVisibilityRule()
        {
            var service = CreateService();
            var filePath = CreateTempConfig(
                "[Constants]\n" +
                "global persist $skirt = 0\n\n" +
                "[KeySkirt]\n" +
                "key = NUMPAD1\n" +
                "type = cycle\n" +
                "$skirt = 0,1,2\n\n" +
                "[TextureOverrideComponent0]\n" +
                "if $skirt == 0 || $skirt == 1\n" +
                "    ; Draw Component 0.SKIRT\n" +
                "    drawindexed = 1, 0, 0\n" +
                "endif\n");

            try
            {
                var buffer = service.LoadBuffer(filePath);
                var updated = service.ToggleVisibility(buffer, "TextureOverrideComponent0", "Draw Component 0.SKIRT", false);

                Assert.Contains("global persist $skirt = 2", updated.Content);
            }
            finally
            {
                DeleteTempConfig(filePath);
            }
        }

        [Fact]
        public void BindVisibilityToParameter_ShouldWrapDrawWithExistingParameterCondition()
        {
            var service = CreateService();
            var filePath = CreateTempConfig(
                "[Constants]\n" +
                "global persist $showHat = 1\n\n" +
                "[KeyHat]\n" +
                "condition = $object_detected\n" +
                "key = NUMPAD1\n" +
                "type = cycle\n" +
                "$showHat = 0,1\n\n" +
                "[TextureOverrideComponent0]\n" +
                "; Draw Component 0.HAT\n" +
                "drawindexed = 1, 0, 0\n");

            try
            {
                var buffer = service.LoadBuffer(filePath);
                var updated = service.BindVisibilityToParameter(buffer, "TextureOverrideComponent0", "Draw Component 0.HAT", "$showHat");

                Assert.Contains("if $showHat == 1", updated.Content);
                Assert.Contains("    ; Draw Component 0.HAT", updated.Content);
                Assert.Contains("    drawindexed = 1, 0, 0", updated.Content);
                Assert.Contains("endif", updated.Content);
                Assert.DoesNotContain("global persist $showHat = 1\nglobal persist $showHat = 1", updated.Content);
            }
            finally
            {
                DeleteTempConfig(filePath);
            }
        }

        [Fact]
        public void BindVisibilityToParameter_ShouldRejectParameterWithoutKeyBinding()
        {
            var service = CreateService();
            var filePath = CreateTempConfig(
                "[Constants]\n" +
                "global persist $showHat = 1\n\n" +
                "[TextureOverrideComponent0]\n" +
                "; Draw Component 0.HAT\n" +
                "drawindexed = 1, 0, 0\n");

            try
            {
                var buffer = service.LoadBuffer(filePath);
                Assert.Throws<InvalidOperationException>(() =>
                    service.BindVisibilityToParameter(buffer, "TextureOverrideComponent0", "Draw Component 0.HAT", "$showHat"));
            }
            finally
            {
                DeleteTempConfig(filePath);
            }
        }

        [Fact]
        public void CreateVisibilityBinding_ShouldCreateParameterKeySectionAndWrapDraw()
        {
            var service = CreateService();
            var filePath = CreateTempConfig(
                "[Constants]\n" +
                "global persist $other = 1\n\n" +
                "[KeyOther]\n" +
                "condition = $object_detected\n" +
                "key = NUMPAD9\n" +
                "type = cycle\n" +
                "$other = 0,1\n\n" +
                "[TextureOverrideComponent0]\n" +
                "; Draw Component 0.HAT\n" +
                "drawindexed = 1, 0, 0\n");

            try
            {
                var buffer = service.LoadBuffer(filePath);
                var updated = service.CreateVisibilityBinding(
                    buffer,
                    "TextureOverrideComponent0",
                    "Draw Component 0.HAT",
                    "$showHat",
                    new[] { "NUMPAD1" });

                Assert.Contains("global persist $showHat = 1", updated.Content);
                Assert.Contains("[Key showHat]", updated.Content);
                Assert.Contains("condition = $object_detected", updated.Content);
                Assert.Contains("key = NUMPAD1", updated.Content);
                Assert.Contains("$showHat = 0,1", updated.Content);
                Assert.Contains("if $showHat == 1", updated.Content);
                Assert.Contains("    ; Draw Component 0.HAT", updated.Content);
                Assert.Contains("    drawindexed = 1, 0, 0", updated.Content);
            }
            finally
            {
                DeleteTempConfig(filePath);
            }
        }

        [Fact]
        public void CreateVisibilityBinding_ShouldReplaceExistingControlVariable_WhenItemAlreadyBound()
        {
            var service = CreateService();
            var filePath = CreateTempConfig(
                "[Constants]\n" +
                "global persist $showHat = 1\n\n" +
                "[KeyHat]\n" +
                "condition = $object_detected\n" +
                "key = NUMPAD9\n" +
                "type = cycle\n" +
                "$showHat = 0,1\n\n" +
                "[TextureOverrideComponent0]\n" +
                "if $showHat == 1\n" +
                "    ; Draw Component 0.HAT\n" +
                "    drawindexed = 1, 0, 0\n" +
                "endif\n");

            try
            {
                var buffer = service.LoadBuffer(filePath);
                var updated = service.CreateVisibilityBinding(
                    buffer,
                    "TextureOverrideComponent0",
                    "Draw Component 0.HAT",
                    "$newHat",
                    new[] { "NUMPAD1" });

                Assert.Contains("global persist $newHat = 1", updated.Content);
                Assert.Contains("[Key newHat]", updated.Content);
                Assert.Contains("key = NUMPAD1", updated.Content);
                Assert.Contains("if $newHat == 1", updated.Content);
                Assert.DoesNotContain("if $showHat == 1", updated.Content);
                Assert.Contains("    ; Draw Component 0.HAT", updated.Content);
                Assert.Contains("    drawindexed = 1, 0, 0", updated.Content);
                Assert.Contains("$showHat = 0,1", updated.Content);
            }
            finally
            {
                DeleteTempConfig(filePath);
            }
        }

        [Fact]
        public void StandardizeToggleSlots_ShouldFullyAlignBinaryCycleToggles()
        {
            var service = CreateService();
            var filePath = CreateTempConfig(
                "[Constants]\n" +
                "global persist $hat = 1\n" +
                "global persist $bra = 1\n\n" +
                "[Key Hat]\n" +
                "condition = $object_detected\n" +
                "key = F1\n" +
                "type = cycle\n" +
                "$hat = 0,1\n\n" +
                "[Key Bra]\n" +
                "condition = $object_detected\n" +
                "key = F2\n" +
                "type = cycle\n" +
                "$bra = 0,1\n\n" +
                "[Key Extra]\n" +
                "condition = $object_detected\n" +
                "key = F3\n" +
                "type = hold\n" +
                "$extra = 0,1\n\n" +
                "[TextureOverrideComponent0]\n" +
                "if $hat == 1\n" +
                "    ; Draw Component 0.HAT\n" +
                "    drawindexed = 1, 0, 0\n" +
                "endif\n");
            var templatePath = Path.Combine(Path.GetDirectoryName(filePath)!, "toggle.ini");
            File.WriteAllText(
                templatePath,
                "[Constants]\n" +
                "global persist $key_1 = 1\n" +
                "global persist $key_2 = 1\n\n" +
                "[Key key_1]\n" +
                "condition = $object_detected\n" +
                "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1\n" +
                "type = cycle\n" +
                "$key_1 = 0,1\n\n" +
                "[Key key_2]\n" +
                "condition = $object_detected\n" +
                "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD2\n" +
                "type = cycle\n" +
                "$key_2 = 0,1\n");

            try
            {
                var result = service.StandardizeToggleSlots(service.LoadBuffer(filePath), templatePath);

                Assert.Equal(2, result.FullyStandardizedCount);
                Assert.Equal(0, result.PartiallyStandardizedCount);
                Assert.Equal(1, result.SkippedCount);
                Assert.Contains("[Key key_1]", result.Buffer.Content);
                Assert.Contains("[Key key_2]", result.Buffer.Content);
                Assert.Contains("global persist $key_1 = 1", result.Buffer.Content);
                Assert.Contains("global persist $key_2 = 1", result.Buffer.Content);
                Assert.Contains("key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1", result.Buffer.Content);
                Assert.Contains("key = NO_CTRL NO_ALT NO_SHIFT NUMPAD2", result.Buffer.Content);
                Assert.Contains("if $key_1 == 1", result.Buffer.Content);
                Assert.Contains("[Key Extra]", result.Buffer.Content);
            }
            finally
            {
                DeleteTempConfig(filePath);
            }
        }

        [Fact]
        public void StandardizeToggleSlots_ShouldOnlyUpdateKeys_WhenTargetVariableConflicts()
        {
            var service = CreateService();
            var filePath = CreateTempConfig(
                "[Constants]\n" +
                "global persist $hat = 1\n" +
                "global persist $key_1 = 1\n\n" +
                "[Key Hat]\n" +
                "condition = $object_detected\n" +
                "key = F1\n" +
                "type = cycle\n" +
                "$hat = 0,1\n");
            var templatePath = Path.Combine(Path.GetDirectoryName(filePath)!, "toggle.ini");
            File.WriteAllText(
                templatePath,
                "[Constants]\n" +
                "global persist $key_1 = 1\n\n" +
                "[Key key_1]\n" +
                "condition = $object_detected\n" +
                "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1\n" +
                "type = cycle\n" +
                "$key_1 = 0,1\n");

            try
            {
                var result = service.StandardizeToggleSlots(service.LoadBuffer(filePath), templatePath);

                Assert.Equal(0, result.FullyStandardizedCount);
                Assert.Equal(1, result.PartiallyStandardizedCount);
                Assert.Equal(0, result.SkippedCount);
                Assert.Contains("key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1", result.Buffer.Content);
                Assert.Contains("global persist $hat = 1", result.Buffer.Content);
                Assert.Contains("[Key Hat]", result.Buffer.Content);
                Assert.DoesNotContain("[Key key_1]", result.Buffer.Content);
            }
            finally
            {
                DeleteTempConfig(filePath);
            }
        }

        [Fact]
        public void StandardizeToggleSlots_ShouldForceKeyBindingsForNonBinaryCycleToggles()
        {
            var service = CreateService();
            var filePath = CreateTempConfig(
                "[Constants]\n" +
                "global persist $shape = 1\n\n" +
                "[Key Shape]\n" +
                "condition = $object_detected\n" +
                "key = F1\n" +
                "type = cycle\n" +
                "$shape = 0,1,2\n");
            var templatePath = Path.Combine(Path.GetDirectoryName(filePath)!, "toggle.ini");
            File.WriteAllText(
                templatePath,
                "[Constants]\n" +
                "global persist $key_1 = 1\n\n" +
                "[Key key_1]\n" +
                "condition = $object_detected\n" +
                "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1\n" +
                "type = cycle\n" +
                "$key_1 = 0,1\n");

            try
            {
                var result = service.StandardizeToggleSlots(service.LoadBuffer(filePath), templatePath);

                Assert.Equal(0, result.FullyStandardizedCount);
                Assert.Equal(1, result.PartiallyStandardizedCount);
                Assert.Equal(0, result.SkippedCount);
                Assert.NotEqual(service.LoadBuffer(filePath).Content, result.Buffer.Content);
                Assert.NotEmpty(result.Buffer.AppliedChanges);
                Assert.Contains("NO_CTRL NO_ALT NO_SHIFT NUMPAD1", result.Buffer.Content);
            }
            finally
            {
                DeleteTempConfig(filePath);
            }
        }

        private static ModConfigUpdateService CreateService()
        {
            var fileSystem = new FileSystemService();
            var parser = new ModConfigParser(fileSystem);
            var analysis = new ModConfigAnalysisService(parser);
            return new ModConfigUpdateService(fileSystem, parser, analysis);
        }

        private static string CreateTempConfig(string content)
        {
            var directory = Path.Combine(Path.GetTempPath(), $"WuwaModModifier_UpdateTests_{Path.GetRandomFileName()}");
            Directory.CreateDirectory(directory);

            var filePath = Path.Combine(directory, "mod.ini");
            File.WriteAllText(filePath, content);
            return filePath;
        }

        private static void DeleteTempConfig(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}