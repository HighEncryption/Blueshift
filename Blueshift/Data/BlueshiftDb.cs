namespace Blueshift.Data
{
    using Microsoft.EntityFrameworkCore;

    /// <summary>
    /// Represents the database used to track files that have been synced
    /// </summary>
    internal class BlueshiftDb : DbContext
    {
        /// <summary>
        /// The default name of the database file
        /// </summary>
        public const string DefaultDatabaseFilename = "blueshift.db";

        /// <summary>
        /// The full path to the database file
        /// </summary>
        public string DatabasePath { get; }

        /// <summary>
        /// The set of folders that have been synced
        /// </summary>
        public DbSet<FolderItem> Folders { get; set; }

        /// <summary>
        /// The set of files that have been synced
        /// </summary>
        public DbSet<FileItem> Files { get; set; }

        /// <summary>
        /// The collection of changes that have been read from OneDrive but have not yet been
        /// applied to the local folders/files
        /// </summary>
        public DbSet<PendingChange> PendingChanges { get; set; }

        public BlueshiftDb(string path)
        {
            this.DatabasePath = Path.Combine(path, DefaultDatabaseFilename);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite($"Data Source={this.DatabasePath}");
    }
}
