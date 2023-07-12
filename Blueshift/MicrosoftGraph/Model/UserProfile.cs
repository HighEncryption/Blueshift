namespace Blueshift.MicrosoftGraph.Model
{
    using Newtonsoft.Json;

    public class UserProfile
    {
        [JsonProperty("id", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Id { get; set; }

        [JsonProperty("displayName", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string DisplayName { get; set; }

        [JsonProperty("givenName", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string GivenName { get; set; }

        [JsonProperty("surname", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Surname { get; set; }

        [JsonProperty("userPrincipalName", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string UserPrincipalName { get; set; }
    }
}
