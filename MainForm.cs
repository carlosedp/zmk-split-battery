using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static ZMKSplit.BatteryMonitor;
using Microsoft.Toolkit.Uwp.Notifications;

namespace ZMKSplit
{
    public partial class MainForm : Form
    {
        private static readonly string RELOAD_BUTTON_STATE_RELOADING = "Refreshing...";
        private static readonly string RELOAD_BUTTON_STATE_READY     = "Refresh Devices";

        private static readonly string CONNECT_BUTTON_CONNECT     = "Connect";
        private static readonly string CONNECT_BUTTON_CONNECTING  = "Connecting...";
        private static readonly string CONNECT_BUTTON_DISCONNECT  = "Disconnect";

        private static readonly string STATUS_CONNECTING        = "Connecting to '{0}'..";
        private static readonly string STATUS_CONNECTING_IN     = "Reconnecting to '{0}' in {1}s..";
        private static readonly string STATUS_CONNECTION_FAILED = "Could not connect to '{0}': {1}";
        private static readonly string STATUS_CONNECTED         = "Connected to {0}";
        private static readonly string STATUS_READY             = "Ready";
        private static readonly string STATUS_COULD_NOT_OPEN_REG_KEY = "Could not open registry key: {0}";

        private static readonly string APP_REG_KEY             = "SOFTWARE\\ZMKSplitBattery";
        private static readonly string APP_REG_START_MINIMIZED = "StartMinimized";
        private static readonly int    BATTERY_LOW_LEVEL_DEFAULT = 20;
        private static readonly string BATTERY_LOW_TIP_TITLE   = "Low battery";
        private static readonly string BATTERY_LOW_TIP_MESSAGE = "{0} battery level is below {1}%";
        private static readonly string BATTERY_NOT_CONNECTED_TITLE = "Not Connected";

        private static readonly int RECONNECT_INTERVAL                  = 300;
        private static readonly int RECONNECT_AFTER_DISCONNECT_INTERVAL = 10;

        // Per-device monitors, keyed by device ID.
        private readonly Dictionary<string, BatteryMonitor> _monitors = new();
        // Device ID -> display name (kept even after disconnect for reconnect labels).
        private readonly Dictionary<string, string> _deviceNames = new();
        // Device ID -> (battery name -> last level) for per-battery low-alert tracking.
        private readonly Dictionary<string, Dictionary<string, int>> _lastBatteryLevels = new();
        // Devices currently waiting to be reconnected after an unexpected BLE drop.
        private readonly HashSet<string> _reconnectingDevices = new();
        // Device ID -> countdown seconds until next reconnect attempt.
        private readonly Dictionary<string, int> _reconnectCounters = new();
        // Device IDs currently in the middle of a connect operation (guards against concurrent retries).
        private readonly HashSet<string> _connectingDevices = new();
        // Ordered list of battery names that have a column in DevicesListView (e.g. "Main", "Peripheral").
        private readonly List<string> _batteryColumnNames = new();

        public MainForm()
        {
            WindowState = FormWindowState.Minimized;
            ShowInTaskbar = false;
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            UpdateTrayIcon();
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += PreferenceChangedHandler;

            StatusLabel.Text = STATUS_READY;
            UpdateConnectButtonText();
            RefreshBatteryButton.Enabled = false;
            ReloadButton.Text = RELOAD_BUTTON_STATE_READY;
            LowBatteryThresholdNumericUpDown.Value = BATTERY_LOW_LEVEL_DEFAULT;
            AutoRunCheckBox.Checked = IsAutoRunEnabled();
            StartMinimizedCheckBox.Checked = IsStartMinimizedEnabled();

            if (IsStartMinimizedEnabled())
                BeginInvoke(new Action(() => Hide()));
            else
            {
                WindowState = FormWindowState.Normal;
                ShowInTaskbar = true;
            }

            ListBLEDevices(); // scans and auto-connects to all available keyboards
        }

