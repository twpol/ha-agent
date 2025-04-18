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

        protected override Task DoStart()
        {
            return Task.CompletedTask;
        }

        protected override Task DoExecute()
        {
            return Task.CompletedTask;
        }
    }
}
