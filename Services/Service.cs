namespace HA_Agent.Services
{
    abstract class Service
    {
        protected readonly bool Verbose;
        protected readonly bool DryRun;

        public Service(bool verbose, bool dryRun)
        {
            Verbose = verbose;
            DryRun = dryRun;
        }

        protected void VerboseLog(string message)
        {
            if (Verbose) Console.WriteLine($"{DateTimeOffset.UtcNow:u}: {message}");
        }
    }
}
