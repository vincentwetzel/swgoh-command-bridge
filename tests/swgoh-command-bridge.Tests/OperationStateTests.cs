#nullable enable

using swgoh_command_bridge.Core.Models;
using Xunit;

namespace swgoh_command_bridge.Tests
{
    /// <summary>
    /// Unit tests verifying state transitions and properties for OperationState.
    /// </summary>
    public class OperationStateTests
    {
        [Fact]
        public void ToLoading_WhenCalled_CreatesStateWithLoadingStatus()
        {
            // Arrange & Act
            var state = OperationState<string>.ToLoading();

            // Assert
            Assert.Equal(OperationStatus.Loading, state.Status);
            Assert.Null(state.Data);
            Assert.Null(state.ErrorMessage);
        }

        [Fact]
        public void ToSuccess_WithData_CreatesStateWithSuccessStatusAndData()
        {
            // Arrange
            var testData = "Optimized Mod List";

            // Act
            var state = OperationState<string>.ToSuccess(testData);

            // Assert
            Assert.Equal(OperationStatus.Success, state.Status);
            Assert.Equal(testData, state.Data);
            Assert.Null(state.ErrorMessage);
        }

        [Fact]
        public void ToError_WithErrorMessage_CreatesStateWithErrorStatusAndMessage()
        {
            // Arrange
            var errorMsg = "Network timeout connection error.";

            // Act
            var state = OperationState<string>.ToError(errorMsg);

            // Assert
            Assert.Equal(OperationStatus.Error, state.Status);
            Assert.Null(state.Data);
            Assert.Equal(errorMsg, state.ErrorMessage);
        }
    }
}