        private void ListBLEDevices()
        {
            DevicesListView.Items.Clear();
            // Remove all dynamic battery columns; keep only col 0 (name).
            while (DevicesListView.Columns.Count > 1)
                DevicesListView.Columns.RemoveAt(1);
            _batteryColumnNames.Clear();
            // Re-add battery columns for already-connected devices so they survive a refresh.
            foreach (var (deviceID, monitor) in _monitors)
                if (monitor.IsConnected())
                    EnsureBatteryColumns(monitor.Batteries.Values.Select(b => b.Name));

            ReloadButton.Enabled = false;
            ReloadButton.Text = RELOAD_BUTTON_STATE_RELOADING;

            // Collect all discovered devices here so AutoConnectAllDevices doesn't
            // have to race against BeginInvoke UI updates that may not have run yet.
            var discovered = new List<(string Name, string ID)>();

            BatteryMonitor.ListPairedDevices((string deviceName, string deviceID) =>
            {
                lock (discovered) discovered.Add((deviceName, deviceID));
                BeginInvoke(new Action(() =>
                {
                    DevicesListView.BeginUpdate();
                    bool connected = _monitors.TryGetValue(deviceID, out var m) && m.IsConnected();
                    var item = new ListViewItem
                    {
                        Text = connected ? "\u2713 " + deviceName : deviceName,
                        Tag  = deviceID
                    };
                    DevicesListView.Items.Add(item);
                    if (connected) UpdateDeviceListBatteryValues(deviceID);
                    DevicesListView.EndUpdate();
                }));
            }, () =>
            {
                List<(string Name, string ID)> snapshot;
                lock (discovered) snapshot = discovered.ToList();
                BeginInvoke(new Action(async () =>
                {
                    AutoResizeListViewColumns();
                    ReloadButton.Enabled = true;
                    ReloadButton.Text = RELOAD_BUTTON_STATE_READY;
                    UpdateConnectButtonText();
                    await AutoConnectAllDevices(snapshot);
                }));
            });
        }

        private async Task AutoConnectAllDevices(List<(string Name, string ID)> devices)
        {
            var tasks = new List<Task>();
            foreach (var (deviceName, deviceID) in devices)
            {
                if (_monitors.ContainsKey(deviceID) || _connectingDevices.Contains(deviceID)) continue;
                tasks.Add(ConnectToDevice(deviceName, deviceID, silent: true));
            }
            await Task.WhenAll(tasks);
        }

        // ── Connection management ─────────────────────────────────────────────────

        private async Task<bool> ConnectToDevice(string deviceName, string deviceID, bool silent = false)
        {
            if (_connectingDevices.Contains(deviceID)) return false;
            _connectingDevices.Add(deviceID);
            try
            {
                _deviceNames[deviceID] = deviceName;
                if (!silent) StatusLabel.Text = String.Format(STATUS_CONNECTING, deviceName);

                bool isReconnect = _monitors.TryGetValue(deviceID, out var monitor);
                if (!isReconnect)
                {
                    monitor = new BatteryMonitor(
                        () => OnBatteryLevelChangedForDevice(deviceID),
                        () => OnDeviceNeedsReconnectForDevice(deviceID));
                }

                var res = await monitor!.Connect(deviceName, deviceID);

                string errorDetail = res.Status switch
                {
                    ConnectStatus.DeviceNotFound => res.ErrorMessage.Length > 0
                        ? res.ErrorMessage : "Device not found",
                    ConnectStatus.BatteryServiceNotFound => "Battery service not found",
                    ConnectStatus.BatteryLevelCharacteristicNotFound =>
                        "Could not find battery level characteristic (device offline?)",
                    ConnectStatus.SubscribtionFailure =>
                        "Could not subscribe to battery level notifications",
                    _ => ""
                };

                if (errorDetail.Length > 0)
                {
                    if (!silent) StatusLabel.Text = String.Format(STATUS_CONNECTION_FAILED, deviceName, errorDetail);
                    if (!isReconnect) monitor.Dispose();
                }
                else
                {
                    if (!isReconnect) _monitors[deviceID] = monitor;

                    bool wasReconnecting = _reconnectingDevices.Remove(deviceID);
                    _reconnectCounters.Remove(deviceID);

                    StatusLabel.Text = String.Format(STATUS_CONNECTED, deviceName);
                    UpdateNameColumnConnected(deviceID, true);
                    UpdateDeviceListBatteryValues(deviceID);
                    UpdateConnectButtonText();
                    RefreshBatteryButton.Enabled = true;
                    _lastBatteryLevels.Remove(deviceID);
                    UpdateWindowTitle();
                    UpdateTrayIcon();

                    PollingTimer.Interval = (int)RefreshIntervalNumericUpDown.Value * 60 * 1000;
                    if (!PollingTimer.Enabled) PollingTimer.Start();

                    if (wasReconnecting)
                    {
                        new ToastContentBuilder()
                            .AddText("Keyboard reconnected")
                            .AddText($"{deviceName} is back online.")
                            .Show();
                    }
                    if (AutoRunCheckBox.Checked) SetAutoRunEnabled(true);
                }

                return res.Status == ConnectStatus.Connected;
            }
            finally
            {
                _connectingDevices.Remove(deviceID);
            }
        }

