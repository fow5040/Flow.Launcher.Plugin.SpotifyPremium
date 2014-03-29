using System.IO;
using System.Text;
using System.Net;
using Newtonsoft.Json;

namespace Wox.Plugin.Spotify.MetadataApi
{
    public class ApiData
    {
        private const string SPOTIFY_API_URI = "http://ws.spotify.com/search/1/{0}.json?q={1}";        

        public TracksApiResponse GetTracks(string searchTerms)
        {
            var data = GetData(searchTerms, SearchType.Track);
            return JsonConvert.DeserializeObject<TracksApiResponse>(data);
        }

        public AlbumsApiResponse GetAlbums(string searchTerms)
        {
            var data = GetData(searchTerms, SearchType.Album);
            return JsonConvert.DeserializeObject<AlbumsApiResponse>(data);
        }

        public ArtistsApiResponse GetArtists(string searchTerms)
        {
            var data = GetData(searchTerms, SearchType.Artist);
            return JsonConvert.DeserializeObject<ArtistsApiResponse>(data);
        }

        public string GetData(string searchTerms, SearchType type)
        {
            var uri = string.Format(SPOTIFY_API_URI, type.ToString().ToLower(), searchTerms);

            return GetData(uri);
        }

        public string GetData(string uri)
        {
            var request = WebRequest.Create(uri);
            var response = (HttpWebResponse) request.GetResponse();

            if (response.StatusCode != HttpStatusCode.OK || response.GetResponseStream() == null)
            {
                return null;
            }

            return new StreamReader(response.GetResponseStream(), Encoding.UTF8).ReadToEnd();
        }
    }

    public enum SearchType
    {
        Track, Album, Artist
    }
}
