namespace Pulau.Core.Activities;

/// <summary>
/// A source of island activities: media, volume, battery… See docs/ADDING_A_MODULE.md.
/// Each module has at most one activity at a time; raising <see cref="ActivityChanged"/> again
/// replaces it. Raise it on the UI thread.
/// </summary>
public interface IIslandModule
{
    /// <summary>Stable, unique id, e.g. "media".</summary>
    string Id { get; }

    string DisplayName { get; }

    /// <summary>The module's current activity changed; <c>null</c> removes it.</summary>
    event EventHandler<IslandActivity?>? ActivityChanged;

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync();
}

/// <summary>
/// Optional for view models: get told when the content is actually on screen,
/// e.g. to run a clock or a progress timer only while visible.
/// </summary>
public interface IIslandContent
{
    void OnShown();

    void OnHidden();
}
