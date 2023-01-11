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
            if (config == null) config = new FileInfo("config.json");
            var agent = new Agent(LoadConfiguration(config), verbose, dryRun);
            await agent.Start();
            if (once)
            {
                Thread.Sleep(10000);
                await agent.Execute();
            }
            else
            {
                var offsetSecondsMs = new Random().Next(60000);
                while (true)
                {
                    Thread.Sleep(60000 - (int)((DateTimeOffset.Now.ToUnixTimeMilliseconds() - offsetSecondsMs) % 60000));
                    await agent.Execute();
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
