using System;
using System.IO;
using System.Linq;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using SoundFxEditor.Core.Export;
using SoundFxEditor.Core.Models;
using SoundFxEditor.Core.Stamps;
using SoundFxEditor.Core.Synthesis;

namespace SoundFxEditor.Wpf;

public partial class MainWindow : Window
{
    private SoundEffect _currentSound = new();
    private int _currentTab = 0;
    private bool _loading = false;
    private SoundPlayer? _player;

    // Per-channel slider references: [channelIndex][paramName]
    private readonly Slider?[,] _sliders = new Slider?[4, 8];
    private readonly CheckBox?[] _activeChecks = new CheckBox?[4];
    private readonly CheckBox?[] _shortModeChecks = new CheckBox?[4];

    // Slider param indices
    private const int SI_Period = 0;
    private const int SI_SweepRate = 1;
    private const int SI_SweepDur = 2;
    private const int SI_Volume = 3;
    private const int SI_Decay = 4;
    private const int SI_Duty = 5;
    private const int SI_Duration = 6;

    public MainWindow()
    {
        InitializeComponent();
        BuildChannelPanels();
        LoadSound(new SoundEffect());
    }

    private void BuildChannelPanels()
    {
        BuildPulsePanel(PanelPulse1, 0);
        BuildPulsePanel(PanelPulse2, 1);
        BuildTrianglePanel(PanelTriangle, 2);
        BuildNoisePanel(PanelNoise, 3);
    }

    private Slider MakeSlider(int ch, int si, double min, double max, double tick = 1)
    {
        var s = new Slider
        {
            Minimum = min, Maximum = max,
            TickFrequency = tick, IsSnapToTickEnabled = true,
            SmallChange = tick, LargeChange = tick * 5,
            Width = double.NaN, HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _sliders[ch, si] = s;
        s.ValueChanged += (_, _) => { if (!_loading) SliderChanged(ch); };
        return s;
    }

    private static StackPanel LabeledRow(string label, UIElement control)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        row.Children.Add(new Label { Content = label, Width = 140, Foreground = Brushes.LightGreen, FontFamily = new FontFamily("Consolas"), FontSize = 11 });
        if (control is Slider sl)
        {
            sl.Width = 180;
            row.Children.Add(sl);
            var lbl = new Label { Width = 40, Foreground = Brushes.LightGreen, FontFamily = new FontFamily("Consolas"), FontSize = 11 };
            sl.ValueChanged += (_, e) => lbl.Content = ((int)e.NewValue).ToString();
            lbl.Content = ((int)sl.Value).ToString();
            row.Children.Add(lbl);
        }
        else
        {
            row.Children.Add(control);
        }
        return row;
    }

    private CheckBox MakeActiveCheck(int ch)
    {
        var cb = new CheckBox { Content = "ACTIVE", Foreground = Brushes.LightGreen, FontFamily = new FontFamily("Consolas") };
        _activeChecks[ch] = cb;
        cb.Checked += (_, _) => { if (!_loading) SliderChanged(ch); };
        cb.Unchecked += (_, _) => { if (!_loading) SliderChanged(ch); };
        return cb;
    }

    private void BuildPulsePanel(StackPanel panel, int ch)
    {
        panel.Children.Add(MakeActiveCheck(ch));
        panel.Children.Add(LabeledRow("Period (0-2047):", MakeSlider(ch, SI_Period, 0, 2047)));
        panel.Children.Add(LabeledRow("Sweep Rate:", MakeSlider(ch, SI_SweepRate, -15, 15)));
        panel.Children.Add(LabeledRow("Sweep Duration:", MakeSlider(ch, SI_SweepDur, 0, 60)));
        panel.Children.Add(LabeledRow("Volume (0-15):", MakeSlider(ch, SI_Volume, 0, 15)));
        panel.Children.Add(LabeledRow("Decay/Frame:", MakeSlider(ch, SI_Decay, 0, 15)));
        panel.Children.Add(LabeledRow("Duty Cycle (0-3):", MakeSlider(ch, SI_Duty, 0, 3)));
        panel.Children.Add(LabeledRow("Duration Frames:", MakeSlider(ch, SI_Duration, 1, 120)));
    }

