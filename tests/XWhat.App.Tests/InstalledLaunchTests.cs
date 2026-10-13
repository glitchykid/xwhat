using System.IO;
using System.Security.Principal;
using System.Xml.Linq;
using XWhat.App.Services;
using Xunit;

namespace XWhat.App.Tests;

public sealed class InstalledLaunchTests
{
    [Fact]
    public void TaskCanOnlyLaunchTheInstalledExecutableForItsUser()
    {
        const string sid = "S-1-5-21-100-200-300-1001";
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "XWhat", "XWhat.exe");
        var xml = InstalledLaunch.Definition(path, sid);
        var task = XDocument.Parse(xml);
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        Assert.Equal(path, task.Descendants(ns + "Command").Single().Value);
        Assert.Equal("--task-launch", task.Descendants(ns + "Arguments").Single().Value);
        Assert.Equal("InteractiveToken", task.Descendants(ns + "LogonType").Single().Value);
        Assert.Equal("HighestAvailable", task.Descendants(ns + "RunLevel").Single().Value);
        Assert.Empty(task.Descendants(ns + "Triggers").Single().Elements());
        Assert.Equal("PT0S", task.Descendants(ns + "ExecutionTimeLimit").Single().Value);
        Assert.Contains($"(A;;GRGX;;;{sid})", InstalledLaunch.Security(sid));
        Assert.DoesNotContain($"(A;;GA;;;{sid})", InstalledLaunch.Security(sid));
        Assert.Throws<ArgumentException>(() => { InstalledLaunch.Definition(Path.Combine(Path.GetTempPath(), "XWhat.exe"), sid); });
        Assert.Throws<ArgumentException>(() => { InstalledLaunch.Definition(path, "S-1-5-18"); });
        Assert.True(InstalledLaunch.Matches(xml, path, sid));
        Assert.False(InstalledLaunch.Matches(xml.Replace("--task-launch", "--install-tasks", StringComparison.Ordinal), path, sid));
        Assert.False(InstalledLaunch.Matches("not XML", path, sid));
        var extraAction = XDocument.Parse(xml);
        extraAction.Descendants(ns + "Actions").Single().Add(new XElement(ns + "ComHandler"));
        Assert.False(InstalledLaunch.Matches(extraAction.ToString(), path, sid));
    }

    [Fact]
    public void WindowsSchedulerAcceptsDefinitionWithoutRegisteringIt()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "XWhat", "XWhat.exe");
        InstalledLaunch.ValidateDefinition(path, identity.User!.Value);
    }
}
