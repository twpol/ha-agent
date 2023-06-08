using System.Diagnostics;
using System.Runtime.Versioning;

namespace HA_Agent.Services
{
    [SupportedOSPlatform("Windows")]
    record WindowsDiskIO(string DriveLetter)
    {
        public PerformanceCounter BytesReadPerSecond { get; } = new PerformanceCounter("LogicalDisk", "Disk Read Bytes/sec", DriveLetter);
        public PerformanceCounter BytesWritePerSecond { get; } = new PerformanceCounter("LogicalDisk", "Disk Write Bytes/sec", DriveLetter);

        public void NextValue()
        {
            BytesReadPerSecond.NextValue();
            BytesWritePerSecond.NextValue();
        }
    }
}