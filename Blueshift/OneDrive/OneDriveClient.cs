namespace Blueshift.OneDrive
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;

    using Blueshift.MicrosoftGraph;
    using Blueshift.MicrosoftGraph.Model;
    using Blueshift.OneDrive.Model;

    public class OneDriveClient : GraphClient
    {
        public const string DefaultDriveName = "OneDrive";

        public OneDriveClient(TokenResponse token)
            : base(token)
        {
        }

        public static string GetAuthorizationUri()
        {
            return string.Format(
                "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize?client_id={0}&scope={1}&response_type=code&redirect_uri={2}",
                Global.Configuration["AppId"],
                HttpUtility.UrlEncode(string.Join(" ", "openid", "files.read", "offline_access", "profile", "User.Read")),
                DefaultReturnUri);
        }

        public async Task<Drive> GetDefaultDrive()
        {
            GraphResponse<Drive> response = await this.GetItemAsync<Drive>("/v1.0/me/drive").ConfigureAwait(false);
            return response.Value;
        }

        public async Task<DriveItem> GetDefaultDriveRoot()
        {
            GraphResponse<DriveItem> response = await this.GetItemAsync<DriveItem>("/v1.0/me/drive/root").ConfigureAwait(false);
            return response.Value;
        }

        public async Task<DriveItem> GetItemAsync(string itemId)
        {
            string requestUri = string.Format("/v1.0/me/drive/items/{0}", itemId);
            var response = await this.GetItemAsync<DriveItem>(requestUri).ConfigureAwait(false);
            return response.Value;
        }

        public async Task<DriveItem> GetItemByPathAsync(string path)
        {
            string requestUri = "/v1.0/me/drive/root:/" + path.TrimStart('/');
            var response = await this.GetItemAsync<DriveItem>(requestUri).ConfigureAwait(false);
            return response.Value;
        }

        public async Task<IEnumerable<DriveItem>> GetChildItems(Drive drive)
        {
            string requestUri = string.Format("/v1.0/drives/{0}/root/children", drive.Id);

            List<DriveItem> items = new List<DriveItem>();
            while (true)
            {
                GraphResponse<DriveItem[]> oneDriveResponse =
                    await this.GetItemSet<DriveItem[]>(requestUri, CancellationToken.None).ConfigureAwait(false);

                items.AddRange(oneDriveResponse.Value);

                if (string.IsNullOrWhiteSpace(oneDriveResponse.NextLink))
                {
                    break;
                }

                requestUri = oneDriveResponse.NextLink;
            }

            return items;
        }

        public async Task<IEnumerable<DriveItem>> GetChildItems(DriveItem parent)
        {
            // If we know the item is NOT a folder, or if it is a folder and has no children, return an empty list since
            // we know that it will not have any child items.
            if (parent.Folder == null || parent.Folder.ChildCount == 0)
            {
                return new List<DriveItem>();
            }

            string requestUri = string.Format("/v1.0/me/drive/items/{0}/children", parent.Id);

            List<DriveItem> items = new List<DriveItem>();
            while (true)
            {
                GraphResponse<DriveItem[]> oneDriveResponse =
                    await this.GetItemSet<DriveItem[]>(requestUri, CancellationToken.None).ConfigureAwait(false);

                items.AddRange(oneDriveResponse.Value);

                if (string.IsNullOrWhiteSpace(oneDriveResponse.NextLink))
                {
                    break;
                }

                requestUri = oneDriveResponse.NextLink;
            }

            return items;
        }

        public async Task<DeltaView<DriveItem>> GetDelta(
            string deltaLink,
            CancellationToken cancellationToken,
            Action<long> onChangesReceived)
        {
            string requestUri;

            if (string.IsNullOrWhiteSpace(deltaLink))
            {
                Global.Logger
                    .WithCallInfo()
                    .Debug("OneDriveClient: Requesting delta view with null delta link");

                requestUri = string.Format(
                    "{0}/v1.0/me/drive/root/delta",
                    MicrosoftGraphBaseAddress);
            }
            else
            {
                Global.Logger
                    .WithCallInfo()
                    .Debug("OneDriveClient: Requesting delta view with non-null delta link");

                requestUri = deltaLink;
            }

            // A delta view from OneDrive can be larger than a single request, so loop until we
            // have built the complete view by following the NextLink properties.
            DeltaView<DriveItem> deltaView = new DeltaView<DriveItem>();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                GraphResponse<DriveItem[]> oneDriveResponse =
                    await this.GetItemSet<DriveItem[]>(requestUri, cancellationToken)
                        .ConfigureAwait(false);

                deltaView.Items.AddRange(oneDriveResponse.Value);

                onChangesReceived?.Invoke(deltaView.Items.Count);

                if (string.IsNullOrWhiteSpace(oneDriveResponse.NextLink))
                {
                    deltaView.Token = oneDriveResponse.DeltaToken;
                    deltaView.DeltaLink = oneDriveResponse.DeltaLink;

                    break;
                }

                requestUri = oneDriveResponse.NextLink;
            }

            return deltaView;
        }

        public async Task<Uri> GetDownloadUriForItem(string id)
        {
            HttpRequestMessage request = new(HttpMethod.Get, "/v1.0/me/drive/items/" + id + "/content");

            var response = await this.SendGraphRequestAsync(
                    request,
                    this.GraphHttpClientNoRedirect,
                    CancellationToken.None)
                .ConfigureAwait(false); 

            return response.Headers.Location;
        }

        public async Task<HttpResponseMessage> DownloadFileFragment(Uri downloadUri, int offset, int length)
        {
            HttpRequestMessage request = new(HttpMethod.Get, downloadUri);
            request.Headers.Range = new RangeHeaderValue(offset * length, ((offset + 1) * length) - 1);

            var response = await this.GraphHttpClient.SendAsync(request).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new GraphHttpException(response.ReasonPhrase, response.StatusCode);
            }

            return response;
        }
    }
}
