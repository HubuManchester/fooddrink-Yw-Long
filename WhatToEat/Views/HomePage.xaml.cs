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
/// </summary>
public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _vm;
    private bool _isSpinning = false;
    private double _totalRotation = 0;

    // ── Shake / Accelerometer ─────────────────────────────────────────────
    // Total-acceleration threshold in G-force units.
    // 1 G = gravity at rest. 2.5 G is a deliberate shake, not normal movement.
    private const double ShakeThreshold = 2.5;
    private DateTime _lastShakeTime = DateTime.MinValue;
    private const int ShakeCooldownMs = 2000; // prevent rapid re-triggering

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

        // Attach the wheel drawable (custom IDrawable)
        WheelCanvas.Drawable = new WheelDrawable(
            HomeViewModel.Categories, SegmentColors);

        // Subscribe to ViewModel events
        _vm.SpinRequested += OnSpinRequested;

        _vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(HomeViewModel.SelectedMeal))
                ScrollToSelected();
        };
    }

    // ── Page lifecycle — Accelerometer management ─────────────────────────

    /// <summary>
    /// Start the accelerometer each time the page becomes visible.
    /// Wraps in try-catch so Windows (no accelerometer) continues gracefully.
    /// </summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        StartAccelerometer();
    }

    /// <summary>
    /// Stop the accelerometer and unsubscribe events when leaving the page.
    /// Prevents battery drain and ghost-shakes triggering on other tabs.
    /// </summary>
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopAccelerometer();
        _vm.SpinRequested -= OnSpinRequested;
    }

    // ── Accelerometer / Shake ─────────────────────────────────────────────

    /// <summary>
    /// Starts monitoring the accelerometer sensor.
    /// SensorSpeed.Game gives ~60 Hz updates — responsive for shake detection.
    /// Falls back silently on platforms that don't support it (e.g. Windows).
    /// </summary>
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
            // Sensor absent (Windows desktop etc.) — fail silently, no crash
            Console.WriteLine("[HomePage] Accelerometer not supported on this platform.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HomePage] Accelerometer start error: {ex.Message}");
        }
    }

    /// <summary>
    /// Stops the accelerometer sensor and removes the event handler.
    /// </summary>
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

    /// <summary>
    /// Fires on every accelerometer reading (~60 Hz).
    /// Computes the vector magnitude; if it exceeds ShakeThreshold and the
    /// cooldown has elapsed, triggers SpinAsync on the UI thread.
    ///
    /// Magnitude formula: √(x² + y² + z²)
    /// At rest this equals ~1 G (gravity). A firm shake peaks above 2.5 G.
    /// </summary>
    private void OnAccelerometerReadingChanged(object? sender, AccelerometerChangedEventArgs e)
    {
        var acc = e.Reading.Acceleration;
        double magnitude = Math.Sqrt(acc.X * acc.X + acc.Y * acc.Y + acc.Z * acc.Z);

        bool cooldownElapsed =
            (DateTime.UtcNow - _lastShakeTime).TotalMilliseconds > ShakeCooldownMs;

        if (magnitude > ShakeThreshold && cooldownElapsed && !_isSpinning)
        {
            _lastShakeTime = DateTime.UtcNow;

            // Accelerometer fires on a background thread — marshal back to UI thread
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                // Trigger the same spin event the button uses
                // The ViewModel picks a random category and raises SpinRequested
                if (_vm.SpinCommand.CanExecute(null))
                    _vm.SpinCommand.Execute(null);
            });
        }
    }

    // ── Wheel animation ───────────────────────────────────────────────────

    /// <summary>
    /// Animates the wheel to the winning segment index.
    /// Called by both the Spin button (via ViewModel event) and shake-to-spin.
    /// Adds haptic feedback on landing — hardware feature.
    /// </summary>
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

        // Animate wheel rotation
        await WheelCanvas.RotateTo(_totalRotation, 2200, Easing.CubicOut);

        // Haptic feedback on landing — hardware feature (tactile confirmation)
        HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);

        // Bounce the result label for visual feedback
        await ResultFrame.ScaleTo(1.1, 120, Easing.CubicOut);
        await ResultFrame.ScaleTo(1.0, 120, Easing.CubicIn);

        _isSpinning = false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Scrolls the meal card list to keep the selected meal centred.
    /// </summary>
    private void ScrollToSelected()
    {
        if (_vm.SelectedMeal == null) return;
        var idx = _vm.RecommendedMeals.IndexOf(_vm.SelectedMeal);
        if (idx >= 0)
            MealCards.ScrollTo(idx,
                position: ScrollToPosition.Center,
                animate: true);
    }
}

// ── WheelDrawable — Custom IDrawable for the spin wheel ──────────────────────

/// <summary>
/// Draws the food-category spin wheel using MAUI's GraphicsView / IDrawable API.
/// Renders coloured segments, dividing lines, emoji icons, and category labels.
/// Rotated by the page's RotateTo animation — no drawing state needed here.
/// </summary>
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

            // ── 1. Fill segment ───────────────────────────────────────────
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

            // ── 2. Dividing line ──────────────────────────────────────────
            canvas.StrokeColor = Colors.White;
            canvas.StrokeSize = 2f;
            canvas.DrawLine(cx, cy,
                cx + (float)(radius * Math.Cos(startRad)),
                cy + (float)(radius * Math.Sin(startRad)));

            // ── 3. Emoji + label along segment midline ────────────────────
            double midDeg = -90.0 + i * sweepDeg + sweepDeg / 2.0;
            float labelR = radius * 0.60f;

            canvas.SaveState();
            canvas.Translate(cx, cy);
            canvas.Rotate((float)(midDeg + 90));

            // Emoji (large)
            canvas.FontSize = 20f;
            canvas.FontColor = Colors.White;
            canvas.DrawString(
                _categories[i].Emoji,
                -18f, -(labelR + 14f),
                36f, 26f,
                HorizontalAlignment.Center,
                VerticalAlignment.Center);

            // Category name (small)
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

        // ── 4. Outer border ring ──────────────────────────────────────────
        canvas.StrokeColor = Colors.White;
        canvas.StrokeSize = 4f;
        canvas.DrawEllipse(cx - radius, cy - radius, radius * 2f, radius * 2f);
    }
}