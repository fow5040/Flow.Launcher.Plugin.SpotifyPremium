using Newtonsoft.Json;

namespace Wox.Plugin.Spotify.MetadataApi
{
    public class Info
    {
        [JsonProperty("num_results")] 
        public int CountResults;

        public int Limit;
        public int Offset;
        public string Query;
        public string Type;
        public int Page;
    }
}
