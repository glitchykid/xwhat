namespace XWhat.Core;

public static class StartupCommand
{
    /// <summary>HKCU Run command, maximum 260 characters per Windows contract. Absolute path is always quoted.</summary>
    public static string Build(string executablePath)
    {
        if (!Path.IsPathFullyQualified(executablePath) || executablePath.Contains('"') || executablePath.Any(char.IsControl))
            throw new ArgumentException("Укажите полный путь к приложению без кавычек и управляющих символов.", nameof(executablePath));
        var command = $"\"{executablePath}\" --autostart";
        if (command.Length > 260) throw new ArgumentException("Для автозапуска переместите приложение в папку с более коротким путём.", nameof(executablePath));
        return command;
    }
}
