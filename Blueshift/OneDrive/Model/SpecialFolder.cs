namespace Blueshift.OneDrive.Model
{
    using Newtonsoft.Json;

    public class SpecialFolder
    {
        [JsonProperty("name")]
        public string Name { get; set; }
    }
}