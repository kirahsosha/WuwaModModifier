using Xunit;
using WuwaModModifier.ViewModels;

namespace UnitTests
{
    public class RelayCommandTTests
    {
        [Fact]
        public void Execute_ShouldCallActionWithParameter()
        {
            // Arrange
            string? receivedValue = null;
            var command = new RelayCommand<string>((value) => { receivedValue = value; });

            // Act
            command.Execute("TestValue");

            // Assert
            Assert.Equal("TestValue", receivedValue);
        }

        [Fact]
        public void CanExecute_ShouldReturnTrue_WhenNoCanExecuteProvided()
        {
            // Arrange
            var command = new RelayCommand<string>((value) => { });

            // Act
            var canExecute = command.CanExecute("Test");

            // Assert
            Assert.True(canExecute);
        }

        [Fact]
        public void CanExecute_ShouldReturnCanExecuteResult()
        {
            // Arrange
            var command = new RelayCommand<string>(
                (value) => { },
                (value) => value != null && value.Length > 5
            );

            // Act & Assert
            Assert.False(command.CanExecute("Test"));
            Assert.True(command.CanExecute("TestValue"));
        }

        [Fact]
        public void Execute_ShouldNotExecute_WhenParameterTypeMismatch()
        {
            // Arrange
            string? receivedValue = null;
            var command = new RelayCommand<string>((value) => { receivedValue = value; });

            // Act
            command.Execute(123); // 传递错误类型

            // Assert
            Assert.Null(receivedValue);
        }

        [Fact]
        public void Constructor_ShouldThrowArgumentNullException_WhenExecuteIsNull()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new RelayCommand<string>(null!));
        }
    }
}

