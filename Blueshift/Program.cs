namespace Blueshift
{
    using System.Diagnostics;

    using Blueshift.OneDrive;

    using Newtonsoft.Json;

    internal class Program
    {
        public static CancellationTokenSource Cts { get; } = new();

        static void Main(string[] argsArray)
        {
            Dictionary<string, string> args = CommandLineHelper.ParseCommandLineArgs(argsArray);

            if (!args.Any())
            {
                Console.WriteLine("No args provided");
                return;
            }

            Global.Initialize(args);

            if (!AcquireLock())
            {
                return;
            }

            try
            {
                Global.InitializeSyncSources();

                Console.CancelKeyPress += ConsoleOnCancelKeyPress;

                if (args.ContainsKey("refreshTokens"))
                {
                    Task.Run(async () =>
                    {
                        var syncManager = new SyncManager();
                        await syncManager.RefreshTokens().ConfigureAwait(false);
                    }).Wait();

                    return;
                }

                if (args.ContainsKey("sync"))
                {
                    Task.Run(async () =>
                    {
                        var syncManager = new SyncManager();
                        await syncManager.SyncAsync().ConfigureAwait(false);
                    }).Wait();
                }
            }
            catch (Exception exception)
            {
                Global.Logger
                    .WithCallInfo()
                    .Fatal(
                        exception,
                        "Caught unhandled exception from Main() call");
            }
            finally
            {
                Global.Logger.Dispose();

                File.Delete(Path.Combine(Global.AppDataPath, ".lock"));
            }
        }

        private static bool AcquireLock()
        {
            FileInfo lockFile = new FileInfo(
                Path.Combine(Global.AppDataPath, ".lock"));

            bool appAlreadyRunning = false;
            LockInfo lockInfo = null;

            if (!lockFile.Exists)
            {
                Global.Logger
                    .WithCallInfo()
                    .Information(
                        "Lock file {Path} not found",
                        lockFile.FullName);
            }
            else
            {
                try
                {
                    lockInfo = JsonConvert.DeserializeObject<LockInfo>(
                        File.ReadAllText(lockFile.FullName));

                    if (lockInfo == null)
                    {
                        throw new InvalidDataException("The content of the lock file is empty");
                    }

                    Global.Logger
                        .WithCallInfo()
                        .Information(
                            "Lock file contains PID={PID}, ProcessName={ProcessName}, StartTime={StartTime}",
                            lockInfo.Pid,
                            lockInfo.ProcessName,
                            lockInfo.StartTime);
                }
                catch (Exception exception)
                {
                    Global.Logger
                        .WithCallInfo()
                        .Fatal(
                            exception,
                            "Failed to read lock file content. Not starting app.");

                    appAlreadyRunning = true;
                }

                if (!appAlreadyRunning)
                {
                    try
                    {
                        Process existingProcess = Process.GetProcessById(lockInfo.Pid);
                        if (!existingProcess.HasExited)
                        {
                            Global.Logger
                                .WithCallInfo()
                                .Information(
                                    "App {ProcessName} is running with active lock. Exiting.",
                                    existingProcess.ProcessName);

                            appAlreadyRunning = true;
                        }
                    }
                    catch (ArgumentException)
                    {
                        Global.Logger
                            .WithCallInfo()
                            .Warning(
                                "Lock file contains PID {pid} which does not appear to be running. Deleting lock file.",
                                lockInfo.Pid);

                        lockFile.Delete();

                        Global.Logger
                            .WithCallInfo()
                            .Information(
                                "Delete of file {Path} succeeded",
                                lockFile.FullName);
                    }
                }
            }

            if (appAlreadyRunning)
            {
                return false;
            }

            Process currentProcess = Process.GetCurrentProcess();
            lockInfo = new LockInfo()
            {
                Pid = currentProcess.Id,
                ProcessName = currentProcess.ProcessName,
                StartTime = currentProcess.StartTime
            };

            File.WriteAllText(
                lockFile.FullName, 
                JsonConvert.SerializeObject(lockInfo));

            Global.Logger
                .WithCallInfo()
                .Verbose(
                    "Created lock file {Path} for PID={Pid}, ProcessName={ProcessName}, StartTime={StartTime}",
                    lockFile.FullName,
                    lockInfo.Pid,
                    lockInfo.ProcessName,
                    lockInfo.StartTime);

            return true;
        }

        private static void ConsoleOnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Global.Logger
                .WithCallInfo()
                .Warning("Caught Ctrl-C. Cancelling.");

            e.Cancel = true;

            Cts.Cancel();
        }
    }

    public class LockInfo
    {
        public int Pid { get; set; }

        public string ProcessName { get; set; }

        public DateTimeOffset StartTime { get; set; }
    }
}