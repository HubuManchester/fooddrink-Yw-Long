using WhatToEat.ViewModels;

namespace WhatToEat.Views;

/// <summary>
/// Code-behind for HomePage.
/// Handles the spin-wheel animation and accelerometer-based shake-to-spin feature.
///
/// Hardware features in this page:
///   • Haptic Feedback  — fires on wheel stop (LongPress haptic).
///   • Accelerometer    — shake detection triggers SpinAsync via ViewModel.
///
/// Accelerometer lifecycle:
///   Start on OnAppearing  → active only while user is on this page.
///   Stop  on OnDisappearing → no background drain on other pages.
///
/// Fix notes (v2):
///   • Removed ScrollToSelected() — it was the root cause of the selected card
///     "jumping to the far right". MAUI's CollectionView ScrollTo with
///     ScrollToPosition.Center on a horizontal list can miscalculate item
///     positions when a DataTrigger simultaneously resizes the card border,
///     causing an overshoot to the last item.
///     The selection state is now communicated purely through the IsSelected
///     binding + two separate Border overlays in XAML (selected / unselected).
///     This is visually equivalent and avoids any programmatic scroll conflict.
///
///   • Pull-to-refresh is handled by RefreshView in XAML bound to
///     HomeViewModel.RefreshCommand / IsRefreshing — no code-behind needed.
/// </summary>
public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _vm;
    private bool _isSpinning = false;
    private double _totalRotation = 0;

    // ── Shake / Accelerometer ─────────────────────────────────────────────
    private const double ShakeThreshold = 2.5;
    private DateTime _lastShakeTime = DateTime.MinValue;
    private const int ShakeCooldownMs = 2000;

    // ── Wheel segment colours ─────────────────────────────────────────────
    private static readonly Color[] SegmentColors =
    {
        Color.FromArgb("#E07B39"),
        Color.FromArgb("#C4773B"),
        Color.FromArgb("#F4A460"),
        Color.FromArgb("#D4956A"),
        Color.FromArgb("#B8622A"),
        Color.FromArgb("#E8903C"),
        Color.FromArgb("#CC8040"),
        Color.FromArgb("#F0C080"),
    };

    public HomePage()
    {
        InitializeComponent();
        _vm = new HomeViewModel();
        BindingContext = _vm;

        WheelCanvas.Drawable = new WheelDrawable(
            HomeViewModel.Categories, SegmentColors);

        _vm.SpinRequested += OnSpinRequested;

        // ── FIX: No longer subscribing to PropertyChanged for scroll.
        //    Removed ScrollToSelected entirely — it caused the jump-to-end bug.
        //    The XAML uses two separate Border elements (selected/unselected)
        //    instead of a DataTrigger that mutated StrokeThickness, which was
        //    also contributing to layout recalculation during scroll.
    }

    // ── Page lifecycle ────────────────────────────────────────────────────

    protected override void OnAppearing()
    {
        base.OnAppearing();
        StartAccelerometer();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopAccelerometer();
        _vm.SpinRequested -= OnSpinRequested;
    }

    // ── Accelerometer / Shake ─────────────────────────────────────────────

    private void StartAccelerometer()
    {
        try
        {
            if (!Accelerometer.Default.IsSupported || Accelerometer.Default.IsMonitoring)
                return;

            Accelerometer.Default.ReadingChanged += OnAccelerometerReadingChanged;
            Accelerometer.Default.Start(SensorSpeed.Game);
        }
        catch (FeatureNotSupportedException)
        {
            Console.WriteLine("[HomePage] Accelerometer not supported on this platform.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HomePage] Accelerometer start error: {ex.Message}");
        }
    }

    private void StopAccelerometer()
    {
        try
        {
            if (!Accelerometer.Default.IsMonitoring) return;

            Accelerometer.Default.Stop();
            Accelerometer.Default.ReadingChanged -= OnAccelerometerReadingChanged;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HomePage] Accelerometer stop error: {ex.Message}");
        }
    }

    private void OnAccelerometerReadingChanged(object? sender, AccelerometerChangedEventArgs e)
    {
        var acc = e.Reading.Acceleration;
        double magnitude = Math.Sqrt(acc.X * acc.X + acc.Y * acc.Y + acc.Z * acc.Z);

        bool cooldownElapsed =
            (DateTime.UtcNow - _lastShakeTime).TotalMilliseconds > ShakeCooldownMs;

        if (magnitude > ShakeThreshold && cooldownElapsed && !_isSpinning)
        {
            _lastShakeTime = DateTime.UtcNow;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (_vm.SpinCommand.CanExecute(null))
                    _vm.SpinCommand.Execute(null);
            });
        }
    }

    // ── Wheel animation ───────────────────────────────────────────────────

    private async void OnSpinRequested(object? sender, int targetIndex)
    {
        if (_isSpinning) return;
        _isSpinning = true;

        int count = HomeViewModel.Categories.Length;
        double segmentAngle = 360.0 / count;

        double currentMod = _totalRotation % 360.0;
        if (currentMod < 0) currentMod += 360.0;

        double targetMod = (90.0 - targetIndex * segmentAngle
                            - segmentAngle / 2.0
                            - 2 * segmentAngle + 3600.0) % 360.0;
        double needed = (targetMod - currentMod + 360.0) % 360.0;
        if (needed < 180.0) needed += 360.0;

        int extraRounds = 3 + new Random().Next(3);
        double spinAmount = extraRounds * 360.0 + needed;
        _totalRotation += spinAmount;

        await WheelCanvas.RotateTo(_totalRotation, 2200, Easing.CubicOut);

        HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);

        await ResultFrame.ScaleTo(1.12, 110, Easing.CubicOut);
        await ResultFrame.ScaleTo(1.0, 110, Easing.CubicIn);

        _isSpinning = false;
    }
}

