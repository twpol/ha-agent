using System.Text.Json;
using Microsoft.Extensions.Configuration;
using MQTTnet;
using MQTTnet.Client;

namespace HA_Agent.Services
{
    class HomeAssistant : Service
    {
        public readonly string Prefix;
        public readonly int UpdateIntervalS;
        public int UpdateIntervalMS => UpdateIntervalS * 1000;

        readonly IConfigurationSection Config;
        readonly IMqttClient Client;

        public HomeAssistant(IConfigurationSection config, bool verbose, bool dryRun)
            : base(verbose, dryRun)
        {
            Config = config;
            Prefix = Config["prefix"] ?? "homeassistant";
            if (!int.TryParse(Config["updateS"], out UpdateIntervalS)) UpdateIntervalS = 60;

            Client = new MqttFactory().CreateMqttClient();
            Client.ConnectedAsync += e => { VerboseLog($"MQTT connected to {Client.Options.ChannelOptions}"); return Task.CompletedTask; };
            Client.DisconnectedAsync += e => { VerboseLog($"MQTT disconnected from {Client.Options.ChannelOptions}"); return Task.CompletedTask; };
        }

        public async Task Connect()
        {
            var options = new MqttClientOptionsBuilder().WithTcpServer(Config["server"], int.Parse(Config["port"] ?? "1883"));
            if (Config["username"] != null) options = options.WithCredentials(Config["username"], Config["password"]);
            await Client.ConnectAsync(options.Build());
        }

        public async Task Publish(string topic, IDictionary<string, object?> json, bool retain = false)
        {
            await Publish(topic, JsonSerializer.Serialize(json), retain);
        }

        public async Task Publish(string topic, string payload, bool retain = false)
        {
            if (!Client.IsConnected) throw new InvalidOperationException("Not connected to MQTT");

            if (DryRun)
            {
                VerboseLog($"Would publish {topic} payload {payload}");
            }
            else
            {
                VerboseLog($"Publish {topic} payload {payload}");
                await Client.PublishAsync(new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payload)
                    .WithRetainFlag(retain)
                    .Build());
            }
        }
    }
}
