namespace Blueshift.MicrosoftGraph.Model
{
    using Newtonsoft.Json;

    public class GraphInnerError
    {
        [JsonProperty("date")]
        public string Date { get; set; }

        [JsonProperty("request-id")]
        public string RequestId { get; set; }

        [JsonProperty("client-request-id")]
        public string ClientRequestId { get; set; }

    }

    public class GraphError
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("innererror")]
        public GraphInnerError InnerError { get; set; }
    }

    public class GraphErrorContainer
    {
        [JsonProperty("error")]
        public GraphError Error { get; set; }
    }

    public class IdentityPlatformError
    {
        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("error_description")]
        public string ErrorDescription { get; set; }
    }
}
