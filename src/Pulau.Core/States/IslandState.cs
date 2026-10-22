namespace Pulau.Core.States;

public enum IslandState
{
    /// <summary>Not shown (fullscreen app, or hidden by the user).</summary>
    Hidden,

    /// <summary>Small empty pill.</summary>
    Idle,

    /// <summary>Slightly wider pill showing the current activity.</summary>
    Compact,

    /// <summary>Large panel: activity controls, or the home panel when there is no activity.</summary>
    Expanded,
}

/// <summary>Why the island is hidden. Several reasons can apply at once.</summary>
[Flags]
public enum HideReasons
{
    None = 0,

    /// <summary>A fullscreen game, video or presentation is running.</summary>
    Fullscreen = 1,

    /// <summary>The user hid the island from the tray menu or with the hotkey.</summary>
    User = 2,
}

/// <summary>Delays used by <see cref="IslandStateMachine"/> (PLAN.md 3.1).</summary>
public sealed record IslandTimings
{
    public static IslandTimings Default { get; } = new();

    /// <summary>How long the pointer must rest on the pill before it expands.</summary>
    public TimeSpan HoverDelay { get; init; } = TimeSpan.FromMilliseconds(150);

    /// <summary>How long the pointer must be away before the expanded island collapses.</summary>
    public TimeSpan LeaveDelay { get; init; } = TimeSpan.FromMilliseconds(400);

    /// <summary>How long an auto-peek keeps the island expanded.</summary>
    public TimeSpan PeekDuration { get; init; } = TimeSpan.FromSeconds(3);
}

public sealed class IslandStateChangedEventArgs(IslandState oldState, IslandState newState) : EventArgs
{
    public IslandState OldState { get; } = oldState;

    public IslandState NewState { get; } = newState;
}
