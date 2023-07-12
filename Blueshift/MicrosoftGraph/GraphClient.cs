using Blueshift.OneDrive;

namespace Blueshift.MicrosoftGraph
{
    using System.Net;
    using System.Net.Http.Headers;
    using Blueshift.MicrosoftGraph.Model;

    using Newtonsoft.Json;

    public class GraphClient : IDisposable
    {
        public const string TokenEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
        public const string DefaultReturnUri = "https://login.microsoftonline.com/common/oauth2/nativeclient";
        public const string MicrosoftGraphBaseAddress = "https://graph.microsoft.com";

        protected HttpClient GraphHttpClient { get; private set; }
        protected HttpClient GraphHttpClientNoRedirect { get; private set; }

        public GraphClient(TokenResponse token)
        {
            Pre.ThrowIfArgumentNull(token, nameof(token));

            this.CurrentToken = token;

            HttpClientHandler noRedirectHandler = new HttpClientHandler { AllowAutoRedirect = false };

            this.GraphHttpClientNoRedirect = new HttpClient(noRedirectHandler)
            {
                BaseAddress = new Uri(MicrosoftGraphBaseAddress),
            };

            this.GraphHttpClient = new HttpClient()
            {
                BaseAddress = new Uri(MicrosoftGraphBaseAddress),
            };
        }

        public TokenResponse CurrentToken { get; set; }

        public event EventHandler<TokenRefreshedEventArgs> TokenRefreshed;

        public static string GetLogoutUri()
        {
            return string.Format(
                "https://login.microsoftonline.com/consumers/oauth2/logout?post_logout_redirect_uri='{0}'",
                DefaultReturnUri);
        }

        public static async Task<TokenResponse> GetAccessToken(AuthenticationResult authenticationResult)
        {
            using (HttpClient client = new HttpClient())
            {
                Dictionary<string, string> paramList = new Dictionary<string, string>
                {
                    ["client_id"] = Global.Configuration["AppId"],
                    ["redirect_uri"] = DefaultReturnUri,
                    ["code"] = authenticationResult.Code,
                    ["grant_type"] = "authorization_code",
                    ["scope"] = string.Join(" ", "openid", "files.read", "offline_access", "profile", "User.Read"),
                };

                FormUrlEncodedContent content = new FormUrlEncodedContent(paramList);

                var postResult = await client.PostAsync(TokenEndpoint, content).ConfigureAwait(false);

                string postContent = await postResult.Content.ReadAsStringAsync().ConfigureAwait(false);

                var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(postContent);

                tokenResponse.AcquireTime = DateTime.UtcNow;

                return tokenResponse;
            }
        }

        public async Task<UserProfile> GetUserProfileAsync()
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, MicrosoftGraphBaseAddress + "/v1.0/me");
            HttpResponseMessage response = await this.SendGraphRequestAsync(request).ConfigureAwait(false);

