namespace Blueshift.Data
{
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Diagnostics;

    [Flags]
    public enum ItemState
    {
        None = 0x00,

        /// <summary>
        /// The item has been deleted
        /// </summary>
        Deleted = 0x01,
    }

    [DebuggerDisplay("{" + nameof(Name) + "} ({" + nameof(Id) + "})")]
    [Table("Files")]
    public class FileItem : IValidatableObject
    {
        [Key]
        public string Id { get; set; }

        public string Name { get; set; }

        public string CTag { get; set; }

        public string ETag { get; set; }

        public string ParentId { get; set; }

        public long Size { get; set; }

        public string Sha1Hash { get; set; }

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

            if (string.IsNullOrWhiteSpace(Sha1Hash))
            {
                yield return new ValidationResult(
                    "The Sha1Hash property cannot be empty",
                    new[] { nameof(Sha1Hash) });
            }

            if (string.IsNullOrWhiteSpace(this.ParentId))
            {
                yield return new ValidationResult(
                    "ParentItemId cannot be empty",
                    new[] { nameof(this.ParentId) });
            }
        }

    }
}