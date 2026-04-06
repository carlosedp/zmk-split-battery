using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using static ZMKSplit.BatteryMonitor;
using Microsoft.Toolkit.Uwp.Notifications;

namespace ZMKSplit
{
    public partial class MainForm : Form
    {
        public static readonly String RELOAD_BUTTON_STATE_RELOADING = "Looking for devices..";
        public static readonly String RELOAD_BUTTON_STATE_READY = "Reload Devices";

        public static readonly String CONNECT_BUTTON_CONNECT = "Connect";
        public static readonly String CONNECT_BUTTON_DISCONNECT = "Disconnect";

        public static readonly String STATUS_CONNECTING = "Connecting to '{0}'..";
        public static readonly String STATUS_CONNECTING_IN = "Connecting to '{0}' in {1} seconds..";
        public static readonly String STATUS_CONNECTION_FAILED = "Could not connected to '{0}': {1}";
        public static readonly String STATUS_CONNECTED = "Connected to {0}";
        public static readonly String STATUS_READY = "Ready";
        public static readonly String STATUS_READ_BATTERY_LEVEL_FAILED = "Could not read battery level: {0}";
        public static readonly String STATUS_COULD_NOT_OPEN_REG_KEY = "Could not open registry key: {0}";
        private static readonly string APP_REG_KEY = "SOFTWARE\\ZMKSplitBattery";
        private static readonly string APP_REG_START_MINIMIZED = "StartMinimized";
        public static readonly int    BATTERY_LOW_LEVEL_THRESHOLD = 20;
        public static readonly string BATTERY_LOW_TIP_TITLE = "Low battery";
        public static readonly string BATTERY_LOW_TIP_MESSAGE = "{0} battery level is below " + BATTERY_LOW_LEVEL_THRESHOLD + "%";
        public static readonly string BATTERY_NOT_CONNECTED_TITLE = "Not Connected";

        public static readonly int    RECONNECT_INTERVAL = 300;
        public static readonly int    RECONNECT_AFTER_DISCONNECT_INTERVAL = 10;

        public static readonly string STARTUP_ARG_DEVICE_NAME = "-devicename";
        public static readonly string STARTUP_ARG_DEVICE_ID = "-deviceid";

        private BatteryMonitor _batteryMonitor;
        private string _deviceName = "";
        private string _deviceID = "";
        private int _lastMinLevel = -1;
        private int _reconnectCounter = RECONNECT_INTERVAL;
        private bool _isReconnecting = false;

        public MainForm()
        {
            _batteryMonitor = new BatteryMonitor(OnBatteryLevelChanged, OnDeviceNeedsReconnect);
            WindowState = FormWindowState.Minimized;
            ShowInTaskbar = false;
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            UpdateTrayIcon();

            Microsoft.Win32.SystemEvents.UserPreferenceChanged += new Microsoft.Win32.UserPreferenceChangedEventHandler(PreferenceChangedHandler);

            StatusLabel.Text = STATUS_READY;
            ConnectButton.Text = CONNECT_BUTTON_CONNECT;
            ReloadButton.Text = RELOAD_BUTTON_STATE_READY;
            AutoRunCheckBox.Checked = IsAutoRunEnabled();
            StartMinimizedCheckBox.Checked = IsStartMinimizedEnabled();

            ParseCommandLineArguments();

            if (_deviceID.Length != 0 && _deviceName.Length != 0)
            {
                // make sure a toast notification will pop up once we connect to the device
                _lastMinLevel = 100;

                BeginInvoke(new Action(() =>
                {
                    Hide();
                    _reconnectCounter = 1;
                    ReconnectTimer.Start();
                }));
            }
            else
            {
                if (IsStartMinimizedEnabled())
                {
                    BeginInvoke(new Action(() => Hide()));
                }
                else
                {
                    WindowState = FormWindowState.Normal;
                    ShowInTaskbar = true;
                }
                ListBLEDevices();
            }
        }

