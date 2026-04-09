using Xunit;
using WuwaModModifier.ViewModels;

namespace UnitTests
{
    public class RelayCommandTests
    {
        [Fact]
        public void Execute_ShouldCallAction()
        {
            // Arrange
            var executed = false;
            var command = new RelayCommand(() => { executed = true; });

            // Act
            command.Execute(null);

            // Assert
            Assert.True(executed);
        }

        [Fact]
        public void CanExecute_ShouldReturnTrue_WhenNoCanExecuteProvided()
        {
            // Arrange
            var command = new RelayCommand(() => { });

            // Act
            var canExecute = command.CanExecute(null);

            // Assert
            Assert.True(canExecute);
        }

        [Fact]
        public void CanExecute_ShouldReturnCanExecuteResult()
        {
            // Arrange
            var command = new RelayCommand(() => { }, () => false);

            // Act
            var canExecute = command.CanExecute(null);

            // Assert
            Assert.False(canExecute);
        }

        [Fact]
        public void Constructor_ShouldThrowArgumentNullException_WhenExecuteIsNull()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new RelayCommand(null!));
        }
    }
}