        private async Task<bool> ConnectToSelectedDevice()
        {
            if (DevicesListView.SelectedItems.Count == 0) return false;
            string deviceName = DevicesListView.SelectedItems[0].Text;
            string? deviceID  = (string?)DevicesListView.SelectedItems[0].Tag;
            return deviceID != null && await ConnectToDevice(deviceName, deviceID);
        }

        private void DisconnectDevice(string deviceID)
        {
            _reconnectingDevices.Remove(deviceID);
            _reconnectCounters.Remove(deviceID);

            if (_monitors.TryGetValue(deviceID, out var monitor))
            {
                monitor.Dispose();
                _monitors.Remove(deviceID);
            }

            _lastBatteryLevels.Remove(deviceID);
            UpdateNameColumnConnected(deviceID, false);
            ClearDeviceBatteryDisplay(deviceID);
            UpdateConnectButtonText();
            RefreshBatteryButton.Enabled = _monitors.Values.Any(m => m.IsConnected());
            UpdateWindowTitle();
            UpdateTrayIcon();

            if (!_monitors.Values.Any(m => m.IsConnected()))
            {
                PollingTimer.Stop();
                StatusLabel.Text = STATUS_READY;
            }
            if (!_reconnectingDevices.Any())
                ReconnectTimer.Stop();
        }

        // ── BatteryMonitor callbacks ──────────────────────────────────────────────

        private void OnBatteryLevelChangedForDevice(string deviceID)
        {
            BeginInvoke(new Action(() =>
            {
                UpdateDeviceListBatteryValues(deviceID);
                UpdateTrayIcon();
            }));
        }

        private void OnDeviceNeedsReconnectForDevice(string deviceID)
        {
            BeginInvoke(new Action(() =>
            {
                if (!_monitors.ContainsKey(deviceID)) return;

                _reconnectingDevices.Add(deviceID);
                _reconnectCounters[deviceID] = RECONNECT_AFTER_DISCONNECT_INTERVAL;
                _lastBatteryLevels.Remove(deviceID);

                UpdateNameColumnConnected(deviceID, false);
                ClearDeviceBatteryDisplay(deviceID);
                UpdateConnectButtonText();
                RefreshBatteryButton.Enabled = _monitors.Values.Any(m => m.IsConnected());
                UpdateWindowTitle();
                UpdateTrayIcon();

                if (!ReconnectTimer.Enabled) ReconnectTimer.Start();
            }));
        }

        // ── UI helpers ────────────────────────────────────────────────────────────

