namespace Blueshift.GraphTokenBroker
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;

    using Blueshift.MicrosoftGraph;
    using Blueshift.OneDrive;

    public static class TokenProvider
    {
        private static string tokenPath;
        private static readonly AutoResetEvent authComplete = new(false);

        public static bool TokenSuccess { get; set; }

        public static void SignIn(string path)
        {
            tokenPath = path;

            string logoutUri = GraphClient.GetLogoutUri();
            string authorizationUri = OneDriveClient.GetAuthorizationUri();

            AuthenticationResult authenticationResult = null;
            BrowserAuthenticationWindow authWindow = new();
            authWindow.Browser.Navigated += (_, args) =>
            {
                if (string.Equals(args.Uri.AbsolutePath, "/consumers/oauth2/logout", StringComparison.OrdinalIgnoreCase))
                {
                    // The logout page has finished loading, so we can now load the login page.
                    authWindow.Browser.Navigate(authorizationUri);
                }

                // If the browser is directed to the redirect URI, the new URI will contain the access code that we can use to 
                // get a token for OneDrive.
                if (string.Equals(args.Uri.AbsolutePath, "/common/oauth2/nativeclient", StringComparison.OrdinalIgnoreCase))
                {
                    // We were directed back to the redirect URI. Extract the code from the query string
                    Dictionary<string, string> queryParameters = args.Uri.GetQueryParameters();

                    authenticationResult = new AuthenticationResult()
                    {
                        Code = queryParameters["code"]
                    };

                    // All done. Close the window.
                    authWindow.Close();
                }
            };

            authWindow.Closed += (_, _) =>
            {
                if (authenticationResult == null)
                {
                    authComplete.Set();
                    return;
                }

                Task.Factory.StartNew(async () =>
                {
                    TokenResponse currentToken = await GraphClient.GetAccessToken(authenticationResult).ConfigureAwait(false);

                    currentToken.SaveProtectedToken(tokenPath);
                    TokenSuccess = true;
                    authComplete.Set();
                });
            };

            authWindow.Loaded += (_, _) =>
            {
                var ptr = Marshal.AllocHGlobal(4);
                Marshal.WriteInt32(ptr, 3);
                InternetSetOption(IntPtr.Zero, 81, ptr, 4);
                Marshal.FreeHGlobal(ptr);

                authWindow.Browser.Navigate(logoutUri);
                //SyncPro.UI.NativeMethods.User32.SetForegroundWindow(new WindowInteropHelper(authWindow).Handle);
            };

            authWindow.ShowDialog();

            authComplete.WaitOne();
        }

        [DllImport("wininet.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool InternetSetOption(IntPtr hInternet, int dwOption,
            IntPtr lpBuffer, int dwBufferLength);
    }
}
