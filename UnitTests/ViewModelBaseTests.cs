using Xunit;
using WuwaModModifier.ViewModels;

namespace UnitTests
{
    public class ViewModelBaseTests
    {
        private class TestViewModel : ViewModelBase
        {
            private string _testProperty = "";
            
            public string TestProperty
            {
                get => _testProperty;
                set => SetProperty(ref _testProperty, value);
            }
        }

        [Fact]
        public void SetProperty_ShouldUpdateValue()
        {
            // Arrange
            var viewModel = new TestViewModel();
            var newValue = "TestValue";

            // Act
            viewModel.TestProperty = newValue;

            // Assert
            Assert.Equal(newValue, viewModel.TestProperty);
        }

        [Fact]
        public void SetProperty_ShouldRaisePropertyChanged()
        {
            // Arrange
            var viewModel = new TestViewModel();
            var propertyChangedRaised = false;
            viewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(TestViewModel.TestProperty))
                {
                    propertyChangedRaised = true;
                }
            };

            // Act
            viewModel.TestProperty = "NewValue";

            // Assert
            Assert.True(propertyChangedRaised);
        }

        [Fact]
        public void SetProperty_ShouldNotRaisePropertyChanged_WhenValueUnchanged()
        {
            // Arrange
            var viewModel = new TestViewModel();
            var initialValue = "InitialValue";
            viewModel.TestProperty = initialValue;
            var propertyChangedRaised = false;
            viewModel.PropertyChanged += (sender, args) =>
            {
                propertyChangedRaised = true;
            };

            // Act
            viewModel.TestProperty = initialValue;

            // Assert
            Assert.False(propertyChangedRaised);
        }
    }
}