        private void UpdateWindowTitle()
        {
            var names = _monitors
                .Where(kv => kv.Value.IsConnected() && _deviceNames.ContainsKey(kv.Key))
                .Select(kv => _deviceNames[kv.Key])
                .ToList();
            Text = names.Count switch
            {
                0 => "ZMK Split Battery Status",
                1 => $"ZMK Split Battery \u2013 {names[0]}",
                _ => $"ZMK Split Battery \u2013 {names.Count} keyboards"
            };
        }

        private void UpdateNameColumnConnected(string deviceID, bool connected)
        {
            string name = _deviceNames.TryGetValue(deviceID, out string? n) ? n! : deviceID;
            DevicesListView.BeginUpdate();
            foreach (ListViewItem lvi in DevicesListView.Items)
            {
                if ((string?)lvi.Tag == deviceID)
                {
                    lvi.Text = connected ? "\u2713 " + name : name;
                    break;
                }
            }
            DevicesListView.EndUpdate();
            AutoResizeListViewColumns();
        }

        private void UpdateDeviceListBatteryValues(string deviceID)
        {
            if (!_monitors.TryGetValue(deviceID, out var monitor) || !monitor.IsConnected()) return;

            ListViewItem? item = null;
            foreach (ListViewItem lvi in DevicesListView.Items)
                if ((string?)lvi.Tag == deviceID) { item = lvi; break; }
            if (item == null) return;

            EnsureBatteryColumns(monitor.Batteries.Values.Select(b => b.Name));

            DevicesListView.BeginUpdate();
            while (item.SubItems.Count < 1 + _batteryColumnNames.Count)
                item.SubItems.Add("");
            foreach (var battery in monitor.Batteries.Values)
            {
                int colIdx = _batteryColumnNames.IndexOf(battery.Name);
                if (colIdx < 0) continue;
                item.SubItems[1 + colIdx].Text = battery.Level >= 0 ? $"{battery.Level}%" : "--";
            }
            DevicesListView.EndUpdate();
            AutoResizeListViewColumns();
        }

        private void ClearDeviceBatteryDisplay(string deviceID)
        {
            DevicesListView.BeginUpdate();
            foreach (ListViewItem lvi in DevicesListView.Items)
            {
                if ((string?)lvi.Tag == deviceID)
                {
                    for (int i = 1; i < lvi.SubItems.Count; i++)
                        lvi.SubItems[i].Text = "";
                    break;
                }
            }
            DevicesListView.EndUpdate();
        }

        private void EnsureBatteryColumns(IEnumerable<string> batteryNames)
        {
            bool added = false;
            foreach (var name in batteryNames)
            {
                if (_batteryColumnNames.Contains(name)) continue;
                _batteryColumnNames.Add(name);
                var col = new ColumnHeader { Text = name, TextAlign = HorizontalAlignment.Right };
                DevicesListView.Columns.Add(col);
                // Pad all existing rows with an empty subitem for the new column.
                foreach (ListViewItem lvi in DevicesListView.Items)
                    while (lvi.SubItems.Count < 1 + _batteryColumnNames.Count)
                        lvi.SubItems.Add("");
                added = true;
            }
            if (added) AutoResizeListViewColumns();
        }

        private void AutoResizeListViewColumns()
        {
            int total = DevicesListView.ClientSize.Width;
            int batteryCols = DevicesListView.Columns.Count - 1; // exclude name col
            if (batteryCols <= 0)
            {
                DevicesListView.Columns[0].Width = total;
                return;
            }
            // Name = 50%, each battery column shares the remaining 50% equally.
            int batteryWidth = total / 2 / batteryCols;
            DevicesListView.Columns[0].Width = total - batteryWidth * batteryCols;
            for (int i = 1; i < DevicesListView.Columns.Count; i++)
                DevicesListView.Columns[i].Width = batteryWidth;
        }

