using Microsoft.Extensions.Time.Testing;
using Pulau.Core.States;

namespace Pulau.Core.Tests.States;

/// <summary>One test (or more) per row of the transition table in PLAN.md 3.1.</summary>
public class IslandStateMachineTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly IslandStateMachine _machine;
    private readonly List<(IslandState Old, IslandState New)> _changes = [];

    public IslandStateMachineTests()
    {
        _machine = new IslandStateMachine(_time);
        _machine.StateChanged += (_, e) => _changes.Add((e.OldState, e.NewState));
    }

    private void Advance(int milliseconds) => _time.Advance(TimeSpan.FromMilliseconds(milliseconds));

    [Fact]
    public void StartsIdle()
    {
        Assert.Equal(IslandState.Idle, _machine.State);
    }

    // Idle | activity appears | Compact
    [Fact]
    public void Idle_ToCompact_WhenActivityAppears()
    {
        _machine.HasActivity = true;

        Assert.Equal(IslandState.Compact, _machine.State);
        Assert.Equal([(IslandState.Idle, IslandState.Compact)], _changes);
    }

    // Compact | all activities gone | Idle
    [Fact]
    public void Compact_ToIdle_WhenActivitiesAreGone()
    {
        _machine.HasActivity = true;

        _machine.HasActivity = false;

        Assert.Equal(IslandState.Idle, _machine.State);
    }

    // Idle / Compact | hover ≥ 150 ms | Expanded
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Expands_AfterHovering150Ms(bool hasActivity)
    {
        _machine.HasActivity = hasActivity;

        _machine.PointerEntered();
        Advance(149);
        Assert.NotEqual(IslandState.Expanded, _machine.State);

        Advance(1);
        Assert.Equal(IslandState.Expanded, _machine.State);
    }

    [Fact]
    public void ShortHover_DoesNotExpand()
    {
        _machine.HasActivity = true;

        _machine.PointerEntered();
        Advance(100);
        _machine.PointerExited();
        Advance(1000);

        Assert.Equal(IslandState.Compact, _machine.State);
    }

    // Idle / Compact | click | Expanded
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Click_ExpandsImmediately(bool hasActivity)
    {
        _machine.HasActivity = hasActivity;

        _machine.Click();

        Assert.Equal(IslandState.Expanded, _machine.State);
    }

    [Fact]
    public void Click_WhileExpanded_Collapses_AndHoverDoesNotReopenUntilPointerReturns()
    {
        _machine.HasActivity = true;
        _machine.PointerEntered();
        Advance(150);
        Assert.Equal(IslandState.Expanded, _machine.State);

        _machine.Click();
        Assert.Equal(IslandState.Compact, _machine.State);

        // Still hovering: stays collapsed.
        _machine.PointerEntered();
        Advance(1000);
        Assert.Equal(IslandState.Compact, _machine.State);

        // Leave and come back: hover works again.
        _machine.PointerExited();
        _machine.PointerEntered();
        Advance(150);
        Assert.Equal(IslandState.Expanded, _machine.State);
    }

    // Idle / Compact | hotkey | Expanded
    [Fact]
    public void ToggleExpanded_ExpandsAndCollapses_RegardlessOfPointer()
    {
        _machine.HasActivity = true;

        _machine.ToggleExpanded();
        Assert.Equal(IslandState.Expanded, _machine.State);

        Advance(10_000);
        Assert.Equal(IslandState.Expanded, _machine.State);

        _machine.ToggleExpanded();
        Assert.Equal(IslandState.Compact, _machine.State);
    }

    // Expanded | pointer away > 400 ms | Compact (with activity) / Idle
    [Theory]
    [InlineData(true, IslandState.Compact)]
    [InlineData(false, IslandState.Idle)]
    public void Collapses_WhenPointerIsAwayFor400Ms(bool hasActivity, IslandState expected)
    {
        _machine.HasActivity = hasActivity;
        _machine.PointerEntered();
        Advance(150);

        _machine.PointerExited();
        Advance(399);
        Assert.Equal(IslandState.Expanded, _machine.State);

        Advance(1);
        Assert.Equal(expected, _machine.State);
    }

    [Fact]
    public void PointerReturningWithin400Ms_KeepsItExpanded()
    {
        _machine.PointerEntered();
        Advance(150);

        _machine.PointerExited();
        Advance(300);
        _machine.PointerEntered();
        Advance(1000);

        Assert.Equal(IslandState.Expanded, _machine.State);
    }

    [Fact]
    public void ClickExpanded_CollapsesWhenPointerLeaves()
    {
        _machine.HasActivity = true;
        _machine.PointerEntered();
        _machine.Click();
        Assert.Equal(IslandState.Expanded, _machine.State);

        _machine.PointerExited();
        Advance(400);

        Assert.Equal(IslandState.Compact, _machine.State);
    }

    // Compact | track changed & auto-peek on | Expanded for 3 s → back
    [Fact]
    public void Peek_ExpandsForThreeSeconds_ThenReturnsToCompact()
    {
        _machine.HasActivity = true;

        _machine.Peek();
        Assert.Equal(IslandState.Expanded, _machine.State);

        Advance(2999);
        Assert.Equal(IslandState.Expanded, _machine.State);

        Advance(1);
        Assert.Equal(IslandState.Compact, _machine.State);
    }

    [Fact]
    public void Peek_IsIgnored_WhenAutoPeekIsOff()
    {
        _machine.HasActivity = true;
        _machine.AutoPeekEnabled = false;

        _machine.Peek();

        Assert.Equal(IslandState.Compact, _machine.State);
    }

    [Fact]
    public void Peek_IsIgnored_WhenIdle()
    {
        _machine.Peek();

        Assert.Equal(IslandState.Idle, _machine.State);
    }

    [Fact]
    public void Peek_DoesNotCollapseAnIslandTheUserIsHovering()
    {
        _machine.HasActivity = true;
        _machine.Peek();

        _machine.PointerEntered();
        Advance(5000);

        Assert.Equal(IslandState.Expanded, _machine.State);
    }

    // Any | fullscreen / user hides | Hidden
    [Theory]
    [InlineData(HideReasons.Fullscreen, IslandState.Idle)]
    [InlineData(HideReasons.Fullscreen, IslandState.Compact)]
    [InlineData(HideReasons.Fullscreen, IslandState.Expanded)]
    [InlineData(HideReasons.User, IslandState.Idle)]
    [InlineData(HideReasons.User, IslandState.Compact)]
    [InlineData(HideReasons.User, IslandState.Expanded)]
    public void AnyState_ToHidden(HideReasons reason, IslandState start)
    {
        _machine.HasActivity = start != IslandState.Idle;
        if (start == IslandState.Expanded)
        {
            _machine.Click();
        }

        Assert.Equal(start, _machine.State);

        _machine.SetHidden(reason, true);

        Assert.Equal(IslandState.Hidden, _machine.State);
    }

    // Hidden | condition gone / user shows | previous state
    [Theory]
    [InlineData(false, IslandState.Idle)]
    [InlineData(true, IslandState.Compact)]
    public void Hidden_ReturnsToPreviousState_WhenConditionClears(bool hasActivity, IslandState previous)
    {
        _machine.HasActivity = hasActivity;
        Assert.Equal(previous, _machine.State);

        _machine.SetHidden(HideReasons.Fullscreen, true);
        _machine.SetHidden(HideReasons.Fullscreen, false);

        Assert.Equal(previous, _machine.State);
    }

    [Fact]
    public void Hidden_ComesBackCollapsed_AfterBeingExpanded()
    {
        _machine.HasActivity = true;
        _machine.Click();

        _machine.SetHidden(HideReasons.User, true);
        _machine.SetHidden(HideReasons.User, false);

        Assert.Equal(IslandState.Compact, _machine.State);
    }

    [Fact]
    public void Hidden_StaysHidden_WhileAnyReasonRemains()
    {
        _machine.SetHidden(HideReasons.Fullscreen, true);
        _machine.SetHidden(HideReasons.User, true);

        _machine.SetHidden(HideReasons.Fullscreen, false);
        Assert.Equal(IslandState.Hidden, _machine.State);
        Assert.Equal(HideReasons.User, _machine.HiddenReasons);

        _machine.SetHidden(HideReasons.User, false);
        Assert.Equal(IslandState.Idle, _machine.State);
    }

    [Fact]
    public void Hidden_ReflectsActivityChangesOnReturn()
    {
        _machine.SetHidden(HideReasons.Fullscreen, true);
        _machine.HasActivity = true;
        Assert.Equal(IslandState.Hidden, _machine.State);

        _machine.SetHidden(HideReasons.Fullscreen, false);

        Assert.Equal(IslandState.Compact, _machine.State);
    }

    [Fact]
    public void Hidden_IgnoresHoverClickAndPeek()
    {
        _machine.HasActivity = true;
        _machine.SetHidden(HideReasons.User, true);

        _machine.PointerEntered();
        Advance(500);
        _machine.Click();
        _machine.ToggleExpanded();
        _machine.Peek();

        Assert.Equal(IslandState.Hidden, _machine.State);
    }

    [Fact]
    public void HoverInProgress_IsCancelledByHiding()
    {
        _machine.PointerEntered();
        Advance(100);
        _machine.SetHidden(HideReasons.Fullscreen, true);
        Advance(100);
        _machine.SetHidden(HideReasons.Fullscreen, false);

        Assert.Equal(IslandState.Idle, _machine.State);
    }

    [Fact]
    public void ActivityDisappearingWhileExpanded_StaysExpanded_ThenCollapsesToIdle()
    {
        _machine.HasActivity = true;
        _machine.PointerEntered();
        Advance(150);

        _machine.HasActivity = false;
        Assert.Equal(IslandState.Expanded, _machine.State);

        _machine.PointerExited();
        Advance(400);
        Assert.Equal(IslandState.Idle, _machine.State);
    }

    [Fact]
    public void RaisesStateChanged_OnlyOnRealChanges()
    {
        _machine.HasActivity = true;
        _machine.HasActivity = true;
        _machine.Click();
        _machine.Click();

        Assert.Equal(
            [
                (IslandState.Idle, IslandState.Compact),
                (IslandState.Compact, IslandState.Expanded),
                (IslandState.Expanded, IslandState.Compact),
            ],
            _changes);
    }

    [Fact]
    public void UsesCustomTimings()
    {
        var machine = new IslandStateMachine(_time, new IslandTimings { HoverDelay = TimeSpan.FromMilliseconds(500) });

        machine.PointerEntered();
        Advance(499);
        Assert.Equal(IslandState.Idle, machine.State);
        Advance(1);
        Assert.Equal(IslandState.Expanded, machine.State);
    }
}
