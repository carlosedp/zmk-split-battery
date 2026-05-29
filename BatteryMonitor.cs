using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;
using System.Diagnostics;

namespace ZMKSplit
{
    internal class BatteryMonitor : IDisposable
    {
        public static readonly Guid BATTERY_UUID = Guid.Parse("{0000180F-0000-1000-8000-00805F9B34FB}");
        public static readonly Guid BATTERY_LEVEL_UUID = Guid.Parse("{00002A19-0000-1000-8000-00805F9B34FB}");
        public static readonly string BATTERY_DEFAULT_NAME = "Main";

        public delegate void ListDevicesCallback(string deviceName, string deviceID);
        public delegate void ListDevicesCompletionCallback();
        public delegate void BatteryLevelChangedCallback();
        public delegate void DeviceNeedsReconnectCallback();

        public struct BatteryStatus
        {
            public string Name { get; set; }
            public int Level { get; set; }
        };

        public enum ConnectStatus
        {
            Connected,
            DeviceNotFound,
            BatteryServiceNotFound,
            BatteryLevelCharacteristicNotFound,
            SubscribtionFailure,
        }

        public struct ConnectResult
        {
            public ConnectStatus Status { get; set; }
            public string ErrorMessage { get; set; }
        }

        public enum ReadStatus
        {
            Success,
            NotConnected,
            Failure,
        }

        public struct ReadBatteryLevelResult
        {
            public ReadStatus Status { get; set; }
            public ConcurrentDictionary<ushort, BatteryStatus> Batteries { get; set; }
            public string ErrorMessage { get; set; }

            public ReadBatteryLevelResult(ReadStatus status, GattCharacteristic gc, int singleValue) : this(status, "")
            {
                Batteries[gc.AttributeHandle] = new BatteryStatus { Name = GetBatteryNameFromGC(gc), Level = singleValue };
            }

            public ReadBatteryLevelResult(ReadStatus status, string errorMessage)
            {
                Status = status;
                Batteries = new ConcurrentDictionary<ushort, BatteryStatus>();
                ErrorMessage = errorMessage;
            }
        }

        private class BLEDevice
        {
            public string Name { get; set; }
            public BluetoothLEDevice Device { get; set; }
            public IReadOnlyList<GattCharacteristic> GattCharacteristics { get; set; }
            public List<GattDeviceService> Services { get; set; }

            public BLEDevice(string name, BluetoothLEDevice dev, IReadOnlyList<GattCharacteristic> gattChrs, List<GattDeviceService> services)
            {
                Name = name;
                Device = dev;
                GattCharacteristics = gattChrs;
                Services = services;
            }
        };

        public IReadOnlyDictionary<ushort, BatteryStatus> Batteries => _batteries;

        private ConcurrentDictionary<ushort, BatteryStatus> _batteries = new();
        private BLEDevice? _bleDevice;
        private static DeviceWatcher? _deviceWatcher;
        private readonly BatteryLevelChangedCallback _batteryLevelChangedCb;
        private readonly DeviceNeedsReconnectCallback _deviceNeedsReconnectCb;
        private bool _disposed;

        public BatteryMonitor(BatteryLevelChangedCallback levelCb, DeviceNeedsReconnectCallback reconnectCb)
        {
            _batteryLevelChangedCb = levelCb;
            _deviceNeedsReconnectCb = reconnectCb;
        }

        public static void ListPairedDevices(ListDevicesCallback cb, ListDevicesCompletionCallback completionCB)
        {
            // Stop and discard any previous watcher to avoid leaks on rapid refreshes.
            if (_deviceWatcher != null)
            {
                try { _deviceWatcher.Stop(); } catch { }
                _deviceWatcher = null;
            }

            string aqsFilter = "(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")";
            string[] bleAdditionalProperties =
            {
                "System.Devices.Aep.DeviceAddress",
                "System.Devices.Aep.Bluetooth.Le.IsConnectable",
            };

