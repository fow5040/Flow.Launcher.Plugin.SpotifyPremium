using System;
using System.IO;
using System.Text;
using System.Net;
using Newtonsoft.Json;

namespace Wox.Plugin.Spotify.MetadataApi
{
    public class ApiData
    {
        private const string SPOTIFY_API_URI = "http://ws.spotify.com/search/1/{0}.json?q={1}";        

        private const string SPOTIFY_EMBED_URI = "https://embed.spotify.com/oembed/?url={0}";

        private string PluginDirecotry;

        public ApiData(string pluginDir = null)
        {
            this.PluginDirecotry = pluginDir ?? Directory.GetCurrentDirectory();
        }

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

        public string GetArtwork(string href)
        {
            var uri = string.Format(SPOTIFY_EMBED_URI, href);
            
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.UserAgent = "Mozilla/5.0";
            var response = (HttpWebResponse)request.GetResponse();
            if (response.StatusCode != HttpStatusCode.OK || response.GetResponseStream() == null)
            {
                return null;
            }

            var responseData = new StreamReader(response.GetResponseStream(), Encoding.UTF8).ReadToEnd();

            var thumbnailUrl = JsonConvert.DeserializeObject<EmbedApiResponse>(responseData).ThumbnailUrl;

            var uniqueId = href.Substring(href.LastIndexOf(":") + 1);

            var path = string.Format(@"{0}\{1}.jpg", CacheFodler, uniqueId);

            //TODO: Optimize...
            if (!File.Exists(path))
                new WebClient().DownloadFile(new Uri(thumbnailUrl), path);

            return path;
        }

        private string CacheFodler
        {
            get { return Path.Combine(PluginDirecotry, "Cache"); }
        }
    }

    public enum SearchType
    {
        Track, Album, Artist
    }
}
