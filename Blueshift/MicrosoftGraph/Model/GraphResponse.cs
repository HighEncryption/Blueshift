namespace Blueshift.MicrosoftGraph.Model
{
    using Newtonsoft.Json;

    public class GraphResponse<T>
    {
        [JsonProperty("@odata.context")]
        public string Context { get; set; }

        [JsonProperty("@odata.nextLink")]
        public string NextLink { get; set; }

        [JsonProperty("@odata.deltaLink")]
        public string DeltaLink { get; set; }

        [JsonProperty("@delta.token")]
        public string DeltaToken { get; set; }

        [JsonProperty("value")]
        public T Value { get; set; }

        public GraphResponse()
        {
        }

        public GraphResponse(T value)
        {
            this.Value = value;
        }
    }
}