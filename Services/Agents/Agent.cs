using HA_Agent.Services;

namespace HA_Agent.Agents
{
    abstract class Agent : Service
    {
        readonly HomeAssistant HomeAssistant;
        protected readonly string NodeId;
        protected readonly string NodeName;

        public Agent(HomeAssistant homeAssistant, string nodeId, string nodeName, bool verbose, bool dryRun)
            : base(verbose, dryRun)
        {
            HomeAssistant = homeAssistant;
            NodeId = GetSafeName(nodeId);
            NodeName = nodeName;
        }

        public abstract Task Start();

        public abstract Task Execute();

        protected override string GetName() => $"{GetType().Name}({NodeId})";

        protected abstract IDictionary<string, object> GetDeviceConfig();

        protected static string GetSafeName(string name) => name.ToLowerInvariant().Replace(' ', '_').Replace(":", "").Replace("(", "").Replace(")", "");

        protected async Task PublishSensor(
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
            var safeName = GetSafeName(name);
            var configTopic = $"{HomeAssistant.Prefix}/{component}/{NodeId}/{NodeId}_{safeName}/config";
            var stateTopic = $"{HomeAssistant.Prefix}/{component}/{NodeId}/{NodeId}_{safeName}/state";

            await HomeAssistant.Publish(configTopic, new Dictionary<string, object?>
            {
                { "state_class", stateClass },
                { "unit_of_measurement", unitOfMeasurement },
                { "device_class", deviceClass },
                { "icon", icon },
                { "name", $"{NodeName} {name}" },
                { "entity_category", entityCategory },
                { "device", GetDeviceConfig() },
                { "unique_id", $"{NodeId}_{safeName}" },
                { "state_topic", stateTopic },
            }.Where(kvp => kvp.Value != null).ToDictionary(kvp => kvp.Key, kvp => kvp.Value));

            await HomeAssistant.Publish(stateTopic, state);
        }
    }
}
