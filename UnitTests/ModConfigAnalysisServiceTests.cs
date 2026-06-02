using System;
using System.IO;
using System.Linq;
using WuwaModModifier.Common;
using WuwaModModifier.Model;

namespace UnitTests
{
    public class ModConfigAnalysisServiceTests
    {
        [Fact]
        public void AnalyzeFile_ShouldExtractToggleAndParameterMappings_FromGeneratedSample()
        {
            var service = CreateService();

            var result = service.AnalyzeFile(GetSamplePath("mod-自动生成.ini"));

            var toggle = result.Toggles.Single(t => t.SectionName == "Key key_1");
            Assert.Equal("key_1", toggle.DisplayName);
            Assert.Equal("cycle", toggle.ToggleType);
            Assert.Single(toggle.KeyBindings);
            Assert.Equal("NO_CTRL NO_ALT NO_SHIFT NUMPAD1", toggle.KeyBindings[0]);
            Assert.Single(toggle.Targets);
            Assert.Equal("$key_1", toggle.Targets[0].VariableName);
            Assert.Equal(new[] { "0", "1" }, toggle.Targets[0].Values);
            Assert.True(toggle.IsStandardizationCandidate);

            var parameter = result.Parameters.Single(p => p.Name == "$key_1");
            Assert.True(parameter.IsDeclaredInConstants);
            Assert.Equal("1", parameter.DefaultValue);
            Assert.Equal(ModConfigParameterKind.Toggle, parameter.Kind);
            Assert.Contains("Key key_1", parameter.BoundKeySections);
            Assert.Contains("NO_CTRL NO_ALT NO_SHIFT NUMPAD1", parameter.KeyBindings);
            Assert.True(parameter.CanRename);
        }

        [Fact]
        public void AnalyzeFile_ShouldClassifyDirectKeyParametersAsToggle_FromAemeathSample()
        {
            var service = CreateService();

            var result = service.AnalyzeFile(GetSamplePath("Aemeath.ini"));

            var braToggle = result.Toggles.Single(t => t.SectionName == "KeyBra");
            Assert.Equal("Bra", braToggle.DisplayName);
            Assert.Equal("cycle", braToggle.ToggleType);
            Assert.Contains("NO_CTRL NO_ALT NO_SHIFT NUMPAD1", braToggle.KeyBindings);
            Assert.Single(braToggle.Targets);
            Assert.Equal("$Bra", braToggle.Targets[0].VariableName);
            Assert.Equal(new[] { "0", "1", "2", "3", "4" }, braToggle.Targets[0].Values);

            var braParameter = result.Parameters.Single(p => p.Name == "$Bra");
            Assert.Equal(ModConfigParameterKind.Toggle, braParameter.Kind);
            Assert.Contains("KeyBra", braParameter.BoundKeySections);
            Assert.Contains("KeyBra", braParameter.ReferencedInSections);

            var pregnancyParameter = result.Parameters.Single(p => p.Name == "$Pregnancy_shape");
            Assert.Equal(ModConfigParameterKind.Toggle, pregnancyParameter.Kind);
            Assert.Contains("key_shapePregnancy_shape", pregnancyParameter.BoundKeySections);
            Assert.True(pregnancyParameter.CanRename);
        }

        [Fact]
        public void AnalyzeFile_ShouldKeepMultipleBindings_ForHoldSections()
        {
            var service = CreateService();

            var result = service.AnalyzeFile(GetSamplePath("VKLynae.ini"));

            var holdToggle = result.Toggles.Single(t => t.SectionName == "KeyHold");
            Assert.Equal("hold", holdToggle.ToggleType);
            Assert.Equal(2, holdToggle.KeyBindings.Count);
            Assert.Contains("no_ctrl no_shift VK_LBUTTON", holdToggle.KeyBindings);
            Assert.Contains("no_ctrl no_shift VK_RBUTTON", holdToggle.KeyBindings);
            Assert.Single(holdToggle.Targets);
            Assert.Equal("$hold", holdToggle.Targets[0].VariableName);

            var holdParameter = result.Parameters.Single(p => p.Name == "$hold");
            Assert.Contains("KeyHold", holdParameter.BoundKeySections);
            Assert.Equal(ModConfigParameterKind.Unknown, holdParameter.Kind);
        }

