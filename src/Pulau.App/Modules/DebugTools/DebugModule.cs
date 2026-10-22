using Pulau.Core.Activities;
using Pulau.Core.Layout;

namespace Pulau.Modules.DebugTools;

/// <summary>
/// Development aid, registered in Debug builds only: the tray's Debug menu uses it to raise dummy
/// activities so every island state can be checked without real media or hardware events.
/// </summary>
public sealed class DebugModule : IIslandModule
{
    public const string ModuleId = "debug";

    public event EventHandler<IslandActivity?>? ActivityChanged;

    public string Id => ModuleId;

    public string DisplayName => "Debug";

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync() => Task.CompletedTask;

    /// <summary>A persistent activity: the island goes compact, and shows a dummy panel when expanded.</summary>
    public void ShowPersistent(bool requestPeek = false) =>
        ActivityChanged?.Invoke(this, new IslandActivity(
            ModuleId,
            ActivityPriority.Normal,
            ActivityKind.Persistent,
            null,
            new DebugViewModel("Debug activity"),
            new DebugViewModel("Expanded debug activity"))
        {
            RequestsPeek = requestPeek,
        });

    /// <summary>A two-second transient activity, like a volume change.</summary>
    public void ShowTransient() =>
        ActivityChanged?.Invoke(this, new IslandActivity(
            ModuleId,
            ActivityPriority.High,
            ActivityKind.Transient,
            TimeSpan.FromSeconds(2),
            new DebugViewModel("Transient (2 s)"),
            null)
        {
            CompactSize = new IslandSize(220, 34, 17),
        });

    public void Clear() => ActivityChanged?.Invoke(this, null);
}

public sealed class DebugViewModel(string text)
{
    public string Text { get; } = text;
}
