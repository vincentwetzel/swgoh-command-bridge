#nullable enable

using swgoh_command_bridge.Core.Models;

namespace swgoh_command_bridge.UI.ViewModels;

/// <summary>
/// Shared loading, empty, success, and error projections for data-driven screens.
/// </summary>
public abstract class StateViewModelBase<T> : ViewModelBase
{
    private OperationState<T> _state = OperationState<T>.ToEmpty();

    public OperationState<T> State
    {
        get => _state;
        protected set
        {
            _state = value;
            OnPropertyChanged(nameof(State));
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(IsLoading));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasData));
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(ErrorMessage));
            OnStateChanged();
        }
    }

    public virtual bool IsBusy => State.Status == OperationStatus.Loading;

    public virtual bool IsLoading => State.Status == OperationStatus.Loading;

    public virtual bool IsEmpty => State.Status == OperationStatus.Empty;

    public bool HasData => State.Status == OperationStatus.Success;

    public bool HasError => State.Status == OperationStatus.Error;

    public string ErrorMessage => State.ErrorMessage ?? string.Empty;

    protected virtual void OnStateChanged()
    {
    }
}
