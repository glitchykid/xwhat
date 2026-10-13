using System.Windows;
using System.Windows.Controls;
using XWhat.Core;
using Xunit;

namespace XWhat.App.Tests;

public sealed class SettingsWindowTests
{
    [Fact]
    public void StyledEditorSavesTypedProcessAndRejectsInvalidThreshold()
    {
        TrayMenuTests.RunSta(() =>
        {
            var application = new Application();
            application.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/XWhat;component/Themes/Technical.xaml")
            });
            MonitorSettings? saved = null;
            var window = new SettingsWindow(new(), [], [], "test", settings => saved = settings);
            try
            {
                var process = (ComboBox)window.FindName("ProcessBox");
                Assert.True(process.ApplyTemplate());
                Assert.IsType<System.Windows.Controls.Primitives.Popup>(process.Template.FindName("PART_Popup", process));
                var editor = Assert.IsType<TextBox>(process.Template.FindName("PART_EditableTextBox", process));
                process.ItemsSource = new[] { "first.exe", "second.exe" };
                process.SelectedIndex = 1;
                Assert.Equal("second.exe", editor.Text);
                editor.Text = "game.exe";
                Assert.Equal("game.exe", process.Text);
                var threshold = (TextBox)window.FindName("ThresholdBox");
                var mode = (ComboBox)window.FindName("ModeBox");
                var scale = (Slider)window.FindName("ScaleSlider");
                mode.SelectedIndex = 2;
                scale.Value = 0.75;
                var apply = (Button)window.FindName("ApplyButton");
                threshold.Text = "NaN";
                apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Null(saved);
                threshold.Text = "40,5";
                apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.NotNull(saved);
                Assert.Equal("game.exe", saved.ProcessName);
                Assert.Equal(40.5, saved.StutterThresholdMs);
                Assert.Equal(OverlayMode.Rtss, saved.Mode);
                Assert.Equal(0.75, saved.Scale);
            }
            finally { window.Close(); application.Shutdown(); }
        });
    }
}
