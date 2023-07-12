namespace Blueshift
{
    using System.Diagnostics;
    using System.Security.Cryptography;

    using Blueshift.Data;
    using Blueshift.MicrosoftGraph;
    using Blueshift.MicrosoftGraph.Model;
    using Blueshift.OneDrive;
    using Blueshift.OneDrive.Model;

    public class SyncManager
    {
        private OneDriveClient oneDriveClient;

        public UserProfile UserProfile { get; set; }

        public async Task RefreshTokens()
        {
            foreach (SyncSource syncSource in Global.SyncSources)
            {
                try
                {
                    await RefreshTokenAsync(syncSource).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    Global.Logger
                        .WithCallInfo()
                        .Error(
                            exception,
                            "Caught top-level exception when syncing source {SourceName}",
                            syncSource.Name);
                }
            }
        }


        public async Task SyncAsync()
        {
            foreach (SyncSource syncSource in Global.SyncSources)
            {
                try
                {
                    await SyncSourceAsync(syncSource).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    Global.Logger
                        .WithCallInfo()
                        .Error(
                            exception,
                            "Caught top-level exception when syncing source {SourceName}",
                            syncSource.Name);
                }
            }
        }

        private async Task RefreshTokenAsync(SyncSource source)
        {
            Global.Logger
                .WithCallInfo()
                .Information(
                    "Starting token refresh for source {SourceName}",
                    source.Name);

            string tokenPath = Path.Combine(source.Path, "token.json");
            TokenResponse currentToken = null;

            if (File.Exists(tokenPath))
            {
                Global.Logger
                    .WithCallInfo()
                    .Information("Found existing token");

                currentToken = TokenResponse.LoadFromFile(tokenPath);
                currentToken.Unprotect();
            }
            else
            {
                Global.Logger
                    .WithCallInfo()
                    .Error("Did not find existing token");

                return;
            }

            this.oneDriveClient = new OneDriveClient(currentToken);
            this.oneDriveClient.TokenRefreshed += (_, e) =>
            {
                Global.Logger
                    .WithCallInfo()
                    .Information(
                        "Token has been refreshed. Saving new token to {Path}. Token expires in {ExpiryMinutes} minutes",
                        tokenPath,
                        e.NewToken.Expires);

                this.oneDriveClient.CurrentToken.SaveProtectedToken(tokenPath);
            };

            Global.Logger
                .WithCallInfo()
                .Information("Fetching profile information for account");

            // Get the user profile. This will have side effect fo refreshing the token and throw
            // an exception if the refresh token is expired and the user needs to re-sign-in.
            this.UserProfile = await this.oneDriveClient.GetUserProfileAsync().ConfigureAwait(false);

            Global.Logger
                .WithCallInfo()
                .Information(
                    "Profile successfully retrieved with for {UPN}",
                    this.UserProfile.UserPrincipalName);

            if (!string.Equals(
                    source.UserPrincipalName,
                    this.UserProfile.UserPrincipalName,
                    StringComparison.OrdinalIgnoreCase))
            {
                Global.Logger
                    .WithCallInfo()
                    .Information(
                        "The UPN {SourceUPN} returned for this source does not match the expected UPN {ExpectedUPN} from configuration",
                        this.UserProfile.UserPrincipalName,
                        source.UserPrincipalName);

                throw new Exception("UPN mismatch");
            }

            Global.Logger
                .WithCallInfo()
                .Information(
                    "Token refresh complete for source {SourceName}",
                    source.Name);
        }

        private async Task SyncSourceAsync(SyncSource source)
        {
            if (source.Disabled)
            {
                Global.Logger
                    .WithCallInfo()
                    .Information(
                        "Skipping disabled sync source {Name}",
                        source.Name);

                return;
            }

            string tokenPath = Path.Combine(source.Path, "token.json");

            if (!File.Exists(tokenPath))
            {
                throw new FileNotFoundException(
                    "The token was not found for this source. Run /refreshTokens");
            }

            TokenResponse currentToken = TokenResponse.LoadFromFile(tokenPath);
            currentToken.Unprotect();

            this.oneDriveClient = new OneDriveClient(currentToken);
            this.oneDriveClient.TokenRefreshed += (_, e) =>
            {
                Global.Logger
                    .WithCallInfo()
                    .Information(
                        "Token has been refreshed. Saving new token to {Path}. Token expires in {ExpiryMinutes} minutes",
                        tokenPath,
                        e.NewToken.Expires);

                this.oneDriveClient.CurrentToken.SaveProtectedToken(tokenPath);
            };

            Global.Logger
                .WithCallInfo()
                .Information("Fetching profile information for account");

            // Get the user profile. This will have side effect fo refreshing the token and throw
            // an exception if the refresh token is expired and the user needs to re-sign-in.
            this.UserProfile = await this.oneDriveClient.GetUserProfileAsync().ConfigureAwait(false);

            Global.Logger
                .WithCallInfo()
                .Information(
                    "Profile successfully retrieved with for {UPN}",
                    this.UserProfile.UserPrincipalName);

            if (!string.Equals(
                    source.UserPrincipalName, 
                    this.UserProfile.UserPrincipalName,
                    StringComparison.OrdinalIgnoreCase))
            {
                Global.Logger
                    .WithCallInfo()
                    .Information(
                        "The UPN {SourceUPN} returned for this source does not match the expected UPN {ExpectedUPN} from configuration",
                        this.UserProfile.UserPrincipalName,
                        source.UserPrincipalName);

                throw new Exception("UPN mismatch");
            }

            using var db = new BlueshiftDb(source.Path);

            int pendingChangeCount = db.PendingChanges.Count();
            if (pendingChangeCount > 0)
            {
                Global.Logger
                    .WithCallInfo()
                    .Information(
                        "Found {Count} pending changes not yet processed. Skipping sync.",
                        pendingChangeCount);
            }
            else
            {
                await GetChanges(db, source).ConfigureAwait(false);
            }

            await ProcessChanges(db, source).ConfigureAwait(false);
        }

        private async Task GetChanges(BlueshiftDb db, SyncSource source)
        {
            Global.Logger
                .WithCallInfo()
                .Information("Fetching changes for this source");

            string deltaPath = Path.Combine(source.Path, "deltaToken.txt");
            string deltaLink = null;

            if (File.Exists(deltaPath))
            {
                deltaLink = await File.ReadAllTextAsync(deltaPath).ConfigureAwait(false);
            }

            string requestUri;

            if (string.IsNullOrWhiteSpace(deltaLink))
            {
                Global.Logger
                    .WithCallInfo()
                    .Debug("SyncManager: Requesting delta view with null delta link");

                requestUri = string.Format(
                    "{0}/v1.0/me/drive/root/delta",
                    GraphClient.MicrosoftGraphBaseAddress);
            }
            else
            {
                Global.Logger
                    .WithCallInfo()
                    .Debug("SyncManager: Requesting delta view with non-null delta link");

                requestUri = deltaLink;
            }


            // Create a transaction so that if an error is encountered while reading the
            // changes, any created changes can be rolled back
            using var transaction = 
                await db.Database.BeginTransactionAsync().ConfigureAwait(false);

            // A delta view from OneDrive can be larger than a single request, so loop until we
            // have built the complete view by following the NextLink properties.
            try
            {
                while (true)
                {
                    Program.Cts.Token.ThrowIfCancellationRequested();

                    GraphResponse<DriveItem[]> oneDriveResponse =
                        await this.oneDriveClient.GetItemSet<DriveItem[]>(requestUri, Program.Cts.Token)
                            .ConfigureAwait(false);

                    foreach (DriveItem driveItem in oneDriveResponse.Value)
                    {
                        await CreatePendingChange(db, driveItem).ConfigureAwait(false);
                    }

                    if (string.IsNullOrWhiteSpace(oneDriveResponse.NextLink))
                    {
                        deltaLink = oneDriveResponse.DeltaLink;
                        break;
                    }

                    requestUri = oneDriveResponse.NextLink;
                }

                Global.Logger
                    .WithCallInfo()
                    .Information(
                        "Finished reading change from client. Saving deltaLink to {Path}",
                        deltaPath);

                await File.WriteAllTextAsync(deltaPath, deltaLink).ConfigureAwait(false);

                Global.Logger
                    .WithCallInfo()
                    .Information("Committing changed to database");

                await transaction.CommitAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                Global.Logger
                    .WithCallInfo()
                    .Error(
                        exception,
                        "Caught exception while processing DriveItem changes. Pending changes will be discarded.");

                await transaction.RollbackAsync().ConfigureAwait(false);
            }
        }

        private async Task CreatePendingChange(BlueshiftDb db, DriveItem driveItem)
        {
            // First check if there are any existing changes for this DriveItem
            PendingChange pendingChange =
                db.PendingChanges.FirstOrDefault(c => c.DriveItemId == driveItem.Id);

            if (pendingChange == null)
            {
                Global.Logger
                    .WithCallInfo()
                    .Information(
                        "Creating new PendingChange for DriveItem={DriveItemId}, Name={Name}, ParentId={ParentId}",
                        driveItem.Id,
                        driveItem.Name,
                        driveItem.ParentReference?.Id);

                pendingChange = new PendingChange()
                {
                    DriveItemId = driveItem.Id
                };

                await db.PendingChanges.AddAsync(pendingChange).ConfigureAwait(false);
            }
            else
            {
                Global.Logger
                    .WithCallInfo()
                    .Information(
                        "Updating existing PendingChange for DriveItem {DriveItemId}",
                        driveItem.Id);
            }

            PopulatePendingChange(pendingChange, driveItem);

            await db.SaveChangesAsync().ConfigureAwait(false);
        }

        public static void PopulatePendingChange(PendingChange pendingChange, DriveItem driveItem)
        {
            pendingChange.CTag = driveItem.CTag;
            pendingChange.ETag = driveItem.ETag;
            pendingChange.Name = driveItem.Name;
            pendingChange.ParentId = driveItem.ParentReference?.Id;
            pendingChange.Sha1Hash = driveItem.File?.Hashes?.Sha1Hash;
            pendingChange.Size = driveItem.Size;

            if (driveItem.Root != null)
            {
                pendingChange.SpecialFolder = "root";
            }
            else if (driveItem.SpecialFolder != null)
            {
                pendingChange.SpecialFolder = driveItem.SpecialFolder.Name;
            }

            if (driveItem.Deleted != null &&
                !string.IsNullOrWhiteSpace(driveItem.Deleted.State))
            {
                pendingChange.State |= ItemState.Deleted;
            }

            if (driveItem.Folder != null)
            {
                pendingChange.ItemType = ItemType.Folder;
            }
            else if (driveItem.File != null)
            {
                pendingChange.ItemType = ItemType.File;
            }
            else
            {
                // Items need to have either the File or Folder facet in order to be considered
                // for downloading/creating.
                // See: https://stackoverflow.com/questions/64099004/onedrive-rest-apis-message-the-specified-item-does-not-have-content-when
                if (driveItem.Package != null &&
                    !string.IsNullOrWhiteSpace(driveItem.Package.Type))
                {
                    Global.Logger
                        .WithCallInfo()
                        .Information(
                            "Handling non-file and non-folder drive item with Package.Type={Type} as Folder",
                            driveItem.Package.Type);

                    pendingChange.ItemType = ItemType.Folder;
                }
                else
                {
                    throw new Exception(
                        "Unable to determine item type");
                }
            }

            pendingChange.CreatedUtc =
                driveItem.FileSystemInfo?.CreatedDateTime ?? driveItem.CreatedDateTime;

            pendingChange.LastModifiedUtc =
                driveItem.FileSystemInfo?.LastModifiedDateTime ?? driveItem.LastModifiedDateTime;
        }

        private async Task ProcessChanges(BlueshiftDb db, SyncSource source)
        {
            int totalChanges = db.PendingChanges.Count();
            int changeCount = 1;

            while (true)
            {
                PendingChange pendingChange = db.PendingChanges.OrderBy(c => c.Id).FirstOrDefault();

                if (pendingChange == null)
                {
                    Global.Logger
                        .WithCallInfo()
                        .Information("No more pending changes to apply");

                    break;
                }

                Global.Logger
                    .WithCallInfo()
                    .Information(
                        "Processing change {Id} ({Count} of {Total}) with DriveItemId {DriveItemId}",
                        pendingChange.Id,
                        changeCount,
                        totalChanges,
                        pendingChange.DriveItemId);

                changeCount++;

                // Validate that the parent already exists in the db
                var parentItem =
                    db.Folders.FirstOrDefault(f => f.Id == pendingChange.ParentId);

                if (parentItem == null && pendingChange.SpecialFolder != "root")
                {
                    // Normally we require the parent to exist before processing any changes for
                    // an item. However, in the case of a delete, we may have never seen the 
                    // parent (because it was already deleted). In this case, we will allow
                    // processing to continue.
                    if (!pendingChange.State.HasFlag(ItemState.Deleted))
                    {
                        throw new Exception(
                            $"Failed to find parent folder {pendingChange.ParentId} for item {pendingChange.Id}");
                    }
                }

                using (var transaction =
                       await db.Database.BeginTransactionAsync().ConfigureAwait(false))
                {
                    try
                    {
                        if (pendingChange.ItemType == ItemType.Folder)
                        {
                            if (!string.IsNullOrWhiteSpace(pendingChange.SpecialFolder))
                            {
                                await ProcessSpecialFolderChange(db, source, pendingChange, parentItem)
                                    .ConfigureAwait(false);
                            }
                            else
                            {
                                await ProcessFolderChange(db, source, pendingChange, parentItem)
                                    .ConfigureAwait(false);
                            }
                        }
                        else if (pendingChange.ItemType == ItemType.File)
                        {
                            FileItem fileItem =
                                db.Files.FirstOrDefault(f => f.Id == pendingChange.DriveItemId);

                            if (fileItem == null)
                            {
                                await CreateNewFile(db, source, pendingChange, parentItem)
                                    .ConfigureAwait(false);
                            }
                            else
                            {
                                await UpdateExistingFile(db, source, pendingChange, fileItem, parentItem)
                                    .ConfigureAwait(false);
                            }
                        }

                        // We have finished processing this change, so remove it from the queue (aka table)
                        db.PendingChanges.Remove(pendingChange);

                        await db.SaveChangesAsync().ConfigureAwait(false);

                        await transaction.CommitAsync().ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        Global.Logger
                            .WithCallInfo()
                            .Error(
                                exception,
                                "Failed to apply pending change. Database changes will be rolled back.");

                        await transaction.RollbackAsync().ConfigureAwait(false);

                        throw;
                    }
                }
            }
        }

        private async Task ProcessFolderChange(
            BlueshiftDb db, 
            SyncSource source, 
            PendingChange pendingChange, 
            FolderItem parent)
        {
            FolderItem folderItem =
                db.Folders.FirstOrDefault(f => f.Id == pendingChange.DriveItemId);

            if (pendingChange.State.HasFlag(ItemState.Deleted))
            {
                if (folderItem == null)
                {
                    Global.Logger
                        .WithCallInfo()
                        .Information(
                            "Pending change is to delete FolderItem with Id {Id}, however item does not exist in DB. Ignoring.",
                            pendingChange.DriveItemId);

                    return;
                }

                // Deletes are not propagated to the files on disk, however we want to update the
                // item in the database to reflect that it was deleted.
                Global.Logger
                    .WithCallInfo()
                    .Information(
                        "FolderItem with Id {Id} will be marked as deleted",
                        pendingChange.DriveItemId);

                folderItem.State |= ItemState.Deleted;

                return;
            }

            if (parent.IsVaultDescendant(db))
            {
                Global.Logger
                    .WithCallInfo()
                    .Information(
                        "FolderItem with Id {Id} belongs to the vault",
                        pendingChange.DriveItemId);

                // Add the item only if it doesn't exist in the database
                if (folderItem == null)
                {
                    folderItem = FolderItem.FromPendingChange(pendingChange);

                    // Names of items in the vault aren't exposed, so we will use the
                    // DriveItemId instead
                    folderItem.Name = pendingChange.DriveItemId;

                    db.Folders.Add(folderItem);
                }
            }

            if (folderItem == null)
            {
                // The folder is new and needs to be created (or already exists on the filesystem
                // and this is the first time we are syncing this change)
                string folderPath = Path.Combine(
                    parent.BuildFullPath(db, source.RootPath),
                    pendingChange.Name);

                Global.Logger
                    .WithCallInfo()
                    .Information(
                        "Created new FolderItem for {DriveItemId} from change {Id} with path {Path}",
                        pendingChange.DriveItemId,
                        pendingChange.Id,
                        folderPath);

                DirectoryInfo directoryInfo = new(folderPath);

                if (directoryInfo.Exists)
                {
                    // TODO Check if the change is different from the folder in any way
                    // TODO that would require us to change it. This is probably not the 
                    // TODO however since we set the properties the the folder below.
                    Global.Logger
                        .WithCallInfo()
                        .Information(
                            "Folder already exists at path {Path} and will be re-used",
                            folderPath);
                }
                else
                {
                    Directory.CreateDirectory(folderPath);
                }

                // Set the creation time, which is the only property on the folder we care to set
                directoryInfo.CreationTimeUtc = pendingChange.CreatedUtc.DateTime;

                folderItem = FolderItem.FromPendingChange(pendingChange);

                await db.Folders.AddAsync(folderItem).ConfigureAwait(false);

                return;
            }

            // The folder already exists in the db. Check if there are any changes that
            // need to be made based on this.
            if (folderItem.ETag == pendingChange.ETag)
            {
                Global.Logger
                    .WithCallInfo()
                    .Information(
                        "Skipping change due to unchanged ETag value {ETag} for item {Id}",
                        folderItem.ETag,
                        folderItem.Id);

                return;
            }

            if (folderItem.CreatedUtc == pendingChange.CreatedUtc)
            {
                Global.Logger
                    .WithCallInfo()
                    .Information(
                        "Skipping modified CreationUtc for item {Id}",
                        folderItem.Id);

                folderItem.CreatedUtc = pendingChange.CreatedUtc;
            }

            if (folderItem.LastModifiedUtc == pendingChange.LastModifiedUtc)
            {
                Global.Logger
                    .WithCallInfo()
                    .Information(
                        "Skipping modified LastModifiedUtc for item {Id}",
                        folderItem.Id);

                folderItem.LastModifiedUtc = pendingChange.LastModifiedUtc;
            }

            if (folderItem.Name != pendingChange.Name)
            {
                throw new NotImplementedException("Folder rename");
            }
        }

        private readonly DateTimeOffset minValidDateTime =
            new DateTimeOffset(1990, 1, 1, 0, 0, 0, TimeSpan.Zero);

        private async Task ProcessSpecialFolderChange(
            BlueshiftDb db,
            SyncSource source,
            PendingChange pendingChange,
            FolderItem parent)
        {
            FolderItem folderItem =
                db.Folders.FirstOrDefault(f => f.Id == pendingChange.DriveItemId);

            // Root is a special case to be handled separately from other folders
            if (pendingChange.SpecialFolder == "root")
            {
                if (folderItem == null)
                {
                    Global.Logger
                        .WithCallInfo()
                        .Information(
                            "Created new FolderItem for {DriveItemId} from change {Id} for root",
                            pendingChange.DriveItemId,
                            pendingChange.Id);

                    folderItem = FolderItem.FromPendingChange(pendingChange);

                    // Ensure that the parent for the root folder is not set
                    folderItem.ParentId = null;

                    await db.Folders.AddAsync(folderItem).ConfigureAwait(false);
                }
                else
                {
                    Global.Logger
                        .WithCallInfo()
                        .Information(
                            "Ignoring change to root FolderItem {DriveItemId} in change {Id}",
                            pendingChange.DriveItemId,
                            pendingChange.Id);
                }

                return;
            }

            if (string.Equals(pendingChange.SpecialFolder, "vault", StringComparison.OrdinalIgnoreCase))
            {
                // The vault is a special case. We don't want to create the folder, but we will add
                // it to the database (along with children) to be able to identify and ignore
                // changes in them.
                if (folderItem == null)
                {
                    if (pendingChange.State.HasFlag(ItemState.Deleted))
                    {
                        Global.Logger
                            .WithCallInfo()
                            .Information(
                                "Ignoring delete for non-existant folder (SpecialFolder vault) for {DriveItemId} from change {Id}",
                                pendingChange.DriveItemId,
                                pendingChange.Id);

                        return;
                    }

                    Global.Logger
                        .WithCallInfo()
                        .Information(
                            "Created new FolderItem (SpecialFolder vault) for {DriveItemId} from change {Id}",
                            pendingChange.DriveItemId,
                            pendingChange.Id);

                    folderItem = FolderItem.FromPendingChange(pendingChange);

                    // Set the name so that we can differentiate it later
                    folderItem.Name = Constants.SpecialFolderVaultName;

                    await db.Folders.AddAsync(folderItem).ConfigureAwait(false);
                }
                else if (pendingChange.State.HasFlag(ItemState.Deleted))
                {
                    Global.Logger
                        .WithCallInfo()
                        .Information(
                            "Persisting delete for (SpecialFolder vault) for {DriveItemId} from change {Id} to db",
                            pendingChange.DriveItemId,
                            pendingChange.Id);

                    folderItem.State |= ItemState.Deleted;
                }
                else
                {
                    Global.Logger
                        .WithCallInfo()
                        .Information(
                            "Ignoring change to SpecialFolder vault FolderItem {DriveItemId} in change {Id}",
                            pendingChange.DriveItemId,
                            pendingChange.Id);
                }

                return;
            }

            // Other special folders should be processed as regular folders. For those, call the 
            // normal ProcessFolderChange method.
            Global.Logger
                .WithCallInfo()
                .Information(
                    "Handling SpecialFolder {SpecialFolder} as a regular folder",
                    pendingChange.SpecialFolder);

            await ProcessFolderChange(
                    db,
                    source,
                    pendingChange,
                    parent)
                .ConfigureAwait(false);
        }

        private async Task CreateNewFile(
            BlueshiftDb db, 
            SyncSource source,
            PendingChange pendingChange, 
            FolderItem parent)
        {
            if (pendingChange.State.HasFlag(ItemState.Deleted))
            {
                Global.Logger
                    .WithCallInfo()
                    .Information(
                        "FileItem {Id} does not exist in db but has the Deleted state. Ignoring change.",
                        pendingChange.DriveItemId);

                return;
            }

            string parentPath = parent.BuildFullPath(db, source.RootPath);
            string filePath = Path.Combine(parentPath, pendingChange.Name);
            string fileHash = null;

            FileInfo fileInfo = new(filePath);
            bool fileDownloadRequired = false;

            if (!fileInfo.Exists)
            {
                Global.Logger
                    .WithCallInfo()
                    .Information(
                        "FileItem {Id} does not exist in db and will be downloaded",
                        pendingChange.DriveItemId);

                fileDownloadRequired = true;
            }
            else
            {
                Global.Logger
                    .WithCallInfo()
                    .Information(
                        "FileItem {Id} does not exist in db, however existing file was found at path {Path}.",
                        pendingChange.DriveItemId,
                        fileInfo.FullName);

                if (fileInfo.Length != pendingChange.Size)
                {
                    if (fileInfo.Length > pendingChange.Size && 
                        IsJpgOrMp4File(pendingChange) &&
                        fileInfo.Length - pendingChange.Size > 10240)
                    {
                        throw new Exception(
                            $"Existing file {fileInfo.FullName} has larger size ({fileInfo.Length}) than version being downloaded ({pendingChange.Size}). Bailing out.");
                    }

                    Global.Logger
                        .WithCallInfo()
                        .Information(
                            "Existing file size ({ExistingSize}) does not match expected size ({ExpectedSize}). File will be downloaded.",
                            fileInfo.Length,
                            pendingChange.Size);

                    fileDownloadRequired = true;
                }
                else
                {
                    fileHash = await ComputeFileHash(fileInfo.FullName).ConfigureAwait(false);

                    if (fileHash != pendingChange.Sha1Hash)
                    {
                        Global.Logger
                            .WithCallInfo()
                            .Information(
                                "Existing file hash ({ExistingHash}) does not match expected hash ({ExpectedHash}). File will be downloaded.",
                                fileHash,
                                pendingChange.Sha1Hash);

                        fileDownloadRequired = true;
                    }
                    else
                    {
                        Global.Logger
                            .WithCallInfo()
                            .Information(
                                "Existing file is an exact match with cloud via hash ({ExistingHash}). File will NOT be downloaded.",
                                fileHash);

                        fileHash = pendingChange.Sha1Hash;
                    }
                }
            }

            if (fileDownloadRequired)
            {
                fileHash =
                    await WriteFileFromChange(filePath, pendingChange).ConfigureAwait(false);

                // The file content has changes, so we need to re-stat the file
                fileInfo.Refresh();
            }

            fileInfo.CreationTimeUtc = pendingChange.CreatedUtc.DateTime;

            if (pendingChange.LastModifiedUtc != null)
            {
                fileInfo.LastWriteTimeUtc = pendingChange.LastModifiedUtc.Value.DateTime;
            }

            Pre.Assert(!string.IsNullOrWhiteSpace(fileHash), "fileHash != null");

            FileItem fileItem = new()
            {
                Id = pendingChange.DriveItemId,
                CreatedUtc = pendingChange.CreatedUtc,
                CTag = pendingChange.CTag,
                ETag = pendingChange.ETag,
                LastModifiedUtc = pendingChange.LastModifiedUtc,
                Name = pendingChange.Name,
                ParentId = pendingChange.ParentId,
                Sha1Hash = fileHash,
                Size = pendingChange.Size,
                State = pendingChange.State
            };

            db.Files.Add(fileItem);
        }

        private bool IsJpgOrMp4File(PendingChange pendingChange)
        {
            return pendingChange.Name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                   ||
                   pendingChange.Name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);
        }

        private async Task UpdateExistingFile(
            BlueshiftDb db, 
            SyncSource source,
            PendingChange pendingChange, 
            FileItem fileItem, 
            FolderItem parent)
        {
            string parentPath = parent.BuildFullPath(db, source.RootPath);

            // Build the path to the file based on the existing file (since it may also be 
            // renamed at the same time, which we will handle below)
            string filePath = Path.Combine(parentPath, fileItem.Name);

            if (fileItem.Sha1Hash == pendingChange.Sha1Hash &&
                fileItem.Size == pendingChange.Size)
            {
                // The db contains an entry for the file AND the hash matches
                Global.Logger
                    .WithCallInfo()
                    .Information(
                        "FileItem {Id} already exists in db and SHA1 hash matched ({Hash}). Content will not be updated",
                        fileItem.Id,
                        fileItem.Sha1Hash);
            }
            else
            {
                if (fileItem.Sha1Hash != pendingChange.Sha1Hash)
                {
                    Global.Logger
                        .WithCallInfo()
                        .Information(
                            "FileItem {Id} exist in db but hash from db {HashFromDb} did not match pending change {HashFromChange}",
                            pendingChange.DriveItemId,
                            fileItem.Sha1Hash,
                            pendingChange.Sha1Hash);
                }
                else
                {
                    Global.Logger
                        .WithCallInfo()
                        .Information(
                            "FileItem {Id} exist in db but size from db {SizeFromDb} did not match pending change {SizeFromChange}",
                            pendingChange.DriveItemId,
                            fileItem.Size,
                            pendingChange.Size);
                }

                await WriteFileFromChange(filePath, pendingChange).ConfigureAwait(false);

                // We updated the file on disk, so update the size and hash in the FileItem
                fileItem.Sha1Hash = pendingChange.Sha1Hash;
                fileItem.Size = pendingChange.Size;
            }

            if (fileItem.Name != pendingChange.Name ||
                fileItem.ParentId != pendingChange.ParentId)
            {
                // The file has been renamed or moved. First find the new parent (which may be the 
                // same as the existing) to build the new file path. This should already exist.
                FolderItem newParentFolder =
                    db.Folders.FirstOrDefault(f => f.Id == pendingChange.ParentId);

                Pre.Assert(newParentFolder != null, "newParentFolder != null");

                string newParentPath = newParentFolder.BuildFullPath(db, source.RootPath);

                // Build the full file path to the renamed/moved file
                string newFilePath = Path.Combine(newParentPath, pendingChange.Name);

                File.Move(filePath, newFilePath);

                fileItem.Name = pendingChange.Name;
                fileItem.ParentId = pendingChange.ParentId;

                // Since we have renamed/moved the file, update filePath so later code will
                // reference the updated file location
                filePath = newFilePath;
            }

            FileInfo fileInfo = new(filePath);

            if (fileItem.CreatedUtc != pendingChange.CreatedUtc)
            {
                if (pendingChange.CreatedUtc < this.minValidDateTime)
                {
                    Global.Logger
                        .WithCallInfo()
                        .Warning(
                            "Ignoring invalid CreateUtc value: {DateTime}",
                            pendingChange.CreatedUtc);
                }
                else
                {
                    fileInfo.CreationTimeUtc = pendingChange.CreatedUtc.DateTime;
                }
                fileItem.CreatedUtc = pendingChange.CreatedUtc;
            }

            if (fileItem.LastModifiedUtc != pendingChange.LastModifiedUtc)
            {
                if (pendingChange.LastModifiedUtc != null)
                {
                    if (pendingChange.LastModifiedUtc < this.minValidDateTime)
                    {
                        Global.Logger
                            .WithCallInfo()
                            .Warning(
                                "Ignoring invalid LastModifiedUtc value: {DateTime}",
                                pendingChange.LastModifiedUtc);
                    }
                    else
                    {
                        fileInfo.LastWriteTimeUtc = pendingChange.LastModifiedUtc.Value.DateTime;
                    }
                }

                fileItem.LastModifiedUtc = pendingChange.LastModifiedUtc;
            }
        }

        private async Task<string> ComputeFileHash(string filePath)
        {
            using FileStream fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using SHA1 sha1 = SHA1.Create();

            byte[] hashBytes = await sha1.ComputeHashAsync(fileStream).ConfigureAwait(false);

            return ByteToHex(hashBytes);
        }

        private async Task<string> WriteFileFromChange(string itemPath, PendingChange pendingChange)
        {
            string parentPath = Path.GetDirectoryName(itemPath);

            if (!Directory.Exists(parentPath))
            {
                // This is already be present, so this is a fatal error
                throw new DirectoryNotFoundException(
                    $"The parent path {parentPath} does not exist");
            }

            string sha1Hash;
            Stopwatch stopwatch = Stopwatch.StartNew();

            if (pendingChange.Size == 0)
            {
                Global.Logger
                    .WithCallInfo()
                    .Information("Creating 0 byte file");

                await File.WriteAllBytesAsync(itemPath, Array.Empty<byte>()).ConfigureAwait(false);
                sha1Hash = ByteToHex(SHA1.HashData(Array.Empty<byte>()));
            }
            else
            {
                Global.Logger
                    .WithCallInfo()
                    .Information(
                        "Preparing to download {Size} bytes to local file",
                        pendingChange.Size);

                Uri downloadUri =
                    await this.oneDriveClient.GetDownloadUriForItem(pendingChange.DriveItemId)
                        .ConfigureAwait(false);

                using FileDownloadStream downloadStream =
                    new(this.oneDriveClient, downloadUri);

                using FileStream fileStream = File.OpenWrite(itemPath);

                using SHA1 sha1 = SHA1.Create();

                int read;
                int readTotal = 0;
                byte[] buffer = new byte[0x10000]; // 64 KiB

                while (true)
                {
                    read = downloadStream.Read(buffer, 0, buffer.Length);
                    fileStream.Write(buffer, 0, read);
                    readTotal += read;

                    if (read < buffer.Length)
                    {
                        sha1.TransformFinalBlock(buffer, 0, read);

                        break;
                    }

                    sha1.TransformBlock(buffer, 0, read, null, 0);
                }

                fileStream.Close();
                downloadStream.Close();

                stopwatch.Stop();

                sha1Hash = ByteToHex(sha1.Hash);
            }

            Global.Logger
                .WithCallInfo()
                .Information(
                    "Successfully wrote {Size} bytes to local file {FileName} in {ElapsedMs}ms. Sha1 hash is {Hash}",
                    pendingChange.Size,
                    pendingChange.Name,
                    stopwatch.ElapsedMilliseconds,
                    sha1Hash);

            if (string.IsNullOrEmpty(pendingChange.Sha1Hash))
            {
                Global.Logger
                    .WithCallInfo()
                    .Information(
                        "Hash was not provided for file. Hash verification will be skipped.");
            }
            else if (sha1Hash != pendingChange.Sha1Hash)
            {
                Global.Logger
                    .WithCallInfo()
                    .Information(
                        "SHA1 mismatch. Checking for updated item.");

                var driveItem =
                    await this.oneDriveClient.GetItemAsync(pendingChange.DriveItemId).ConfigureAwait(false);

                if (driveItem.ETag != pendingChange.ETag &&
                    sha1Hash == driveItem.File?.Hashes?.Sha1Hash)
                {
                    Global.Logger
                        .WithCallInfo()
                        .Information(
                            "Item was refreshed after the pending change was created. Using updated item information.");

                    PopulatePendingChange(pendingChange, driveItem);

                    Pre.Assert(pendingChange.ItemType == ItemType.File, "pendingChange.ItemType == ItemType.File");

                    return sha1Hash;
                }

                throw new Exception("SHA1 mismatch!");
            }

            return sha1Hash;
        }

        /// <summary>
        /// Convert a byte array into its hexadecimal representation
        /// </summary>
        /// <param name="bytes">The bytes to be converted</param>
        /// <returns>The byte string</returns>
        /// <remarks>
        /// Based on work here: https://stackoverflow.com/a/14333437/7852297
        /// </remarks>
        public static string ByteToHex(byte[] bytes)
        {
            char[] c = new char[bytes.Length * 2];
            int b;
            for (int i = 0; i < bytes.Length; i++)
            {
                b = bytes[i] >> 4;
                c[i * 2] = (char)(55 + b + (((b - 10) >> 31) & -7));
                b = bytes[i] & 0xF;
                c[i * 2 + 1] = (char)(55 + b + (((b - 10) >> 31) & -7));
            }

            return new string(c).ToUpper();
        }
    }

    public static class Constants
    {
        public const string SpecialFolderVaultName = "[specialFolder:vault]";
    }
}