#nullable enable

using swgoh_command_bridge.Core.Models;
using swgoh_command_bridge.UI.ViewModels;
using Xunit;

namespace swgoh_command_bridge.Tests;

public sealed class StateViewModelBaseTests
{
    [Fact]
    public void StateTransitionsExposeSharedLoadingEmptySuccessAndErrorProjections()
    {
        var viewModel = new TestStateViewModel();

        Assert.True(viewModel.IsEmpty);
        Assert.False(viewModel.IsLoading);
        Assert.False(viewModel.HasData);
        Assert.False(viewModel.HasError);

        viewModel.SetState(OperationState<string>.ToLoading());
        Assert.True(viewModel.IsBusy);
        Assert.True(viewModel.IsLoading);
        Assert.False(viewModel.IsEmpty);

        viewModel.SetState(OperationState<string>.ToSuccess("loaded"));
        Assert.True(viewModel.HasData);
        Assert.False(viewModel.IsLoading);
        Assert.False(viewModel.HasError);

        viewModel.SetState(OperationState<string>.ToError("cache unavailable"));
        Assert.True(viewModel.HasError);
        Assert.Equal("cache unavailable", viewModel.ErrorMessage);
        Assert.False(viewModel.HasData);
    }

    private sealed class TestStateViewModel : StateViewModelBase<string>
    {
        public void SetState(OperationState<string> state) => State = state;
    }
}
