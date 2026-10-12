using System.IO;
using Microsoft.Win32;
using XWhat.Core;

namespace XWhat.App.Services;

/// <summary>User-only Run entry. Registration changes are made from settings, never from the startup path.</summary>
internal sealed class AutostartService(string executablePath, string registryPath = @"Software\Microsoft\Windows\CurrentVersion\Run")
{
    private const string ValueName = "XWhat";
    public bool IsEnabled
    {
        get { using var key = Registry.CurrentUser.OpenSubKey(registryPath); return key?.GetValue(ValueName) is string value && value.Length > 0; }
    }
    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            if (!File.Exists(executablePath)) throw new FileNotFoundException("Приложение не найдено для автозапуска.", executablePath);
            var command = StartupCommand.Build(executablePath);
            using var key = Registry.CurrentUser.CreateSubKey(registryPath);
            if (!string.Equals(key.GetValue(ValueName) as string, command, StringComparison.OrdinalIgnoreCase))
                key.SetValue(ValueName, command, RegistryValueKind.String);
        }
        else
        {
            using var key = Registry.CurrentUser.OpenSubKey(registryPath, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
