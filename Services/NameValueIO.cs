namespace HA_Agent.Services
{
    record NameValueIO(string Name, float ReadBytesPerSec, float WriteBytesPerSec, float BandwidthBitsPerSec = 0)
    {
        public float ReadKiBPerSec => ReadBytesPerSec / 1024;
        public float ReadMiBPerSec => ReadBytesPerSec / 1024 / 1024;

        public float WriteKiBPerSec => WriteBytesPerSec / 1024;
        public float WriteMiBPerSec => WriteBytesPerSec / 1024 / 1024;

        public float TotalBytesPerSec => ReadBytesPerSec + WriteBytesPerSec;
        public float TotalKiBPerSec => TotalBytesPerSec / 1024;
        public float TotalMiBPerSec => TotalBytesPerSec / 1024 / 1024;

        public float BandwidthMbitPerSec => BandwidthBitsPerSec / 1000 / 1000;
        public float BandwidthKiBPerSec => BandwidthBitsPerSec / 8 / 1024;

        public NameValueIO(string name, NameValueIO left, NameValueIO right)
        : this(name, left.ReadBytesPerSec + right.ReadBytesPerSec, left.WriteBytesPerSec + right.WriteBytesPerSec, left.BandwidthBitsPerSec + right.BandwidthBitsPerSec)
        { }
    }
}