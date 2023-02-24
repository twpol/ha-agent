using HA_Agent.Services;
using Microsoft.Extensions.Configuration;

namespace HA_Agent.Agents
{
    class Noop : Agent
    {
        public Noop(HomeAssistant homeAssistant, IConfigurationSection config, bool verbose, bool dryRun)
            : base(homeAssistant, config.Key, "", verbose, dryRun)
        {
            Log($"Unknown device type: {config["type"]}");
        }

        public override Task Start()
        {
            return Task.CompletedTask;
        }

        public override Task Execute()
        {
            return Task.CompletedTask;
        }

        protected override IDictionary<string, object> GetDeviceConfig()
        {
            return new Dictionary<string, object>();
        }
    }
}
