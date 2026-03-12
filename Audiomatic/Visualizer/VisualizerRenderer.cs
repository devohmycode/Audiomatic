using Audiomatic.Services;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace Audiomatic.Visualizer;

public sealed class VisualizerRenderer
{
    private readonly SpectrumAnalyzer _spectrum;
    private readonly Func<TimeSpan> _getPosition;
    private readonly Func<bool> _hasTrack;

    private CanvasAnimatedControl? _canvas;
    private IVisualizerMode _currentMode;
    private string _currentModeName;
    private VisualizerSettings _settings;

    private readonly Dictionary<string, Button> _modeButtons = new();

    public Action? OnModeChanged { get; set; }

    public CanvasAnimatedControl? Canvas => _canvas;

    private static readonly Dictionary<string, Func<IVisualizerMode>> ModeFactories = new()
    {
        ["bars"] = () => new BarsMode(),
        ["circle"] = () => new CircleMode(),
        ["wave"] = () => new WaveMode(),
    };

    public VisualizerRenderer(SpectrumAnalyzer spectrum, Func<TimeSpan> getPosition, Func<bool> hasTrack)
    {
        _spectrum = spectrum;
        _getPosition = getPosition;
        _hasTrack = hasTrack;

        var s = SettingsManager.Load();
        _currentModeName = s.VisualizerMode ?? "classic";
        _settings = new VisualizerSettings(
            Color: s.VisualizerColor ?? "",
            GlowEnabled: s.VisualizerGlow,
            DarkBackground: false);

        _currentMode = ModeFactories.TryGetValue(_currentModeName, out var factory)
            ? factory()
            : new BarsMode();
    }

    public bool IsClassicMode => _currentModeName == "classic";

    public StackPanel BuildSelector()
    {
        var selector = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 0,
            Margin = new Thickness(0, 4, 0, 4)
        };

        void AddModeButton(string mode, string label)
        {
            var btn = new Button
            {
                Content = new TextBlock { Text = label, FontSize = 11 },
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 4, 10, 4),
                CornerRadius = new CornerRadius(4),
                MinHeight = 0, MinWidth = 0
            };
            btn.Click += (_, _) => SwitchMode(mode);
            _modeButtons[mode] = btn;
            selector.Children.Add(btn);
        }

        AddModeButton("classic", "Classic");
        AddModeButton("bars", "Bars");
        AddModeButton("circle", "Circle");
        AddModeButton("wave", "Wave");

        selector.Children.Add(new Border { Width = 12 });

        // Glow toggle
        var glowBtn = new Button
        {
            Content = new FontIcon { Glyph = "\uE706", FontSize = 12 },
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 4, 6, 4),
            CornerRadius = new CornerRadius(4),
            MinHeight = 0, MinWidth = 0
        };

        glowBtn.Click += (_, _) =>
        {
            _settings = _settings with { GlowEnabled = !_settings.GlowEnabled };
            SaveVisualizerSettings();
            UpdateGlowButton(glowBtn);
        };

        selector.Children.Add(glowBtn);

        // Color picker button
        var colorPreview = new Border
        {
            Width = 22, Height = 22, CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeHelper.Brush("ControlStrokeColorDefaultBrush"),
            Background = new SolidColorBrush(EffectsHelper.GetRenderColor(_settings)),
            Margin = new Thickness(6, 0, 0, 0)
        };

        var colorPicker = new ColorPicker
        {
            IsColorSliderVisible = false,
            IsColorChannelTextInputVisible = false,
            IsHexInputVisible = true,
            IsAlphaEnabled = false,
            Color = EffectsHelper.GetRenderColor(_settings),
        };

        colorPicker.ColorChanged += (_, args) =>
        {
            var c = args.NewColor;
            var hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            _settings = _settings with { Color = hex };
            colorPreview.Background = new SolidColorBrush(c);
            SaveVisualizerSettings();
        };

        var defaultColor = EffectsHelper.ParseColor("");
        var resetBtn = new Button
        {
            Content = new TextBlock { Text = "Reset", FontSize = 11 },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 8, 0, 0),
        };
        resetBtn.Click += (_, _) =>
        {
            colorPicker.Color = defaultColor;
            _settings = _settings with { Color = "" };
            colorPreview.Background = new SolidColorBrush(defaultColor);
            SaveVisualizerSettings();
        };

        var pickerPanel = new StackPanel { Children = { colorPicker, resetBtn } };

        var pickerFlyout = new Flyout
        {
            Content = pickerPanel,
            Placement = FlyoutPlacementMode.Bottom
        };

        var colorBtn = new Button
        {
            Content = colorPreview,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(4),
            MinHeight = 0, MinWidth = 0,
            Flyout = pickerFlyout
        };

        selector.Children.Add(colorBtn);

        UpdateModeHighlight();
        UpdateGlowButton(glowBtn);

        // Create canvas
        _canvas = new CanvasAnimatedControl
        {
            ClearColor = Windows.UI.Color.FromArgb(0, 0, 0, 0),
            IsFixedTimeStep = true,
            TargetElapsedTime = TimeSpan.FromMilliseconds(16),
        };
        _canvas.Draw += Canvas_Draw;
        _canvas.Visibility = IsClassicMode ? Visibility.Collapsed : Visibility.Visible;
        _canvas.Paused = IsClassicMode;

        return selector;
    }

    public void Start()
    {
        if (_canvas != null && !IsClassicMode)
            _canvas.Paused = false;
    }

    public void Stop()
    {
        if (_canvas != null)
            _canvas.Paused = true;
    }

    public void SetCanvasVisibility(bool win2dVisible)
    {
        if (_canvas != null)
            _canvas.Visibility = win2dVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Canvas_Draw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
    {
        if (IsClassicMode) return;

        float w = (float)sender.Size.Width;
        float h = (float)sender.Size.Height;
        if (w <= 0 || h <= 0) return;

        int bandCount = _currentMode.GetBandCount(w, h);
        var bands = _spectrum.GetSpectrum(_hasTrack() ? _getPosition() : TimeSpan.Zero, bandCount);

        _currentMode.Render(args.DrawingSession, bands, w, h, _settings);
    }

    private void SwitchMode(string mode)
    {
        _currentModeName = mode;

        if (ModeFactories.TryGetValue(mode, out var factory))
            _currentMode = factory();

        SettingsManager.Save(SettingsManager.Load() with { VisualizerMode = mode });
        UpdateModeHighlight();

        SetCanvasVisibility(!IsClassicMode);
        if (IsClassicMode)
            Stop();
        else
            Start();

        OnModeChanged?.Invoke();
    }

    private void UpdateModeHighlight()
    {
        var accent = ThemeHelper.Brush("AccentTextFillColorPrimaryBrush");
        var normal = ThemeHelper.Brush("TextFillColorSecondaryBrush");

        foreach (var (mode, btn) in _modeButtons)
        {
            if (btn.Content is TextBlock tb)
                tb.Foreground = mode == _currentModeName ? accent : normal;
        }
    }

    private void UpdateGlowButton(Button glowBtn)
    {
        var accent = ThemeHelper.Brush("AccentTextFillColorPrimaryBrush");
        var normal = ThemeHelper.Brush("TextFillColorSecondaryBrush");
        if (glowBtn.Content is FontIcon gi)
            gi.Foreground = _settings.GlowEnabled ? accent : normal;
    }

    private void SaveVisualizerSettings()
    {
        var s = SettingsManager.Load();
        SettingsManager.Save(s with
        {
            VisualizerColor = _settings.Color,
            VisualizerGlow = _settings.GlowEnabled,
        });
    }
}
