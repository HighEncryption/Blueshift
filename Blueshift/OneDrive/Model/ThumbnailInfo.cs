﻿namespace Blueshift.OneDrive.Model
{
    using Newtonsoft.Json;

    public class ThumbnailInfo
    {
        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }
    }
}