        private void UpdateConnectButtonText()
        {
            if (DevicesListView.SelectedItems.Count == 0)
            {
                ConnectButton.Text = CONNECT_BUTTON_CONNECT;
                ConnectButton.Enabled = false;
                return;
            }
            ConnectButton.Enabled = true;
            string? selectedID = (string?)DevicesListView.SelectedItems[0].Tag;
            bool isConnected = selectedID != null
                && _monitors.TryGetValue(selectedID, out var m)
                && m.IsConnected();
            ConnectButton.Text = isConnected ? CONNECT_BUTTON_DISCONNECT : CONNECT_BUTTON_CONNECT;
        }

        public void UpdateTrayIcon()
        {
            int minLevel = int.MaxValue;
            var lines = new List<string>();
            int threshold = (int)LowBatteryThresholdNumericUpDown.Value;

            foreach (var (deviceID, monitor) in _monitors)
            {
                if (!monitor.IsConnected() || monitor.Batteries.Count == 0) continue;
                string name = _deviceNames.TryGetValue(deviceID, out string? n) ? n! : deviceID;

                if (!_lastBatteryLevels.TryGetValue(deviceID, out var lastLevels))
                    lastLevels = new Dictionary<string, int>();

                var parts = new List<string>();
                foreach (var battery in monitor.Batteries.Values)
                {
                    if (battery.Level >= 0) minLevel = Math.Min(minLevel, battery.Level);
                    string lvlStr = battery.Level >= 0 ? $"{battery.Level}%" : "--";
                    parts.Add($"{battery.Name[0]}:{lvlStr}");

                    lastLevels.TryGetValue(battery.Name, out int lastLevel);
                    if (lastLevel > threshold && battery.Level >= 0 && battery.Level <= threshold)
                    {
                        new ToastContentBuilder()
                            .AddText(BATTERY_LOW_TIP_TITLE)
                            .AddText(String.Format(BATTERY_LOW_TIP_MESSAGE,
                                $"{name} ({battery.Name})", threshold))
                            .Show();
                    }
                    lastLevels[battery.Name] = battery.Level;
                }
                _lastBatteryLevels[deviceID] = lastLevels;
                lines.Add($"{name}: {string.Join(" ", parts)}");
            }

            if (minLevel == int.MaxValue)
            {
                minLevel = -1;
                NotifyIcon.Text = BATTERY_NOT_CONNECTED_TITLE;
            }
            else
            {
                // Greedily fit complete lines within the 63-char Windows tooltip limit.
                // The first keyboard is always shown in full (truncated at 63 only if its
                // own line exceeds the limit).
                var sb = new StringBuilder();
                foreach (var line in lines)
                {
                    if (sb.Length == 0)
                    {
                        sb.Append(line.Length > 63 ? line[..63] : line);
                    }
                    else
                    {
                        if (sb.Length + 1 + line.Length > 63) break;
                        sb.Append('\n').Append(line);
                    }
                }
                NotifyIcon.Text = sb.ToString();
            }

            NotifyIcon.Icon = GetBatteryIcon(minLevel);
            if (minLevel != -1)
                LastUpdatedLabel.Text = "Updated: " + DateTime.Now.ToString("HH:mm:ss");
        }

        // ── Registry helpers ──────────────────────────────────────────────────────

        private bool IsAutoRunEnabled()
        {
            var keyPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
            var keyOpt = Registry.CurrentUser.OpenSubKey(keyPath);
            if (keyOpt is RegistryKey key)
                return key.GetValue(Application.ProductName) is string;
            StatusLabel.Text = String.Format(STATUS_COULD_NOT_OPEN_REG_KEY, keyPath);
            return false;
        }

        private bool SetAutoRunEnabled(bool enabled)
        {
            var keyPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
            var keyOpt = Registry.CurrentUser.OpenSubKey(keyPath, true);
            if (keyOpt is RegistryKey key)
            {
                if (enabled)
                    key.SetValue(Application.ProductName, $"\"{Application.ExecutablePath}\"");
                else if (Application.ProductName != null)
                    key.DeleteValue(Application.ProductName, false);
                return true;
            }
            StatusLabel.Text = String.Format(STATUS_COULD_NOT_OPEN_REG_KEY, keyPath);
            return false;
        }

