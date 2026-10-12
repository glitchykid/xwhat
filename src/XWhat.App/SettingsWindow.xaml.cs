using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using XWhat.Core;

namespace XWhat.App;

public partial class SettingsWindow : Window
{
    private readonly Action<MonitorSettings> _apply;
    private MonitorSettings _settings;
    public SettingsWindow(MonitorSettings settings, DeviceChoice[] gpus, DeviceChoice[] disks, string status, Action<MonitorSettings> apply)
    {
        InitializeComponent();
        _settings = settings; _apply = apply;
        ModeBox.SelectedIndex = settings.Mode switch { OverlayMode.Auto => 0, OverlayMode.Desktop => 1, _ => 2 };
        VersionText.Text = "v" + typeof(App).Assembly.GetName().Version?.ToString(3);
        AutostartBox.IsChecked = settings.StartWithWindows;
        CornerBox.SelectedIndex = (int)settings.Corner;
        VisibleBox.IsChecked = settings.ShowOverlay; GraphsBox.IsChecked = settings.ShowGraphs; ClickThroughBox.IsChecked = settings.ClickThrough;
        ScaleSlider.Value = settings.Scale; OpacitySlider.Value = settings.BackgroundOpacity;
        ProcessBox.Text = settings.ProcessName;
        GpuBox.ItemsSource = new[] { new DeviceChoice("", "Авто · наибольшая VRAM") }.Concat(gpus).ToArray();
        DiskBox.ItemsSource = new[] { new DeviceChoice("", "Все диски") }.Concat(disks).ToArray();
        GpuBox.SelectedValue = settings.GpuId; DiskBox.SelectedValue = settings.DiskId;
        IntervalBox.ItemsSource = new[] { 250, 500, 1000, 2000 }; IntervalBox.SelectedItem = settings.SampleIntervalMs;
        HistoryBox.ItemsSource = new[] { 5, 10, 30, 60 }; HistoryBox.SelectedItem = settings.HistorySeconds;
        ThresholdBox.Text = settings.StutterThresholdMs.ToString("0.#", CultureInfo.CurrentCulture);
        StatusText.Text = status;
        UpdateSummary();
    }
    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(ThresholdBox.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var threshold) || !double.IsFinite(threshold) || threshold is < 16 or > 200)
        { ErrorText.Text = "Укажите порог от 16 до 200 ms."; return; }
        var updated = (_settings with
        {
            Mode = ModeBox.SelectedIndex switch { 0 => OverlayMode.Auto, 1 => OverlayMode.Desktop, _ => OverlayMode.Rtss }, Corner = (OverlayCorner)Math.Max(0, CornerBox.SelectedIndex),
            StartWithWindows = AutostartBox.IsChecked == true,
            ShowOverlay = VisibleBox.IsChecked == true, ShowGraphs = GraphsBox.IsChecked == true, ClickThrough = ClickThroughBox.IsChecked == true,
            Scale = ScaleSlider.Value, BackgroundOpacity = OpacitySlider.Value, ProcessName = ProcessBox.Text,
            GpuId = GpuBox.SelectedValue as string ?? "", DiskId = DiskBox.SelectedValue as string ?? "",
            SampleIntervalMs = IntervalBox.SelectedItem as int? ?? 500, HistorySeconds = HistoryBox.SelectedItem as int? ?? 10, StutterThresholdMs = threshold
        }).Normalize();
        try { _apply(updated); _settings = updated; ErrorText.Text = ""; UpdateSummary(); SummaryText.Text = "Сохранено"; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException) { ErrorText.Text = $"Настройки не сохранены: {ex.Message}"; }
    }
    private void Processes_Click(object sender, RoutedEventArgs e)
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try { if (process.MainWindowHandle != 0 && process.Id != Environment.ProcessId) names.Add(process.ProcessName + ".exe"); }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
            }
        }
        var text = ProcessBox.Text;
        ProcessBox.ItemsSource = names; ProcessBox.Text = text; ProcessBox.IsDropDownOpen = true;
    }
    public void UpdateDevices(DeviceChoice[] gpus, DeviceChoice[] disks)
    {
        UpdateChoices(GpuBox, gpus, "Авто · наибольшая VRAM");
        UpdateChoices(DiskBox, disks, "Все диски");
    }
    private void UpdateSummary() => SummaryText.Text = $"{(_settings.Mode == OverlayMode.Auto ? "Авто" : _settings.Mode == OverlayMode.Rtss ? "RTSS" : "Windows")} · {_settings.SampleIntervalMs} ms";
    private static void UpdateChoices(System.Windows.Controls.ComboBox box, DeviceChoice[] choices, string defaultName)
    {
        var items = new[] { new DeviceChoice("", defaultName) }.Concat(choices).ToArray();
        if (box.ItemsSource is DeviceChoice[] old && old.SequenceEqual(items)) return;
        var selected = box.SelectedValue as string ?? "";
        box.ItemsSource = items; box.SelectedValue = items.Any(i => i.Id == selected) ? selected : "";
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void RtssLink_Click(object sender, RoutedEventArgs e) => OpenLink("https://www.guru3d.com/download/rtss-rivatuner-statistics-server-download/");
    private void PawnIoLink_Click(object sender, RoutedEventArgs e) => OpenLink("https://pawnio.eu/");
    private void OpenLink(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (System.ComponentModel.Win32Exception ex) { ErrorText.Text = ex.Message; }
    }
}
