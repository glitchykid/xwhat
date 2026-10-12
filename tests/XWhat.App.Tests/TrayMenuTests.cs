using System.Runtime.ExceptionServices;
using XWhat.App.Controls;
using Xunit;

namespace XWhat.App.Tests;

public sealed class TrayMenuTests
{
    [Fact]
    public void MenuKeepsActionsAndAccessibleOverlayStateInSync()
    {
        RunSta(() =>
        {
            var actions = new List<string>();
            using var menu = new TrayMenu(() => actions.Add("settings"), () => actions.Add("overlay"),
                () => actions.Add("log"), () => actions.Add("exit"));
            menu.CreateControl();
            menu.PerformLayout();
            Assert.True(menu.Items.Cast<System.Windows.Forms.ToolStripItem>().All(item => item.Bounds.Bottom <= menu.Height),
                string.Join("; ", menu.Items.Cast<System.Windows.Forms.ToolStripItem>().Select(item => $"{item.Text}: {item.Bounds}, menu: {menu.Size}")));
            menu.SetOverlayVisible(true);
            Assert.Contains("ON", menu.Items[2].AccessibleDescription!);
            menu.SetOverlayVisible(false);
            Assert.Contains("OFF", menu.Items[2].AccessibleDescription!);
            foreach (var index in new[] { 1, 2, 3, 5 }) menu.Items[index].PerformClick();
            Assert.Equal(new[] { "settings", "overlay", "log", "exit" }, actions);
        });
    }

    internal static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
