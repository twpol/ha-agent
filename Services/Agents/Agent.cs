using System.Text.RegularExpressions;
using HA_Agent.Services;

namespace HA_Agent.Agents
{
    abstract class Agent : Service
    {
        readonly HomeAssistant HomeAssistant;
        protected readonly string NodeId;
        protected readonly string NodeName;
        protected int UpdateFrequency { get; init; } = 1;

        int UpdateCounter;

        public Agent(HomeAssistant homeAssistant, string nodeId, string nodeName, bool verbose, bool dryRun)
            : base(verbose, dryRun)
        {
            HomeAssistant = homeAssistant;
            NodeId = GetSafeName(nodeId);
            NodeName = nodeName;
        }

        public Task Start()
        {
            UpdateCounter = UpdateFrequency;
            return DoStart();
        }

        public Task Execute()
        {
            if (++UpdateCounter >= UpdateFrequency)
            {
                UpdateCounter = 0;
                return DoExecute();
            }
            return Task.CompletedTask;
        }

        protected abstract Task DoStart();

        protected abstract Task DoExecute();

        protected override string GetName() => $"{GetType().Name}({NodeId})";

        IDictionary<string, object>? DeviceConfig;
        protected virtual IDictionary<string, object> GetCustomDeviceConfig() => new Dictionary<string, object>();
        IDictionary<string, object> GetDeviceConfig()
        {
            if (DeviceConfig == null)
            {
                DeviceConfig = GetCustomDeviceConfig();
                DeviceConfig["identifiers"] = $"ha-agent.{NodeId}";
                DeviceConfig["name"] = NodeName;
            }
            return DeviceConfig;
        }


        readonly static Regex NormaliseSpaceRegex = new(" +", RegexOptions.Compiled);

        protected static string GetNormalised(string name) => NormaliseSpaceRegex.Replace(name, " ").Trim();

        // The ID of the device must only consist of characters from the character class [a-zA-Z0-9_-] (alphanumerics, underscore and hyphen).
        // Not including the underscore ("_") here allows any existing underscores to merge into disallowed characters.
        readonly static Regex SafeNameRegex = new("[^a-zA-Z0-9-]+", RegexOptions.Compiled);

        protected static string GetSafeName(string name) => SafeNameRegex.Replace(name.ToLowerInvariant(), "_").Trim('_');

        protected async Task PublishSensor(
            string component,
            string name,
            string? deviceClass = null,
            string? entityCategory = null,
            string? icon = null,
            string? stateClass = null,
            string? unitOfMeasurement = null,
            string? state = ""
        )
        {
            if (state == null) return;
            name = GetNormalised(name);
            var safeName = GetSafeName(name);
            var configTopic = $"{HomeAssistant.Prefix}/{component}/{NodeId}/{NodeId}_{safeName}/config";
            var stateTopic = $"{HomeAssistant.Prefix}/{component}/{NodeId}/{NodeId}_{safeName}/state";

            await HomeAssistant.Publish(configTopic, new Dictionary<string, object?>
            {
                { "state_class", stateClass },
                { "unit_of_measurement", unitOfMeasurement },
                { "device_class", deviceClass },
                { "icon", icon },
                { "name", name },
                { "entity_category", entityCategory },
                { "device", GetDeviceConfig() },
                { "unique_id", $"{NodeId}_{safeName}" },
                { "state_topic", stateTopic },
                { "expire_after", (int)(HomeAssistant.UpdateIntervalS * UpdateFrequency * 5.5) },
            }.Where(kvp => kvp.Value != null).ToDictionary(kvp => kvp.Key, kvp => kvp.Value));

            await HomeAssistant.Publish(stateTopic, state);
        }
    }
}
