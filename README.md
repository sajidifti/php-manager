# PHP Manager

A Windows tray app for switching between PHP builds unpacked into one folder, such as:

```text
C:\laragon\bin\php
  php-8.3.10-Win32-vs16-x64
  php-8.4.2-Win32-vs17-x64
```

## How Switching Works

PHP Manager avoids rewriting `Path` every time you switch. Instead, it creates a stable switch directory:

```text
%LOCALAPPDATA%\PhpManager\current
```

The app adds that folder to your user `Path` automatically when you switch versions. After that, switching PHP versions only updates:

```text
%APPDATA%\PhpManager\selected-php.txt
```

The switch directory is a Windows junction to the selected PHP folder, so it exposes the real `php.exe` and all DLLs beside it. If junction creation is unavailable, the app falls back to `php.cmd` / `php.bat` forwarding.

## Build

```powershell
dotnet build .\PhpManager\PhpManager.csproj --configfile .\NuGet.Config
```

## Run

```powershell
dotnet run --project .\PhpManager\PhpManager.csproj --configfile .\NuGet.Config
```

The app starts in the system tray.

## Current Features

- Tray menu with quick-switch versions.
- Left-click tray icon cycles through checked quick-switch versions.
- Full versions window scans a configurable PHP root folder.
- Add the stable switch directory to user `Path`, or machine `Path` when running elevated.
- PATH activation removes old PHP-version folders under the configured PHP root from the same PATH target.
- Selecting a version automatically activates the user PATH shim unless disabled in settings.
- Basic `php.ini` maintenance:
  - Creates `php.ini` from `php.ini-development` or `php.ini-production` when missing.
  - Enables/disables extensions found in the version's `ext` folder.
  - Edits `disable_functions`.
