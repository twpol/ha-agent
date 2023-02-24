using Microsoft.Extensions.Configuration;

namespace HA_Agent
{
    class Program
    {
        /// <summary>A service which collects device data for Home Assistant</summary>
        /// <param name="config">Path to configuration file</param>
        /// <param name="verbose">Display more details about what's going on</param>
        /// <param name="dryRun">Do not perform any actions, only pretend</param>
        /// <param name="once">Run data collection once only</param>
        static async Task Main(FileInfo? config = null, bool verbose = false, bool dryRun = false, bool once = false)
        {
            config ??= new FileInfo("config.json");
            var configRoot = LoadConfiguration(config);

            var ha = new Services.HomeAssistant(configRoot.GetSection("homeassistant"), verbose, dryRun);
            await ha.Connect();

            var agents = configRoot.GetSection("agents").GetChildren().Select<IConfigurationSection, Agents.Agent>(section => section["type"] switch
            {
                "system" => new Agents.System(ha, section, verbose, dryRun),
                _ => new Agents.Noop(ha, section, verbose, dryRun),
            }).ToList();
            foreach (var agent in agents) await agent.Start();

            if (once)
            {
                Thread.Sleep(10000);
                foreach (var agent in agents) await agent.Execute();
            }
            else
            {
                var offsetSecondsMs = new Random().Next(60000);
                while (true)
                {
                    Thread.Sleep(60000 - (int)((DateTimeOffset.Now.ToUnixTimeMilliseconds() - offsetSecondsMs) % 60000));
                    foreach (var agent in agents) await agent.Execute();
                }
            }
        }

        static IConfigurationRoot LoadConfiguration(FileInfo config)
        {
            return new ConfigurationBuilder()
                .AddJsonFile(config.FullName, true)
                .Build();
        }
    }
}
