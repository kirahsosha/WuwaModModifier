using Xunit;
using WuwaModModifier.Common;

namespace UnitTests
{
    public class ModPathHelperTests
    {
        [Theory]
        [InlineData(@"E:\Game\wuwa mods\Mods\Encore", "Encore")]
        [InlineData(@"C:\Test\Path\Name", "Name")]
        [InlineData(@"NameOnly", "NameOnly")]
        public void GetCharacterNameFromFolder_ShouldReturnLastSegment(string path, string expected)
        {
            var result = ModPathHelper.GetCharacterNameFromFolder(path);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("[12345]MyMod", "12345", "MyMod")]
        [InlineData("[ABC_DEF]Another Mod", "ABC_DEF", "Another Mod")]
        public void ParseModFolderName_ShouldParseIdAndName_WhenFormatIsValid(string folderName, string expectedId, string expectedName)
        {
            var (id, name) = ModPathHelper.ParseModFolderName(folderName);

            Assert.Equal(expectedId, id);
            Assert.Equal(expectedName, name);
        }

        [Theory]
        [InlineData("InvalidName", "", "InvalidName")]
        [InlineData("", "", "")]
        public void ParseModFolderName_ShouldFallbackToName_WhenFormatInvalid(string folderName, string expectedId, string expectedName)
        {
            var (id, name) = ModPathHelper.ParseModFolderName(folderName);

            Assert.Equal(expectedId, id);
            Assert.Equal(expectedName, name);
        }

        [Theory]
        [InlineData(@"E:\Mods\[Encore][12345]MyMod", "Encore", "12345", "MyMod")]
        [InlineData(@"C:\Any\Path\[Char Name][ID_001]Some Mod", "Char Name", "ID_001", "Some Mod")]
        public void ParseWwmiFolderPath_ShouldParseAllParts_WhenFormatIsValid(string path, string expectedCharacter, string expectedId, string expectedName)
        {
            var (character, id, name) = ModPathHelper.ParseWwmiFolderPath(path);

            Assert.Equal(expectedCharacter, character);
            Assert.Equal(expectedId, id);
            Assert.Equal(expectedName, name);
        }

        [Theory]
        [InlineData(@"E:\Mods\InvalidFormat")]
        [InlineData("")]
        public void ParseWwmiFolderPath_ShouldReturnEmpty_WhenFormatInvalid(string path)
        {
            var (character, id, name) = ModPathHelper.ParseWwmiFolderPath(path);

            Assert.Equal(string.Empty, character);
            Assert.Equal(string.Empty, id);
            Assert.Equal(string.Empty, name);
        }
    }
}


