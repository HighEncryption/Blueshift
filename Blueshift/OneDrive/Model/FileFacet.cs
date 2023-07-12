namespace Blueshift.OneDrive.Model
{
    using Newtonsoft.Json;

    public class FileFacet
    {
        [JsonProperty("mimeType")]
        public string MimeType { get; set; }

        [JsonProperty("hashes")]
        public HashesFacet Hashes { get; set; }
    }
}