// ── WheelDrawable ─────────────────────────────────────────────────────────────

public class WheelDrawable : IDrawable
{
    private readonly (string Display, string Emoji, string SearchKey)[] _categories;
    private readonly Color[] _colors;

    public WheelDrawable(
        (string Display, string Emoji, string SearchKey)[] categories,
        Color[] colors)
    {
        _categories = categories;
        _colors = colors;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float cx = dirtyRect.X + dirtyRect.Width / 2f;
        float cy = dirtyRect.Y + dirtyRect.Height / 2f;
        float radius = Math.Min(dirtyRect.Width, dirtyRect.Height) / 2f - 2f;

        int count = _categories.Length;
        double sweep = 2.0 * Math.PI / count;
        double sweepDeg = 360.0 / count;

        for (int i = 0; i < count; i++)
        {
            double startRad = -Math.PI / 2.0 + i * sweep;

            // Fill segment
            var path = new PathF();
            path.MoveTo(cx, cy);
            const int steps = 36;
            for (int s = 0; s <= steps; s++)
            {
                double a = startRad + sweep * s / steps;
                path.LineTo(cx + (float)(radius * Math.Cos(a)),
                            cy + (float)(radius * Math.Sin(a)));
            }
            path.Close();

            canvas.FillColor = _colors[i % _colors.Length];
            canvas.FillPath(path);

            // Dividing line
            canvas.StrokeColor = Colors.White.WithAlpha(0.7f);
            canvas.StrokeSize = 1.5f;
            canvas.DrawLine(cx, cy,
                cx + (float)(radius * Math.Cos(startRad)),
                cy + (float)(radius * Math.Sin(startRad)));

            // Emoji + label
            double midDeg = -90.0 + i * sweepDeg + sweepDeg / 2.0;
            float labelR = radius * 0.60f;

            canvas.SaveState();
            canvas.Translate(cx, cy);
            canvas.Rotate((float)(midDeg + 90));

            canvas.FontSize = 20f;
            canvas.FontColor = Colors.White;
            canvas.DrawString(
                _categories[i].Emoji,
                -18f, -(labelR + 14f),
                36f, 26f,
                HorizontalAlignment.Center,
                VerticalAlignment.Center);

            canvas.FontSize = 8.5f;
            canvas.FontColor = Colors.White.WithAlpha(0.92f);
            canvas.DrawString(
                _categories[i].Display,
                -28f, -(labelR - 14f),
                56f, 14f,
                HorizontalAlignment.Center,
                VerticalAlignment.Center);

            canvas.RestoreState();
        }

        // Outer border ring
        canvas.StrokeColor = Colors.White.WithAlpha(0.6f);
        canvas.StrokeSize = 3f;
        canvas.DrawEllipse(cx - radius, cy - radius, radius * 2f, radius * 2f);
    }
}