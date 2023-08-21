using System.Diagnostics;
using System.Runtime.Versioning;

namespace HA_Agent.Services
{
    [SupportedOSPlatform("Windows")]
    record WindowsNetworkIO(string AdapterName)
    {
        public PerformanceCounter BytesReceivedPerSecond { get; } = new PerformanceCounter("Network Adapter", "Bytes Received/sec", AdapterName);
        public PerformanceCounter BytesSentPerSecond { get; } = new PerformanceCounter("Network Adapter", "Bytes Sent/sec", AdapterName);
        public PerformanceCounter CurrentBandwidth { get; } = new PerformanceCounter("Network Adapter", "Current Bandwidth", AdapterName);

        public void NextValue()
        {
            BytesReceivedPerSecond.NextValue();
            BytesSentPerSecond.NextValue();
            CurrentBandwidth.NextValue();
        }
    }
}