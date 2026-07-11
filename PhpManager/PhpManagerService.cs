using System.Diagnostics;
namespace PhpManager;

public sealed class PhpManagerService
{
    public AppSettings Settings { get; private set; } = AppSettings.Load();

    public IReadOnlyList<PhpVersion> ScanVersions()
    {
        if (string.IsNullOrWhiteSpace(Settings.PhpRoot) || !Directory.Exists(Settings.PhpRoot))
        {
            return [];
        }

        return Directory.GetDirectories(Settings.PhpRoot)
            .Where(path => File.Exists(System.IO.Path.Combine(path, "php.exe")))
            .Select(path => new PhpVersion(path))
            .OrderByDescending(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void Reload() => Settings = AppSettings.Load();

    public void SaveSettings(AppSettings settings)
    {
        Settings = settings;
        Settings.QuickSwitchPaths = Settings.QuickSwitchPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        Settings.Save();
        EnsureSwitchDirectory();
    }

    public void SwitchTo(string phpPath)
    {
        if (!File.Exists(System.IO.Path.Combine(phpPath, "php.exe")))
        {
            throw new InvalidOperationException($"No php.exe found in {phpPath}");
        }

        Settings.SelectedPhpPath = phpPath;
        Settings.Save();
        EnsureSwitchDirectory(force: true);

        if (Settings.UseUserPath)
        {
            EnsureUserPath();
        }
    }

    public PhpVersion? CycleQuick()
    {
        var quick = Settings.QuickSwitchPaths
            .Where(path => File.Exists(System.IO.Path.Combine(path, "php.exe")))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (quick.Count == 0)
        {
            return null;
        }

        var currentIndex = quick.FindIndex(path => string.Equals(path, Settings.SelectedPhpPath, StringComparison.OrdinalIgnoreCase));
        var next = quick[(currentIndex + 1 + quick.Count) % quick.Count];
        SwitchTo(next);
        return new PhpVersion(next);
    }

    public void EnsureUserPath()
    {
        EnsureSwitchDirectory();
        EnsurePath(EnvironmentVariableTarget.User);
    }

    public void EnsureMachinePath()
    {
        EnsureSwitchDirectory();
        EnsurePath(EnvironmentVariableTarget.Machine);
    }

    public void EnsureSwitchDirectory(bool force = false)
    {
        Directory.CreateDirectory(AppSettings.RuntimeDirectory);

        if (!string.IsNullOrWhiteSpace(Settings.SelectedPhpPath) &&
            File.Exists(Path.Combine(Settings.SelectedPhpPath, "php.exe")))
        {
            if (!force && File.Exists(Path.Combine(AppSettings.CurrentDirectory, "php.exe")))
            {
                return;
            }

            RemoveCurrentDirectory();
            if (TryCreateJunction(AppSettings.CurrentDirectory, Settings.SelectedPhpPath))
            {
                return;
            }
        }

        CreateCommandFallback();
    }

    private static void CreateCommandFallback()
    {
        RemoveCurrentDirectory();
        Directory.CreateDirectory(AppSettings.CurrentDirectory);

        var cmd = Path.Combine(AppSettings.CurrentDirectory, "php.cmd");
        File.WriteAllText(cmd, """
@echo off
setlocal
set "PHP_MANAGER_SELECTION=%APPDATA%\PhpManager\selected-php.txt"
if not exist "%PHP_MANAGER_SELECTION%" (
  echo PhpManager has no selected PHP version. Open PhpManager and select one. 1>&2
  exit /b 9009
)
set /p PHP_MANAGER_HOME=<"%PHP_MANAGER_SELECTION%"
if not exist "%PHP_MANAGER_HOME%\php.exe" (
  echo Selected PHP executable was not found: %PHP_MANAGER_HOME%\php.exe 1>&2
  exit /b 9009
)
"%PHP_MANAGER_HOME%\php.exe" %*
exit /b %ERRORLEVEL%
""");

        var bat = Path.Combine(AppSettings.CurrentDirectory, "php.bat");
        File.Copy(cmd, bat, overwrite: true);
    }

    public string GetShellStatus()
    {
        var selected = Settings.SelectedPhpPath;
        var selectedExists = selected is not null && File.Exists(System.IO.Path.Combine(selected, "php.exe"));
        var currentPhp = Path.Combine(AppSettings.CurrentDirectory, "php.exe");
        var fallbackCmd = Path.Combine(AppSettings.CurrentDirectory, "php.cmd");
        var userPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? string.Empty;
        var machinePath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine) ?? string.Empty;
        var userHasShim = ContainsPath(userPath, AppSettings.CurrentDirectory);
        var machineHasShim = ContainsPath(machinePath, AppSettings.CurrentDirectory);

        return $"""
Selected PHP: {(selected is null ? "none" : selected)}
Selected php.exe exists: {(selectedExists ? "yes" : "no")}
Switch directory: {AppSettings.CurrentDirectory}
php.exe available there: {(File.Exists(currentPhp) ? "yes" : "no")}
Command fallback available: {(File.Exists(fallbackCmd) ? "yes" : "no")}
User PATH has shim: {(userHasShim ? "yes" : "no")}
Machine PATH has shim: {(machineHasShim ? "yes" : "no")}

Open a new terminal after PATH changes, then run:
where.exe php
php -v
""";
    }

    public static void OpenFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("File not found.", path);
        }

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    public static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void EnsurePath(EnvironmentVariableTarget target)
    {
        var currentPath = Environment.GetEnvironmentVariable("Path", target) ?? string.Empty;
        var parts = currentPath
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => !string.Equals(part, AppSettings.CurrentDirectory, StringComparison.OrdinalIgnoreCase))
            .Where(part => !string.Equals(part, AppSettings.LegacyShimDirectory, StringComparison.OrdinalIgnoreCase))
            .Where(part => !IsManagedPhpPath(part))
            .ToList();