    private void BuildTrianglePanel(StackPanel panel, int ch)
    {
        panel.Children.Add(MakeActiveCheck(ch));
        panel.Children.Add(LabeledRow("Period (0-2047):", MakeSlider(ch, SI_Period, 0, 2047)));
        panel.Children.Add(LabeledRow("Sweep Rate:", MakeSlider(ch, SI_SweepRate, -15, 15)));
        panel.Children.Add(LabeledRow("Sweep Duration:", MakeSlider(ch, SI_SweepDur, 0, 60)));
        panel.Children.Add(LabeledRow("Volume (0-15):", MakeSlider(ch, SI_Volume, 0, 15)));
        panel.Children.Add(LabeledRow("Decay/Frame:", MakeSlider(ch, SI_Decay, 0, 15)));
        panel.Children.Add(LabeledRow("Duration Frames:", MakeSlider(ch, SI_Duration, 1, 120)));
    }

    private void BuildNoisePanel(StackPanel panel, int ch)
    {
        panel.Children.Add(MakeActiveCheck(ch));
        panel.Children.Add(LabeledRow("Period Index (0-15):", MakeSlider(ch, SI_Period, 0, 15)));
        panel.Children.Add(LabeledRow("Sweep Rate:", MakeSlider(ch, SI_SweepRate, -15, 15)));
        panel.Children.Add(LabeledRow("Sweep Duration:", MakeSlider(ch, SI_SweepDur, 0, 60)));
        panel.Children.Add(LabeledRow("Volume (0-15):", MakeSlider(ch, SI_Volume, 0, 15)));
        panel.Children.Add(LabeledRow("Decay/Frame:", MakeSlider(ch, SI_Decay, 0, 15)));
        var smCb = new CheckBox { Content = "SHORT MODE", Foreground = Brushes.LightGreen, FontFamily = new FontFamily("Consolas") };
        _shortModeChecks[ch] = smCb;
        smCb.Checked += (_, _) => { if (!_loading) SliderChanged(ch); };
        smCb.Unchecked += (_, _) => { if (!_loading) SliderChanged(ch); };
        panel.Children.Add(smCb);
        panel.Children.Add(LabeledRow("Duration Frames:", MakeSlider(ch, SI_Duration, 1, 120)));
    }

    private void LoadSound(SoundEffect sound)
    {
        _loading = true;
        _currentSound = sound;
        TxtName.Text = sound.Name;
        RefreshFromModel();
        _loading = false;
        RedrawWaveform();
    }

    private void RefreshFromModel()
    {
        for (int ch = 0; ch < 4; ch++)
        {
            var c = _currentSound.Channels[ch];
            if (_activeChecks[ch] != null) _activeChecks[ch]!.IsChecked = c.IsActive;
            SetSlider(ch, SI_Period, c.InitialPeriod);
            SetSlider(ch, SI_SweepRate, c.PeriodSweepRate);
            SetSlider(ch, SI_SweepDur, c.SweepDuration);
            SetSlider(ch, SI_Volume, c.InitialVolume);
            SetSlider(ch, SI_Decay, c.VolumeDecayPerFrame);
            if (ch < 2) SetSlider(ch, SI_Duty, c.DutyCycle);
            SetSlider(ch, SI_Duration, Math.Max(1, c.DurationFrames));
            if (ch == 3 && _shortModeChecks[3] != null)
                _shortModeChecks[3]!.IsChecked = c.ShortMode;
        }
    }

    private void SetSlider(int ch, int si, double value)
    {
        var s = _sliders[ch, si];
        if (s == null) return;
        s.Value = Math.Clamp(value, s.Minimum, s.Maximum);
    }

