namespace Blueshift.OneDrive.Model
{
    using Newtonsoft.Json;

    public class DeletedFacet
    {
        [JsonProperty("state")]
        public string State { get; set; }
    }
}