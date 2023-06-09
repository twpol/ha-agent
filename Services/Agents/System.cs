using System.Diagnostics;
using System.Management;
using HA_Agent.Services;
using Microsoft.Extensions.Configuration;

namespace HA_Agent.Agents
{
    class System : Agent
    {
        readonly PerformanceCounter? CPUUtility;
        readonly PerformanceCounter? CPUPerformance;

        readonly Dictionary<string, WindowsDiskIO>? WindowsDiskIO;
        readonly Dictionary<string, WindowsNetworkIO>? WindowsNetworkIO;

        public System(HomeAssistant homeAssistant, IConfigurationSection config, bool verbose, bool dryRun)
            : base(homeAssistant, Environment.MachineName, config["name"] ?? Environment.MachineName, verbose, dryRun)
        {
            CPUPerformance = OperatingSystem.IsWindows() ? new PerformanceCounter("Processor Information", "% Processor Performance", "_Total") : null;
            CPUUtility = OperatingSystem.IsWindows() ? new PerformanceCounter("Processor Information", "% Processor Utility", "_Total") : null;
            WindowsDiskIO = OperatingSystem.IsWindows() ? new() : null;
            WindowsNetworkIO = OperatingSystem.IsWindows() ? new() : null;
            UpdateCounterLists();
        }

        public override Task Start()
        {
            if (OperatingSystem.IsWindows())
            {
                CPUPerformance?.NextValue();
                CPUUtility?.NextValue();
                if (WindowsDiskIO != null) foreach (var diskIo in WindowsDiskIO) diskIo.Value.NextValue();
                if (WindowsNetworkIO != null) foreach (var networkIo in WindowsNetworkIO) networkIo.Value.NextValue();
                UpdateCounterLists();
            }
            return Task.CompletedTask;
        }

        public override async Task Execute()
        {
            VerboseLog("Execute: Start");

            await PublishSensor("sensor", "Last Reboot", icon: "mdi:restart", deviceClass: "timestamp", entityCategory: "diagnostic", state: GetLastReboot().ToString("s") + "Z");
            await PublishSensor("sensor", "Battery Level", icon: "mdi:battery-charging", deviceClass: "battery", stateClass: "measurement", unitOfMeasurement: "%", entityCategory: "diagnostic", state: GetBatteryLevel().ToString());
            foreach (var data in GetMemoryData())
            {
                await PublishSensor("sensor", $"{data.Name} total", icon: "mdi:memory", stateClass: "measurement", unitOfMeasurement: "MiB", entityCategory: "diagnostic", state: data.TotalMiB.ToString("F1"));
                await PublishSensor("sensor", $"{data.Name} free", icon: "mdi:memory", stateClass: "measurement", unitOfMeasurement: "MiB", entityCategory: "diagnostic", state: data.FreeMiB.ToString("F1"));
                await PublishSensor("sensor", $"{data.Name} use", icon: "mdi:memory", stateClass: "measurement", unitOfMeasurement: "MiB", entityCategory: "diagnostic", state: data.UsedMiB.ToString("F1"));
                await PublishSensor("sensor", $"{data.Name} use (percent)", icon: "mdi:memory", stateClass: "measurement", unitOfMeasurement: "%", entityCategory: "diagnostic", state: data.UsedPercent.ToString("F1"));
            }
            foreach (var data in GetStorageData())
            {
                await PublishSensor("sensor", $"Disk {data.Name} total", icon: "mdi:harddisk", stateClass: "measurement", unitOfMeasurement: "GiB", entityCategory: "diagnostic", state: data.TotalGiB.ToString("F1"));
                await PublishSensor("sensor", $"Disk {data.Name} free", icon: "mdi:harddisk", stateClass: "measurement", unitOfMeasurement: "GiB", entityCategory: "diagnostic", state: data.FreeGiB.ToString("F1"));
                await PublishSensor("sensor", $"Disk {data.Name} use", icon: "mdi:harddisk", stateClass: "measurement", unitOfMeasurement: "GiB", entityCategory: "diagnostic", state: data.UsedGiB.ToString("F1"));
                await PublishSensor("sensor", $"Disk {data.Name} use (percent)", icon: "mdi:harddisk", stateClass: "measurement", unitOfMeasurement: "%", entityCategory: "diagnostic", state: data.UsedPercent.ToString("F1"));
            }
            foreach (var data in GetStorageIO())
            {
                await PublishSensor("sensor", $"Disk {data.Name} bytes/sec", icon: "mdi:harddisk", stateClass: "measurement", unitOfMeasurement: "MiB/s", entityCategory: "diagnostic", state: data.TotalMiBPerSec.ToString("F1"));
                await PublishSensor("sensor", $"Disk {data.Name} read bytes/sec", icon: "mdi:folder-upload", stateClass: "measurement", unitOfMeasurement: "MiB/s", entityCategory: "diagnostic", state: data.ReadMiBPerSec.ToString("F1"));
                await PublishSensor("sensor", $"Disk {data.Name} write bytes/sec", icon: "mdi:folder-download", stateClass: "measurement", unitOfMeasurement: "MiB/s", entityCategory: "diagnostic", state: data.WriteMiBPerSec.ToString("F1"));
            }
            foreach (var data in GetNetworkIO())
            {
                await PublishSensor("sensor", $"Network {data.Name} bytes/sec", icon: "mdi:network", stateClass: "measurement", unitOfMeasurement: "KiB/s", entityCategory: "diagnostic", state: data.TotalKiBPerSec.ToString("F1"));
                await PublishSensor("sensor", $"Network {data.Name} received bytes/sec", icon: "mdi:download-network", stateClass: "measurement", unitOfMeasurement: "KiB/s", entityCategory: "diagnostic", state: data.ReadKiBPerSec.ToString("F1"));
                await PublishSensor("sensor", $"Network {data.Name} sent bytes/sec", icon: "mdi:upload-network", stateClass: "measurement", unitOfMeasurement: "KiB/s", entityCategory: "diagnostic", state: data.WriteKiBPerSec.ToString("F1"));
            }
            if (OperatingSystem.IsWindows() && CPUPerformance != null && CPUUtility != null)
            {
                var icon = Environment.Is64BitOperatingSystem ? "mdi:cpu-64-bit" : "mdi:cpu-32-bit";
                var performance = CPUPerformance.NextValue();
                var utility = CPUUtility.NextValue();
                if (performance > 0 && utility > 0)
                {
                    await PublishSensor("sensor", "Processor performance", icon: icon, stateClass: "measurement", unitOfMeasurement: "%", entityCategory: "diagnostic", state: performance.ToString("F1"));
                    await PublishSensor("sensor", "Processor utility", icon: icon, stateClass: "measurement", unitOfMeasurement: "%", entityCategory: "diagnostic", state: utility.ToString("F1"));
                    await PublishSensor("sensor", "Processor use", icon: icon, stateClass: "measurement", unitOfMeasurement: "%", entityCategory: "diagnostic", state: (100 * utility / performance).ToString("F1"));
                }
            }

            VerboseLog("Execute: Finish");
        }

