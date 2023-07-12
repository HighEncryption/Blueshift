namespace Blueshift
{
    using System.IO;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Xml.Serialization;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public static class HttpContentExtensions
    {
        public static async Task<T> ReadAsJsonAsync<T>(this HttpContent content)
        {
            JsonSerializer serializer = new JsonSerializer();
            using (var stream = await content.ReadAsStreamAsync().ConfigureAwait(false))
            {
                return serializer.Deserialize<T>(new JsonTextReader(new StreamReader(stream)));
            }
        }

        public static async Task<T> TryReadAsJsonAsync<T>(this HttpContent content)
            where T : class
        {
            try
            {
                JsonSerializer serializer = new JsonSerializer();
                using (var stream = await content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    return serializer.Deserialize<T>(new JsonTextReader(new StreamReader(stream)));
                }
            }
            catch
            {
                return null;
            }
        }

        public static async Task<JObject> ReadAsJObjectAsync(this HttpContent content)
        {
            using (var stream = await content.ReadAsStreamAsync().ConfigureAwait(false))
            {
                return JObject.Load(new JsonTextReader(new StreamReader(stream)));
            }
        }

        public static async Task<T> ReadAsXmlObjectAsync<T>(this HttpContent content)
        {
            using (var stream = await content.ReadAsStreamAsync().ConfigureAwait(false))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(T));

                return (T)serializer.Deserialize(stream);
            }
        }
    }
}
