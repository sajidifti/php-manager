namespace PhpManager;

public sealed class PhpIniService
{
    private readonly string phpDirectory;
    private readonly string iniPath;

    public PhpIniService(string phpDirectory)
    {
        this.phpDirectory = phpDirectory;
        iniPath = Path.Combine(phpDirectory, "php.ini");
    }

    public string IniPath => iniPath;

    public void EnsureIni()
    {
        if (File.Exists(iniPath))
        {
            return;
        }

        var development = Path.Combine(phpDirectory, "php.ini-development");
        var production = Path.Combine(phpDirectory, "php.ini-production");
        var source = File.Exists(development) ? development : production;

        if (source is not null && File.Exists(source))
        {
            File.Copy(source, iniPath);
        }
        else
        {
            File.WriteAllText(iniPath, "; php.ini created by PHP Manager\r\n");
        }
    }

    public IReadOnlyList<PhpExtension> GetExtensions()
    {
        EnsureIni();
        var extDir = Path.Combine(phpDirectory, "ext");
        var dlls = Directory.Exists(extDir)
            ? Directory.GetFiles(extDir, "php_*.dll").Select(Path.GetFileName).Where(name => name is not null).Cast<string>().Order().ToList()
            : [];

        var lines = File.ReadAllLines(iniPath);
        return dlls.Select(name =>
        {
            var extensionName = name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
            if (extensionName.StartsWith("php_", StringComparison.OrdinalIgnoreCase))
            {
                extensionName = extensionName[4..];
            }
            var enabled = lines.Any(line => IsEnabledExtensionLine(line, extensionName, name));
            return new PhpExtension(extensionName, name, enabled);
        }).ToList();
    }

    public string GetDisabledFunctions()
    {
        EnsureIni();
        var line = File.ReadLines(iniPath).LastOrDefault(line => IsDirective(line, "disable_functions"));
        if (line is null)
        {
            return string.Empty;
        }

        var equals = line.IndexOf('=');
        return equals >= 0 ? line[(equals + 1)..].Trim() : string.Empty;
    }

    public void Save(IEnumerable<PhpExtension> extensions, string disabledFunctions)
    {
        EnsureIni();
        File.Copy(iniPath, iniPath + ".bak", overwrite: true);
        var lines = File.ReadAllLines(iniPath).ToList();

        foreach (var extension in extensions)
        {
            lines = SetExtension(lines, extension);
        }

        lines = SetDirective(lines, "disable_functions", disabledFunctions.Trim());
        File.WriteAllLines(iniPath, lines);
    }

    private static List<string> SetExtension(List<string> lines, PhpExtension extension)
    {
        var found = false;
        for (var i = 0; i < lines.Count; i++)
        {
            if (!TryGetDirectiveValue(lines[i], "extension", allowCommented: true, out var value))
            {
                continue;
            }

            if (!string.Equals(value, extension.Name, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(value, $"php_{extension.Name}", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(value, extension.FileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            lines[i] = extension.Enabled ? $"extension={extension.Name}" : $";extension={extension.Name}";
            found = true;
        }

        if (!found && extension.Enabled)
        {
            lines.Add($"extension={extension.Name}");
        }

        return lines;
    }

    private static List<string> SetDirective(List<string> lines, string key, string value)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (IsDirective(lines[i], key))
            {
                lines[i] = $"{key} = {value}";
                return lines;
            }
        }

        lines.Add($"{key} = {value}");
        return lines;
    }

    private static bool IsEnabledExtensionLine(string line, string extensionName, string fileName)
    {
        if (!TryGetDirectiveValue(line, "extension", allowCommented: false, out var value))
        {
            return false;
        }

        return string.Equals(value, extensionName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, $"php_{extensionName}", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, fileName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDirective(string line, string key)
    {
        return TryGetDirectiveValue(line, key, allowCommented: false, out _);
    }

    private static bool TryGetDirectiveValue(string line, string key, bool allowCommented, out string value)
    {
        value = string.Empty;
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith(';'))
        {
            if (!allowCommented)
            {
                return false;
            }

            trimmed = trimmed[1..].TrimStart();
        }

        var equals = trimmed.IndexOf('=');
        if (equals < 0 || !string.Equals(trimmed[..equals].Trim(), key, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        value = trimmed[(equals + 1)..].Trim().Trim('"');
        return true;
    }
}

public sealed record PhpExtension(string Name, string FileName, bool Enabled)
{
    public override string ToString() => Name;
}
