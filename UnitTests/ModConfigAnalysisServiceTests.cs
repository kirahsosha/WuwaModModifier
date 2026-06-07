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
        public void Analyze_ShouldClassifyWWMIVersionPathParameters_AsInternalSystem()
        {
            var parser = new ModConfigParser(new FileSystemService());
            var service = new ModConfigAnalysisService(parser);

            var result = service.Analyze(parser.Parse(
                "[Constants]\n" +
                "global $Bra = 0\n\n" +
                "[KeyBra]\n" +
                "condition = $object_detected\n" +
                "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1\n" +
                "type = cycle\n" +
                "$Bra = 0,1,2\n\n" +
                "[CommandListRegisterMod]\n" +
                "$\\WWMIv1\\required_wwmi_version = $required_wwmi_version\n" +
                "$\\WWMIv1\\object_guid = $object_guid\n" +
                "Resource\\WWMIv1\\ModName = ref ResourceModName\n"));

            Assert.Equal(ModConfigParameterKind.InternalSystem, result.Parameters.Single(p => p.Name == "$\\WWMIv1\\required_wwmi_version").Kind);
            Assert.Equal(ModConfigParameterKind.InternalSystem, result.Parameters.Single(p => p.Name == "$\\WWMIv1\\object_guid").Kind);
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
        public void Analyze_ShouldRecognizeInlineControlVariables_AsModelParameters()
        {
            var parser = new ModConfigParser(new FileSystemService());
            var service = new ModConfigAnalysisService(parser);

            var result = service.Analyze(parser.Parse(
                "[Constants]\n" +
                "global $object_detected_A = 0\n" +
                "global $mod_enabled_A = 0\n" +
                "global $draw_component_2_ts_A = 1\n" +
                "global $draw_component_2_tuoxie_A = 1\n" +
                "global $draw_component_2_zhi_A = 1\n" +
                "global $draw_component_2_shenti1_A = 1\n\n" +
                "[TextureOverride_A_Component2]\n" +
                "hash = 89d30421\n" +
                "match_first_index = 78834\n" +
                "match_index_count = 59412\n" +
                "if $mod_enabled_A\n" +
                "    handling = skip\n" +
                "    if $draw_component_2_ts_A\n" +
                "        drawindexed = 312, 78834, 0\n" +
                "    endif\n" +
                "    if $draw_component_2_tuoxie_A\n" +
                "        drawindexed = 4050, 79146, 0\n" +
                "    endif\n" +
                "    if $draw_component_2_zhi_A\n" +
                "        drawindexed = 54, 83196, 0\n" +
                "    endif\n" +
                "    if $draw_component_2_shenti1_A\n" +
                "        drawindexed = 212676, 83250, 0\n" +
                "    endif\n" +
                "endif\n"));

            var items = result.VisibilityItems.Where(vi => vi.SectionName == "TextureOverride_A_Component2").ToList();
            Assert.NotEmpty(items);

            var allModelParameters = items.SelectMany(vi => vi.ModelParameters)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            Assert.Contains("$draw_component_2_ts_A", allModelParameters);
            Assert.Contains("$draw_component_2_tuoxie_A", allModelParameters);
            Assert.Contains("$draw_component_2_zhi_A", allModelParameters);
            Assert.Contains("$draw_component_2_shenti1_A", allModelParameters);
            Assert.DoesNotContain("$mod_enabled", allModelParameters);
        }

        [Fact]
        public void AnalyzeFile_ShouldExtractVisibilityItems_FromModSample()
        {
            var service = CreateService();

            var result = service.AnalyzeFile(GetSamplePath("mod.ini"));

            // The sample contains TextureOverride_A_Component2 with 4 drawindexed lines
            // guarded by inline control variables (Denia pattern).
            var componentA2Items = result.VisibilityItems
                .Where(item => item.SectionName == "TextureOverride_A_Component2")
                .ToList();
            Assert.NotEmpty(componentA2Items);
            Assert.All(componentA2Items, item => Assert.Equal(1, item.DrawCallCount));
            Assert.Equal(4, componentA2Items.Count);
            Assert.Equal("89d30421", componentA2Items[0].Hash);

            // Each draw entry should have its controlling variable in ModelParameters.
            var allModelParams = componentA2Items
                .SelectMany(item => item.ModelParameters)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            Assert.Contains("$draw_component_2_ts_A", allModelParams);
            Assert.Contains("$draw_component_2_tuoxie_A", allModelParams);
            Assert.Contains("$draw_component_2_zhi_A", allModelParams);
            Assert.Contains("$draw_component_2_shenti1_A", allModelParams);

            // TextureOverride_C_Component0 has an unconditional drawindexed (no if guard).
            var componentC0 = result.VisibilityItems
                .SingleOrDefault(item => item.SectionName == "TextureOverride_C_Component0");
            Assert.NotNull(componentC0);
            Assert.Equal(1, componentC0.DrawCallCount);
        }

        [Fact]
        public void AnalyzeFile_ShouldClassifyStaticVisibilityItems_FromModSample()
        {
            var service = CreateService();

            var result = service.AnalyzeFile(GetSamplePath("mod.ini"));

            // Component C0: one drawindexed guarded by $mod_enabled_C.
            var componentC0Items = result.VisibilityItems
                .Where(item => item.SectionName == "TextureOverride_C_Component0")
                .ToList();
            Assert.Single(componentC0Items);
            Assert.Equal(1, componentC0Items[0].DrawCallCount);

            // Component C1: four drawindexed calls, each creating a draw entry.
            var componentC1Items = result.VisibilityItems
                .Where(item => item.SectionName == "TextureOverride_C_Component1")
                .ToList();
            Assert.Equal(4, componentC1Items.Count);
            Assert.All(componentC1Items, item => Assert.Equal(1, item.DrawCallCount));
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