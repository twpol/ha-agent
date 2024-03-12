using HA_Agent.Services;
using Microsoft.Exchange.WebServices.Data;
using Microsoft.Extensions.Configuration;
using Task = System.Threading.Tasks.Task;

namespace HA_Agent.Agents
{
    class Exchange : Agent
    {
        readonly IConfigurationSection Config;

        readonly ExchangeService Service;

        DateTimeOffset NextSyncTime = DateTimeOffset.MinValue;

        public Exchange(HomeAssistant homeAssistant, IConfigurationSection config, bool verbose, bool dryRun)
            : base(homeAssistant, config["email"] ?? "(unnamed)", config["name"] ?? config["email"] ?? "(unnamed)", verbose, dryRun)
        {
            Config = config;
            UpdateFrequency = 5;
            Service = new ExchangeService(ExchangeVersion.Exchange2016)
            {
                Credentials = new WebCredentials(Config["username"], Config["password"])
            };
        }

        protected override Task DoStart()
        {
            Service.AutodiscoverUrl(Config["email"], redirectionUri => new Uri(redirectionUri).Scheme == "https");
            VerboseLog($"Connected to {Service.Url}");
            return Task.CompletedTask;
        }

        protected override async Task DoExecute()
        {
            if (NextSyncTime > DateTimeOffset.Now)
            {
                VerboseLog($"Execute: Skip (next sync at {NextSyncTime})");
                return;
            }

            VerboseLog("Execute: Start");

            try
            {
                var inbox = await Folder.Bind(Service, WellKnownFolderName.Inbox);
                var subfolders = await inbox.FindFolders(new FolderView(int.MaxValue) { Traversal = FolderTraversal.Deep });
                var folders = subfolders.Prepend(inbox).ToList();
                VerboseLog($"Folders: {string.Join(", ", folders.Select(folder => folder.DisplayName))}");

                foreach (var folder in folders)
                {
                    await PublishSensor("sensor", $"{folder.DisplayName} total", icon: "mdi:email", stateClass: "measurement", unitOfMeasurement: "emails", entityCategory: "diagnostic", state: folder.TotalCount.ToString("F0"));
                    await PublishSensor("sensor", $"{folder.DisplayName} unread", icon: "mdi:email", stateClass: "measurement", unitOfMeasurement: "emails", entityCategory: "diagnostic", state: folder.UnreadCount.ToString("F0"));
                }
            }
            catch (ServerBusyException error)
            {
                Log($"Server busy; wait {error.BackOffMilliseconds} ms");
                NextSyncTime = DateTimeOffset.Now.AddMilliseconds(error.BackOffMilliseconds);
            }
            catch (ServiceRemoteException error)
            {
                Log(error.ToString());
            }

            VerboseLog("Execute: Finish");
        }
    }
}
