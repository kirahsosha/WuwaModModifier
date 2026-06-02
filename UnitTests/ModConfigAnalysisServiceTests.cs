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
        public void AnalyzeFile_ShouldClassifyEnumAndShapeParameters_FromAemeathSample()
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
            Assert.Equal(ModConfigParameterKind.EnumLike, braParameter.Kind);
            Assert.Contains("KeyBra", braParameter.BoundKeySections);
            Assert.Contains("KeyBra", braParameter.ReferencedInSections);

            var pregnancyParameter = result.Parameters.Single(p => p.Name == "$Pregnancy_shape");
            Assert.Equal(ModConfigParameterKind.ShapeLike, pregnancyParameter.Kind);
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
            Assert.Equal(ModConfigParameterKind.EnumLike, result.Parameters.Single(p => p.Name == "$Bra").Kind);
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
        public void AnalyzeFile_ShouldExtractHighConfidenceVisibilityItems_FromModSample()
        {
            var service = CreateService();

            var result = service.AnalyzeFile(GetSamplePath("mod.ini"));

            var component3Items = result.VisibilityItems.Where(item => item.SectionName == "TextureOverrideComponent3").ToList();
            Assert.True(component3Items.Count > 5);
            Assert.All(component3Items, item => Assert.Equal(1, item.DrawCallCount));

            var bodyItem = Assert.Single(component3Items.Where(item => item.DrawLabels.Contains("3.BODY")));
            Assert.Equal("2738a0f0", bodyItem.Hash);
            Assert.Equal("75096", bodyItem.MatchFirstIndex);
            Assert.Equal("95856", bodyItem.MatchIndexCount);
            Assert.Equal("skip", bodyItem.HandlingMode);
            Assert.Equal(ModConfigVisibilityConfidence.Medium, bodyItem.Confidence);

            var shoeItem = Assert.Single(component3Items.Where(item => item.DrawLabels.Contains("3.SHOE1")));
            Assert.Equal(ModConfigVisibilityConfidence.High, shoeItem.Confidence);
            Assert.Contains("$curShoeIndex", shoeItem.ControllingParameters);
            Assert.Contains("Key curShoeIndex", shoeItem.ControllingKeySections);
            Assert.Contains("NO_CTRL NO_ALT NO_SHIFT NUMPAD1", shoeItem.ControllingKeyBindings);
            Assert.Contains("CommandListDrawComp3", shoeItem.RelatedSectionNames);

            var pantyItem = Assert.Single(component3Items.Where(item => item.DrawLabels.Contains("3.PANTY1")));
            Assert.Contains("$curPantyIndex", pantyItem.ControllingParameters);

            var headBandItem = Assert.Single(component3Items.Where(item => item.DrawLabels.Contains("3.BUNNY_EAR")));
            Assert.Contains("$curHeadBandIndex", headBandItem.ControllingParameters);

            var component2Items = result.VisibilityItems.Where(item => item.SectionName == "TextureOverrideComponent2").ToList();
            Assert.Equal(2, component2Items.Count);

            var faceItem = Assert.Single(component2Items.Where(item => item.DrawLabels.Contains("2.FACE")));
            Assert.Equal(ModConfigVisibilityConfidence.High, faceItem.Confidence);
            Assert.Contains("$curImpressionIndex", faceItem.ControllingParameters);
            Assert.Contains("Key curImpressionIndex", faceItem.ControllingKeySections);

            var ahegaoFaceItem = Assert.Single(component2Items.Where(item => item.DrawLabels.Contains("2.FACE_AHEGAO")));
            Assert.Contains("$curImpressionIndex", ahegaoFaceItem.ControllingParameters);
        }

        [Fact]
        public void AnalyzeFile_ShouldClassifyStaticAndSkippedVisibilityItems_FromModSample()
        {
            var service = CreateService();

            var result = service.AnalyzeFile(GetSamplePath("mod.ini"));

            var component0 = result.VisibilityItems.Single(item => item.SectionName == "TextureOverrideComponent0");
            Assert.Equal(ModConfigVisibilityConfidence.Medium, component0.Confidence);
            Assert.Equal(1, component0.DrawCallCount);
            Assert.Contains("0", component0.DrawLabels);
            Assert.Empty(component0.ControllingParameters);

            var component5Items = result.VisibilityItems.Where(item => item.SectionName == "TextureOverrideComponent5").ToList();
            Assert.Equal(2, component5Items.Count);
            Assert.Contains(component5Items, item => item.DrawLabels.Contains("5"));
            Assert.Contains(component5Items, item => item.DrawLabels.Contains("5.HEART"));

            var component6 = result.VisibilityItems.Single(item => item.SectionName == "TextureOverrideComponent6");
            Assert.Equal(ModConfigVisibilityConfidence.Low, component6.Confidence);
            Assert.Equal(0, component6.DrawCallCount);
            Assert.Empty(component6.DrawLabels);
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