        [Fact]
        public void Analyze_ShouldClassifySystemVariables_AsInternalSystem()
        {
            var parser = new ModConfigParser(new FileSystemService());
            var service = new ModConfigAnalysisService(parser);

            var result = service.Analyze(parser.Parse(
                "[Constants]\n" +
                "global $object_detected = 0\n" +
                "global $required_wwmi_version = 0.96\n" +
                "global $Bra = 0\n\n" +
                "[KeyBra]\n" +
                "condition = $object_detected\n" +
                "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1\n" +
                "type = cycle\n" +
                "$Bra = 0,1,2\n"));

            Assert.Equal(ModConfigParameterKind.InternalSystem, result.Parameters.Single(p => p.Name == "$object_detected").Kind);
            Assert.Equal(ModConfigParameterKind.InternalSystem, result.Parameters.Single(p => p.Name == "$required_wwmi_version").Kind);
            Assert.Equal(ModConfigParameterKind.Toggle, result.Parameters.Single(p => p.Name == "$Bra").Kind);
        }

        [Fact]
        public void Analyze_ShouldKeepDeclaredConstantsParameters_AsUnknownBeforeBinding()
        {
            var parser = new ModConfigParser(new FileSystemService());
            var service = new ModConfigAnalysisService(parser);

            var result = service.Analyze(parser.Parse(
                "[Constants]\n" +
                "global persist $showHat = 1\n"));

            var parameter = Assert.Single(result.Parameters);
            Assert.Equal("$showHat", parameter.Name);
            Assert.True(parameter.IsDeclaredInConstants);
            Assert.Equal(ModConfigParameterKind.Unknown, parameter.Kind);
        }

        [Fact]
        public void Analyze_ShouldPropagateIndirectToggleControl_ToVisibilityItems()
        {
            var parser = new ModConfigParser(new FileSystemService());
            var service = new ModConfigAnalysisService(parser);

            var result = service.Analyze(parser.Parse(
                "[Constants]\n" +
                "global persist $swapvar_shoes = 0\n" +
                "global persist $draw_component_3_shoes = 0\n\n" +
                "[KeySwapShoes]\n" +
                "condition = $object_detected\n" +
                "key = F3\n" +
                "type = cycle\n" +
                "$swapvar_shoes = 0,1,2\n\n" +
                "[TextureOverrideComponent3]\n" +
                "hash = abcdef12\n" +
                "run = CommandListShoes\n\n" +
                "[CommandListShoes]\n" +
                "$draw_component_3_shoes = $swapvar_shoes\n" +
                "if $draw_component_3_shoes == 1\n" +
                "    ; Draw Component 3 Shoes\n" +
                "    drawindexed = 1, 0, 0\n" +
                "endif\n"));

            var item = Assert.Single(result.VisibilityItems.Where(visibilityItem =>
                visibilityItem.SectionName == "TextureOverrideComponent3" &&
                visibilityItem.DrawLabels.Contains("3 Shoes")));

            Assert.Equal(ModConfigVisibilityConfidence.High, item.Confidence);
            Assert.Contains("$swapvar_shoes", item.ControllingParameters);
            Assert.Contains("KeySwapShoes", item.ControllingKeySections);
            Assert.Contains("F3", item.ControllingKeyBindings);
            Assert.DoesNotContain("$draw_component_3_shoes", item.ControllingParameters);
        }

        [Fact]
        public void AnalyzeFile_ShouldClassifyToggleTextureAndLinkedParameters_FromModSample()
        {
            var parser = new ModConfigParser(new FileSystemService());
            var service = new ModConfigAnalysisService(parser);

            var result = service.Analyze(parser.Parse(
                "[Constants]\n" +
                "global persist $swapvar_toggle_0 = 0\n" +
                "global persist $draw_component_4_pantsu = 1\n\n" +
                "[KeySwapToggle0]\n" +
                "key = VK_DOWN\n" +
                "type = cycle\n" +
                "$swapvar_toggle_0 = -1, 0\n\n" +
                "[CommandListProcessToggles]\n" +
                "$draw_component_4_pantsu = ($swapvar_toggle_0 == 0)\n\n" +
                "[TextureOverrideComponent4]\n" +
                "run = CommandListProcessToggles\n" +
                "if $draw_component_4_pantsu\n" +
                "    ; Draw Component 4 Pantsu\n" +
                "    drawindexed = 1, 0, 0\n" +
                "endif\n"));

            var toggleParameter = result.Parameters.Single(parameter => parameter.Name == "$swapvar_toggle_0");
            Assert.Equal(ModConfigParameterKind.Toggle, toggleParameter.Kind);
            Assert.Contains("$draw_component_4_pantsu", toggleParameter.LinkedParameterNames);

            var textureParameter = result.Parameters.Single(parameter => parameter.Name == "$draw_component_4_pantsu");
            Assert.Equal(ModConfigParameterKind.Texture, textureParameter.Kind);
            Assert.Contains("$swapvar_toggle_0", textureParameter.LinkedParameterNames);
        }