        parts.Insert(0, AppSettings.CurrentDirectory);
        Environment.SetEnvironmentVariable("Path", string.Join(';', parts), target);
        BroadcastEnvironmentChange();
    }

    private static bool ContainsPath(string pathValue, string expectedPath)
    {
        return pathValue
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(part => string.Equals(part, expectedPath, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsManagedPhpPath(string path)
    {
        if (string.IsNullOrWhiteSpace(Settings.PhpRoot))
        {
            return false;
        }

        try
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Settings.PhpRoot));
            var candidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Environment.ExpandEnvironmentVariables(path)));
            var parent = Directory.GetParent(candidate)?.FullName;

            return parent is not null &&
                   string.Equals(parent, root, StringComparison.OrdinalIgnoreCase) &&
                   Path.GetFileName(candidate).StartsWith("php", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryCreateJunction(string junctionPath, string targetPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("/d");
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("mklink");
        startInfo.ArgumentList.Add("/J");
        startInfo.ArgumentList.Add(junctionPath);
        startInfo.ArgumentList.Add(targetPath);

        using var process = Process.Start(startInfo);
        process?.WaitForExit();
        return process?.ExitCode == 0 && File.Exists(Path.Combine(junctionPath, "php.exe"));
    }

    private static void RemoveCurrentDirectory()
    {
        if (!Directory.Exists(AppSettings.CurrentDirectory))
        {
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("/d");
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("rmdir");
        startInfo.ArgumentList.Add(AppSettings.CurrentDirectory);

        using var process = Process.Start(startInfo);
        process?.WaitForExit();

        if (Directory.Exists(AppSettings.CurrentDirectory))
        {
            Directory.Delete(AppSettings.CurrentDirectory, recursive: true);
        }
    }

    private static void BroadcastEnvironmentChange()
    {
        NativeMethods.SendMessageTimeout(
            NativeMethods.HwndBroadcast,
            NativeMethods.WmSettingChange,
            UIntPtr.Zero,
            "Environment",
            NativeMethods.SendMessageTimeoutFlags.AbortIfHung,
            5000,
            out _);
    }
}
