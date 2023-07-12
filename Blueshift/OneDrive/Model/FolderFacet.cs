namespace Blueshift.OneDrive.Model
{
    using Newtonsoft.Json;

    public class FolderFacet
    {
        /// <summary>
        /// Number of children contained immediately within this container.
        /// </summary>
        [JsonProperty("childCount")]
        public long ChildCount { get; set; }
    }
}