        [Fact]
        public void Analyze_ShouldExposeModelParametersAndKeyParameterValues_ForVisibilityItems()
        {
            var parser = new ModConfigParser(new FileSystemService());
            var service = new ModConfigAnalysisService(parser);

            var result = service.Analyze(parser.Parse(
                "[Constants]\n" +
                "global persist $swapvar_shoes = 0\n" +
                "global persist $draw_component_3_shoes = 0\n\n" +
                "[KeySwapShoes]\n" +
                "condition = $object_detected\n" +
                "key = F3\n" +
                "type = cycle\n" +
                "$swapvar_shoes = 0,1,2\n\n" +
                "[TextureOverrideComponent3]\n" +
                "hash = abcdef12\n" +
                "run = CommandListShoes\n\n" +
                "[CommandListShoes]\n" +
                "$draw_component_3_shoes = ($swapvar_shoes == 1)\n" +
                "if $draw_component_3_shoes\n" +
                "    drawindexed = 1, 0, 0\n" +
                "endif\n"));

            var item = Assert.Single(result.VisibilityItems.Where(visibilityItem =>
                visibilityItem.SectionName == "TextureOverrideComponent3"));

            Assert.Contains("drawindexed = 1, 0, 0", item.DrawLabels);
            Assert.Contains("$draw_component_3_shoes", item.ModelParameters);

            var keyParameterBinding = Assert.Single(item.KeyParameterBindings);
            Assert.Equal("$swapvar_shoes", keyParameterBinding.ParameterName);
            Assert.Equal(new[] { "1" }, keyParameterBinding.EffectiveValues);
            Assert.Contains("KeySwapShoes", keyParameterBinding.KeySections);
            Assert.Contains("F3", keyParameterBinding.KeyBindings);
        }

        [Fact]
        public void Analyze_ShouldExposeMultipleKeyParameterBindings_ForSingleVisibilityItem()
        {
            var parser = new ModConfigParser(new FileSystemService());
            var service = new ModConfigAnalysisService(parser);

            var result = service.Analyze(parser.Parse(
                "[Constants]\n" +
                "global persist $body = 0\n" +
                "global persist $hat = 0\n\n" +
                "[KeyBody]\n" +
                "key = F1\n" +
                "type = cycle\n" +
                "$body = 0,1\n\n" +
                "[KeyHat]\n" +
                "key = F2\n" +
                "type = cycle\n" +
                "$hat = 0,1\n\n" +
                "[TextureOverrideComponent0]\n" +
                "if $body == 0\n" +
                "    if $hat == 0\n" +
                "        drawindexed = 2583, 1799652, 0\n" +
                "    endif\n" +
                "endif\n"));

            var item = Assert.Single(result.VisibilityItems);
            Assert.Empty(item.ModelParameters);
            Assert.Equal(2, item.KeyParameterBindings.Count);
            Assert.Contains(item.KeyParameterBindings, binding =>
                binding.ParameterName == "$body" && binding.EffectiveValues.SequenceEqual(new[] { "0" }));
            Assert.Contains(item.KeyParameterBindings, binding =>
                binding.ParameterName == "$hat" && binding.EffectiveValues.SequenceEqual(new[] { "0" }));
        }