    private void SliderChanged(int ch)
    {
        var c = _currentSound.Channels[ch];
        c.IsActive = _activeChecks[ch]?.IsChecked == true;
        c.InitialPeriod = (int)(_sliders[ch, SI_Period]?.Value ?? 0);
        c.PeriodSweepRate = (int)(_sliders[ch, SI_SweepRate]?.Value ?? 0);
        c.SweepDuration = (int)(_sliders[ch, SI_SweepDur]?.Value ?? 0);
        c.InitialVolume = (int)(_sliders[ch, SI_Volume]?.Value ?? 0);
        c.VolumeDecayPerFrame = (int)(_sliders[ch, SI_Decay]?.Value ?? 0);
        if (ch < 2) c.DutyCycle = (int)(_sliders[ch, SI_Duty]?.Value ?? 0);
        c.DurationFrames = (int)(_sliders[ch, SI_Duration]?.Value ?? 1);
        if (ch == 3) c.ShortMode = _shortModeChecks[3]?.IsChecked == true;
        RedrawWaveform();
    }

    private void RedrawWaveform()
    {
        WaveformCanvas.Children.Clear();
        var samples = SoundSynthesizer.Synthesize(_currentSound);
        if (samples.Length == 0) return;

        double w = WaveformCanvas.ActualWidth;
        double h = WaveformCanvas.ActualHeight;
        if (w < 2 || h < 2) return;

        int n = (int)w;
        double midY = h / 2;
        double scaleY = midY * 0.9;

        var pts = new System.Windows.Media.PointCollection();
        for (int i = 0; i < n; i++)
        {
            int idx = (int)((double)i / n * samples.Length);
            double x = i;
            double y = midY - samples[idx] * scaleY;
            pts.Add(new System.Windows.Point(x, y));
        }

        var poly = new Polyline
        {
            Points = pts,
            Stroke = new SolidColorBrush(Color.FromRgb(0, 255, 136)),
            StrokeThickness = 1
        };
        WaveformCanvas.Children.Add(poly);
    }

    // Event handlers
    private void StampBoing_Click(object sender, RoutedEventArgs e) => LoadSound(SoundStamps.Boing());
    private void StampLaser_Click(object sender, RoutedEventArgs e) => LoadSound(SoundStamps.Laser());
    private void StampGlass_Click(object sender, RoutedEventArgs e) => LoadSound(SoundStamps.BreakingGlass());
    private void StampExplosion_Click(object sender, RoutedEventArgs e) => LoadSound(SoundStamps.Explosion());
    private void StampGunshot_Click(object sender, RoutedEventArgs e) => LoadSound(SoundStamps.Gunshot());

    private void NewSound_Click(object sender, RoutedEventArgs e) => LoadSound(new SoundEffect());

    private void TxtName_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loading) _currentSound.Name = TxtName.Text;
    }

    private void TabChannels_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TabChannels.SelectedItem is TabItem ti && ti.Tag is string tagStr && int.TryParse(tagStr, out int idx))
            _currentTab = idx;
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        _player?.Stop();
        _player?.Dispose();

        var samples = SoundSynthesizer.Synthesize(_currentSound);
        if (samples.Length == 0) return;

        var ms = new MemoryStream();
        WavExporter.Export(samples, SoundSynthesizer.OutputSampleRate, ms);
        ms.Seek(0, SeekOrigin.Begin);
        _player = new SoundPlayer(ms);
        _player.Play();
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        _player?.Stop();
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "WAV files|*.wav", DefaultExt = ".wav" };
        if (dlg.ShowDialog() != true) return;
        var samples = SoundSynthesizer.Synthesize(_currentSound);
        WavExporter.Export(samples, SoundSynthesizer.OutputSampleRate, dlg.FileName);
        MessageBox.Show($"Exported to {dlg.FileName}", "Export WAV", MessageBoxButton.OK);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        Dispatcher.InvokeAsync(RedrawWaveform);
    }
}
