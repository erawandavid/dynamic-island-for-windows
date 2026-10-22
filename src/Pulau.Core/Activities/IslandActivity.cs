using Pulau.Core.Layout;

namespace Pulau.Core.Activities;

public enum ActivityPriority
{
    Low,
    Normal,
    High,
    Critical,
}

public enum ActivityKind
{
    /// <summary>Stays until its module removes or replaces it (e.g. music playing).</summary>
    Persistent,

    /// <summary>Shown for <see cref="IslandActivity.Duration"/>, then removed automatically (e.g. volume changed).</summary>
    Transient,
}

/// <summary>
/// Something a module wants the island to show. The view models are plain objects; the app maps
/// each view-model type to a view with a DataTemplate.
/// </summary>
/// <param name="ModuleId">Id of the module that owns the activity.</param>
/// <param name="Priority">The highest priority wins; ties go to the most recent activity.</param>
/// <param name="Kind">Persistent or transient.</param>
/// <param name="Duration">Required for transient activities: how long they stay.</param>
/// <param name="CompactViewModel">Shown in the compact pill.</param>
/// <param name="ExpandedViewModel">Shown when the island is expanded; <c>null</c> shows the home panel instead.</param>
public sealed record IslandActivity(
    string ModuleId,
    ActivityPriority Priority,
    ActivityKind Kind,
    TimeSpan? Duration,
    object CompactViewModel,
    object? ExpandedViewModel)
{
    /// <summary>Pill size while compact. Compact content can use 220–280 DIPs of width.</summary>
    public IslandSize CompactSize { get; init; } = IslandSizes.Compact;

    /// <summary>Pill size while expanded.</summary>
    public IslandSize ExpandedSize { get; init; } = IslandSizes.Expanded;

    /// <summary>Ask the island to expand briefly (auto-peek), e.g. because the track changed.</summary>
    public bool RequestsPeek { get; init; }
}
