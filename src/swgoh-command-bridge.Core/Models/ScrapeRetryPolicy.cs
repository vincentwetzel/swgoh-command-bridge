#nullable enable

using System;

namespace swgoh_command_bridge.Core.Models;

/// <summary>
/// Controls retry, backoff, and inter-request pacing for community recommendation refreshes.
/// </summary>
public sealed record ScrapeRetryPolicy
{
    /// <summary>
    /// Creates a retry policy with conservative defaults suitable for swgoh.gg.
    /// </summary>
    public ScrapeRetryPolicy(
        int maxAttempts = 4,
        TimeSpan? initialBackoff = null,
        TimeSpan? maximumBackoff = null,
        TimeSpan? interRequestDelay = null)
    {
        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "At least one request attempt is required.");
        }

        MaxAttempts = maxAttempts;
        InitialBackoff = ValidateDuration(initialBackoff ?? TimeSpan.FromSeconds(2), nameof(initialBackoff));
        MaximumBackoff = ValidateDuration(maximumBackoff ?? TimeSpan.FromSeconds(60), nameof(maximumBackoff));
        InterRequestDelay = ValidateDuration(interRequestDelay ?? TimeSpan.FromSeconds(3), nameof(interRequestDelay));

        if (MaximumBackoff < InitialBackoff)
        {
            throw new ArgumentException("Maximum backoff cannot be shorter than initial backoff.", nameof(maximumBackoff));
        }
    }

    /// <summary>Gets the number of HTTP attempts, including the initial request.</summary>
    public int MaxAttempts { get; }

    /// <summary>Gets the delay before the first retry.</summary>
    public TimeSpan InitialBackoff { get; }

    /// <summary>Gets the largest delay allowed for one retry.</summary>
    public TimeSpan MaximumBackoff { get; }

    /// <summary>Gets the delay between characters during an incremental refresh.</summary>
    public TimeSpan InterRequestDelay { get; }

    /// <summary>Calculates deterministic exponential backoff for a one-based retry number.</summary>
    public TimeSpan GetBackoff(int retryNumber)
    {
        if (retryNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(retryNumber), "Retry numbers start at one.");
        }

        var multiplier = Math.Pow(2, Math.Min(retryNumber - 1, 30));
        var milliseconds = Math.Min(
            MaximumBackoff.TotalMilliseconds,
            InitialBackoff.TotalMilliseconds * multiplier);
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    /// <summary>Combines server-provided retry timing with local backoff while respecting the cap.</summary>
    public TimeSpan GetDelay(int retryNumber, TimeSpan? serverDelay = null)
    {
        var backoff = GetBackoff(retryNumber);
        if (serverDelay is not { } requestedDelay || requestedDelay <= backoff)
        {
            return backoff;
        }

        return requestedDelay <= MaximumBackoff ? requestedDelay : MaximumBackoff;
    }

    private static TimeSpan ValidateDuration(TimeSpan duration, string parameterName)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Durations cannot be negative.");
        }

        return duration;
    }
}
