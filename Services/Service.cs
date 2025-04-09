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

        protected virtual string GetName() => GetType().Name;

        protected void Log(string message)
        {
            Console.WriteLine($"{DateTimeOffset.UtcNow:u}: {GetName()}: {message}");
        }

        protected void VerboseLog(string message)
        {
            if (Verbose) Log(message);
        }
    }
}
