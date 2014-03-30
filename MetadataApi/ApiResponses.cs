using Newtonsoft.Json;

namespace Wox.Plugin.Spotify.MetadataApi
{
    public class TracksApiResponse
    {
        public Info Info;
        public Track[] Tracks;
    }

    public class AlbumsApiResponse
    {
        public Info Info;
        public Album[] Albums;
    }

    public class ArtistsApiResponse
    {
        public Info Info;
        public Artist[] Artists;
    }

    public class EmbedApiResponse
    {
        public string Title;

        [JsonProperty("thumbnail_url")]
        public string ThumbnailUrl;
    }
}
