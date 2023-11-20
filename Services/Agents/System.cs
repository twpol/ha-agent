using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
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

        readonly ulong InstalledMemory;
        readonly ulong UsableMemory;
        readonly PerformanceCounter? WindowsMemoryKernelNonPagedPool;
        readonly PerformanceCounter? WindowsMemoryKernelPagedPool;
        readonly PerformanceCounter? WindowsMemoryKernelDrivers;
        readonly PerformanceCounter? WindowsMemoryKernelCode;
        readonly PerformanceCounter? WindowsMemoryFileCache;
        readonly PerformanceCounter? WindowsMemoryPrivateWorkingSet;
        readonly PerformanceCounter? WindowsMemoryModified;
        readonly PerformanceCounter? WindowsMemoryStandbyCore;
        readonly PerformanceCounter? WindowsMemoryStandbyNormal;
        readonly PerformanceCounter? WindowsMemoryStandbyReserve;
        readonly PerformanceCounter? WindowsMemoryFree;

        public System(HomeAssistant homeAssistant, IConfigurationSection config, bool verbose, bool dryRun)
            : base(homeAssistant, Environment.MachineName, config["name"] ?? Environment.MachineName, verbose, dryRun)
        {
            if (OperatingSystem.IsWindows())
            {
                CPUPerformance = new PerformanceCounter("Processor Information", "% Processor Performance", "_Total");
                CPUUtility = new PerformanceCounter("Processor Information", "% Processor Utility", "_Total");
                WindowsDiskIO = new();
                WindowsNetworkIO = new();
                var ph = new ManagementClass("Win32_PhysicalMemory");
                foreach (var mo in ph.GetInstances())
                {
                    InstalledMemory += (ulong)mo.Properties["Capacity"].Value;
                }
                var cs = new ManagementClass("Win32_ComputerSystem");
                foreach (var mo in cs.GetInstances())
                {
                    UsableMemory = (ulong)mo.Properties["TotalPhysicalMemory"].Value;
                }
                var os = new ManagementClass("Win32_OperatingSystem");
                foreach (var mo in os.GetInstances())
                {
                    UsableMemory = (ulong)mo.Properties["TotalVisibleMemorySize"].Value * 1024;
                }
                WindowsMemoryKernelNonPagedPool = new PerformanceCounter("Memory", "Pool Nonpaged Bytes");
                WindowsMemoryKernelPagedPool = new PerformanceCounter("Memory", "Pool Paged Resident Bytes");
                WindowsMemoryKernelDrivers = new PerformanceCounter("Memory", "System Driver Resident Bytes");
                WindowsMemoryKernelCode = new PerformanceCounter("Memory", "System Code Resident Bytes");
                WindowsMemoryFileCache = new PerformanceCounter("Memory", "System Cache Resident Bytes");
                WindowsMemoryPrivateWorkingSet = new PerformanceCounter("Process", "Working Set - Private", "_Total");
                WindowsMemoryModified = new PerformanceCounter("Memory", "Modified Page List Bytes");
                WindowsMemoryStandbyCore = new PerformanceCounter("Memory", "Standby Cache Core Bytes");
                WindowsMemoryStandbyNormal = new PerformanceCounter("Memory", "Standby Cache Normal Priority Bytes");
                WindowsMemoryStandbyReserve = new PerformanceCounter("Memory", "Standby Cache Reserve Bytes");
                WindowsMemoryFree = new PerformanceCounter("Memory", "Free & Zero Page List Bytes");
            }
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
                await PublishSensor("sensor", $"DiskIO {data.Name} total", icon: "mdi:harddisk", stateClass: "measurement", unitOfMeasurement: "MiB/s", entityCategory: "diagnostic", state: data.TotalMiBPerSec.ToString("F1"));
                await PublishSensor("sensor", $"DiskIO {data.Name} read", icon: "mdi:folder-upload", stateClass: "measurement", unitOfMeasurement: "MiB/s", entityCategory: "diagnostic", state: data.ReadMiBPerSec.ToString("F1"));
                await PublishSensor("sensor", $"DiskIO {data.Name} write", icon: "mdi:folder-download", stateClass: "measurement", unitOfMeasurement: "MiB/s", entityCategory: "diagnostic", state: data.WriteMiBPerSec.ToString("F1"));
            }
            foreach (var data in GetNetworkIO())
            {
                await PublishSensor("sensor", $"NetworkIO {data.Name} total", icon: "mdi:network", stateClass: "measurement", unitOfMeasurement: "KiB/s", entityCategory: "diagnostic", state: data.TotalKiBPerSec.ToString("F1"));
                await PublishSensor("sensor", $"NetworkIO {data.Name} received", icon: "mdi:download-network", stateClass: "measurement", unitOfMeasurement: "KiB/s", entityCategory: "diagnostic", state: data.ReadKiBPerSec.ToString("F1"));
                await PublishSensor("sensor", $"NetworkIO {data.Name} sent", icon: "mdi:upload-network", stateClass: "measurement", unitOfMeasurement: "KiB/s", entityCategory: "diagnostic", state: data.WriteKiBPerSec.ToString("F1"));
                await PublishSensor("sensor", $"NetworkIO {data.Name} bandwidth", icon: "mdi:network", stateClass: "measurement", unitOfMeasurement: "KiB/s", entityCategory: "diagnostic", state: data.BandwidthKiBPerSec.ToString("F1"));
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

            if (OperatingSystem.IsWindows() && WindowsMemoryKernelNonPagedPool != null && WindowsMemoryKernelPagedPool != null && WindowsMemoryKernelDrivers != null && WindowsMemoryKernelCode != null && WindowsMemoryFileCache != null && WindowsMemoryPrivateWorkingSet != null && WindowsMemoryModified != null && WindowsMemoryStandbyCore != null && WindowsMemoryStandbyNormal != null && WindowsMemoryStandbyReserve != null && WindowsMemoryFree != null)
            {
                var hardware = (float)(InstalledMemory - UsableMemory);
                // NOTE: Kernel driver resident bytes is negative on Windows 10 22H2 sometimes (looks like the number of pages resident wraps around giving huge byte values)
                var kernel = WindowsMemoryKernelNonPagedPool.NextValue() + WindowsMemoryKernelPagedPool.NextValue() + FixWrappedRawPageBytes(WindowsMemoryKernelDrivers.NextValue()) + WindowsMemoryKernelCode.NextValue();
                var cache = WindowsMemoryFileCache.NextValue();
                var application = WindowsMemoryPrivateWorkingSet.NextValue();
                var modified = WindowsMemoryModified.NextValue();
                var standby = WindowsMemoryStandbyCore.NextValue() + WindowsMemoryStandbyNormal.NextValue() + WindowsMemoryStandbyReserve.NextValue();
                var free = WindowsMemoryFree?.NextValue() ?? 0;
                var shared = UsableMemory - kernel - cache - application - modified - standby - free;
                await PublishSensor("sensor", "Memory hardware type", icon: "mdi:memory", stateClass: "measurement", unitOfMeasurement: "MiB", entityCategory: "diagnostic", state: (hardware / 1024 / 1024).ToString("F1"));
                await PublishSensor("sensor", "Memory kernel type", icon: "mdi:memory", stateClass: "measurement", unitOfMeasurement: "MiB", entityCategory: "diagnostic", state: (kernel / 1024 / 1024).ToString("F1"));
                await PublishSensor("sensor", "Memory cache type", icon: "mdi:memory", stateClass: "measurement", unitOfMeasurement: "MiB", entityCategory: "diagnostic", state: (cache / 1024 / 1024).ToString("F1"));
                await PublishSensor("sensor", "Memory shared type", icon: "mdi:memory", stateClass: "measurement", unitOfMeasurement: "MiB", entityCategory: "diagnostic", state: (shared / 1024 / 1024).ToString("F1"));
                await PublishSensor("sensor", "Memory application type", icon: "mdi:memory", stateClass: "measurement", unitOfMeasurement: "MiB", entityCategory: "diagnostic", state: (application / 1024 / 1024).ToString("F1"));
                await PublishSensor("sensor", "Memory modified type", icon: "mdi:memory", stateClass: "measurement", unitOfMeasurement: "MiB", entityCategory: "diagnostic", state: (modified / 1024 / 1024).ToString("F1"));
                await PublishSensor("sensor", "Memory standby type", icon: "mdi:memory", stateClass: "measurement", unitOfMeasurement: "MiB", entityCategory: "diagnostic", state: (standby / 1024 / 1024).ToString("F1"));
                await PublishSensor("sensor", "Memory free type", icon: "mdi:memory", stateClass: "measurement", unitOfMeasurement: "MiB", entityCategory: "diagnostic", state: (free / 1024 / 1024).ToString("F1"));
            }

            await PublishSensor("sensor", $"Ping Internal", icon: "mdi:lan", stateClass: "measurement", unitOfMeasurement: "ms", entityCategory: "diagnostic", state: GetPing(GetInternalIPs())?.ToString("F1"));
            await PublishSensor("sensor", $"Ping External", icon: "mdi:wan", stateClass: "measurement", unitOfMeasurement: "ms", entityCategory: "diagnostic", state: GetPing(GetExternalIPs())?.ToString("F1"));

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

        static HashSet<IPAddress> GetInternalIPs()
        {
            var internalIps = new HashSet<IPAddress>();
            foreach (var inter in NetworkInterface.GetAllNetworkInterfaces())
            {
                var ipInfo = inter.GetIPProperties();
                foreach (var ip in ipInfo.DhcpServerAddresses)
                    internalIps.Add(ip);
                foreach (var ip in ipInfo.DnsAddresses)
                    internalIps.Add(ip);
                foreach (var ip in ipInfo.GatewayAddresses)
                    internalIps.Add(ip.Address);
            }
            return internalIps;
        }

        static HashSet<IPAddress> GetExternalIPs()
        {
            return new HashSet<IPAddress>
            {
                IPAddress.Parse("1.0.0.1"), // Cloudflare DNS
                IPAddress.Parse("1.1.1.1"), // Cloudflare DNS
                IPAddress.Parse("208.67.220.220"), // OpenDNS
                IPAddress.Parse("208.67.222.222"), // OpenDNS
                IPAddress.Parse("64.6.64.6"), // Verisign DNS
                IPAddress.Parse("64.6.65.6"), // Verisign DNS
                IPAddress.Parse("8.20.247.20"), // Comodo Secure DNS
                IPAddress.Parse("8.26.56.26"), // Comodo Secure DNS
                IPAddress.Parse("8.8.4.4"), // Google DNS
                IPAddress.Parse("8.8.8.8"), // Google DNS
                IPAddress.Parse("84.200.69.80"), // DNS.Watch
                IPAddress.Parse("84.200.70.40"), // DNS.Watch
                IPAddress.Parse("9.9.9.9") // Quad9 DNS
            };
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
                list.Add(list.Aggregate(new NameValueData("", 0, 0), (a, b) => new NameValueData("", a, b)));
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
                list.Add(list.Aggregate(new NameValueIO("", 0, 0), (a, b) => new NameValueIO("", a, b)));
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
                    list.Add(new NameValueIO(network.Key, network.Value.BytesReceivedPerSecond.NextValue(), network.Value.BytesSentPerSecond.NextValue(), network.Value.CurrentBandwidth.NextValue()));
                }
                list.Add(list.Aggregate(new NameValueIO("", 0, 0), (a, b) => new NameValueIO("", a, b)));
            }
            return list;
        }

        double? GetPing(HashSet<IPAddress> ips)
        {
            var pings = new List<long>();
            foreach (var ip in ips)
            {
                var ping = new Ping();
                try
                {
                    var reply = ping.Send(ip, 5000);
                    if (reply.Status == IPStatus.Success) pings.Add(reply.RoundtripTime);
                }
                catch (PingException error)
                {
                    VerboseLog($"Ping {ip} {error.GetBaseException().Message}");
                }
            }
            return pings.Count > 0 ? pings.Average() : null;
        }

        static float FixWrappedRawPageBytes(float value)
        {
            return value > 0x40000000000 ? 0 : value;
        }
    }
}
