namespace Pulau.Core.Animation;

/// <summary>
/// A damped spring (unit mass) that pulls one value toward a target. It is integrated with
/// semi-implicit Euler in small fixed substeps, which is stable for the stiffness range the
/// island uses. Changing <see cref="Target"/> mid-flight keeps the current velocity, so a
/// retargeted animation continues smoothly instead of restarting.
/// </summary>
public sealed class Spring
{
    /// <summary>The largest frame time that is simulated; a longer gap (a stalled frame) is clamped to this.</summary>
    public const double MaxFrameSeconds = 1.0 / 30;

    private const double SubstepSeconds = 1.0 / 240;

    private double _stiffness;
    private double _dampingRatio;

    /// <param name="value">Start value; the spring starts at rest on it.</param>
    /// <param name="stiffness">Spring constant k (per second squared). Higher is faster.</param>
    /// <param name="dampingRatio">1 = critically damped (no overshoot); below 1 bounces a little.</param>
    /// <param name="restThreshold">
    /// Distance from the target below which the spring counts as settled (velocity must also be
    /// below ten times this, per second). Use the smallest change that is still visible.
    /// </param>
    public Spring(double value, double stiffness, double dampingRatio, double restThreshold)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(restThreshold);
        Stiffness = stiffness;
        DampingRatio = dampingRatio;
        RestThreshold = restThreshold;
        Value = value;
        Target = value;
    }

    public double Value { get; private set; }

    /// <summary>Current speed in value units per second.</summary>
    public double Velocity { get; private set; }

    /// <summary>The value the spring moves toward. Setting it keeps the current velocity.</summary>
    public double Target { get; set; }

    public double Stiffness
    {
        get => _stiffness;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _stiffness = value;
        }
    }

    public double DampingRatio
    {
        get => _dampingRatio;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _dampingRatio = value;
        }
    }

    public double RestThreshold { get; }

    public bool IsAtRest =>
        Math.Abs(Value - Target) < RestThreshold && Math.Abs(Velocity) < RestThreshold * 10;

    /// <summary>
    /// Advances the simulation by <paramref name="deltaSeconds"/>, clamped to
    /// <see cref="MaxFrameSeconds"/>. Once settled, the value snaps exactly onto the target.
    /// </summary>
    public void Step(double deltaSeconds)
    {
        if (IsAtRest)
        {
            SnapToTarget();
            return;
        }

        if (deltaSeconds <= 0 || double.IsNaN(deltaSeconds))
        {
            return;
        }

        var remaining = Math.Min(deltaSeconds, MaxFrameSeconds);
        var damping = 2 * DampingRatio * Math.Sqrt(Stiffness);
        while (remaining > 0)
        {
            var h = Math.Min(SubstepSeconds, remaining);
            var acceleration = (-Stiffness * (Value - Target)) - (damping * Velocity);
            Velocity += acceleration * h;
            Value += Velocity * h;
            remaining -= h;
        }

        if (IsAtRest)
        {
            SnapToTarget();
        }
    }

    /// <summary>Jumps to the target and stops.</summary>
    public void SnapToTarget()
    {
        Value = Target;
        Velocity = 0;
    }
}
