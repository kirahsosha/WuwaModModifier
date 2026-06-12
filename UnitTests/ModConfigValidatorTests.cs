using System;
using WuwaModModifier.Common;
using WuwaModModifier.Model;
using Xunit;

namespace UnitTests
{
    public class ModConfigValidatorTests
    {
        [Fact]
        public void ValidateDocument_ShouldThrow_WhenNull()
        {
            Assert.Throws<ArgumentNullException>(() => ModConfigValidator.ValidateDocument(null!, "doc"));
        }

        [Fact]
        public void ValidateDocument_ShouldThrow_WhenSectionsNull()
        {
            var doc = new ModConfigDocument { Sections = null! };
            Assert.Throws<ArgumentException>(() => ModConfigValidator.ValidateDocument(doc, "doc"));
        }

        [Fact]
        public void ValidateDocument_ShouldNotThrow_WhenValid()
        {
            var doc = new ModConfigDocument
            {
                Sections = new System.Collections.Generic.List<ModConfigSection>()
            };
            ModConfigValidator.ValidateDocument(doc, "doc");
        }

        [Fact]
        public void ValidateSection_ShouldThrow_WhenNull()
        {
            Assert.Throws<ArgumentNullException>(() => ModConfigValidator.ValidateSection(null!, "sec"));
        }

        [Fact]
        public void ValidateSection_ShouldThrow_WhenNameEmpty()
        {
            var section = new ModConfigSection { Name = "" };
            Assert.Throws<ArgumentException>(() => ModConfigValidator.ValidateSection(section, "sec"));
        }

        [Fact]
        public void ValidateSection_ShouldNotThrow_WhenValid()
        {
            var section = new ModConfigSection { Name = "KeyHat" };
            ModConfigValidator.ValidateSection(section, "sec");
        }

        [Fact]
        public void ValidateParameterName_ShouldThrow_WhenDoesNotStartWithDollar()
        {
            Assert.Throws<ArgumentException>(() =>
                ModConfigValidator.ValidateParameterName("hat", "param"));
        }

        [Fact]
        public void ValidateParameterName_ShouldThrow_WhenEmpty()
        {
            Assert.Throws<ArgumentException>(() =>
                ModConfigValidator.ValidateParameterName("", "param"));
        }

        [Fact]
        public void ValidateParameterName_ShouldNotThrow_WhenStartsWithDollar()
        {
            ModConfigValidator.ValidateParameterName("$hat", "param");
        }

        [Fact]
        public void ValidateToggleDefinition_ShouldThrow_WhenNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                ModConfigValidator.ValidateToggleDefinition(null!, "toggle"));
        }

        [Fact]
        public void ValidateToggleDefinition_ShouldThrow_WhenSectionNameEmpty()
        {
            var toggle = new ModToggleDefinition { SectionName = "" };
            Assert.Throws<ArgumentException>(() =>
                ModConfigValidator.ValidateToggleDefinition(toggle, "toggle"));
        }

        [Fact]
        public void ValidateFilePath_ShouldThrow_WhenEmpty()
        {
            Assert.Throws<ArgumentException>(() =>
                ModConfigValidator.ValidateFilePath("", "path"));
        }

        [Fact]
        public void ValidateFilePath_ShouldNotThrow_WhenValid()
        {
            ModConfigValidator.ValidateFilePath(@"C:\mods\config.ini", "path");
        }
    }
}
