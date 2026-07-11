namespace PhpManager;

public sealed class MainForm : Form
{
    private readonly PhpManagerService service;
    private readonly Action changed;
    private readonly TextBox rootBox = new();
    private readonly ListView versionsList = new();
    private readonly CheckedListBox extensionsList = new();
    private readonly TextBox disabledFunctionsBox = new();
    private readonly Label selectedLabel = new();
    private readonly Button saveIniButton = new();
    private readonly CheckBox startWithWindowsBox = new();
    private bool refreshing;

    public MainForm(PhpManagerService service, Action changed)
    {
        this.service = service;
        this.changed = changed;

        Text = "PHP Manager";
        Width = 980;
        Height = 680;
        MinimumSize = new Size(780, 520);
        StartPosition = FormStartPosition.CenterScreen;

        BuildUi();
        RefreshVersions();
    }

    public void RefreshVersions()
    {
        service.Reload();
        refreshing = true;
        rootBox.Text = service.Settings.PhpRoot;
        selectedLabel.Text = service.Settings.SelectedPhpPath is null
            ? "Selected: none"
            : $"Selected: {Path.GetFileName(service.Settings.SelectedPhpPath)}";
        startWithWindowsBox.Checked = service.Settings.StartWithWindows && StartupManager.IsEnabled();

        versionsList.Items.Clear();
        foreach (var version in service.ScanVersions())
        {
            var item = new ListViewItem(version.Name) { Tag = version };
            item.SubItems.Add(version.Path);
            item.Checked = service.Settings.QuickSwitchPaths.Any(path => string.Equals(path, version.Path, StringComparison.OrdinalIgnoreCase));
            item.Font = string.Equals(version.Path, service.Settings.SelectedPhpPath, StringComparison.OrdinalIgnoreCase)
                ? new Font(versionsList.Font, FontStyle.Bold)
                : versionsList.Font;
            versionsList.Items.Add(item);
        }
        refreshing = false;

        LoadIniPanel();
    }