        void UpdateCounterLists()
        {
            if (OperatingSystem.IsWindows())
            {
                VerboseLog("UpdateCounterLists: Start");

                if (WindowsDiskIO != null)
                {
                    var query = new ObjectQuery("SELECT * FROM Win32_PerfFormattedData_PerfDisk_LogicalDisk");
                    var searcher = new ManagementObjectSearcher(query);
                    var collection = searcher.Get();
                    var names = new HashSet<string>();
                    foreach (var disk in collection)
                    {
                        var name = (string)disk["Name"];
                        // Only accept two-letter drive names (e.g. allow "C:", skip "HarddiskVolume1" and "_Total")
                        if (name.Length != 2) continue;
                        names.Add(name);
                        if (!WindowsDiskIO.ContainsKey(name))
                        {
                            VerboseLog($"UpdateCounterLists: Add Disk: {name}");
                            WindowsDiskIO.Add(name, new WindowsDiskIO(name));
                        }
                    }
                    foreach (var name in WindowsDiskIO.Keys.Where(name => !names.Contains(name)).ToList())
                    {
                        VerboseLog($"UpdateCounterLists: Remove Disk: {name}");
                        WindowsDiskIO.Remove(name);
                    }
                }

                if (WindowsNetworkIO != null)
                {
                    var query = new ObjectQuery("SELECT * FROM Win32_PerfFormattedData_Tcpip_NetworkInterface");
                    var searcher = new ManagementObjectSearcher(query);
                    var collection = searcher.Get();
                    var names = new HashSet<string>();
                    foreach (var network in collection)
                    {
                        var name = (string)network["Name"];
                        names.Add(name);
                        if (!WindowsNetworkIO.ContainsKey(name))
                        {
                            VerboseLog($"UpdateCounterLists: Add Network: {name}");
                            WindowsNetworkIO.Add(name, new WindowsNetworkIO(name));
                        }
                    }
                    foreach (var name in WindowsNetworkIO.Keys.Where(name => !names.Contains(name)).ToList())
                    {
                        VerboseLog($"UpdateCounterLists: Remove Network: {name}");
                        WindowsNetworkIO.Remove(name);
                    }
                }

                VerboseLog("UpdateCounterLists: Finish");
            }
        }