            _deviceWatcher = DeviceInformation.CreateWatcher(aqsFilter, bleAdditionalProperties, DeviceInformationKind.AssociationEndpoint);
            _deviceWatcher.Added += (DeviceWatcher deviceWatcher, DeviceInformation di) =>
            {
                if (di.Pairing.IsPaired && !String.IsNullOrWhiteSpace(di.Name))
                {
                    cb(di.Name, di.Id);
                }
            };
            _deviceWatcher.Updated += (_, __) => { };
            _deviceWatcher.EnumerationCompleted += (DeviceWatcher deviceWatcher, object arg) =>
            {
                deviceWatcher.Stop();
            };
            _deviceWatcher.Stopped += (DeviceWatcher deviceWatcher, object arg) =>
            {
                _deviceWatcher = null;
                completionCB();
            };
            _deviceWatcher.Start();
        }

        public async Task<ConnectResult> Connect(string deviceName, string deviceID)
        {
            Disconnect();

            try
            {
                var dev = await BluetoothLEDevice.FromIdAsync(deviceID);
                if (dev == null)
                {
                    return new ConnectResult { Status = ConnectStatus.DeviceNotFound, ErrorMessage = "Device not found or not available" };
                }

                dev.ConnectionStatusChanged += OnBLEConnectionStatusChanged;

                var gattServices = await dev.GetGattServicesForUuidAsync(BATTERY_UUID, BluetoothCacheMode.Uncached).AsTask();
                if (gattServices == null)
                {
                    return new ConnectResult { Status = ConnectStatus.BatteryServiceNotFound };
                }

                var services = new List<GattDeviceService>();
                var allCharacteristics = new List<GattCharacteristic>();
                for (int i = 0; i < gattServices.Services.Count; i++)
                {
                    var gattService = gattServices.Services[i];
                    services.Add(gattService); // kept alive; disposed in Disconnect()
                    var gattCharacteristics = await gattService.GetCharacteristicsForUuidAsync(BATTERY_LEVEL_UUID, BluetoothCacheMode.Uncached);

                    foreach (var gc in gattCharacteristics!.Characteristics)
                    {
                        _batteries[gc.AttributeHandle] = new BatteryStatus { Name = GetBatteryNameFromGC(gc), Level = -1 };
                        gc.ValueChanged += OnGattValueChanged;
                        // Tell the device to push unsolicited battery level notifications.
                        // Ignore failure — polling covers devices that don't support Notify.
                        await gc.WriteClientCharacteristicConfigurationDescriptorAsync(
                            GattClientCharacteristicConfigurationDescriptorValue.Notify);
                        allCharacteristics.Add(gc);
                    }
                }

                if (_batteries.Count != 0)
                {
                    _bleDevice = new BLEDevice(deviceName, dev, allCharacteristics, services);

                    var readResult = await ReadBatteryLevels();
                    if (readResult.Status == ReadStatus.Success)
                    {
                        foreach (var kv in readResult.Batteries)
                            _batteries[kv.Key] = kv.Value;
                    }

                    return new ConnectResult { Status = ConnectStatus.Connected };
                }
                else
                {
                    return new ConnectResult { Status = ConnectStatus.BatteryLevelCharacteristicNotFound };
                }
            }
            catch (ArgumentException ex)
            {
                return new ConnectResult { Status = ConnectStatus.DeviceNotFound, ErrorMessage = $"Invalid device ID: {ex.Message}" };
            }
            catch (Exception ex)
            {
                return new ConnectResult { Status = ConnectStatus.DeviceNotFound, ErrorMessage = $"Connection failed: {ex.Message}" };
            }
        }

        public void Disconnect()
        {
            _batteries.Clear();
            if (_bleDevice != null)
            {
                foreach (var gc in _bleDevice.GattCharacteristics)
                    gc.ValueChanged -= OnGattValueChanged;
                _bleDevice.Device.ConnectionStatusChanged -= OnBLEConnectionStatusChanged;
                foreach (var svc in _bleDevice.Services)
                    svc.Dispose();
                _bleDevice.Device.Dispose();
                _bleDevice = null;
            }
        }

