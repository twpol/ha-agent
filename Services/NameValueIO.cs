namespace HA_Agent.Services
{
    record NameValueIO(string Name, float ReadBytesPerSec, float WriteBytesPerSec)
    {
        public float ReadKiBPerSec => ReadBytesPerSec / 1024;
        public float ReadMiBPerSec => ReadBytesPerSec / 1024 / 1024;

        public float WriteKiBPerSec => WriteBytesPerSec / 1024;
        public float WriteMiBPerSec => WriteBytesPerSec / 1024 / 1024;

        public float TotalBytesPerSec => ReadBytesPerSec + WriteBytesPerSec;
        public float TotalKiBPerSec => TotalBytesPerSec / 1024;
        public float TotalMiBPerSec => TotalBytesPerSec / 1024 / 1024;
    }
}