namespace Blueshift.GraphTokenBroker
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Threading;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private Dispatcher localDispatcher;

        internal new static App Current { get; private set; }

        internal static int Start(Dictionary<string, string> args)
        {
            App app = new App();
            Current = app;

            app.localDispatcher = Dispatcher.CurrentDispatcher;

            using (Task task = new Task(app.Initialize, args))
            {
                task.Start();
                app.Run();
            }

            return TokenProvider.TokenSuccess ? 1 : 0;
        }

        private void Initialize(object objArgs)
        {
            Dictionary<string, string> args = (Dictionary<string, string>)objArgs;

            if (!args.TryGetValue("getToken", out string _))
            {
                return;
            }

            if (!args.TryGetValue("path", out string path))
            {
                return;
            }

            string parentPath = Path.GetDirectoryName(path);
            if (!Directory.Exists(parentPath))
            {
                Directory.CreateDirectory(parentPath);
            }

            Global.Initialize();

            this.localDispatcher.Invoke(() =>
            {
                TokenProvider.SignIn(path);
            });
        }
    }
}
