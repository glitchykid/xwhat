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
                var apply = (Button)window.FindName("ApplyButton");
                threshold.Text = "NaN";
                apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Null(saved);
                threshold.Text = "40,5";
                apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.NotNull(saved);
                Assert.Equal("game.exe", saved.ProcessName);
                Assert.Equal(40.5, saved.StutterThresholdMs);
            }
            finally { window.Close(); application.Shutdown(); }
        });
    }
}
