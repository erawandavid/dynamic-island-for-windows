using Pulau.Core.Timing;

namespace Pulau.Core.Activities;

public enum ModuleFailurePhase
{
    Start,
    Stop,
    Activity,
}

public sealed class ModuleFailedEventArgs(string moduleId, ModuleFailurePhase phase, Exception exception) : EventArgs
{
    public string ModuleId { get; } = moduleId;

    public ModuleFailurePhase Phase { get; } = phase;

    public Exception Exception { get; } = exception;
}

/// <summary>
/// Collects activities from modules and decides which one the island shows (PLAN.md 3.2):
/// <list type="bullet">
/// <item>the highest priority wins; ties go to the most recent activity;</item>
/// <item>a transient activity is removed after its duration, so the persistent one underneath shows again;</item>
/// <item>while expanded, transient activities are ignored unless they are <see cref="ActivityPriority.Critical"/>;</item>
/// <item>a module that fails to start is disabled and reported, and the others keep running.</item>
/// </list>
/// Not thread-safe: use it, and raise module events, on one thread (the UI thread).
/// </summary>
public sealed class ActivityManager : IDisposable
{
    private readonly TimeProvider _time;
    private readonly List<IIslandModule> _modules = [];
    private readonly HashSet<string> _disabled = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private long _sequence;

    public ActivityManager(TimeProvider time)
    {
        _time = time;
    }

    /// <summary>The set of activities changed (added, replaced, removed or expired).</summary>
    public event EventHandler? ActivitiesChanged;

    /// <summary>An activity asked for an auto-peek (<see cref="IslandActivity.RequestsPeek"/>).</summary>
    public event EventHandler<IslandActivity>? PeekRequested;

    /// <summary>A module threw or misbehaved. It is disabled when this happens during start.</summary>
    public event EventHandler<ModuleFailedEventArgs>? ModuleFailed;

    public IReadOnlyList<IIslandModule> Modules => _modules;

    public bool HasActivity => _entries.Count > 0;

    public IReadOnlyCollection<IslandActivity> Activities => _entries.Values.Select(e => e.Activity).ToList();

    public bool IsEnabled(string moduleId) => !_disabled.Contains(moduleId);

    public void Register(IIslandModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        if (_modules.Any(m => m.Id == module.Id))
        {
            throw new InvalidOperationException($"A module with id '{module.Id}' is already registered.");
        }

        _modules.Add(module);
        module.ActivityChanged += (_, activity) => OnActivityChanged(module, activity);
    }

    /// <summary>Starts every registered module. A module that throws is disabled; the rest still start.</summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var module in _modules.ToList())
        {
            if (_disabled.Contains(module.Id))
            {
                continue;
            }

            try
            {
                await module.StartAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Disable(module.Id);
                ModuleFailed?.Invoke(this, new ModuleFailedEventArgs(module.Id, ModuleFailurePhase.Start, ex));
            }
        }
    }

    /// <summary>Stops every enabled module and clears all activities.</summary>
    public async Task StopAsync()
    {
        foreach (var module in _modules.ToList())
        {
            if (_disabled.Contains(module.Id))
            {
                continue;
            }

            try
            {
                await module.StopAsync();
            }
            catch (Exception ex)
            {
                ModuleFailed?.Invoke(this, new ModuleFailedEventArgs(module.Id, ModuleFailurePhase.Stop, ex));
            }
        }

        if (_entries.Count > 0)
        {
            ClearEntries();
            ActivitiesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// The activity to show. While <paramref name="expanded"/>, transient activities are skipped
    /// unless they are critical.
    /// </summary>
    public IslandActivity? Resolve(bool expanded)
    {
        Entry? best = null;
        foreach (var entry in _entries.Values)
        {
            var activity = entry.Activity;
            if (expanded && activity.Kind == ActivityKind.Transient && activity.Priority != ActivityPriority.Critical)
            {
                continue;
            }

            if (best is null ||
                activity.Priority > best.Activity.Priority ||
                (activity.Priority == best.Activity.Priority && entry.Sequence > best.Sequence))
            {
                best = entry;
            }
        }

        return best?.Activity;
    }

    public void Dispose() => ClearEntries();

    private void OnActivityChanged(IIslandModule module, IslandActivity? activity)
    {
        if (_disabled.Contains(module.Id))
        {
            return;
        }

        if (activity is null)
        {
            if (RemoveEntry(module.Id))
            {
                ActivitiesChanged?.Invoke(this, EventArgs.Empty);
            }

            return;
        }

        if (!string.Equals(activity.ModuleId, module.Id, StringComparison.Ordinal))
        {
            ReportInvalid(module, $"Activity has ModuleId '{activity.ModuleId}' but was raised by module '{module.Id}'.");
            return;
        }

        if (activity.Kind == ActivityKind.Transient && !(activity.Duration > TimeSpan.Zero))
        {
            ReportInvalid(module, "Transient activities need a positive Duration.");
            return;
        }

        RemoveEntry(module.Id);
        var entry = new Entry(activity, ++_sequence);
        _entries[module.Id] = entry;

        if (activity.Kind == ActivityKind.Transient)
        {
            entry.Expiry = new OneShotTimer(_time, () => Expire(module.Id, entry));
            entry.Expiry.Start(activity.Duration!.Value);
        }

        ActivitiesChanged?.Invoke(this, EventArgs.Empty);

        if (activity.RequestsPeek)
        {
            PeekRequested?.Invoke(this, activity);
        }
    }

    private void Expire(string moduleId, Entry entry)
    {
        if (_entries.TryGetValue(moduleId, out var current) && ReferenceEquals(current, entry))
        {
            RemoveEntry(moduleId);
            ActivitiesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ReportInvalid(IIslandModule module, string message) =>
        ModuleFailed?.Invoke(this, new ModuleFailedEventArgs(module.Id, ModuleFailurePhase.Activity, new ArgumentException(message)));

    private void Disable(string moduleId)
    {
        _disabled.Add(moduleId);
        if (RemoveEntry(moduleId))
        {
            ActivitiesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool RemoveEntry(string moduleId)
    {
        if (!_entries.Remove(moduleId, out var entry))
        {
            return false;
        }

        entry.Expiry?.Dispose();
        return true;
    }

    private void ClearEntries()
    {
        foreach (var entry in _entries.Values)
        {
            entry.Expiry?.Dispose();
        }

        _entries.Clear();
    }

    private sealed class Entry(IslandActivity activity, long sequence)
    {
        public IslandActivity Activity { get; } = activity;

        public long Sequence { get; } = sequence;

        public OneShotTimer? Expiry { get; set; }
    }
}
