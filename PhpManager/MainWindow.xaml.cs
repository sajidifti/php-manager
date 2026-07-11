using System.Collections.ObjectModel;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;
using Windows.UI.Text;

namespace PhpManager;

public sealed partial class MainWindow : Window
{
    private readonly PhpManagerService service;
    private readonly Action changed;
    private readonly ObservableCollection<VersionItem> versions = [];
    private readonly ObservableCollection<ExtensionItem> extensions = [];
    private bool refreshing;

    public MainWindow(PhpManagerService service, Action changed)
    {
        InitializeComponent();
        this.service = service;
        this.changed = changed;
        VersionsList.ItemsSource = versions;
        ExtensionsList.ItemsSource = extensions;
        Navigation.SelectedItem = VersionsNavigationItem;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        SystemBackdrop = new MicaBackdrop();

        var appWindow = AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this)));
        appWindow.Resize(new Windows.Graphics.SizeInt32(1040, 720));
        appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "php.ico"));
        appWindow.Closing += (_, args) =>
        {
            args.Cancel = true;
            appWindow.Hide();
        };
        RefreshVersions();
    }

    public void ShowAndActivate()
    {
        AppWindow.Show();
        Activate();
    }


    public void RefreshVersions()
    {
        service.Reload();
        refreshing = true;
        RootBox.Text = service.Settings.PhpRoot;
        SelectedLabel.Text = service.Settings.SelectedPhpPath is null ? "Selected: none" : $"Selected: {Path.GetFileName(service.Settings.SelectedPhpPath)}";
        StartupToggle.IsOn = service.Settings.StartWithWindows && StartupManager.IsEnabled();
        versions.Clear();
        foreach (var version in service.ScanVersions())
        {
            versions.Add(new VersionItem(version,
                service.Settings.QuickSwitchPaths.Contains(version.Path, StringComparer.OrdinalIgnoreCase),
                string.Equals(version.Path, service.Settings.SelectedPhpPath, StringComparison.OrdinalIgnoreCase)));
        }
        refreshing = false;
        LoadIniPanel();
    }

    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var ini = (args.SelectedItemContainer?.Tag as string) == "ini";
        VersionsPage.Visibility = ini ? Visibility.Collapsed : Visibility.Visible;
        IniPage.Visibility = ini ? Visibility.Visible : Visibility.Collapsed;
        if (ini) LoadIniPanel();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Safe(RefreshVersions);

    private void SaveRoot_Click(object sender, RoutedEventArgs e) => Safe(() =>
    {
        service.Settings.PhpRoot = RootBox.Text.Trim();
        service.SaveSettings(service.Settings);
        changed();
        RefreshVersions();
    });

    private async void BrowseRoot_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            var folder = await picker.PickSingleFolderAsync();
            if (folder is not null)
            {
                RootBox.Text = folder.Path;
                SaveRoot_Click(sender, e);
            }
        }
        catch (Exception ex)
        {
            await ShowMessageAsync(ex.Message, "PHP Manager error");
        }
    }

    private void QuickCheck_Click(object sender, RoutedEventArgs e)
    {
        if (refreshing) return;
        service.Settings.QuickSwitchPaths = versions.Where(item => item.IsQuick).Select(item => item.Path).ToList();
        service.SaveSettings(service.Settings);
        changed();
    }

    private void Switch_Click(object sender, RoutedEventArgs e) => SwitchSelected();
    private void VersionsList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => SwitchSelected();

    private async void SwitchSelected()
    {
        try
        {
            if (VersionsList.SelectedItem is not VersionItem item)
            {
                await ShowMessageAsync("Select a PHP version first.");
                return;
            }
            service.SwitchTo(item.Path);
            changed();
            RefreshVersions();
        }
        catch (Exception ex) { await ShowMessageAsync(ex.Message, "PHP Manager error"); }
    }

    private async void Cycle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var selected = service.CycleQuick();
            await ShowMessageAsync(selected is null ? "No quick switch versions are checked." : $"Switched to {selected.Name}");
            changed();
            RefreshVersions();
        }
        catch (Exception ex) { await ShowMessageAsync(ex.Message, "PHP Manager error"); }
    }

    private async void ShellStatus_Click(object sender, RoutedEventArgs e) => await ShowMessageAsync(service.GetShellStatus(), "Shell status");
    private void OpenFolder_Click(object sender, RoutedEventArgs e) => Safe(() => PhpManagerService.OpenFolder(AppSettings.RuntimeDirectory));

    private async void MachinePath_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            service.EnsureMachinePath();
            await ShowMessageAsync("The switch folder is now in the machine PATH. Restart terminals that are already open.");
        }
        catch (Exception ex) { await ShowMessageAsync(ex.Message, "PHP Manager error"); }
    }

    private void StartupToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (refreshing) return;
        Safe(() =>
        {
            service.Settings.StartWithWindows = StartupToggle.IsOn;
            service.SaveSettings(service.Settings);
            StartupManager.SetEnabled(StartupToggle.IsOn);
        });
    }

    private void LoadIniPanel()
    {
        extensions.Clear();
        DisabledFunctionsBox.Text = string.Empty;
        var version = CurrentIniVersion();
        SaveIniButton.IsEnabled = version is not null;
        if (version is null) return;
        Safe(() =>
        {
            var ini = new PhpIniService(version.Path);
            foreach (var extension in ini.GetExtensions()) extensions.Add(new ExtensionItem(extension));
            DisabledFunctionsBox.Text = ini.GetDisabledFunctions();
        });
    }

    private void ReloadIni_Click(object sender, RoutedEventArgs e) => LoadIniPanel();
    private void OpenIni_Click(object sender, RoutedEventArgs e) => Safe(() =>
    {
        var version = CurrentIniVersion();
        if (version is null) return;
        var ini = new PhpIniService(version.Path);
        ini.EnsureIni();
        PhpManagerService.OpenFile(ini.IniPath);
    });

    private async void SaveIni_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var version = CurrentIniVersion();
            if (version is null) return;
            new PhpIniService(version.Path).Save(extensions.Select(item => new PhpExtension(item.Name, item.FileName, item.Enabled)), DisabledFunctionsBox.Text);
            await ShowMessageAsync("php.ini saved. A backup was created.");
        }
        catch (Exception ex) { await ShowMessageAsync(ex.Message, "PHP Manager error"); }
    }

    private PhpVersion? CurrentIniVersion() => VersionsList.SelectedItem is VersionItem item
        ? new PhpVersion(item.Path)
        : service.Settings.SelectedPhpPath is null ? null : new PhpVersion(service.Settings.SelectedPhpPath);

    private void Safe(Action action)
    {
        try { action(); }
        catch (Exception ex) { _ = ShowMessageAsync(ex.Message, "PHP Manager error"); }
    }

    private async Task ShowMessageAsync(string message, string title = "PHP Manager")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new ScrollViewer
            {
                Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true },
                MaxHeight = 420
            },
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }
}

public sealed class VersionItem(PhpVersion version, bool isQuick, bool selected)
{
    public string Name { get; } = version.Name;
    public string Path { get; } = version.Path;
    public bool IsQuick { get; set; } = isQuick;
    public FontWeight Weight { get; } = new() { Weight = selected ? (ushort)600 : (ushort)400 };
}

public sealed class ExtensionItem(PhpExtension extension)
{
    public string Name { get; } = extension.Name;
    public string FileName { get; } = extension.FileName;
    public bool Enabled { get; set; } = extension.Enabled;
}
