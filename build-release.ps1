param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = Join-Path $root 'PhpManager\PhpManager.csproj'
$publishRoot = Join-Path $root 'artifacts\publish'
$publishDirectory = Join-Path $publishRoot 'win-x64-release'
$installerScript = Join-Path $root 'installer\PhpManager.iss'

& (Join-Path $root 'build\Create-Icon.ps1')

if (Test-Path $publishDirectory) {
    try {
        Remove-Item -LiteralPath $publishDirectory -Recurse -Force
    }
    catch {
        $staleDirectory = Join-Path $publishRoot ("stale-" + [DateTime]::Now.ToString('yyyyMMddHHmmss'))
        Move-Item -LiteralPath $publishDirectory -Destination $staleDirectory
        Write-Warning "The previous publish output was locked and was moved to $staleDirectory"
    }
}

dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:Version=$Version `
    -o $publishDirectory

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

$isccCandidates = @(
    (Get-Command iscc.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
) | Where-Object { $_ -and (Test-Path $_) }

$iscc = $isccCandidates | Select-Object -First 1
if (-not $iscc) {
    throw "Inno Setup 6 was not found. Install JRSoftware.InnoSetup with winget."
}

& $iscc "/DAppVersion=$Version" $installerScript
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed."
}

Write-Host "Release executable: $publishDirectory\PhpManager.exe"
Write-Host "Installer: $root\artifacts\installer\PHP-Manager-Setup-$Version.exe"
