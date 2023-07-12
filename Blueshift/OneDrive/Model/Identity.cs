namespace Blueshift.OneDrive.Model
{
    using Newtonsoft.Json;

    public class Identity
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("thumbnails")]
        public ThumbnailSet Thumbnails { get; set; }
    }
}