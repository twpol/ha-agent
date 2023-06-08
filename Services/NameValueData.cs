namespace HA_Agent.Services
{
    record NameValueData(string Name, float FreeBytes, float TotalBytes)
    {
        public float FreeMiB => FreeBytes / 1024 / 1024;
        public float FreeGiB => FreeBytes / 1024 / 1024 / 1024;
        public float FreePercent => 100 * FreeBytes / TotalBytes;

        public float UsedBytes => TotalBytes - FreeBytes;
        public float UsedMiB => UsedBytes / 1024 / 1024;
        public float UsedGiB => UsedBytes / 1024 / 1024 / 1024;
        public float UsedPercent => 100 * UsedBytes / TotalBytes;

        public float TotalMiB => TotalBytes / 1024 / 1024;
        public float TotalGiB => TotalBytes / 1024 / 1024 / 1024;
    }
}