    private void BuildUi()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildVersionsTab());
        tabs.TabPages.Add(BuildIniTab());
        Controls.Add(tabs);
    }

    private TabPage BuildVersionsTab()
    {
        var page = new TabPage("Versions");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12) };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var rootPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 8),
            MinimumSize = new Size(0, 36)
        };
        rootPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        rootPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        rootPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rootPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        rootPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        rootPanel.Controls.Add(new Label
        {
            Text = "PHP root",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 0, 8, 0)
        }, 0, 0);
        rootBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        rootBox.Margin = new Padding(0, 5, 8, 5);
        rootPanel.Controls.Add(rootBox, 1, 0);
        var saveRootButton = Button("Save root", () =>
        {
            SaveRoot();
            RefreshVersions();
        });
        saveRootButton.Anchor = AnchorStyles.None;
        saveRootButton.Margin = new Padding(0, 3, 8, 3);
        var browseButton = Button("Browse", BrowseRoot);
        browseButton.Anchor = AnchorStyles.None;
        browseButton.Margin = new Padding(0, 3, 0, 3);
        rootPanel.Controls.Add(saveRootButton, 2, 0);
        rootPanel.Controls.Add(browseButton, 3, 0);

        versionsList.Dock = DockStyle.Fill;
        versionsList.View = View.Details;
        versionsList.CheckBoxes = true;
        versionsList.FullRowSelect = true;
        versionsList.Columns.Add("Version", 300);
        versionsList.Columns.Add("Path", 600);
        versionsList.ClientSizeChanged += (_, _) => ResizeVersionColumns();
        versionsList.ItemCheck += (_, e) => BeginInvoke(() => SaveQuickSwitch(versionsList.Items[e.Index], e.NewValue));
        versionsList.SelectedIndexChanged += (_, _) => LoadIniPanel();
        versionsList.DoubleClick += (_, _) => SetSelected();

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Margin = new Padding(0, 6, 0, 0)
        };
        actions.Controls.Add(Button("Refresh", RefreshVersions));
        actions.Controls.Add(Button("Switch to selected", SetSelected));
        actions.Controls.Add(Button("Cycle quick", CycleQuick));
        actions.Controls.Add(Button("Activate user PATH", AddUserPath));
        actions.Controls.Add(Button("Activate machine PATH", AddMachinePath));
        actions.Controls.Add(Button("Shell status", ShowShellStatus));
        actions.Controls.Add(Button("Open switch folder", () => PhpManagerService.OpenFolder(AppSettings.RuntimeDirectory)));
        startWithWindowsBox.Text = "Start with Windows";
        startWithWindowsBox.AutoSize = true;
        startWithWindowsBox.CheckedChanged += (_, _) => SaveStartupSetting();
        actions.Controls.Add(startWithWindowsBox);

        selectedLabel.AutoSize = true;
        selectedLabel.Padding = new Padding(0, 10, 0, 0);

        layout.Controls.Add(rootPanel, 0, 0);
        layout.Controls.Add(versionsList, 0, 1);
        layout.Controls.Add(actions, 0, 2);
        layout.Controls.Add(selectedLabel, 0, 3);
        page.Controls.Add(layout);
        page.HandleCreated += (_, _) => BeginInvoke(ResizeVersionColumns);
        return page;
    }

    private void ResizeVersionColumns()
    {
        if (versionsList.Columns.Count < 2 || versionsList.ClientSize.Width <= 0)
        {
            return;
        }

        var availableWidth = Math.Max(400, versionsList.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4);
        var versionWidth = Math.Clamp((int)(availableWidth * 0.34), 280, 340);
        versionsList.Columns[0].Width = versionWidth;
        versionsList.Columns[1].Width = Math.Max(120, availableWidth - versionWidth);
    }

    private TabPage BuildIniTab()
    {
        var page = new TabPage("php.ini");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12) };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        actions.Controls.Add(Button("Open php.ini", OpenIni));
        actions.Controls.Add(Button("Reload", LoadIniPanel));
        saveIniButton.Text = "Save php.ini changes";
        saveIniButton.AutoSize = true;
        saveIniButton.Click += (_, _) => SaveIni();
        actions.Controls.Add(saveIniButton);

        extensionsList.Dock = DockStyle.Fill;
        extensionsList.CheckOnClick = true;

        var disabledLabel = new Label { Text = "Disabled functions", AutoSize = true, Padding = new Padding(0, 8, 0, 2) };
        disabledFunctionsBox.Dock = DockStyle.Fill;

        layout.Controls.Add(actions, 0, 0);
        layout.Controls.Add(extensionsList, 0, 1);
        layout.Controls.Add(disabledLabel, 0, 2);
        layout.Controls.Add(disabledFunctionsBox, 0, 3);
        page.Controls.Add(layout);
        return page;
    }

    private Button Button(string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true, Margin = new Padding(0, 0, 8, 8) };
        button.Click += (_, _) => Safe(action);
        return button;
    }

    private void BrowseRoot()
    {
        using var dialog = new FolderBrowserDialog { SelectedPath = Directory.Exists(rootBox.Text) ? rootBox.Text : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            rootBox.Text = dialog.SelectedPath;
            SaveRoot();
            RefreshVersions();
        }
    }

    private void SaveRoot()
    {
        var settings = service.Settings;
        settings.PhpRoot = rootBox.Text.Trim();
        settings.Save();
        changed();
    }

    private void SaveQuickSwitch(ListViewItem changedItem, CheckState newValue)
    {
        if (refreshing)
        {
            return;
        }

        var settings = service.Settings;
        settings.QuickSwitchPaths = versionsList.Items.Cast<ListViewItem>()
            .Where(item => (ReferenceEquals(item, changedItem) ? newValue == CheckState.Checked : item.Checked) && item.Tag is PhpVersion)
            .Select(item => ((PhpVersion)item.Tag!).Path)
            .ToList();
        service.SaveSettings(settings);
        changed();
    }

    private void SetSelected()
    {
        var version = CurrentVersion();
        if (version is null)
        {
            MessageBox.Show(this, "Select a PHP version first.", "PHP Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        service.SwitchTo(version.Path);
        changed();
        RefreshVersions();
    }

    private void CycleQuick()
    {
        var version = service.CycleQuick();
        MessageBox.Show(this, version is null ? "No quick switch versions are checked." : $"Switched to {version.Name}", "PHP Manager");
        changed();
        RefreshVersions();
    }

    private void AddUserPath()
    {
        service.EnsureUserPath();
        MessageBox.Show(this, "The PHP Manager shim folder is now in your user PATH. Restart terminals that are already open.", "PHP Manager");
    }

    private void AddMachinePath()
    {
        try
        {
            service.EnsureMachinePath();
            MessageBox.Show(this, "The PHP Manager shim folder is now in the machine PATH. Restart terminals that are already open.", "PHP Manager");
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show(this, "Machine PATH needs administrator permission. Use user PATH or run PHP Manager as Administrator.", "PHP Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ShowShellStatus()
    {
        service.EnsureSwitchDirectory();
        MessageBox.Show(this, service.GetShellStatus(), "PHP Manager shell status", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SaveStartupSetting()
    {
        if (refreshing)
        {
            return;
        }

        var settings = service.Settings;
        settings.StartWithWindows = startWithWindowsBox.Checked;
        service.SaveSettings(settings);
        StartupManager.SetEnabled(settings.StartWithWindows);
    }

    private void LoadIniPanel()
    {
        extensionsList.Items.Clear();
        disabledFunctionsBox.Clear();

        var version = CurrentVersion() ?? SelectedVersionFromSettings();
        if (version is null)
        {
            saveIniButton.Enabled = false;
            return;
        }

        try
        {
            var ini = new PhpIniService(version.Path);
            foreach (var extension in ini.GetExtensions())
            {
                extensionsList.Items.Add(extension, extension.Enabled);
            }
            disabledFunctionsBox.Text = ini.GetDisabledFunctions();
            saveIniButton.Enabled = true;
        }
        catch (Exception ex)
        {
            saveIniButton.Enabled = false;
            MessageBox.Show(this, ex.Message, "php.ini", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OpenIni()
    {
        var version = CurrentVersion() ?? SelectedVersionFromSettings();
        if (version is null)
        {
            return;
        }

        var ini = new PhpIniService(version.Path);
        ini.EnsureIni();
        PhpManagerService.OpenFile(ini.IniPath);
    }

    private void SaveIni()
    {
        var version = CurrentVersion() ?? SelectedVersionFromSettings();
        if (version is null)
        {
            return;
        }

        var extensions = extensionsList.Items.Cast<PhpExtension>()
            .Select((extension, index) => extension with { Enabled = extensionsList.GetItemChecked(index) })
            .ToList();

        new PhpIniService(version.Path).Save(extensions, disabledFunctionsBox.Text);
        MessageBox.Show(this, "php.ini saved.", "PHP Manager");
    }

    private PhpVersion? CurrentVersion()
    {
        return versionsList.SelectedItems.Count == 0 ? null : versionsList.SelectedItems[0].Tag as PhpVersion;
    }

    private PhpVersion? SelectedVersionFromSettings()
    {
        return service.Settings.SelectedPhpPath is null ? null : new PhpVersion(service.Settings.SelectedPhpPath);
    }

    private void Safe(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "PHP Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
