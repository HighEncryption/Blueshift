namespace Blueshift.OneDrive.Model
{
    using Newtonsoft.Json;

    public class HashesFacet
    {
        [JsonProperty("sha1Hash")]
        public string Sha1Hash { get; set; }

        [JsonProperty("crc32Hash")]
        public string Crc32Hash { get; set; }

        [JsonProperty("quickXorHash")]
        public string QuickXorHash { get; set; }
    }
}