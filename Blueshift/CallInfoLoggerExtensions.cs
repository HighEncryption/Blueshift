namespace Blueshift
{
    using System.Runtime.CompilerServices;

    using Serilog;

    public static class CallInfoLoggerExtensions
    {
        private static long seqNum;

        public static ILogger WithCallInfo(this ILogger logger,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            long thisSeqNum = Interlocked.Increment(ref seqNum);

            return logger
                .ForContext("SeqNum", thisSeqNum)
                .ForContext("CallMemberName", memberName)
                .ForContext("CallFileName", filePath.Substring(filePath.LastIndexOf('\\') + 1))
                .ForContext("CallLineNumber", lineNumber);
        }
    }
}