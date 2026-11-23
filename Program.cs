using System.Runtime.InteropServices;

namespace PowerModeSwitcher;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayAppContext());
    }
}

public class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly AppConfig _config;
    private readonly HotkeyWindow _hotkeyWindow;
    private int _activeModeIndex = -1;

    public TrayAppContext()
    {
        _config = AppConfig.Load();
        _hotkeyWindow = new HotkeyWindow(index => ActivateMode(index, showBalloon: true));

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "PowerModeSwitcher",
            Visible = true
        };

        _trayIcon.DoubleClick += (_, _) => OpenSettings();

        SyncActiveModeFromSystem();
        RebuildTrayMenu();
        RegisterAllHotkeys();
    }

    private void SyncActiveModeFromSystem()
    {
        string currentGuid = PowerManager.GetActivePlanGuid().ToString();
        _activeModeIndex = _config.Modes.FindIndex(m =>
            string.Equals(m.PowerPlanGuid, currentGuid, StringComparison.OrdinalIgnoreCase));
    }

    private void RebuildTrayMenu()
    {
        var menu = new ContextMenuStrip();

        for (int i = 0; i < _config.Modes.Count; i++)
        {
            int index = i;
            var mode = _config.Modes[i];
            var item = new ToolStripMenuItem(mode.Name) { Checked = (i == _activeModeIndex) };
            item.Click += (_, _) => ActivateMode(index, showBalloon: false);
            menu.Items.Add(item);
        }

        menu.Items.Add(new ToolStripSeparator());

        var settingsItem = new ToolStripMenuItem("Settings...");
        settingsItem.Click += (_, _) => OpenSettings();
        menu.Items.Add(settingsItem);

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitApp();
        menu.Items.Add(exitItem);

        _trayIcon.ContextMenuStrip = menu;
    }

    private void ActivateMode(int index, bool showBalloon)
    {
        if (index < 0 || index >= _config.Modes.Count) return;

        var mode = _config.Modes[index];
        PowerManager.ApplyMode(mode, _config.AfterburnerPath);
        _activeModeIndex = index;
        _trayIcon.Text = $"PowerModeSwitcher — {mode.Name}";
        RebuildTrayMenu();

        if (showBalloon)
        {
            _trayIcon.ShowBalloonTip(1500, "Mode Switched", $"Active: {mode.Name}", ToolTipIcon.Info);
        }
    }

    private void RegisterAllHotkeys()
    {
        _hotkeyWindow.UnregisterAll();
        for (int i = 0; i < _config.Modes.Count; i++)
        {
            int vk = _config.Modes[i].HotkeyVirtualKey;
            if (vk > 0) _hotkeyWindow.Register(i, vk);
        }
    }

    private void OpenSettings()
    {
        using var form = new SettingsForm(_config, () =>
        {
            RebuildTrayMenu();
            RegisterAllHotkeys();
        });
        form.ShowDialog();
    }

    private void ExitApp()
    {
        _hotkeyWindow.UnregisterAll();
        _trayIcon.Visible = false;
        Application.Exit();
    }
}

public sealed class HotkeyWindow : NativeWindow, IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly Action<int> _onHotkeyPressed;
    private readonly List<int> _registeredIds = new();

    public HotkeyWindow(Action<int> onHotkeyPressed)
    {
        _onHotkeyPressed = onHotkeyPressed;
        CreateHandle(new CreateParams());
    }

    public void Register(int id, int virtualKey)
    {
        if (RegisterHotKey(Handle, id, MOD_CONTROL | MOD_ALT, (uint)virtualKey))
        {
            _registeredIds.Add(id);
        }
    }

    public void UnregisterAll()
    {
        foreach (int id in _registeredIds) UnregisterHotKey(Handle, id);
        _registeredIds.Clear();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY)
        {
            _onHotkeyPressed(m.WParam.ToInt32());
        }
        base.WndProc(ref m);
    }

    public void Dispose() => UnregisterAll();
}