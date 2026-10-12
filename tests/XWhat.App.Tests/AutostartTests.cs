using System.IO;
using Microsoft.Win32;
using XWhat.App.Services;
using XWhat.Core;
using Xunit;

namespace XWhat.App.Tests;

public sealed class AutostartTests
{
    [Fact]
    public void RegistrationCanBeEnabledAndDisabledWithoutTouchingOtherEntries()
    {
        var registryPath = @"Software\XWhat\Tests\" + Guid.NewGuid().ToString("N");
        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(registryPath)) key.SetValue("UnrelatedApp", "keep");
            var path = Environment.ProcessPath!;
            var startup = new AutostartService(path, registryPath);
            Assert.False(startup.IsEnabled);
            startup.SetEnabled(true);
            Assert.True(startup.IsEnabled);
            using (var key = Registry.CurrentUser.OpenSubKey(registryPath))
                Assert.Equal(StartupCommand.Build(path), key!.GetValue("XWhat"));
            startup.SetEnabled(false);
            Assert.False(startup.IsEnabled);
            using (var key = Registry.CurrentUser.OpenSubKey(registryPath))
                Assert.Equal("keep", key!.GetValue("UnrelatedApp"));
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(registryPath, false); }
    }

    [Fact]
    public void MissingExecutableDoesNotCreateStartupEntry()
    {
        var registryPath = @"Software\XWhat\Tests\" + Guid.NewGuid().ToString("N");
        var startup = new AutostartService(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".exe"), registryPath);
        Assert.Throws<FileNotFoundException>(() => startup.SetEnabled(true));
        using var key = Registry.CurrentUser.OpenSubKey(registryPath);
        Assert.Null(key);
    }
}
