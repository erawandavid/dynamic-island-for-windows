using Pulau.Core.Animation;

namespace Pulau.Core.Tests.Animation;

public class SpringTests
{
    private const double Frame = 1.0 / 60;

    private static Spring CreateSpring(double value = 0, double dampingRatio = 0.8) =>
        new(value, stiffness: 300, dampingRatio, restThreshold: 0.01);

    private static void Run(Spring spring, double seconds, double frame = Frame)
    {
        for (var t = 0.0; t < seconds; t += frame)
        {
            spring.Step(frame);
        }
    }

    [Fact]
    public void ConvergesToTarget()
    {
        var spring = CreateSpring();
        spring.Target = 100;

        Run(spring, 0.15);
        Assert.InRange(spring.Value, 1, 99.9);

        Run(spring, 2);
        Assert.Equal(100, spring.Value, precision: 6);
    }

    [Fact]
    public void Settles_AndSnapsExactlyOntoTarget()
    {
        var spring = CreateSpring();
        spring.Target = 240;

        Run(spring, 3);

        Assert.True(spring.IsAtRest);
        Assert.Equal(240, spring.Value);
        Assert.Equal(0, spring.Velocity);
    }

    [Fact]
    public void StartsAtRest()
    {
        var spring = CreateSpring(value: 120);

        Assert.True(spring.IsAtRest);
        spring.Step(Frame);
        Assert.Equal(120, spring.Value);
    }

    [Fact]
    public void Retarget_KeepsVelocity()
    {
        var spring = CreateSpring();
        spring.Target = 100;
        Run(spring, 0.1);
        var velocity = spring.Velocity;
        var value = spring.Value;
        Assert.True(velocity > 0);

        spring.Target = 200;

        Assert.Equal(velocity, spring.Velocity);
        Assert.Equal(value, spring.Value);

        // It keeps moving in the same direction rather than restarting from zero speed.
        spring.Step(Frame);
        Assert.True(spring.Velocity > velocity);
    }

    [Fact]
    public void ClampsLongFrames()
    {
        var clamped = CreateSpring();
        var reference = CreateSpring();
        clamped.Target = reference.Target = 100;

        clamped.Step(5.0);
        reference.Step(Spring.MaxFrameSeconds);

        Assert.Equal(reference.Value, clamped.Value, precision: 10);
        Assert.Equal(reference.Velocity, clamped.Velocity, precision: 10);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    [InlineData(double.NaN)]
    public void IgnoresNonPositiveFrameTimes(double delta)
    {
        var spring = CreateSpring();
        spring.Target = 100;

        spring.Step(delta);

        Assert.Equal(0, spring.Value);
        Assert.Equal(0, spring.Velocity);
    }

    [Fact]
    public void Underdamped_OvershootsSlightly()
    {
        var spring = CreateSpring(dampingRatio: 0.8);
        spring.Target = 100;
        var max = 0.0;

        for (var i = 0; i < 180; i++)
        {
            spring.Step(Frame);
            max = Math.Max(max, spring.Value);
        }

        // ζ = 0.8 overshoots by about 1.5%.
        Assert.InRange(max, 100.5, 103);
    }

    [Fact]
    public void CriticallyDamped_DoesNotOvershoot()
    {
        var spring = CreateSpring(dampingRatio: 1.0);
        spring.Target = 100;

        for (var i = 0; i < 180; i++)
        {
            spring.Step(Frame);
            Assert.True(spring.Value <= 100 + 1e-9, $"overshot to {spring.Value}");
        }
    }

    [Fact]
    public void SnapToTarget_StopsImmediately()
    {
        var spring = CreateSpring();
        spring.Target = 50;
        spring.Step(Frame);

        spring.SnapToTarget();

        Assert.Equal(50, spring.Value);
        Assert.Equal(0, spring.Velocity);
        Assert.True(spring.IsAtRest);
    }

    [Fact]
    public void RejectsInvalidParameters()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Spring(0, stiffness: 0, dampingRatio: 0.8, restThreshold: 0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Spring(0, stiffness: 300, dampingRatio: -1, restThreshold: 0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Spring(0, stiffness: 300, dampingRatio: 0.8, restThreshold: 0));
    }
}
