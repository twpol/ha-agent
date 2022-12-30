using System.Linq;
using System.Management;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using MQTTnet;
using MQTTnet.Client;

namespace HA_Agent
{
    class Agent
    {
        bool Verbose;
        IConfigurationSection ConfigMQTT;
        IConfigurationSection ConfigHA;
        string Prefix;
        string MachineName;
        string DeviceName;

        IMqttClient? Client;

        public Agent(IConfigurationRoot config, bool verbose)
        {
            Verbose = verbose;
            ConfigMQTT = config.GetSection("mqtt");
            ConfigHA = config.GetSection("homeassistant");
            Prefix = ConfigHA["prefix"] ?? "homeassistant";
            MachineName = Environment.MachineName.ToLowerInvariant();
            DeviceName = ConfigHA["deviceName"] ?? Environment.MachineName;
        }

        public async Task Start()
        {
            Client = new MqttFactory().CreateMqttClient();
            Client.ConnectedAsync += e => { VerboseLog($"MQTT connected to {Client.Options.ChannelOptions}"); return Task.CompletedTask; };
            Client.DisconnectedAsync += e => { VerboseLog($"MQTT disconnected from {Client.Options.ChannelOptions}"); return Task.CompletedTask; };

            var options = new MqttClientOptionsBuilder().WithTcpServer(ConfigMQTT["server"], int.Parse(ConfigMQTT["port"] ?? "1883"));
            if (ConfigMQTT["username"] != null) options = options.WithCredentials(ConfigMQTT["username"], ConfigMQTT["password"]);
            await Client.ConnectAsync(options.Build());
        }

        public async Task Execute()
        {
            if (Client == null) throw new InvalidOperationException("Cannot execute on uninitialised agent");
            if (!Client.IsConnected) throw new InvalidOperationException("Not connected to MQTT");

            VerboseLog("Execute: Start");

            await PublishSensor("sensor", "Last Reboot", icon: "mdi:restart", deviceClass: "timestamp", entityCategory: "diagnostic", state: GetLastReboot().ToString("s") + "Z");
            await PublishSensor("sensor", "Battery Level", icon: "mdi:battery-charging", deviceClass: "battery", unitOfMeasurement: "%", entityCategory: "diagnostic", state: GetBatteryLevel().ToString());
            foreach (var data in GetMemoryData()) await PublishSensor("sensor", $"{data.Name} Memory Free", icon: "mdi:memory", stateClass: "measurement", unitOfMeasurement: "MiB", entityCategory: "diagnostic", state: data.FreeMiB.ToString("G3"));
            foreach (var data in GetStorageData()) await PublishSensor("sensor", $"{data.Name} Storage Free", icon: "mdi:harddisk", stateClass: "measurement", unitOfMeasurement: "GiB", entityCategory: "diagnostic", state: data.FreeGiB.ToString("G3"));

            VerboseLog("Execute: Finish");
        }

        async Task PublishSensor(
            string component,
            string name,
            string? deviceClass = null,
            string? entityCategory = null,
            string? icon = null,
            string? stateClass = null,
            string? unitOfMeasurement = null,
            string state = ""
        )
        {
            var prefix = $"{Prefix}/{component}/{MachineName}/{MachineName}";
            var safeName = name.ToLowerInvariant().Replace(' ', '_').Replace(":", "");
            var stateTopic = $"{prefix}_{safeName}/state";

            await Publish($"{prefix}_{safeName}/config", new Dictionary<string, object?>
            {
                { "state_class", stateClass },
                { "unit_of_measurement", unitOfMeasurement },
                { "device_class", deviceClass },
                { "icon", icon },
                { "name", $"{DeviceName} {name}" },
                { "entity_category", "diagnostic" },
                { "device", GetDeviceConfig() },
                { "unique_id", $"{MachineName}_{safeName}" },
                { "state_topic", stateTopic },
            }.Where(kvp => kvp.Value != null).ToDictionary(kvp => kvp.Key, kvp => kvp.Value));

            await Publish(stateTopic, state);
        }

        async Task Publish(string topic, IDictionary<string, object?> json, bool retain = false)
        {
            await Publish(topic, JsonSerializer.Serialize(json), retain);
        }

        async Task Publish(string topic, string payload, bool retain = false)
        {
            if (Client == null) throw new InvalidOperationException("Cannot publish on uninitialised agent");

            VerboseLog($"Publish {topic} payload {payload}");
            await Client.PublishAsync(new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithRetainFlag(retain)
                .Build());
        }

        IDictionary<string, object>? DeviceConfig;
        IDictionary<string, object> GetDeviceConfig()
        {
            if (DeviceConfig == null)
            {
                DeviceConfig = new Dictionary<string, object>
                {
                    { "identifiers", $"ha-agent.{MachineName}" },
                    { "manufacturer", GetDeviceManufacturer() },
                    { "model", GetDeviceModel() },
                    { "name", DeviceName },
                };
            }
            return DeviceConfig;
        }

        void VerboseLog(string message)
        {
            if (Verbose) Console.WriteLine($"{DateTimeOffset.UtcNow.ToString("u")}: {message}");
        }

        string GetDeviceManufacturer()
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

        string GetDeviceModel()
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

        DateTimeOffset GetLastReboot()
        {
            return DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64);
        }

        int GetBatteryLevel()
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

        List<NameValueData> GetMemoryData()
        {
            var list = new List<NameValueData>();
            if (OperatingSystem.IsWindows())
            {
                var query = new ObjectQuery("SELECT * FROM Win32_OperatingSystem");
                var searcher = new ManagementObjectSearcher(query);
                var collection = searcher.Get().GetEnumerator();
                if (collection.MoveNext())
                {
                    list.Add(new NameValueData("Physical", (ulong)collection.Current["FreePhysicalMemory"] * 1024));
                    list.Add(new NameValueData("Paging", (ulong)collection.Current["FreeSpaceInPagingFiles"] * 1024));
                    list.Add(new NameValueData("Virtual", (ulong)collection.Current["FreeVirtualMemory"] * 1024));
                }
            }
            return list;
        }

        List<NameValueData> GetStorageData()
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
                        list.Add(new NameValueData((string)volume["DriveLetter"], (ulong)volume["FreeSpace"]));
                    }
                }
            }
            return list;
        }
    }

    record NameValueData(string Name, float FreeBytes)
    {
        public float FreeMiB => FreeBytes / 1024 / 1024;
        public float FreeGiB => FreeBytes / 1024 / 1024 / 1024;
    }
}