        private void ParseCommandLineArguments()
        {
            // parse -deviceid and -devicename
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == STARTUP_ARG_DEVICE_ID && i + 1 < args.Length)
                {
                    _deviceID = args[++i].Trim("\"".ToCharArray());
                }
                else if (args[i] == STARTUP_ARG_DEVICE_NAME && i + 1 < args.Length)
                {
                    _deviceName = args[++i].Trim("\"".ToCharArray());
                }
            }
        }

        private void ListBLEDevices()
        {
            DevicesListView.Items.Clear();
            ReloadButton.Enabled = false;
            ReloadButton.Text = RELOAD_BUTTON_STATE_RELOADING;
            BatteryMonitor.ListPairedDevices((string deviceName, string deviceID) =>
            {
                BeginInvoke(new Action(() =>
                {
                    DevicesListView.BeginUpdate();
                    DevicesListView.Items.Add(new ListViewItem { Text = deviceName, Tag = deviceID });
                    DevicesListView.EndUpdate();
                }));
            }, () =>
            {
                BeginInvoke(new Action(() =>
                {
                    ReloadButton.Enabled = true;
                    ReloadButton.Text = RELOAD_BUTTON_STATE_READY;
                }));
            });
        }

        public void OnBatteryLevelChanged()
        {
            BeginInvoke(new Action(() =>
            {
                UpdateTrayIcon();
                UpdateDeviceListBatteryValues();
            }));
        }

        private async Task<bool> ConnectToDevice(string deviceName, string deviceID)
        {
            StatusLabel.Text = String.Format(STATUS_CONNECTING, deviceName);

            var res = await _batteryMonitor.Connect(deviceName, deviceID);

            if (res.Status == BatteryMonitor.ConnectStatus.DeviceNotFound)
            {
                StatusLabel.Text = String.Format(STATUS_CONNECTION_FAILED, deviceName, "Device not found");
            }
            else if (res.Status == BatteryMonitor.ConnectStatus.BatteryServiceNotFound)
            {
                StatusLabel.Text = String.Format(STATUS_CONNECTION_FAILED, deviceName, "Battery service not found");
            }
            else if (res.Status == BatteryMonitor.ConnectStatus.BatteryLevelCharacteristicNotFound)
            {
                StatusLabel.Text = String.Format(STATUS_CONNECTION_FAILED, deviceName, "Could not find battery level GATT characteristic. Is the device offline?");
            }
            else if (res.Status == BatteryMonitor.ConnectStatus.SubscribtionFailure)
            {
                StatusLabel.Text = String.Format(STATUS_CONNECTION_FAILED, deviceName, "Could not subscribe to battery level notifications");
            }
            else
            {
                Debug.Assert(res.Status == BatteryMonitor.ConnectStatus.Connected, "Unknown BatteryMonitor.ConnectStatus");
                if (res.Status == BatteryMonitor.ConnectStatus.Connected)
                {
                    _deviceName = deviceName;
                    _deviceID = deviceID;
                    StatusLabel.Text = string.Format(STATUS_CONNECTED, _deviceName);
                    UpdateDeviceListBatteryColumns();
                    UpdateTrayIcon();
                    PollingTimer.Interval = (int)RefreshIntervalNumericUpDown.Value * 60 * 1000;
                    PollingTimer.Start();
                    if (_isReconnecting)
                    {
                        _isReconnecting = false;
                        new ToastContentBuilder()
                            .AddText("Keyboard reconnected")
                            .AddText(String.Format("{0} is back online.", _deviceName))
                            .Show();
                    }
                    if (AutoRunCheckBox.Checked)
                    {
                        // refresh credentials stored in registry
                        SetAutoRunEnabled(true);
                    }
                }
            }

            return res.Status == ConnectStatus.Connected;
        }

        private async Task<bool> ConnectToSelectedDevice()
        {
            if (DevicesListView.SelectedItems.Count == 0)
                return false;

            string deviceName = DevicesListView.SelectedItems[0].Text;
            string? deviceID = (string?)DevicesListView.SelectedItems[0].Tag;

            ReconnectTimer.Stop();

            return deviceID != null ? await ConnectToDevice(deviceName, deviceID) : false;
        }

        private void DisconnectFromSelectedDevice()
        {
            PollingTimer.Stop();
            _batteryMonitor.Disconnect();
            _deviceName = "";
            _deviceID = "";
            StatusLabel.Text = STATUS_READY;
            RemoveDeviceListBatteryColumns();
            UpdateTrayIcon();
        }

        private bool IsAutoRunEnabled()
        {
            var keyPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
            var keyOpt = Registry.CurrentUser.OpenSubKey(keyPath);

            if (keyOpt is RegistryKey key)
            {
                return key.GetValue(Application.ProductName) is String;
            }
            else
            {
                StatusLabel.Text = String.Format(STATUS_COULD_NOT_OPEN_REG_KEY, keyPath);
                return false;
            }
        }

        private bool SetAutoRunEnabled(bool enabled)
        {
            var keyPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
            var keyOpt =  Registry.CurrentUser.OpenSubKey(keyPath, true);

            if (keyOpt is RegistryKey key)
            {
                if (enabled)
                {
                    string value;
                    if (_deviceName.Length != 0 && _deviceID.Length != 0)
                    {
                        value = String.Format("\"{0}\" {1} \"{2}\" {3} \"{4}\"", Application.ExecutablePath, STARTUP_ARG_DEVICE_NAME, _deviceName, STARTUP_ARG_DEVICE_ID, _deviceID);
                    }
                    else
                    {
                        value = String.Format("\"{0}\"", Application.ExecutablePath);
                    }
                    key.SetValue(Application.ProductName, value);
                }
                else
                {
                    if (Application.ProductName != null)
                        key.DeleteValue(Application.ProductName, false);
                }
                return true;
            }
            else
            {
                StatusLabel.Text = String.Format(STATUS_COULD_NOT_OPEN_REG_KEY, keyPath);
                return false;
            }
        }

        private void PreferenceChangedHandler(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General)
            {
                // Reload icon if Color Theme has been changed
                UpdateTrayIcon();
            }
        }

        private bool IsWindowsThemeLight()
        {
            var keyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize";
            var keyOpt = Registry.CurrentUser.OpenSubKey(keyPath);
            if (keyOpt is RegistryKey key)
            {
                var valOpt = key.GetValue("SystemUsesLightTheme");
                if (valOpt is int val)
                {
                    return val != 0;
                }
            }
            else
            {
                StatusLabel.Text = String.Format(STATUS_COULD_NOT_OPEN_REG_KEY, keyPath);
            }
            return true;
        }

        private Icon GetBatteryIcon(int pcnt)
        {
            string iconName = "white-";
            if (IsWindowsThemeLight())
            {
                iconName = "black-";
            }
            if (pcnt == -1)
            {
                iconName += "dsc";
            }
            else
            {
                pcnt = ((int)Math.Round(pcnt / 10.0)) * 10;
                iconName += pcnt.ToString("d3");
            }
            object obj = ZMKSplit.Properties.Resources.ResourceManager.GetObject(iconName, ZMKSplit.Properties.Resources.Culture)!;
            return ((Icon)(obj));
        }

        public void UpdateTrayIcon()
        {
            int minLevel = 100;
            string tooltipText;
            if (!_batteryMonitor.IsConnected() || _batteryMonitor.Batteries.Count == 0)
            {
                minLevel = -1;
                tooltipText = BATTERY_NOT_CONNECTED_TITLE;
            }
            else if (_batteryMonitor.Batteries.Count == 1)
            {
                minLevel = _batteryMonitor.Batteries.First().Value.Level;
                tooltipText = String.Format("{0}: {1}%", _deviceName, minLevel);
            }
            else
            {
                var sb = new StringBuilder(_deviceName);
                foreach (var battery in _batteryMonitor.Batteries.Values)
                {
                    sb.Append($"\n{battery.Name}: {battery.Level}%");
                    minLevel = Math.Min(minLevel, battery.Level);
                }
                tooltipText = sb.ToString();
            }
            // Windows tray tooltip is capped at 63 characters
            NotifyIcon.Text = tooltipText.Length > 63 ? tooltipText.Substring(0, 63) : tooltipText;
            NotifyIcon.Icon = GetBatteryIcon(minLevel);
            if (_lastMinLevel > BATTERY_LOW_LEVEL_THRESHOLD && minLevel != -1 && minLevel <= BATTERY_LOW_LEVEL_THRESHOLD)
            {
                new ToastContentBuilder()
                    .AddText(BATTERY_LOW_TIP_TITLE)
                    .AddText(String.Format(BATTERY_LOW_TIP_MESSAGE, _deviceName))
                    .Show();
            }
            _lastMinLevel = minLevel;
            if (minLevel != -1)
                LastUpdatedLabel.Text = "Updated: " + DateTime.Now.ToString("HH:mm:ss");
        }

        private void ReloadButton_MouseClick(object sender, MouseEventArgs e)
        {
            ListBLEDevices();
        }

        private void DevicesListView_DoubleClick(object sender, EventArgs e)
        {
            if (ConnectButton.Enabled && !_batteryMonitor.IsConnected())
            {
                ConnectButton_Click(sender, e);
            }
        }

        private async void ConnectButton_Click(object sender, EventArgs e)
        {
            ConnectButton.Enabled = false;
            if (_batteryMonitor.IsConnected())
            {
                DisconnectFromSelectedDevice();
                ConnectButton.Text = CONNECT_BUTTON_CONNECT;
            }
            else
            {
                var res = await ConnectToSelectedDevice();
                if (res)
                {
                    ConnectButton.Text = CONNECT_BUTTON_DISCONNECT;
                }
            }
            ConnectButton.Enabled = true;
        }

        private void OnDeviceNeedsReconnect()
        {
            // Called from BatteryMonitor when the BLE link drops unexpectedly
            // (e.g. keyboard switched to USB charging mode).
            BeginInvoke(new Action(() =>
            {
                _isReconnecting = true;
                PollingTimer.Stop();
                UpdateTrayIcon(); // now shows disconnected
                if (_deviceID.Length > 0 && _deviceName.Length > 0)
                {
                    ReconnectTimer.Stop();
                    _reconnectCounter = RECONNECT_AFTER_DISCONNECT_INTERVAL;
                    ReconnectTimer.Start();
                }
            }));
        }

        private async void PollingTimer_Tick(object sender, EventArgs e)
        {
            if (!_batteryMonitor.IsConnected())
                return;
            bool success = await _batteryMonitor.RefreshBatteryLevels();
            if (success)
            {
                UpdateTrayIcon();
                UpdateDeviceListBatteryValues();
            }
        }

        /// <summary>
        /// Adds/replaces battery columns in the list view to match the currently connected device's batteries.
        /// Should be called right after a successful connection.
        /// </summary>
        private void UpdateDeviceListBatteryColumns()
        {
            DevicesListView.BeginUpdate();

            // Remove any existing battery columns (keep only column 0 = Name)
            while (DevicesListView.Columns.Count > 1)
                DevicesListView.Columns.RemoveAt(1);

            var batteries = _batteryMonitor.Batteries;
            int batteryColWidth = batteries.Count > 0 ? 80 : 0;
            // Shrink Name column to make room
            NameColumn.Width = DevicesListView.ClientSize.Width - batteryColWidth * batteries.Count - 4;

            foreach (var battery in batteries.Values)
            {
                var col = new ColumnHeader
                {
                    Text = battery.Name,
                    Width = batteryColWidth,
                    TextAlign = HorizontalAlignment.Right,
                };
                DevicesListView.Columns.Add(col);
            }

            DevicesListView.EndUpdate();
            UpdateDeviceListBatteryValues();
        }

        /// <summary>
        /// Updates the subitems of the connected device's row with current battery percentages.
        /// </summary>
        private void UpdateDeviceListBatteryValues()
        {
            if (!_batteryMonitor.IsConnected())
                return;

            // Find the list view item that matches the connected device
            ListViewItem? item = null;
            foreach (ListViewItem lvi in DevicesListView.Items)
            {
                if ((string?)lvi.Tag == _deviceID)
                {
                    item = lvi;
                    break;
                }
            }
            if (item == null)
                return;

            var batteries = _batteryMonitor.Batteries.Values.ToList();
            DevicesListView.BeginUpdate();

            // Ensure enough subitems exist (index 0 is the item itself = Name)
            while (item.SubItems.Count - 1 < batteries.Count)
                item.SubItems.Add("");

            for (int i = 0; i < batteries.Count; i++)
            {
                string text = batteries[i].Level >= 0 ? $"{batteries[i].Level}%" : "--";
                item.SubItems[i + 1].Text = text;
            }

            DevicesListView.EndUpdate();
        }

        /// <summary>
        /// Removes battery columns and subitems, restoring the list to Name-only.
        /// </summary>
        private void RemoveDeviceListBatteryColumns()
        {
            DevicesListView.BeginUpdate();
            while (DevicesListView.Columns.Count > 1)
                DevicesListView.Columns.RemoveAt(1);
            NameColumn.Width = DevicesListView.ClientSize.Width - 4;
            foreach (ListViewItem lvi in DevicesListView.Items)
                while (lvi.SubItems.Count > 1)
                    lvi.SubItems.RemoveAt(1);
            DevicesListView.EndUpdate();
        }

        private void RefreshIntervalNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            PollingTimer.Interval = (int)RefreshIntervalNumericUpDown.Value * 60 * 1000;
        }

        private async void RefreshNowContextMenuItem_Click(object sender, EventArgs e)
        {
            bool success = await _batteryMonitor.RefreshBatteryLevels();
            if (success)
            {
                UpdateTrayIcon();
                UpdateDeviceListBatteryValues();
            }
        }

        private void DisconnectContextMenuItem_Click(object sender, EventArgs e)
        {
            ReconnectTimer.Stop();
            _isReconnecting = false;
            DisconnectFromSelectedDevice();
            ConnectButton.Text = CONNECT_BUTTON_CONNECT;
        }

        private void TrayContextMenu_Opening(object sender, CancelEventArgs e)
        {
            bool connected = _batteryMonitor.IsConnected();
            refreshNowContextMenuItem.Enabled = connected;
            disconnectContextMenuItem.Enabled = connected;
        }

        private void ExitContextMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

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
            }
        }

        private async void ReconnectTimer_Tick(object sender, EventArgs e)
        {
            if (--_reconnectCounter == 0)
            {
                ReconnectTimer.Stop();

                Debug.Assert(_deviceID.Length > 0);
                Debug.Assert(_deviceName.Length > 0);

                bool res = await ConnectToDevice(_deviceName, _deviceID);
                if (!res)
                {
                    _reconnectCounter = RECONNECT_INTERVAL;
                    ReconnectTimer.Start();
                }
                else
                {
                    ConnectButton.Text = CONNECT_BUTTON_DISCONNECT;
                }
            }
            else
            {
                StatusLabel.Text = String.Format(STATUS_CONNECTING_IN, _deviceName, _reconnectCounter);
            }
        }

        private void AutoRunCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            SetAutoRunEnabled(AutoRunCheckBox.Checked);
        }

        private bool IsStartMinimizedEnabled()
        {
            var keyOpt = Registry.CurrentUser.OpenSubKey(APP_REG_KEY);
            if (keyOpt is RegistryKey key)
            {
                return key.GetValue(APP_REG_START_MINIMIZED) is int val && val != 0;
            }
            return false;
        }

        private void SetStartMinimizedEnabled(bool enabled)
        {
            var keyOpt = Registry.CurrentUser.CreateSubKey(APP_REG_KEY);
            if (keyOpt is RegistryKey key)
            {
                key.SetValue(APP_REG_START_MINIMIZED, enabled ? 1 : 0, RegistryValueKind.DWord);
            }
        }

        private void StartMinimizedCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            SetStartMinimizedEnabled(StartMinimizedCheckBox.Checked);
        }

        private void NotifyIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ShowContextMenuItem_Click(sender, e);
        }
    }
}
