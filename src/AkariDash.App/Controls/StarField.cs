using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace AkariDash.App.Controls;

/// <summary>
/// The galaxy behind every page, after Vain Toolbox's: small white stars drifting slowly down and
/// twinkling, in every theme: white on dark themes, black on Light. It moves only while it is
/// loaded and visible.
/// </summary>
public sealed class StarField : Canvas
{
    private static readonly SolidColorBrush DarkThemeStarBrush = new(Colors.White);
    private static readonly SolidColorBrush LightThemeStarBrush = new(Colors.Black);

    private readonly List<Star> _stars = [];
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private readonly Random _random = new();
    private double _elapsedSeconds;

    public StarField()
    {
        IsHitTestVisible = false;
        _timer.Tick += (_, _) => Step();
        Loaded += (_, _) => UpdateRunning();
        Unloaded += (_, _) => _timer.Stop();
        SizeChanged += (_, _) => Scatter();
        ActualThemeChanged += (_, _) => Recolor();
        RegisterPropertyChangedCallback(VisibilityProperty, (_, _) => UpdateRunning());
    }

    public int StarCount { get; set; } = 120;

    private bool IsLight => ActualTheme == ElementTheme.Light;

    private Brush StarBrush => IsLight ? LightThemeStarBrush : DarkThemeStarBrush;

    private void Recolor()
    {
        foreach (var star in _stars)
        {
            star.Element.Fill = StarBrush;
        }
    }

    private void UpdateRunning()
    {
        if (Visibility == Visibility.Visible && IsLoaded)
        {
            if (_stars.Count == 0)
            {
                Scatter();
            }

            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    // Spreads the stars over the whole field again, as when the window is resized.
    private void Scatter()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        Children.Clear();
        _stars.Clear();

        for (var i = 0; i < StarCount; i++)
        {
            // Mostly faint pin-pricks, a few brighter and larger ones.
            var size = _random.NextDouble() < 0.15 ? 2.5 : 1.5;
            var star = new Star(
                new Ellipse { Width = size, Height = size, Fill = StarBrush },
                X: _random.NextDouble() * ActualWidth,
                Y: _random.NextDouble() * ActualHeight,
                BaseOpacity: _random.NextDouble() * 0.5 + 0.15,
                Speed: _random.NextDouble() * 0.25 + 0.1);

            star.Place();
            _stars.Add(star);
            Children.Add(star.Element);
        }
    }

    private void Step()
    {
        _elapsedSeconds += _timer.Interval.TotalSeconds;

        foreach (var star in _stars)
        {
            star.Y += star.Speed;
            if (star.Y > ActualHeight)
            {
                star.Y = 0;
                star.X = _random.NextDouble() * ActualWidth;
            }

            star.Place();
            star.Element.Opacity = Math.Clamp(star.BaseOpacity + Math.Sin(_elapsedSeconds * 2 + star.X + star.Y) * 0.15, 0.05, 0.85);
        }
    }

    private sealed record Star(Ellipse Element, double X, double Y, double BaseOpacity, double Speed)
    {
        public double X { get; set; } = X;

        public double Y { get; set; } = Y;

        public void Place()
        {
            SetLeft(Element, X);
            SetTop(Element, Y);
        }
    }
}
