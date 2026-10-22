using Pulau.Core.Timing;

namespace Pulau.Core.States;

/// <summary>
/// Decides whether the island is hidden, idle, compact or expanded (PLAN.md 3.1).
/// <list type="bullet">
/// <item>Idle ⇄ Compact follows whether there is any activity.</item>
/// <item>Hovering for <see cref="IslandTimings.HoverDelay"/>, a click or <see cref="ToggleExpanded"/> expands;
/// the pointer being away for <see cref="IslandTimings.LeaveDelay"/> collapses back.</item>
/// <item><see cref="Peek"/> expands a compact island for <see cref="IslandTimings.PeekDuration"/>.</item>
/// <item>Any hide reason (fullscreen, user) hides the island; when the last one clears, it returns to
/// Idle or Compact, whichever matches the activities at that moment.</item>
/// </list>
/// Not thread-safe: call it from one thread, with a <see cref="TimeProvider"/> whose timers call back on that thread.
/// </summary>
public sealed class IslandStateMachine : IDisposable
{
    private readonly IslandTimings _timings;
    private readonly OneShotTimer _hoverTimer;
    private readonly OneShotTimer _leaveTimer;
    private readonly OneShotTimer _peekTimer;

    private bool _hasActivity;
    private bool _pointerInside;
    private bool _hoverExpanded;
    private bool _pinned;
    private bool _peeking;

    // After a click collapses the island under the pointer, hovering must not re-open it
    // until the pointer has left and come back.
    private bool _hoverSuppressed;

    public IslandStateMachine(TimeProvider time)
        : this(time, IslandTimings.Default)
    {
    }

    public IslandStateMachine(TimeProvider time, IslandTimings timings)
    {
        _timings = timings;
        _hoverTimer = new OneShotTimer(time, OnHoverElapsed);
        _leaveTimer = new OneShotTimer(time, OnLeaveElapsed);
        _peekTimer = new OneShotTimer(time, OnPeekElapsed);
    }

    public event EventHandler<IslandStateChangedEventArgs>? StateChanged;

    public IslandState State { get; private set; } = IslandState.Idle;

    public HideReasons HiddenReasons { get; private set; }

    /// <summary>Whether <see cref="Peek"/> may expand the island (setting "auto-peek on track change").</summary>
    public bool AutoPeekEnabled { get; set; } = true;

    public bool HasActivity
    {
        get => _hasActivity;
        set
        {
            _hasActivity = value;
            Update();
        }
    }

    public void PointerEntered()
    {
        _pointerInside = true;
        _leaveTimer.Cancel();

        if (HiddenReasons != HideReasons.None || _hoverSuppressed)
        {
            return;
        }

        if (State == IslandState.Expanded)
        {
            // Already open (clicked, or peeking): keep it open for as long as the pointer stays.
            _hoverExpanded = true;
            return;
        }

        _hoverTimer.Start(_timings.HoverDelay);
    }

    public void PointerExited()
    {
        _pointerInside = false;
        _hoverSuppressed = false;
        _hoverTimer.Cancel();

        if (State == IslandState.Expanded)
        {
            _leaveTimer.Start(_timings.LeaveDelay);
        }
    }

    /// <summary>A click on the pill: expands it, or collapses it when it is already expanded.</summary>
    public void Click()
    {
        _hoverTimer.Cancel();
        if (HiddenReasons != HideReasons.None)
        {
            return;
        }

        if (State == IslandState.Expanded)
        {
            Collapse();
            _hoverSuppressed = _pointerInside;
        }
        else
        {
            _pinned = true;
        }

        Update();
    }

    /// <summary>Hotkey: expands or collapses regardless of where the pointer is.</summary>
    public void ToggleExpanded()
    {
        _hoverTimer.Cancel();
        if (HiddenReasons != HideReasons.None)
        {
            return;
        }

        if (State == IslandState.Expanded)
        {
            Collapse();
        }
        else
        {
            _pinned = true;
        }

        Update();
    }

    /// <summary>
    /// The user acted on what the island shows (e.g. clicked it to open the app): collapse if
    /// expanded, and don't expand again under the pointer until it leaves and comes back.
    /// </summary>
    public void Dismiss()
    {
        _hoverTimer.Cancel();
        if (HiddenReasons != HideReasons.None)
        {
            return;
        }

        if (State == IslandState.Expanded)
        {
            Collapse();
        }

        _hoverSuppressed = _pointerInside;
        Update();
    }

    /// <summary>
    /// Briefly expands a compact island (e.g. the track changed). Ignored unless the island is
    /// compact and <see cref="AutoPeekEnabled"/> is on.
    /// </summary>
    public void Peek()
    {
        if (!AutoPeekEnabled || State != IslandState.Compact)
        {
            return;
        }

        _peeking = true;
        _peekTimer.Start(_timings.PeekDuration);
        Update();
    }

    public void SetHidden(HideReasons reason, bool hidden)
    {
        var wasHidden = HiddenReasons != HideReasons.None;
        HiddenReasons = hidden ? HiddenReasons | reason : HiddenReasons & ~reason;

        if (!wasHidden && HiddenReasons != HideReasons.None)
        {
            // Expansion is transient; after being hidden the island comes back collapsed.
            Collapse();
            _hoverTimer.Cancel();
        }

        Update();
    }

    public void Dispose()
    {
        _hoverTimer.Dispose();
        _leaveTimer.Dispose();
        _peekTimer.Dispose();
    }

    private void OnHoverElapsed()
    {
        if (_pointerInside && HiddenReasons == HideReasons.None && !_hoverSuppressed)
        {
            _hoverExpanded = true;
            Update();
        }
    }

    private void OnLeaveElapsed()
    {
        if (!_pointerInside)
        {
            Collapse();
            Update();
        }
    }

    private void OnPeekElapsed()
    {
        _peeking = false;
        Update();
    }

    private void Collapse()
    {
        _hoverExpanded = false;
        _pinned = false;
        _peeking = false;
        _peekTimer.Cancel();
        _leaveTimer.Cancel();
    }

    private void Update()
    {
        var next =
            HiddenReasons != HideReasons.None ? IslandState.Hidden :
            _hoverExpanded || _pinned || _peeking ? IslandState.Expanded :
            _hasActivity ? IslandState.Compact :
            IslandState.Idle;

        if (next == State)
        {
            return;
        }

        var old = State;
        State = next;
        StateChanged?.Invoke(this, new IslandStateChangedEventArgs(old, next));
    }
}
