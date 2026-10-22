using Pulau.Core.Activities;
using Pulau.Core.Layout;
using Pulau.Core.States;

namespace Pulau.ViewModels;

/// <summary>What the pill should look like right now: its state, size and content.</summary>
public readonly record struct IslandPresentation(IslandState State, IslandSize Size, object? Content);

/// <summary>
/// Glue between the activity manager, the state machine and the window: works out which content
/// and size the pill should have, and forwards pointer input to the state machine.
/// </summary>
public sealed class IslandViewModel
{
    private readonly ActivityManager _activities;
    private readonly IslandStateMachine _states;
    private readonly HomeViewModel _home;
    private IIslandContent? _visibleContent;

    public IslandViewModel(ActivityManager activities, IslandStateMachine states, HomeViewModel home)
    {
        _activities = activities;
        _states = states;
        _home = home;
        Presentation = new IslandPresentation(IslandState.Idle, IslandSizes.Idle, null);

        _activities.ActivitiesChanged += OnActivitiesChanged;
        _activities.PeekRequested += OnPeekRequested;
        _states.StateChanged += (_, _) => Refresh();

        _states.HasActivity = _activities.HasActivity;
        Refresh();
    }

    public event EventHandler? PresentationChanged;

    public IslandPresentation Presentation { get; private set; }

    public bool IsUserHidden => (_states.HiddenReasons & HideReasons.User) != 0;

    public void PointerEntered() => _states.PointerEntered();

    public void PointerExited() => _states.PointerExited();

    public void Click() => _states.Click();

    public void ToggleExpanded() => _states.ToggleExpanded();

    public void SetUserHidden(bool hidden) => _states.SetHidden(HideReasons.User, hidden);

    private void OnActivitiesChanged(object? sender, EventArgs e)
    {
        _states.HasActivity = _activities.HasActivity;
        Refresh();
    }

    private void OnPeekRequested(object? sender, IslandActivity activity)
    {
        // Only peek for the activity the compact pill is actually showing.
        if (ReferenceEquals(_activities.Resolve(expanded: false), activity))
        {
            _states.Peek();
        }
    }

    private void Refresh()
    {
        var next = Compute();
        UpdateVisibleContent(next.State == IslandState.Hidden ? null : next.Content);
        if (next == Presentation)
        {
            return;
        }

        Presentation = next;
        PresentationChanged?.Invoke(this, EventArgs.Empty);
    }

    private IslandPresentation Compute()
    {
        switch (_states.State)
        {
            case IslandState.Hidden:
                // Keep the last shape and content while fading out.
                return Presentation with { State = IslandState.Hidden };

            case IslandState.Expanded:
                var expanded = _activities.Resolve(expanded: true);
                return expanded?.ExpandedViewModel is { } expandedContent
                    ? new IslandPresentation(IslandState.Expanded, expanded.ExpandedSize, expandedContent)
                    : new IslandPresentation(IslandState.Expanded, IslandSizes.Home, _home);

            case IslandState.Compact when _activities.Resolve(expanded: false) is { } compact:
                return new IslandPresentation(IslandState.Compact, compact.CompactSize, compact.CompactViewModel);

            default:
                return new IslandPresentation(IslandState.Idle, IslandSizes.Idle, null);
        }
    }

    /// <summary>Tells content view models when they go on and off screen.</summary>
    private void UpdateVisibleContent(object? content)
    {
        var next = content as IIslandContent;
        if (ReferenceEquals(next, _visibleContent))
        {
            return;
        }

        _visibleContent?.OnHidden();
        _visibleContent = next;
        _visibleContent?.OnShown();
    }
}
