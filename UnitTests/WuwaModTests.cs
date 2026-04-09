using Xunit;
using WuwaModModifier.Model;

namespace UnitTests
{
    public class WuwaModTests
    {
        [Fact]
        public void WuwaMod_ShouldInitializeWithDefaultValues()
        {
            // Act
            var mod = new WuwaMod();

            // Assert
            Assert.NotNull(mod);
            Assert.Equal("", mod.CharacterName);
            Assert.Equal("", mod.Id);
            Assert.Equal("", mod.FullPath);
            Assert.Equal("", mod.ModName);
        }

        [Fact]
        public void WuwaMod_ShouldSetProperties()
        {
            // Arrange
            var mod = new WuwaMod
            {
                CharacterName = "TestCharacter",
                Id = "123",
                FullPath = @"C:\Test\Path",
                ModName = "TestMod"
            };

            // Assert
            Assert.Equal("TestCharacter", mod.CharacterName);
            Assert.Equal("123", mod.Id);
            Assert.Equal(@"C:\Test\Path", mod.FullPath);
            Assert.Equal("TestMod", mod.ModName);
        }

        [Fact]
        public void WuwaMods_ShouldInitializeWithDefaultValues()
        {
            // Act
            var mods = new WuwaMods();

            // Assert
            Assert.NotNull(mods);
            Assert.Equal("", mods.CharacterName);
            Assert.Equal("", mods.Folder);
            Assert.NotNull(mods.Mods);
            Assert.Empty(mods.Mods);
        }

        [Fact]
        public void WuwaMods_ShouldAddMods()
        {
            // Arrange
            var mods = new WuwaMods
            {
                CharacterName = "TestCharacter",
                Folder = @"C:\Test\Folder"
            };

            var mod1 = new WuwaMod { CharacterName = "TestCharacter", ModName = "Mod1" };
            var mod2 = new WuwaMod { CharacterName = "TestCharacter", ModName = "Mod2" };

            // Act
            mods.Mods.Add(mod1);
            mods.Mods.Add(mod2);

            // Assert
            Assert.Equal(2, mods.Mods.Count);
            Assert.Contains(mod1, mods.Mods);
            Assert.Contains(mod2, mods.Mods);
        }
    }
}

