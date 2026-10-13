using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace XWhat.App.Services;

internal sealed class RtssHost(SettingsStore store)
{
    public string? Executable { get; } = FindExecutable();
    public bool IsInstalled => Executable != null;
    public string StartError { get; private set; } = "";
    public void Start()
    {
        if (Executable == null) return;
        using var current = Process.GetCurrentProcess();
        var existing = Process.GetProcessesByName("RTSS");
        try { if (existing.Any(process => process.SessionId == current.SessionId)) return; }
        finally { foreach (var process in existing) process.Dispose(); }
        try { using var process = Process.Start(new ProcessStartInfo(Executable) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(Executable)!, CreateNoWindow = true }); }
        catch (System.ComponentModel.Win32Exception ex) { StartError = "Не удалось запустить RTSS"; store.Log($"RTSS launch: {ex.Message}"); }
    }
    private static string? FindExecutable()
    {
        var roots = new[] { Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.ProgramFiles }.Select(Environment.GetFolderPath).Where(path => path.Length > 0).ToArray();
        var candidates = roots.Select(root => Path.Combine(root, "RivaTuner Statistics Server", "RTSS.exe")).ToList();
        foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
        {
            using var registry = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var settings = registry.OpenSubKey(@"SOFTWARE\Unwinder\RTSS");
            if (settings?.GetValue("InstallPath") is string installPath && Path.IsPathFullyQualified(installPath)) candidates.Add(Path.Combine(installPath, "RTSS.exe"));
            using var uninstall = registry.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstall == null) continue;
            foreach (var name in uninstall.GetSubKeyNames())
            {
                using var product = uninstall.OpenSubKey(name);
                if (product?.GetValue("DisplayName") as string != "RivaTuner Statistics Server") continue;
                if (product.GetValue("InstallLocation") is string path && Path.IsPathFullyQualified(path)) candidates.Add(Path.Combine(path, "RTSS.exe"));
            }
        }
        return candidates.FirstOrDefault(path => roots.Any(root => Path.GetFullPath(path).StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) && File.Exists(path));
    }
}