        private bool IsStartMinimizedEnabled()
        {
            var keyOpt = Registry.CurrentUser.OpenSubKey(APP_REG_KEY);
            if (keyOpt is RegistryKey key)
                return key.GetValue(APP_REG_START_MINIMIZED) is int val && val != 0;
            return false;
        }

        private void SetStartMinimizedEnabled(bool enabled)
        {
            var keyOpt = Registry.CurrentUser.CreateSubKey(APP_REG_KEY);
            if (keyOpt is RegistryKey key)
                key.SetValue(APP_REG_START_MINIMIZED, enabled ? 1 : 0, RegistryValueKind.DWord);
        }

        // ── Theme / icon helpers ──────────────────────────────────────────────────

        private void PreferenceChangedHandler(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General)
                UpdateTrayIcon();
        }

        private bool IsWindowsThemeLight()
        {
            var keyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize";
            var keyOpt = Registry.CurrentUser.OpenSubKey(keyPath);
            if (keyOpt is RegistryKey key && key.GetValue("SystemUsesLightTheme") is int val)
                return val != 0;
            return true;
        }

        private Icon GetBatteryIcon(int pcnt)
        {
            string iconName = IsWindowsThemeLight() ? "black-" : "white-";
            if (pcnt == -1)
                iconName += "dsc";
            else
            {
                pcnt = ((int)Math.Round(pcnt / 10.0)) * 10;
                iconName += pcnt.ToString("d3");
            }
            return (Icon)ZMKSplit.Properties.Resources.ResourceManager
                .GetObject(iconName, ZMKSplit.Properties.Resources.Culture)!;
        }

        // ── Event handlers ────────────────────────────────────────────────────────

        private void ReloadButton_MouseClick(object sender, MouseEventArgs e) => ListBLEDevices();

        private void DevicesListView_DoubleClick(object sender, EventArgs e)
        {
            if (DevicesListView.SelectedItems.Count == 0) return;
            string? selectedID = (string?)DevicesListView.SelectedItems[0].Tag;
            bool isConnected = selectedID != null
                && _monitors.TryGetValue(selectedID, out var m) && m.IsConnected();
            if (ConnectButton.Enabled && !isConnected)
                ConnectButton_Click(sender, e);
        }

        private void DevicesListView_SelectedIndexChanged(object sender, EventArgs e)
            => UpdateConnectButtonText();

        private void DevicesListView_Resize(object sender, EventArgs e)
            => AutoResizeListViewColumns();

        private async void ConnectButton_Click(object sender, EventArgs e)
        {
            if (DevicesListView.SelectedItems.Count == 0) return;
            ConnectButton.Enabled = false;

            string deviceName = DevicesListView.SelectedItems[0].Text;
            string? deviceID  = (string?)DevicesListView.SelectedItems[0].Tag;
            if (deviceID == null) { UpdateConnectButtonText(); ConnectButton.Enabled = true; return; }

            bool isConnected = _monitors.TryGetValue(deviceID, out var mon) && mon.IsConnected();
            if (isConnected)
            {
                DisconnectDevice(deviceID);
            }
            else
            {
                ConnectButton.Text = CONNECT_BUTTON_CONNECTING;
                await ConnectToDevice(deviceName, deviceID);
            }
            UpdateConnectButtonText();
            ConnectButton.Enabled = DevicesListView.SelectedItems.Count > 0;
        }

        private async void PollingTimer_Tick(object sender, EventArgs e)
        {
            foreach (var (deviceID, monitor) in _monitors.ToList())
            {
                if (!monitor.IsConnected()) continue;
                bool success = await monitor.RefreshBatteryLevels();
                if (success) UpdateDeviceListBatteryValues(deviceID);
            }
            UpdateTrayIcon();
        }

