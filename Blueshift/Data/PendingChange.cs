namespace Blueshift.Data
{
    using System.ComponentModel.DataAnnotations;
    using System.Diagnostics;

    public enum ItemType
    {
        Undefined,
        Folder,
        File
    }

    [DebuggerDisplay("{" + nameof(Name) + "}  ({" + nameof(ItemType) + "})  ({" + nameof(Id) + "})")]
    public class PendingChange
    {
        [Key]
        public long Id { get; set; }

        public string DriveItemId { get; set; }

        public ItemType ItemType { get; set; }

        public string Name { get; set; }

        public string CTag { get; set; }

        public string ETag { get; set; }

        public string ParentId { get; set; }

        public long Size { get; set; }

        public string Sha1Hash { get; set; }

        public DateTimeOffset CreatedUtc { get; set; }

        public DateTimeOffset? LastModifiedUtc { get; set; }

        public ItemState State { get; set; }

        public string SpecialFolder { get; set; }
    }
}