        IDictionary<string, object>? DeviceConfig;

        protected override IDictionary<string, object> GetDeviceConfig()
        {
            DeviceConfig ??= new Dictionary<string, object>
            {
                { "identifiers", $"ha-agent.{NodeId}" },
                { "manufacturer", GetDeviceManufacturer() },
                { "model", GetDeviceModel() },
                { "name", NodeName },
            };
            return DeviceConfig;
        }

        static string GetDeviceManufacturer()
        {
            if (OperatingSystem.IsWindows())
            {
                var query = new ObjectQuery("SELECT * FROM Win32_OperatingSystem");
                var searcher = new ManagementObjectSearcher(query);
                var collection = searcher.Get().GetEnumerator();
                collection.MoveNext();
                return (string)collection.Current["Manufacturer"];
            }
            return "";
        }

        static string GetDeviceModel()
        {
            if (OperatingSystem.IsWindows())
            {
                var query = new ObjectQuery("SELECT * FROM Win32_OperatingSystem");
                var searcher = new ManagementObjectSearcher(query);
                var collection = searcher.Get().GetEnumerator();
                if (collection.MoveNext()) return $"{(string)collection.Current["Caption"]} {(string)collection.Current["Version"]}";
            }
            return "";
        }

        static DateTimeOffset GetLastReboot()
        {
            return DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64);
        }

        static int GetBatteryLevel()
        {
            if (OperatingSystem.IsWindows())
            {
                var query = new ObjectQuery("SELECT * FROM Win32_Battery");
                var searcher = new ManagementObjectSearcher(query);
                var collection = searcher.Get().GetEnumerator();
                if (collection.MoveNext()) return (ushort)collection.Current["EstimatedChargeRemaining"];
            }
            return 100;
        }

        static List<NameValueData> GetMemoryData()
        {
            var list = new List<NameValueData>();
            if (OperatingSystem.IsWindows())
            {
                var query = new ObjectQuery("SELECT * FROM Win32_OperatingSystem");
                var searcher = new ManagementObjectSearcher(query);
                var collection = searcher.Get().GetEnumerator();
                if (collection.MoveNext())
                {
                    list.Add(new NameValueData("Memory", (ulong)collection.Current["FreePhysicalMemory"] * 1024, (ulong)collection.Current["TotalVisibleMemorySize"] * 1024));
                    list.Add(new NameValueData("Swap", (ulong)collection.Current["FreeSpaceInPagingFiles"] * 1024, (ulong)collection.Current["SizeStoredInPagingFiles"] * 1024));
                    list.Add(new NameValueData("Committed", (ulong)collection.Current["FreeVirtualMemory"] * 1024, (ulong)collection.Current["TotalVirtualMemorySize"] * 1024));
                }
            }
            return list;
        }

        static List<NameValueData> GetStorageData()
        {
            var list = new List<NameValueData>();
            if (OperatingSystem.IsWindows())
            {
                var query = new ObjectQuery("SELECT * FROM Win32_Volume");
                var searcher = new ManagementObjectSearcher(query);
                var collection = searcher.Get();
                foreach (var volume in collection)
                {
                    if (volume["DriveLetter"] != null && (uint)volume["DriveType"] == 3)
                    {
                        list.Add(new NameValueData((string)volume["DriveLetter"], (ulong)volume["FreeSpace"], (ulong)volume["Capacity"]));
                    }
                }
            }
            return list;
        }

        List<NameValueIO> GetStorageIO()
        {
            var list = new List<NameValueIO>();
            if (OperatingSystem.IsWindows() && WindowsDiskIO != null)
            {
                foreach (var disk in WindowsDiskIO)
                {
                    list.Add(new NameValueIO(disk.Key, disk.Value.BytesReadPerSecond.NextValue(), disk.Value.BytesWritePerSecond.NextValue()));
                }
            }
            return list;
        }

        List<NameValueIO> GetNetworkIO()
        {
            var list = new List<NameValueIO>();
            if (OperatingSystem.IsWindows() && WindowsNetworkIO != null)
            {
                foreach (var network in WindowsNetworkIO)
                {
                    list.Add(new NameValueIO(network.Key, network.Value.BytesReceivedPerSecond.NextValue(), network.Value.BytesSentPerSecond.NextValue()));
                }
            }
            return list;
        }
    }
}
