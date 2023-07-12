namespace Blueshift.OneDrive.Model
{
    using Newtonsoft.Json;

    public class Quota
    {
        [JsonProperty("total")]
        public long Total { get; set; }

        [JsonProperty("used")]
        public long Used { get; set; }

        [JsonProperty("remaining")]
        public long Remaining { get; set; }
    }
}