            // Request was successful. Read the content returned.
            string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            return JsonConvert.DeserializeObject<UserProfile>(content);
        }

        protected async Task<GraphResponse<T>> GetItemAsync<T>(string requestUri)
        {
            // Send the request to OneDrive and get the response.
            var response = await this
                .SendGraphRequestAsync(new HttpRequestMessage(HttpMethod.Get, requestUri))
                .ConfigureAwait(false);

            // Request was successful. Read the content returned.
            string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            T resultObject = JsonConvert.DeserializeObject<T>(content);
            return new GraphResponse<T>(resultObject);
        }


        public async Task<GraphResponse<T>> GetItemSet<T>(
            string requestUri,
            CancellationToken cancellationToken)
        {
            // Send the request to OneDrive and get the response.
            var response = await this.SendGraphRequestAsync(
                new HttpRequestMessage(HttpMethod.Get, requestUri),
                this.GraphHttpClient,
                cancellationToken).ConfigureAwait(false);

            // Request was successful. Read the content returned.
            string content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return JsonConvert.DeserializeObject<GraphResponse<T>>(content);
        }

        protected async Task<HttpResponseMessage> SendGraphRequestAsync(HttpRequestMessage request)
        {
            return await SendGraphRequestAsync(request, this.GraphHttpClient, CancellationToken.None)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Send an HTTP request to the OneDrive endpoint, handling the case when a token refresh is required.
        /// </summary>
        /// <remarks>
        /// The caller must provide the request to send. The authentication header will be set by this method. Any error
        /// returned by the call (including failure to refresh the token) will result in an exception being thrown.
        /// </remarks>
        protected async Task<HttpResponseMessage> SendGraphRequestAsync(
            HttpRequestMessage request,
            HttpClient client,
            CancellationToken cancellationToken)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("bearer", this.CurrentToken.AccessToken);

            try
            {
                // Attempt to send a request. Responses that indicate an error can be retried will be
                // retried, and throttling will be honored. Any other response will throw an exception.
                return await SendWithRetryAsync(request, client, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (GraphHttpException exception)
                when (IsTokenRefreshError(exception))
            {
                Global.Logger
                    .WithCallInfo()
                    .Information(
                        exception,
                        "Response indicates expired token. Will attempt token refresh.");
            }

            // The access token is expired. Refresh the token, then re-issue the request.
            await this.RefreshToken().ConfigureAwait(false);

            using var newRequest = await request.Clone().ConfigureAwait(false);

            // Re-add the access token now that it has been refreshed.
            newRequest.Headers.Authorization = new AuthenticationHeaderValue("bearer", this.CurrentToken.AccessToken);

            return await SendWithRetryAsync(newRequest, client, cancellationToken)
                .ConfigureAwait(false);
        }

        private static async Task<HttpResponseMessage> SendWithRetryAsync(
            HttpRequestMessage request,
            HttpClient client,
            CancellationToken cancellationToken)
        {
            const int maxAttemptCount = 3;

            int attemptCount = 0;
            while (true)
            {
                LogRequest(request, client.BaseAddress);

                var response = await client.SendAsync(request, cancellationToken)
                    .ConfigureAwait(false);

                attemptCount++;

                LogResponse(response);

                if (response.IsSuccessStatusCode ||
                    response.StatusCode == HttpStatusCode.Found)
                {
                    return response;
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests &&
                    attemptCount < maxAttemptCount)
                {
                    int retrySeconds = 10;
                    if (response.Headers.RetryAfter?.Delta != null)
                    {
                        retrySeconds = 
                            Convert.ToInt32(response.Headers.RetryAfter.Delta.Value.TotalSeconds);
                    }

                    Global.Logger
                        .WithCallInfo()
                        .Warning(
                            "Received TooManyRequests response from server. Delaying for {DelaySeconds} seconds",
                            retrySeconds);

                    await Task.Delay(retrySeconds, cancellationToken).ConfigureAwait(false);

                    continue;
                }

                if (attemptCount > maxAttemptCount &&
                    IsRetriableError(response))
                {
                    Global.Logger
                        .WithCallInfo()
                        .Warning(
                            "Received response code {StatusCode} from server (attempt {Attempt} of {MaxAttempts}). Request will be retried after delay.",
                            response.StatusCode,
                            attemptCount,
                            maxAttemptCount);

                    await Task.Delay(3000, cancellationToken).ConfigureAwait(false);

                    var newRequest = await request.Clone().ConfigureAwait(false);

                    // Not exactly required, but attempt to clean up the request & response objects
                    // before 
                    request.Dispose();
                    response.Dispose();
                    request = newRequest;

                    continue;
                }

                Global.Logger
                    .WithCallInfo()
                    .Warning(
                        "Returning error response after {Attempt} of {MaxAttempts} attempts",
                        attemptCount,
                        maxAttemptCount);

                var errorContainer =
                    await response.Content
                        .TryReadAsJsonAsync<GraphErrorContainer>()
                        .ConfigureAwait(false);

                // An error occurred that wasn't a token refresh issue
                var exception = GraphHttpException.FromResponse(response, errorContainer);

                response.Dispose();

                throw exception;
            }
        }

        private static bool IsRetriableError(HttpResponseMessage response)
        {
            int code = (int)response.StatusCode;

            // Error can be retried for 5xx responses
            return 500 <= code && code < 600;
        }

        private static bool IsTokenRefreshError(GraphHttpException exception)
        {
            return exception.StatusCode == HttpStatusCode.Unauthorized &&
                   exception.ErrorResponse.Code == "InvalidAuthenticationToken" &&
                   exception.ErrorResponse.Message != null &&
                   exception.ErrorResponse.Message.Contains("80049228");
        }

        private static void LogRequest(
            HttpRequestMessage request,
            Uri defaultBaseAddress,
            bool includeDetail = false)
        {
            Uri uri = request.RequestUri;

            Pre.Assert(uri != null, "uri != null");

            if (!uri.IsAbsoluteUri)
            {
                uri = new Uri(defaultBaseAddress, uri);
            }

            uri = uri.ReplaceQueryParameterIfExists("access_token", "<removed>");

            Global.Logger
                .WithCallInfo()
                .Debug(
                    "HttpRequest: {Method} to {Uri}",
                    request.Method,
                    uri);

            if (!includeDetail)
            {
                return;
            }

            Global.Logger
                .WithCallInfo()
                .Debug(
                    "Options: {Options}",
                    request.Options);

            Global.Logger
                .WithCallInfo()
                .Debug(
                    "Content Headers: {Headers}",
                    request.Content?.Headers);
        }

        private static void LogResponse(HttpResponseMessage response, bool includeDetail = false)
        {
            Global.Logger
                .WithCallInfo()
                .Debug(
                    "HttpResponse: {StatusCode} ({Reason})",
                    (int)response.StatusCode,
                    response.ReasonPhrase);

            if (!includeDetail)
            {
                return;
            }

            Global.Logger
                .WithCallInfo()
                .Debug(
                    "Headers: {Headers}",
                    response.Headers);

            Global.Logger
                .WithCallInfo()
                .Debug(
                    "Content Headers: {Headers}",
                    response.Content?.Headers);
        }

        private async Task RefreshToken()
        {
            Global.Logger
                .WithCallInfo()
                .Information("Refreshing token for Graph");

            using (HttpClient client = new HttpClient())
            {
                Dictionary<string, string> paramList = new Dictionary<string, string>
                {
                    ["client_id"] = Global.Configuration["AppId"],
                    ["redirect_uri"] = DefaultReturnUri,
                    ["refresh_token"] = this.CurrentToken.RefreshToken,
                    ["grant_type"] = "refresh_token"
                };

                FormUrlEncodedContent content = new FormUrlEncodedContent(paramList);

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
                {
                    Content = content
                };

                LogRequest(request, client.BaseAddress);
                HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                LogResponse(response);

                if (!response.IsSuccessStatusCode)
                {
                    // This will throw an exception according to the type of failure that occurred
                    await HandleTokenRefreshFailure(response).ConfigureAwait(false);
                }

                string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                this.CurrentToken = JsonConvert.DeserializeObject<TokenResponse>(responseContent);

                // We just acquired a new token through a refresh, so update the acquire time accordingly.
                this.CurrentToken.AcquireTime = DateTime.UtcNow;

                this.TokenRefreshed?.Invoke(this, new TokenRefreshedEventArgs { NewToken = this.CurrentToken });
            }
        }

        private static async Task HandleTokenRefreshFailure(HttpResponseMessage response)
        {
            // OneDrive specific logic: If the refresh token is expired, the server will return a 400 Bad Request
            // response with json content saying that the user must sign in.
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                IdentityPlatformError errorData = await response.Content.TryReadAsJsonAsync<IdentityPlatformError>().ConfigureAwait(false);
                if (errorData != null && errorData.Error == "invalid_grant")
                {
                    throw new TokenRefreshFailedException("The refresh token is expired.", errorData);
                }
            }

            // Dev note: Try to understand all of the refresh token failures. Any expected failures should be
            // throw as TokenRefreshFailedException. This is here as a catch-all.
            var exception = new GraphHttpException("Failed to refresh token.", response.StatusCode);

            if (response.Headers.Contains("WwwAuthenticate"))
            {
                exception.Data["HttpAuthenticationHeader"] = response.Headers.WwwAuthenticate;
            }

            throw exception;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.GraphHttpClient.Dispose();
                this.GraphHttpClient = null;

                this.GraphHttpClientNoRedirect.Dispose();
                this.GraphHttpClientNoRedirect = null;
            }

            // free native resources if there are any.
        }
    }
}