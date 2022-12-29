using Microsoft.Extensions.Configuration;
using MQTTnet;
using MQTTnet.Client;

namespace HA_Agent
{
    class Agent
    {
        bool Verbose;
        IMqttClient Client;

        public Agent(IConfigurationRoot config, bool verbose)
        {
            Verbose = verbose;

            var configMqtt = config.GetSection("mqtt");

            Client = new MqttFactory().CreateMqttClient();
            Client.ConnectedAsync += e => { VerboseLog($"MQTT connected to {Client.Options.ChannelOptions}"); return Task.CompletedTask; };
            Client.DisconnectedAsync += e => { VerboseLog($"MQTT disconnected from {Client.Options.ChannelOptions}"); return Task.CompletedTask; };

            var options = new MqttClientOptionsBuilder().WithTcpServer(configMqtt["server"], int.Parse(configMqtt["port"] ?? "1883"));
            if (configMqtt["username"] != null) options = options.WithCredentials(configMqtt["username"], configMqtt["password"]);
            Client.ConnectAsync(options.Build()).Wait();
        }

        public void Execute()
        {
            VerboseLog("Execute: Start");
            if (!Client.IsConnected) throw new InvalidOperationException("Not connected to MQTT");

            // TODO: Implement this

            VerboseLog("Execute: Finish");
        }

        void VerboseLog(string message)
        {
            if (Verbose) Console.WriteLine($"{DateTimeOffset.UtcNow.ToString("u")}: {message}");
        }
    }
}
