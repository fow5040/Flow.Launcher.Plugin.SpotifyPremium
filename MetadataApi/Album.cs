using Newtonsoft.Json;

namespace Wox.Plugin.Spotify.MetadataApi
{
    public class Album
    {
        public string Name;
        public string Popularity;

        [JsonProperty("external-ids")]
        public ExternalId[] ExternalIds;

        public string Href;
        public Artist[] Artists;

        public Availability Availability;
    }
}
