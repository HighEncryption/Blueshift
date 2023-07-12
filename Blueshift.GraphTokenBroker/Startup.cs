namespace Blueshift.GraphTokenBroker
{
    using System;
    using System.Collections.Generic;

    using Blueshift.OneDrive;

    public class Startup
    {
        [STAThread]
        internal static int Main(string[] arguments)
        {
            Dictionary<string, string> args = CommandLineHelper.ParseCommandLineArgs(arguments);
            return App.Start(args);
        }
    }
}