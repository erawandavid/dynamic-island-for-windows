using System.Globalization;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Pulau.Core.Activities;
using Serilog;
using Windows.System.Power;

namespace Pulau.ViewModels;

/// <summary>
/// The expanded panel when nothing else is going on: clock, date (in the system's format) and,
/// on laptops, the battery. It only updates while it is on screen.
/// </summary>
public sealed class HomeViewModel : ObservableObject, IIslandContent
{
    /// <summary>Width of the battery glyph's inside, in DIPs (see HomeView.xaml).</summary>
    private const double BatteryFillMaxWidth = 18;

    private static readonly ILogger Logger = Log.ForContext<HomeViewModel>();
    private static readonly Brush NormalBattery = Freeze(new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF2)));
    private static readonly Brush ChargingBattery = Freeze(new SolidColorBrush(Color.FromRgb(0x34, 0xC7, 0x59)));
    private static readonly Brush LowBattery = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x3A)));

    private readonly TimeProvider _clock;
    private readonly DispatcherTimer _timer;

    private string _timeText = string.Empty;
    private string _dateText = string.Empty;
    private bool _hasBattery;
    private string _batteryText = string.Empty;
    private string _batteryStatusText = string.Empty;
    private double _batteryFillWidth;
    private Brush _batteryFillBrush = NormalBattery;

    public HomeViewModel(TimeProvider clock)
    {
        _clock = clock;
        _timer = new DispatcherTimer(DispatcherPriority.Background);
        _timer.Tick += (_, _) =>
        {
            Refresh();
            ScheduleNextMinute();
        };
    }

    public string TimeText
    {
        get => _timeText;
        private set => SetProperty(ref _timeText, value);
    }

    public string DateText
    {
        get => _dateText;
        private set => SetProperty(ref _dateText, value);
    }

    public bool HasBattery
    {
        get => _hasBattery;
        private set => SetProperty(ref _hasBattery, value);
    }

    public string BatteryText
    {
        get => _batteryText;
        private set => SetProperty(ref _batteryText, value);
    }

    public string BatteryStatusText
    {
        get => _batteryStatusText;
        private set => SetProperty(ref _batteryStatusText, value);
    }

    public double BatteryFillWidth
    {
        get => _batteryFillWidth;
        private set => SetProperty(ref _batteryFillWidth, value);
    }

    public Brush BatteryFillBrush
    {
        get => _batteryFillBrush;
        private set => SetProperty(ref _batteryFillBrush, value);
    }

    public void OnShown()
    {
        Refresh();
        ScheduleNextMinute();
    }

    public void OnHidden() => _timer.Stop();

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }

    private void ScheduleNextMinute()
    {
        var now = _clock.GetLocalNow();
        var untilNextMinute = TimeSpan.FromSeconds(60 - now.Second) - TimeSpan.FromMilliseconds(now.Millisecond);

        // Land just after the minute turns over.
        _timer.Interval = untilNextMinute + TimeSpan.FromMilliseconds(50);
        _timer.Start();
    }

    private void Refresh()
    {
        var now = _clock.GetLocalNow();
        var culture = CultureInfo.CurrentCulture;
        TimeText = now.ToString("t", culture);

        var day = now.ToString("dddd", culture);
        var monthDay = now.ToString(culture.DateTimeFormat.MonthDayPattern, culture);
        DateText = day.Length > 0 ? $"{char.ToUpper(day[0], culture)}{day[1..]}, {monthDay}" : monthDay;

        RefreshBattery();
    }

    private void RefreshBattery()
    {
        try
        {
            var status = PowerManager.BatteryStatus;
            HasBattery = status != BatteryStatus.NotPresent;
            if (!HasBattery)
            {
                return;
            }

            var percent = Math.Clamp(PowerManager.RemainingChargePercent, 0, 100);
            var pluggedIn = PowerManager.PowerSupplyStatus != PowerSupplyStatus.NotPresent;
            var charging = status == BatteryStatus.Charging;

            BatteryText = string.Create(CultureInfo.CurrentCulture, $"{percent}%");
            BatteryStatusText = charging ? "Charging" : pluggedIn ? "Plugged in" : "On battery";
            BatteryFillWidth = BatteryFillMaxWidth * percent / 100;
            BatteryFillBrush = charging || pluggedIn ? ChargingBattery : percent <= 20 ? LowBattery : NormalBattery;
        }
        catch (Exception ex)
        {
            HasBattery = false;
            Logger.Warning(ex, "Could not read the battery state");
        }
    }
}
