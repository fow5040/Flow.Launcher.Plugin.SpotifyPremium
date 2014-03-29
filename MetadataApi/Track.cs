using Newtonsoft.Json;

namespace Wox.Plugin.Spotify.MetadataApi
{
    public class Track
    {
        public Album Album;
        public string Name;
        public string Popularity;

        [JsonProperty("external-ids")] 
        public ExternalId[] ExternalIds;

        public bool Explicit = false;
        public double Lenght;

        public string Href;
        public Artist[] Artists;

        [JsonProperty("track-number")] 
        public string TrackNumber;
    }
}
