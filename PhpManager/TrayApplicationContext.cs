namespace PhpManager;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly PhpManagerService service = new();
    private readonly NotifyIcon trayIcon;
    private readonly Icon appIcon;
    private MainForm? mainForm;

    public TrayApplicationContext()
    {
        string? startupWarning = null;
        try
        {
            service.EnsureSwitchDirectory();
            if (service.Settings.UseUserPath)
            {
                service.EnsureUserPath();
            }

            StartupManager.SetEnabled(service.Settings.StartWithWindows);
        }
        catch (Exception ex)
        {
            startupWarning = ex.Message;
        }

        appIcon = AppIcon.LoadTrayIcon();
        trayIcon = new NotifyIcon
        {
            Icon = appIcon,
            Text = "PHP Manager",
            Visible = true
        };

        trayIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                CycleQuick();
            }
        };

        RebuildMenu();

        if (startupWarning is not null)
        {
            ShowBalloon($"Startup setup needs attention: {startupWarning}");
        }
    }

    public void RebuildMenu()
    {
        var menu = new ContextMenuStrip();
        var versions = service.ScanVersions();
        var quick = service.Settings.QuickSwitchPaths
            .Where(path => versions.Any(v => string.Equals(v.Path, path, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        menu.Items.Add("Cycle quick PHP", null, (_, _) => CycleQuick());
        menu.Items.Add(new ToolStripSeparator());

        if (quick.Count > 0)
        {
            foreach (var path in quick)
            {
                var version = versions.First(v => string.Equals(v.Path, path, StringComparison.OrdinalIgnoreCase));
                menu.Items.Add(BuildVersionItem(version));
            }
        }
        else
        {
            menu.Items.Add("No quick versions selected").Enabled = false;
        }

        var allVersions = new ToolStripMenuItem("All versions");
        foreach (var version in versions)
        {
            allVersions.DropDownItems.Add(BuildVersionItem(version));
        }

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(allVersions);
        menu.Items.Add("Open manager", null, (_, _) => ShowMainForm());
        menu.Items.Add("Refresh", null, (_, _) =>
        {
            service.Reload();
            RebuildMenu();
            mainForm?.RefreshVersions();
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        var oldMenu = trayIcon.ContextMenuStrip;
        trayIcon.ContextMenuStrip = menu;
        oldMenu?.Dispose();
        UpdateTooltip();
    }

    protected override void ExitThreadCore()
    {
        trayIcon.Visible = false;
        trayIcon.Dispose();
        appIcon.Dispose();
        base.ExitThreadCore();
    }

    private ToolStripMenuItem BuildVersionItem(PhpVersion version)
    {
        var item = new ToolStripMenuItem(version.Name, null, (_, _) => SwitchTo(version.Path))
        {
            Checked = string.Equals(version.Path, service.Settings.SelectedPhpPath, StringComparison.OrdinalIgnoreCase)
        };
        return item;
    }

    private void ShowMainForm()
    {
        if (mainForm is null || mainForm.IsDisposed)
        {
            mainForm = new MainForm(service, RebuildMenu);
        }

        mainForm.Show();
        mainForm.WindowState = FormWindowState.Normal;
        mainForm.Activate();
    }

    private void CycleQuick()
    {
        try
        {
            var selected = service.CycleQuick();
            RebuildMenu();
            mainForm?.RefreshVersions();
            ShowBalloon(selected is null ? "No quick versions selected" : $"Switched to {selected.Name}");
        }
        catch (Exception ex)
        {
            ShowBalloon(ex.Message);
        }
    }

    private void SwitchTo(string path)
    {
        try
        {
            service.SwitchTo(path);
            RebuildMenu();
            mainForm?.RefreshVersions();
            ShowBalloon($"Switched to {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            ShowBalloon(ex.Message);
        }
    }

    private void UpdateTooltip()
    {
        var selected = service.Settings.SelectedPhpPath is null ? "None" : Path.GetFileName(service.Settings.SelectedPhpPath);
        var text = $"PHP Manager - {selected}";
        trayIcon.Text = text.Length <= 63 ? text : text[..63];
    }

    private void ShowBalloon(string message)
    {
        trayIcon.BalloonTipTitle = "PHP Manager";
        trayIcon.BalloonTipText = message;
        trayIcon.ShowBalloonTip(2500);
    }
}
