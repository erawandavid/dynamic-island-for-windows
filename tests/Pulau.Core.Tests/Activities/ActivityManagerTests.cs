using Microsoft.Extensions.Time.Testing;
using Pulau.Core.Activities;

namespace Pulau.Core.Tests.Activities;

public class ActivityManagerTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly ActivityManager _manager;
    private readonly List<ModuleFailedEventArgs> _failures = [];
    private int _changes;

    public ActivityManagerTests()
    {
        _manager = new ActivityManager(_time);
        _manager.ActivitiesChanged += (_, _) => _changes++;
        _manager.ModuleFailed += (_, e) => _failures.Add(e);
    }

    private FakeModule Add(string id)
    {
        var module = new FakeModule(id);
        _manager.Register(module);
        return module;
    }

    private static IslandActivity Persistent(string moduleId, ActivityPriority priority = ActivityPriority.Normal) =>
        new(moduleId, priority, ActivityKind.Persistent, null, $"{moduleId}-compact", $"{moduleId}-expanded");

    private static IslandActivity Transient(string moduleId, ActivityPriority priority, double seconds) =>
        new(moduleId, priority, ActivityKind.Transient, TimeSpan.FromSeconds(seconds), $"{moduleId}-compact", null);

    [Fact]
    public void IsEmptyInitially()
    {
        Assert.False(_manager.HasActivity);
        Assert.Null(_manager.Resolve(expanded: false));
    }

    [Fact]
    public void ShowsHighestPriority()
    {
        var media = Add("media");
        var battery = Add("battery");

        media.Raise(Persistent("media", ActivityPriority.Normal));
        battery.Raise(Persistent("battery", ActivityPriority.High));

        Assert.Equal("battery", _manager.Resolve(false)!.ModuleId);
    }

    [Fact]
    public void TieGoesToMostRecent()
    {
        var a = Add("a");
        var b = Add("b");

        a.Raise(Persistent("a"));
        b.Raise(Persistent("b"));
        Assert.Equal("b", _manager.Resolve(false)!.ModuleId);

        a.Raise(Persistent("a"));
        Assert.Equal("a", _manager.Resolve(false)!.ModuleId);
    }

    [Fact]
    public void Transient_OverridesPersistent_ForItsDuration_ThenPersistentReturns()
    {
        var media = Add("media");
        var volume = Add("volume");
        media.Raise(Persistent("media"));

        volume.Raise(Transient("volume", ActivityPriority.High, 1.5));
        Assert.Equal("volume", _manager.Resolve(false)!.ModuleId);

        _time.Advance(TimeSpan.FromMilliseconds(1499));
        Assert.Equal("volume", _manager.Resolve(false)!.ModuleId);

        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal("media", _manager.Resolve(false)!.ModuleId);
        Assert.Single(_manager.Activities);
    }

    [Fact]
    public void Transient_WithEqualPriority_Overrides_BecauseItIsNewer()
    {
        var media = Add("media");
        var battery = Add("battery");
        media.Raise(Persistent("media", ActivityPriority.Normal));

        battery.Raise(Transient("battery", ActivityPriority.Normal, 2));

        Assert.Equal("battery", _manager.Resolve(false)!.ModuleId);
    }

    [Fact]
    public void Transient_WithLowerPriority_DoesNotOverride()
    {
        var media = Add("media");
        var other = Add("other");
        media.Raise(Persistent("media", ActivityPriority.High));

        other.Raise(Transient("other", ActivityPriority.Normal, 2));

        Assert.Equal("media", _manager.Resolve(false)!.ModuleId);
    }

    [Fact]
    public void RaisingAgain_RestartsTheTransientDuration()
    {
        var volume = Add("volume");

        volume.Raise(Transient("volume", ActivityPriority.High, 1.5));
        _time.Advance(TimeSpan.FromSeconds(1));
        volume.Raise(Transient("volume", ActivityPriority.High, 1.5));
        _time.Advance(TimeSpan.FromSeconds(1));

        Assert.True(_manager.HasActivity);

        _time.Advance(TimeSpan.FromSeconds(0.5));
        Assert.False(_manager.HasActivity);
    }

    [Fact]
    public void PersistentReplacingTransient_CancelsItsExpiry()
    {
        var module = Add("battery");

        module.Raise(Transient("battery", ActivityPriority.High, 1));
        module.Raise(Persistent("battery"));
        _time.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal(ActivityKind.Persistent, _manager.Resolve(false)!.Kind);
    }

    [Fact]
    public void Null_RemovesTheModulesActivity()
    {
        var media = Add("media");
        media.Raise(Persistent("media"));
        var before = _changes;

        media.Raise(null);

        Assert.False(_manager.HasActivity);
        Assert.Equal(before + 1, _changes);

        // Removing again is a no-op.
        media.Raise(null);
        Assert.Equal(before + 1, _changes);
    }

    [Fact]
    public void Expanded_IgnoresTransientsUnlessCritical()
    {
        var media = Add("media");
        var volume = Add("volume");
        var alarm = Add("alarm");
        media.Raise(Persistent("media"));
        volume.Raise(Transient("volume", ActivityPriority.High, 2));

        Assert.Equal("volume", _manager.Resolve(expanded: false)!.ModuleId);
        Assert.Equal("media", _manager.Resolve(expanded: true)!.ModuleId);

        alarm.Raise(Transient("alarm", ActivityPriority.Critical, 2));
        Assert.Equal("alarm", _manager.Resolve(expanded: true)!.ModuleId);
    }

    [Fact]
    public void Expanded_WithOnlyTransients_ResolvesToNothing()
    {
        var volume = Add("volume");
        volume.Raise(Transient("volume", ActivityPriority.High, 2));

        Assert.True(_manager.HasActivity);
        Assert.Null(_manager.Resolve(expanded: true));
    }

    [Fact]
    public void RaisesChangedEvent_ForAddReplaceAndExpiry()
    {
        var volume = Add("volume");

        volume.Raise(Transient("volume", ActivityPriority.High, 1));
        volume.Raise(Transient("volume", ActivityPriority.High, 1));
        _time.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(3, _changes);
    }

    [Fact]
    public void RaisesPeekRequested_WhenTheActivityAsksForIt()
    {
        var media = Add("media");
        IslandActivity? peeked = null;
        _manager.PeekRequested += (_, a) => peeked = a;

        media.Raise(Persistent("media"));
        Assert.Null(peeked);

        var withPeek = Persistent("media") with { RequestsPeek = true };
        media.Raise(withPeek);

        Assert.Same(withPeek, peeked);
    }

    [Fact]
    public async Task ModuleThatFailsToStart_IsDisabled_AndOthersStillStart()
    {
        var broken = Add("broken");
        broken.StartException = new InvalidOperationException("boom");
        var media = Add("media");

        await _manager.StartAsync(CancellationToken.None);

        Assert.True(media.Started);
        Assert.False(_manager.IsEnabled("broken"));
        Assert.True(_manager.IsEnabled("media"));
        var failure = Assert.Single(_failures);
        Assert.Equal("broken", failure.ModuleId);
        Assert.Equal(ModuleFailurePhase.Start, failure.Phase);

        // A disabled module's activities are ignored.
        broken.Raise(Persistent("broken"));
        Assert.False(_manager.HasActivity);
    }

    [Fact]
    public async Task DisablingAModule_RemovesItsActivity()
    {
        var broken = Add("broken");
        broken.RaiseDuringStart = Persistent("broken");
        broken.StartException = new InvalidOperationException("boom");

        await _manager.StartAsync(CancellationToken.None);

        Assert.False(_manager.HasActivity);
    }

    [Fact]
    public async Task StopFailures_AreReported_AndActivitiesCleared()
    {
        var a = Add("a");
        a.StopException = new InvalidOperationException("stop failed");
        var b = Add("b");
        await _manager.StartAsync(CancellationToken.None);
        a.Raise(Persistent("a"));

        await _manager.StopAsync();

        Assert.True(b.Stopped);
        Assert.False(_manager.HasActivity);
        Assert.Equal(ModuleFailurePhase.Stop, Assert.Single(_failures).Phase);
    }

    [Fact]
    public void RejectsTransientWithoutDuration()
    {
        var module = Add("volume");

        module.Raise(new IslandActivity("volume", ActivityPriority.High, ActivityKind.Transient, null, "vm", null));
        module.Raise(new IslandActivity("volume", ActivityPriority.High, ActivityKind.Transient, TimeSpan.Zero, "vm", null));

        Assert.False(_manager.HasActivity);
        Assert.Equal(2, _failures.Count);
        Assert.All(_failures, f => Assert.Equal(ModuleFailurePhase.Activity, f.Phase));
    }

    [Fact]
    public void RejectsActivityWithAnotherModulesId()
    {
        var module = Add("volume");

        module.Raise(Persistent("media"));

        Assert.False(_manager.HasActivity);
        Assert.Single(_failures);
    }

    [Fact]
    public void RejectsDuplicateModuleIds()
    {
        Add("media");

        Assert.Throws<InvalidOperationException>(() => Add("media"));
    }

    private sealed class FakeModule(string id) : IIslandModule
    {
        public event EventHandler<IslandActivity?>? ActivityChanged;

        public string Id { get; } = id;

        public string DisplayName => Id;

        public Exception? StartException { get; set; }

        public Exception? StopException { get; set; }

        public IslandActivity? RaiseDuringStart { get; set; }

        public bool Started { get; private set; }

        public bool Stopped { get; private set; }

        public void Raise(IslandActivity? activity) => ActivityChanged?.Invoke(this, activity);

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (RaiseDuringStart is not null)
            {
                Raise(RaiseDuringStart);
            }

            if (StartException is not null)
            {
                throw StartException;
            }

            Started = true;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            if (StopException is not null)
            {
                throw StopException;
            }

            Stopped = true;
            return Task.CompletedTask;
        }
    }
}
