using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Xml.Linq;
using Microsoft.Win32;

namespace XWhat.App.Services;

/// <summary>Installer-owned, on-demand launch task. Only administrators may change it or its Program Files executable.</summary>
internal static partial class InstalledLaunch
{
    private const string RegistryPath = @"SOFTWARE\XWhat";
    private const string FrameGroupSid = "S-1-5-32-559";
    public static bool FrameAccess
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator) || principal.IsInRole(new SecurityIdentifier(FrameGroupSid));
        }
    }

    public static string Definition(string executable, string userSid)
    {
        Validate(executable, userSid);
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        XElement Element(string name, object? content = null) => new(ns + name, content);
        return new XDocument(new XElement(ns + "Task", new XAttribute("version", "1.2"),
            Element("RegistrationInfo", Element("Description", "X What: hardware sensors and frame capture")),
            Element("Triggers"), Element("Principals", new XElement(ns + "Principal", new XAttribute("id", "User"),
                Element("UserId", userSid), Element("LogonType", "InteractiveToken"), Element("RunLevel", "HighestAvailable"))),
            Element("Settings", new[] {
                Element("MultipleInstancesPolicy", "IgnoreNew"), Element("DisallowStartIfOnBatteries", "false"),
                Element("StopIfGoingOnBatteries", "false"), Element("AllowStartOnDemand", "true"),
                Element("Enabled", "true"), Element("ExecutionTimeLimit", "PT0S") }),
            new XElement(ns + "Actions", new XAttribute("Context", "User"), Element("Exec", new[] {
                Element("Command", executable), Element("Arguments", "--task-launch"), Element("WorkingDirectory", Path.GetDirectoryName(executable)) }))))
            .ToString();
    }
    public static string Security(string userSid)
    {
        ValidateSid(userSid);
        // The user may read/run the fixed task, but cannot replace its action with another privileged executable.
        return $"D:P(A;;GA;;;SY)(A;;GA;;;BA)(A;;GRGX;;;{userSid})";
    }
    private static string TaskName(string sid) => "XWhat-Monitor-" + sid;
    private static void ValidateSid(string sid)
    {
        var identifier = new SecurityIdentifier(sid);
        if (!identifier.IsAccountSid() || identifier.AccountDomainSid == null) throw new ArgumentException("A user account SID is required.", nameof(sid));
    }
    private static void Validate(string executable, string sid)
    {
        ValidateSid(sid);
        if (!Path.IsPathFullyQualified(executable) || Path.GetFileName(executable) != "XWhat.exe" ||
            !new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 }
                .Select(Environment.GetFolderPath).Where(root => root.Length > 0)
                .Any(root => Path.GetFullPath(executable).StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Installed launch requires XWhat.exe inside Program Files.", nameof(executable));
    }
    public static bool TryLaunch(SettingsStore store)
    {
        using var identity = WindowsIdentity.GetCurrent();
        if (new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator)) return false;
        using var key = Registry.LocalMachine.OpenSubKey(RegistryPath);
        var executable = Path.Combine(AppContext.BaseDirectory, "XWhat.exe");
        if (!string.Equals(key?.GetValue("Executable") as string, executable, StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            Validate(executable, identity.User!.Value);
            WithFolder(folder =>
            {
                object task = folder.GetTask(TaskName(identity.User.Value));
                try
                {
                    dynamic registered = task;
                    if (!Matches((string)registered.Xml, executable, identity.User.Value)) throw new InvalidOperationException("Installed launch task has changed.");
                    object running = registered.Run(null);
                    Marshal.FinalReleaseComObject(running);
                }
                finally { Marshal.FinalReleaseComObject(task); }
            });
            return true;
        }
        catch (Exception ex) when (ex is COMException or ArgumentException or InvalidOperationException)
        { store.Log($"Installed launch: {ex.Message}"); return false; }
    }
    internal static bool Matches(string xml, string executable, string sid)
    {
        try
        {
            var document = XDocument.Parse(xml);
            XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
            var actions = document.Root?.Element(ns + "Actions")?.Elements().ToArray();
            return actions is { Length: 1 } && actions[0].Name == ns + "Exec" &&
                string.Equals(actions[0].Element(ns + "Command")?.Value, executable, StringComparison.OrdinalIgnoreCase) &&
                actions[0].Element(ns + "Arguments")?.Value == "--task-launch" &&
                document.Descendants(ns + "UserId").SingleOrDefault()?.Value == sid;
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or InvalidOperationException) { return false; }
    }
    public static int Configure()
    {
        var sid = SessionUserSid();
        var executable = Path.Combine(AppContext.BaseDirectory, "XWhat.exe");
        Validate(executable, sid);
        var changed = AddFrameAccess(new SecurityIdentifier(sid));
        WithFolder(folder =>
        {
            object task = folder.RegisterTask(TaskName(sid), Definition(executable, sid), 6, sid, null, 3, Security(sid));
            Marshal.FinalReleaseComObject(task);
        });
        using var key = Registry.LocalMachine.CreateSubKey(RegistryPath);
        key.SetValue("Executable", executable);
        key.SetValue("TaskName", TaskName(sid));
        using var identity = WindowsIdentity.GetCurrent();
        return changed && identity.User!.Value != sid ? 3010 : 0;
    }
    internal static void ValidateDefinition(string executable, string sid) => WithFolder(folder =>
    {
        object? task = folder.RegisterTask(TaskName(sid), Definition(executable, sid), 1, sid, null, 3, null);
        if (task != null) Marshal.FinalReleaseComObject(task);
    });
    public static void Remove()
    {
        using var key = Registry.LocalMachine.OpenSubKey(RegistryPath);
        var executable = Path.Combine(AppContext.BaseDirectory, "XWhat.exe");
        var name = key?.GetValue("TaskName") as string;
        if (!string.Equals(key?.GetValue("Executable") as string, executable, StringComparison.OrdinalIgnoreCase) || name == null || !name.StartsWith("XWhat-Monitor-S-1-5-21-", StringComparison.Ordinal)) return;
        WithFolder(folder => folder.DeleteTask(name, 0));
        Registry.LocalMachine.DeleteSubKey(RegistryPath, false);
    }
    private static void WithFolder(Action<dynamic> action)
    {
        object service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service", true)!)!;
        object? folder = null;
        try { dynamic scheduler = service; scheduler.Connect(); folder = scheduler.GetFolder(@"\"); action(folder); }
        finally { if (folder != null) Marshal.FinalReleaseComObject(folder); Marshal.FinalReleaseComObject(service); }
    }
    private static string SessionUserSid()
    {
        // UAC may use another administrator account; grant access to the logged-in session user instead.
        var user = SessionValue(5);
        var domain = SessionValue(7);
        if (user.Length == 0) throw new InvalidOperationException("An interactive user session is required.");
        return ((SecurityIdentifier)new NTAccount(domain, user).Translate(typeof(SecurityIdentifier))).Value;
    }
    private static string SessionValue(int information)
    {
        using var process = Process.GetCurrentProcess();
        if (!WTSQuerySessionInformationW(0, process.SessionId, information, out var pointer, out _))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        try { return Marshal.PtrToStringUni(pointer) ?? ""; }
        finally { WTSFreeMemory(pointer); }
    }
    private static unsafe bool AddFrameAccess(SecurityIdentifier sid)
    {
        var name = ((NTAccount)new SecurityIdentifier(FrameGroupSid).Translate(typeof(NTAccount))).Value.Split('\\').Last();
        var bytes = new byte[sid.BinaryLength]; sid.GetBinaryForm(bytes, 0);
        fixed (byte* pointer = bytes)
        {
            var member = (nint)pointer;
            var result = NetLocalGroupAddMembers(null, name, 0, ref member, 1);
            if (result is not (0 or 1378)) throw new System.ComponentModel.Win32Exception((int)result);
            return result == 0;
        }
    }
    [LibraryImport("wtsapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WTSQuerySessionInformationW(nint server, int session, int information, out nint buffer, out int bytes);
    [LibraryImport("wtsapi32.dll")] private static partial void WTSFreeMemory(nint memory);
    [LibraryImport("netapi32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint NetLocalGroupAddMembers(string? server, string group, uint level, ref nint members, uint count);
}