        private void RefreshIntervalNumericUpDown_ValueChanged(object sender, EventArgs e)
            => PollingTimer.Interval = (int)RefreshIntervalNumericUpDown.Value * 60 * 1000;

        private async void RefreshNowContextMenuItem_Click(object sender, EventArgs e)
        {
            foreach (var (deviceID, monitor) in _monitors.ToList())
            {
                if (!monitor.IsConnected()) continue;
                bool success = await monitor.RefreshBatteryLevels();
                if (success) UpdateDeviceListBatteryValues(deviceID);
            }
            UpdateTrayIcon();
        }

        private async void RefreshBatteryButton_Click(object sender, EventArgs e)
        {
            RefreshBatteryButton.Enabled = false;
            foreach (var (deviceID, monitor) in _monitors.ToList())
            {
                if (!monitor.IsConnected()) continue;
                bool success = await monitor.RefreshBatteryLevels();
                if (success) UpdateDeviceListBatteryValues(deviceID);
            }
            UpdateTrayIcon();
            RefreshBatteryButton.Enabled = _monitors.Values.Any(m => m.IsConnected());
        }

        private void DisconnectContextMenuItem_Click(object sender, EventArgs e)
        {
            ReconnectTimer.Stop();
            _reconnectingDevices.Clear();
            _reconnectCounters.Clear();
            foreach (var deviceID in _monitors.Keys.ToList())
                DisconnectDevice(deviceID);
        }

        private void TrayContextMenu_Opening(object sender, CancelEventArgs e)
        {
            bool anyConnected = _monitors.Values.Any(m => m.IsConnected());
            refreshNowContextMenuItem.Enabled = anyConnected;
            disconnectContextMenuItem.Enabled = anyConnected;
        }

        private void ExitContextMenuItem_Click(object sender, EventArgs e) => Application.Exit();

        private void ShowContextMenuItem_Click(object sender, EventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
            ShowInTaskbar = true;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            Microsoft.Win32.SystemEvents.UserPreferenceChanged -= PreferenceChangedHandler;
            BatteryMonitor.StopDeviceWatcher();
            foreach (var monitor in _monitors.Values) monitor.Dispose();
            _monitors.Clear();
        }

        private async void ReconnectTimer_Tick(object sender, EventArgs e)
        {
            var readyToReconnect = new List<string>();

            foreach (var deviceID in _reconnectingDevices.ToList())
            {
                if (!_reconnectCounters.TryGetValue(deviceID, out int counter)) continue;
                counter--;
                if (counter <= 0)
                {
                    _reconnectCounters.Remove(deviceID);
                    readyToReconnect.Add(deviceID);
                }
                else
                {
                    _reconnectCounters[deviceID] = counter;
                    if (_deviceNames.TryGetValue(deviceID, out string? name))
                        StatusLabel.Text = String.Format(STATUS_CONNECTING_IN, name, counter);
                }
            }

            if (!_reconnectingDevices.Any()) ReconnectTimer.Stop();

            foreach (var deviceID in readyToReconnect)
            {
                _reconnectingDevices.Remove(deviceID);
                if (!_deviceNames.TryGetValue(deviceID, out string? name)) continue;
                bool ok = await ConnectToDevice(name, deviceID);
                if (!ok)
                {
                    _reconnectingDevices.Add(deviceID);
                    _reconnectCounters[deviceID] = RECONNECT_INTERVAL;
                    if (!ReconnectTimer.Enabled) ReconnectTimer.Start();
                }
            }
        }

        private void AutoRunCheckBox_CheckedChanged(object sender, EventArgs e)
            => SetAutoRunEnabled(AutoRunCheckBox.Checked);

        private void StartMinimizedCheckBox_CheckedChanged(object sender, EventArgs e)
            => SetStartMinimizedEnabled(StartMinimizedCheckBox.Checked);

        private void NotifyIcon_MouseDoubleClick(object sender, MouseEventArgs e)
            => ShowContextMenuItem_Click(sender, e);
    }
}
