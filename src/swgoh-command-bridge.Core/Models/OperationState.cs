#nullable enable

namespace swgoh_command_bridge.Core.Models
{
    /// <summary>
    /// Represents the status of an operation state.
    /// </summary>
    public enum OperationStatus
    {
        /// <summary>
        /// No data has been loaded yet, or the loaded collection is empty.
        /// </summary>
        Empty,

        /// <summary>
        /// Data is currently being fetched or processed.
        /// </summary>
        Loading,

        /// <summary>
        /// The operation completed successfully with data.
        /// </summary>
        Success,

        /// <summary>
        /// The operation failed with an error.
        /// </summary>
        Error
    }

    /// <summary>
    /// A performance-optimized, non-allocating structure representing empty, loading, success, and error states.
    /// </summary>
    public readonly record struct OperationState<T>(
        OperationStatus Status,
        T? Data = default,
        string? ErrorMessage = null
    )
    {
        /// <summary>
        /// Creates a loading state.
        /// </summary>
        public static OperationState<T> ToLoading() => new(OperationStatus.Loading);

        /// <summary>
        /// Creates an empty state.
        /// </summary>
        public static OperationState<T> ToEmpty() => new(OperationStatus.Empty);

        /// <summary>
        /// Creates a success state containing data.
        /// </summary>
        public static OperationState<T> ToSuccess(T data) => new(OperationStatus.Success, data);

        /// <summary>
        /// Creates an error state containing a descriptive error message.
        /// </summary>
        public static OperationState<T> ToError(string errorMessage) => new(OperationStatus.Error, ErrorMessage: errorMessage);
    }
}