        public static void StopDeviceWatcher()
        {
            if (_deviceWatcher != null)
            {
                try { _deviceWatcher.Stop(); } catch { }
                _deviceWatcher = null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Disconnect();
        }

        public bool IsConnected()
        {
            return _bleDevice != null &&
                   _bleDevice.Device.ConnectionStatus == BluetoothConnectionStatus.Connected;
        }

        /// <summary>Reads fresh battery levels from the device and updates internal state.</summary>
        /// <returns>True on success, false if not connected or read failed.</returns>
        public async Task<bool> RefreshBatteryLevels()
        {
            var readResult = await ReadBatteryLevels();
            if (readResult.Status == ReadStatus.Success)
            {
                foreach (var kv in readResult.Batteries)
                    _batteries[kv.Key] = kv.Value;
                return true;
            }
            return false;
        }

        private async void OnBLEConnectionStatusChanged(BluetoothLEDevice sender, object args)
        {
            if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            {
                // BLE link dropped (e.g. keyboard switched to USB charging)
                _deviceNeedsReconnectCb();
            }
            else
            {
                // BLE link restored – try to refresh readings immediately.
                // If GATT session is stale a full reconnect will be triggered.
                var readResult = await ReadBatteryLevels();
                if (readResult.Status == ReadStatus.Success)
                {
                    foreach (var kv in readResult.Batteries)
                        _batteries[kv.Key] = kv.Value;
                    _batteryLevelChangedCb();
                }
                else
                {
                    _deviceNeedsReconnectCb();
                }
            }
        }

        private static string GetBatteryNameFromGC(GattCharacteristic gc)
        {
            if (string.IsNullOrWhiteSpace(gc.UserDescription))
            {
                return BATTERY_DEFAULT_NAME;
            }
            else
            {
                return gc.UserDescription;
            }
        }

        private static async Task<ReadBatteryLevelResult> ReadBatteryLevel(GattCharacteristic gc)
        {
            try
            {
                var res = await gc.ReadValueAsync(BluetoothCacheMode.Uncached);
                if (res == null)
                {
                    return new ReadBatteryLevelResult(ReadStatus.Failure, "ReadValueAsync returned null");
                }

                if (res.Status != GattCommunicationStatus.Success)
                {
                    return new ReadBatteryLevelResult(ReadStatus.Failure, "ReadValueAsync returned status " + res.Status.ToString());
                }

                IBuffer buffer = res.Value;
                byte[] data = new byte[buffer.Length];
                DataReader.FromBuffer(buffer).ReadBytes(data);
                int lvl = data[0] == 255 ? -1 : data[0];
                return new ReadBatteryLevelResult(ReadStatus.Success, gc, lvl);
            }
            catch (Exception e)
            {
                return new ReadBatteryLevelResult(ReadStatus.Failure, e.Message);
            }
        }

        public async Task<ReadBatteryLevelResult> ReadBatteryLevels()
        {
            if (_bleDevice == null)
                return new ReadBatteryLevelResult { Status = ReadStatus.NotConnected, Batteries = new ConcurrentDictionary<ushort, BatteryStatus>() };

            var bleDev = _bleDevice;
            var overalResult = new ReadBatteryLevelResult(ReadStatus.Success, "");
            foreach (var gc in bleDev.GattCharacteristics)
            {
                var res = await ReadBatteryLevel(gc);
                if (res.Status != ReadStatus.Success)
                    return res;
                overalResult.Batteries[gc.AttributeHandle] = res.Batteries[gc.AttributeHandle];
            }
            return overalResult;
        }

        private void OnGattValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            byte[] data = new byte[args.CharacteristicValue.Length];
            DataReader.FromBuffer(args.CharacteristicValue).ReadBytes(data);
            int lvl = data[0] == 255 ? -1 : data[0];
            _batteries[sender.AttributeHandle] = new BatteryStatus { Name = GetBatteryNameFromGC(sender), Level = lvl };
            _batteryLevelChangedCb();
        }
    }
}
