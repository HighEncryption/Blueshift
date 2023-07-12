namespace Blueshift.Data
{
    using System.ComponentModel.DataAnnotations;
    using System.Diagnostics;

    [DebuggerDisplay("{" + nameof(Name) + "} ({" + nameof(Id) + "})")]
    public class FolderItem : IValidatableObject
    {
        [Key]
        public string Id { get; set; }

        public string Name { get; set; }

        public string ETag { get; set; }

        public string ParentId { get; set; }

        public DateTimeOffset CreatedUtc { get; set; }

        public DateTimeOffset? LastModifiedUtc { get; set; }

        public ItemState State { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                yield return new ValidationResult(
                    "The Name property cannot be empty",
                    new[] { nameof(Name) });
            }

            if (string.IsNullOrWhiteSpace(ETag))
            {
                yield return new ValidationResult(
                    "The ETag property cannot be empty",
                    new[] { nameof(ETag) });
            }

            if (string.IsNullOrWhiteSpace(this.ParentId) && Name != "root")
            {
                yield return new ValidationResult(
                    "ParentId must be present for non-root items",
                    new[] { nameof(this.ParentId) });
            }
        }


        internal string BuildFullPath(BlueshiftDb db, string rootPath)
        {
            List<string> pathParts = new();

            string id = this.Id;

            while (!string.IsNullOrWhiteSpace(id))
            {
                var item = db.Folders.Single(i => i.Id == id);

                if (string.IsNullOrWhiteSpace(item.ParentId))
                {
                    pathParts.Add(rootPath);
                }
                else
                {
                    pathParts.Add(item.Name);
                }

                id = item.ParentId;
            }

            pathParts.Reverse();

            return Path.Combine(pathParts.ToArray());
        }


        public static FolderItem FromPendingChange(PendingChange change)
        {
            return new FolderItem()
            {
                Id = change.DriveItemId,
                Name = change.Name,
                ETag = change.ETag,
                ParentId = change.ParentId,
                CreatedUtc = change.CreatedUtc,
                LastModifiedUtc = change.LastModifiedUtc
            };
        }

        internal bool IsVaultDescendant(BlueshiftDb db)
        {
            FolderItem item = this;

            while (true)
            {
                if (item.Name == Constants.SpecialFolderVaultName)
                {
                    // We found the vault folder
                    return true;
                }

                if (item.ParentId == null)
                {
                    // We found the root folder
                    return false;
                }

                item = db.Folders.Single(f => f.Id == item.ParentId);
            }
        }
    }
}