        [Fact]
        public void Analyze_ShouldFallbackToDrawindexedLine_WhenDrawLabelIsMissing()
        {
            var parser = new ModConfigParser(new FileSystemService());
            var service = new ModConfigAnalysisService(parser);

            var result = service.Analyze(parser.Parse(
                "[Constants]\n" +
                "global persist $body = 0\n\n" +
                "[KeyBody]\n" +
                "key = F1\n" +
                "type = cycle\n" +
                "$body = 0,1\n\n" +
                "[TextureOverrideComponent0]\n" +
                "if $body == 0\n" +
                "    drawindexed = 2583, 1799652, 0\n" +
                "endif\n"));

            var item = Assert.Single(result.VisibilityItems);
            Assert.Contains("drawindexed = 2583, 1799652, 0", item.DrawLabels);
        }

        [Fact]
        public void AnalyzeFile_ShouldExtractHighConfidenceVisibilityItems_FromModSample()
        {
            var service = CreateService();

            var result = service.AnalyzeFile(GetSamplePath("mod.ini"));

            var component4Items = result.VisibilityItems.Where(item => item.SectionName == "TextureOverrideComponent4").ToList();
            Assert.Equal(5, component4Items.Count);
            Assert.All(component4Items, item => Assert.Equal(1, item.DrawCallCount));

            var pantsuItem = Assert.Single(component4Items.Where(item => item.DrawLabels.Contains("4 Pantsu")));
            Assert.Equal("25a5716a", pantsuItem.Hash);
            Assert.Equal("120117", pantsuItem.MatchFirstIndex);
            Assert.Equal("52302", pantsuItem.MatchIndexCount);
            Assert.Equal("skip", pantsuItem.HandlingMode);
            Assert.Equal(ModConfigVisibilityConfidence.High, pantsuItem.Confidence);
            Assert.Contains("$draw_component_4_pantsu", pantsuItem.ModelParameters);
            Assert.Contains("$swapvar_toggle_0", pantsuItem.ControllingParameters);
            Assert.Contains("KeySwapToggle0", pantsuItem.ControllingKeySections);
            Assert.Contains("VK_DOWN", pantsuItem.ControllingKeyBindings);
            Assert.Contains(pantsuItem.KeyParameterBindings, binding =>
                binding.ParameterName == "$swapvar_toggle_0" &&
                binding.EffectiveValues.SequenceEqual(new[] { "0" }));

            var skirtFrontItem = Assert.Single(component4Items.Where(item => item.DrawLabels.Contains("4 Skirt Front.001")));
            Assert.Contains("$swapvar_toggle_1", skirtFrontItem.ControllingParameters);
            Assert.Contains("KeySwapToggle1", skirtFrontItem.ControllingKeySections);
            Assert.Contains("VK_LEFT", skirtFrontItem.ControllingKeyBindings);
        }

        [Fact]
        public void AnalyzeFile_ShouldClassifyStaticVisibilityItems_FromModSample()
        {
            var service = CreateService();

            var result = service.AnalyzeFile(GetSamplePath("mod.ini"));

            var component0 = result.VisibilityItems.Single(item => item.SectionName == "TextureOverrideComponent0");
            Assert.Equal(ModConfigVisibilityConfidence.Medium, component0.Confidence);
            Assert.Equal(1, component0.DrawCallCount);
            Assert.Contains("0", component0.DrawLabels);
            Assert.Empty(component0.ControllingParameters);

            var component5Items = result.VisibilityItems.Where(item => item.SectionName == "TextureOverrideComponent5").ToList();
            Assert.Single(component5Items);
            Assert.Contains("5.001", component5Items[0].DrawLabels);
            Assert.Equal(ModConfigVisibilityConfidence.Medium, component5Items[0].Confidence);

            var component6 = result.VisibilityItems.Single(item => item.SectionName == "TextureOverrideComponent6");
            Assert.Equal(ModConfigVisibilityConfidence.Medium, component6.Confidence);
            Assert.Equal(1, component6.DrawCallCount);
            Assert.Contains("6", component6.DrawLabels);
            Assert.Empty(component6.ControllingParameters);
        }

        private static IModConfigAnalysisService CreateService()
        {
            var fileSystem = new FileSystemService();
            var parser = new ModConfigParser(fileSystem);
            return new ModConfigAnalysisService(parser);
        }

        private static string GetSamplePath(string fileName)
        {
            var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            return Path.Combine(repositoryRoot, "docs", "sample", fileName);
        }
    }
}