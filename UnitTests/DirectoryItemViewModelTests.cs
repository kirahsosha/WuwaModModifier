using Xunit;
using WuwaModModifier.Data.ViewModels;

namespace UnitTests
{
    public class DirectoryItemViewModelTests
    {
        [Fact]
        public void Name_ShouldRaisePropertyChanged()
        {
            // Arrange
            var item = new DirectoryItemViewModel();
            var propertyChangedRaised = false;
            item.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(DirectoryItemViewModel.Name))
                {
                    propertyChangedRaised = true;
                }
            };

            // Act
            item.Name = "TestName";

            // Assert
            Assert.True(propertyChangedRaised);
            Assert.Equal("TestName", item.Name);
        }

        [Fact]
        public void IsChecked_ShouldCheckAllChildren_WhenParentChecked()
        {
            // Arrange
            var parent = new DirectoryItemViewModel { Name = "Parent" };
            var child1 = new DirectoryItemViewModel { Name = "Child1", Parent = parent };
            var child2 = new DirectoryItemViewModel { Name = "Child2", Parent = parent };
            parent.Children.Add(child1);
            parent.Children.Add(child2);

            // Act
            parent.IsChecked = true;

            // Assert
            Assert.True(parent.IsChecked);
            Assert.True(child1.IsChecked);
            Assert.True(child2.IsChecked);
        }

        [Fact]
        public void FullPath_ShouldUpdateCorrectly()
        {
            // Arrange
            var item = new DirectoryItemViewModel();
            var testPath = @"C:\Test\Path";

            // Act
            item.FullPath = testPath;

            // Assert
            Assert.Equal(testPath, item.FullPath);
        }

        [Fact]
        public void IsDirectory_ShouldUpdateCorrectly()
        {
            // Arrange
            var item = new DirectoryItemViewModel();

            // Act
            item.IsDirectory = true;

            // Assert
            Assert.True(item.IsDirectory);
        }
    }
}

