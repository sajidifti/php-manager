namespace PhpManager;

public sealed record PhpVersion(string Path)
{
    public string Name => System.IO.Path.GetFileName(Path);
    public string PhpExe => System.IO.Path.Combine(Path, "php.exe");
    public string PhpIni => System.IO.Path.Combine(Path, "php.ini");
    public override string ToString() => Name;
}
