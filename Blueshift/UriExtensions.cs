namespace Blueshift
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Web;

    public static class UriExtensions
    {
        public static Dictionary<string, string> GetQueryParameters(this Uri uri)
        {
            if (string.IsNullOrEmpty(uri.Query) || uri.Query == "?")
            {
                return new Dictionary<string, string>();
            }

            // Trim the '?' from the start of the string, split at '&' into individual parameters, then split parameters 
            // at '=' to produce the name and value of each parameter;
            return uri.Query.TrimStart('?')
                .Split('&')
                .Select(e => e.Split('='))
                .ToDictionary(e => e[0], e => HttpUtility.UrlDecode(e[1]), StringComparer.OrdinalIgnoreCase);
        }

        public static string ToQueryParameters(this Dictionary<string, string> dictionary)
        {
            return string.Join("&", dictionary.Select(p => string.Format("{0}={1}", p.Key, p.Value)));
        }

        public static Uri ReplaceQueryParameterIfExists(this Uri uri, string name, string value)
        {
            if (string.IsNullOrEmpty(uri.Query) || uri.Query == "?")
            {
                return uri;
            }

            Dictionary<string, string> queryParams = uri.GetQueryParameters();

            if (!queryParams.ContainsKey(name))
            {
                return uri;
            }

            queryParams[name] = value;

            UriBuilder builder = new UriBuilder(uri);
            builder.Query = string.Join("&", queryParams.Select(param => param.Key + "=" + param.Value));

            return builder.Uri;
        }

        public static string CombineQueryString(ICollection<KeyValuePair<string, string>> parameter)
        {
            if (!parameter.Any())
            {
                return string.Empty;
            }

            return "?" + string.Join("&", parameter.Select(p => p.Key + "=" + p.Value));
        }
    }
}
