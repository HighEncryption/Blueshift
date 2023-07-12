namespace Blueshift
{
    using Microsoft.Extensions.Configuration;

    using Blueshift.Data;

    using Serilog;
    using Serilog.Core;
    using Serilog.Sinks.SystemConsole.Themes;

    public static class Global
    {
        public static string AppDataPath { get; private set; }

        public static Logger Logger { get; private set; }

        public static IConfiguration Configuration { get; private set; }

        public static IReadOnlyList<SyncSource> SyncSources { get; private set; }

        public static void Initialize(
            Dictionary<string, string> args = null)
        {
            args ??= new Dictionary<string, string>();

            string programDataPath =
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            AppDataPath = Path.Combine(programDataPath, "Blueshift");

            // Create the directory in case it doesn't exist
            Directory.CreateDirectory(AppDataPath);

            IConfigurationBuilder configurationBuilder = new ConfigurationBuilder();

            configurationBuilder.AddJsonFile("config.json");

            Configuration = configurationBuilder.Build();

            if (Logger == null)
            {
                var loggerConfig = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .Enrich.WithProperty("AppName", "Blueshift");

                if (!args.ContainsKey("noConsole"))
                {
                    loggerConfig =
                        loggerConfig.WriteTo.Console(
                            outputTemplate:
                            "[{Timestamp:s}] [{Level:u4}] [{SeqNum}] [{CallMemberName}] [{CallFileName}@{CallLineNumber}] {Message:lj}{NewLine}{Exception}",
                            theme: AnsiConsoleTheme.Literate);
                }

                if (Configuration.TryGetString("SeqConnectionString", out string connStr))
                {
                    if (!Configuration.TryGetString("SeqApiKey", out string apiKey))
                    {
                        apiKey = null;
                    }

                    loggerConfig = loggerConfig.WriteTo.Seq(connStr, apiKey: apiKey);
                }

                Logger = loggerConfig.CreateLogger();
            }
        }

        public static void InitializeSyncSources()
        {
            var sourcesSectionsValue = Global.Configuration["SourceSections"];
            Pre.ThrowIfArgumentNull(sourcesSectionsValue, "sourcesSectionsValue");

            var sourcesSections = sourcesSectionsValue.Split(",");

            List<SyncSource> sources = new();
            foreach (var sectionName in sourcesSections)
            {
                var section = Configuration.GetSection(sectionName);
                var source = new SyncSource()
                {
                    Name = sectionName,
                    Path = Path.Combine(AppDataPath, sectionName),
                    RootPath = section["RootPath"],
                    Disabled = Convert.ToBoolean(section["Disabled"]),
                    UserPrincipalName = section["UserPrincipalName"]
                };

                Pre.Assert(
                    !string.IsNullOrWhiteSpace(source.RootPath), 
                    "!string.IsNullOrWhiteSpace(source.RootPath)");

                if (sources.Any(s => s.Path == source.Path))
                {
                    throw new Exception("Duplicate source path");
                }

                if (sources.Any(s => s.RootPath == source.RootPath))
                {
                    throw new Exception("Duplicate source root");
                }

                DirectoryInfo sourceDirectoryInfo = new(source.RootPath);

                if (!sourceDirectoryInfo.Exists)
                {
                    Global.Logger
                        .WithCallInfo()
                        .Error(
                            "The root path {Path} could not be accessed for source {Name}",
                            source.RootPath,
                            source.Name);

                    throw new Exception(
                        $"Failed to locate root path '{source.RootPath}' for source '{source.Name}'");
                }

                Directory.CreateDirectory(source.Path);

                using (var db = new BlueshiftDb(source.Path))
                {
                    if (db.Database.EnsureCreated())
                    {
                        Global.Logger
                            .WithCallInfo()
                            .Information("Successfully created db at path {Path}", db.DatabasePath);
                    }
                }

                sources.Add(source);
            }

            SyncSources = sources.AsReadOnly();
        }
    }

    public static class ConfigurationExtensions
    {
        public static bool TryGetString(this IConfiguration config, string name, out string value)
        {
            IConfigurationSection item = 
                config.GetChildren().FirstOrDefault(c => c.Key == name);

            if (item == null)
            {
                value = null;
                return false;
            }

            value = item.Value;
            return true;
        }
    }

    public class SyncSource
    {
        public string Name { get; set; }

        public string Path { get; set; }

        public string RootPath { get; set; }

        public bool Disabled { get; set; }

        public string UserPrincipalName { get; set; }
    }
}